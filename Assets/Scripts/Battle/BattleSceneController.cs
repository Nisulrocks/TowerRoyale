using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TR.Infrastructure;
using TR.VFX;
using TMPro;
using TR.Systems;
using TR.Data;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace TR.Battle
{
    
    public class BattleSceneController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text arenaNameText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text enemiesRemainingText; 
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private TMP_Text resultsText;
        [Header("Controls")]
        [SerializeField] private UnityEngine.UI.Button startSkipButton; 
        [SerializeField] private TMP_Text startSkipButtonText;

        [Header("Refs")]
        [SerializeField] private WaveSpawner waveSpawner;
        [SerializeField] private BattleDeckBarUI deckBar;
        [SerializeField] private PartnerDeckBarUI partnerDeckBar;
        [SerializeField] private TowerPlacementController placement;
        [SerializeField] private MatchEconomy economy;
        [Header("Arena Override (Optional)")]
        [SerializeField] private ArenaDefinition overrideArena; 

        private ArenaDefinition _arena;
        
        public static ArenaDefinition CurrentArena { get; private set; }
        private int _wavesCleared;
        private bool _running;
        private bool _ended;
        private int _lastEnemiesCount = -1;
        private Coroutine _enemiesPulseCo;
        private Vector3 _enemiesDefaultScale = Vector3.one;
        private Color _enemiesDefaultColor = Color.white;
        private bool _started = false;       
        private bool _skipRequested = false; 
        
        private bool _localVotedSkip = false;
        private int _skipVotes = 0;
        private int _skipNeeded = 2;
        
        private bool _remoteAllowSkip = false;
        
        private TR.Net.DuoBattleCoordinator _coordinator;
        private bool _isDuoClient;
        private bool _matchEndedReturnToLobby;

        
        private const string PROP_DUO_WAVE = "DuoWave";
        [Header("Skip Settings")]
        [Tooltip("Player can only skip the wait if (active enemies + pending spawns this wave) are less than or equal to this number.")]
        [SerializeField] private int maxEnemiesToAllowSkip = 5;

        public static BattleSceneController Instance { get; private set; }

        private readonly System.Collections.Generic.Dictionary<int, int> _waveRemainingEnemies = new();
        private readonly System.Collections.Generic.Dictionary<int, int> _waveKillMoney = new();
        private int _totalWaves;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            
            if (MatchContext.IsDuo)
            {
                SetupDuoCoordinator();
                if (_matchEndedReturnToLobby) return;
            }
            SetupArenaFromContext();
            UpdateTopBar();
            SetupDeckAndPlacement();
            HookCastle();
            
            if (startSkipButton != null)
            {
                startSkipButton.onClick.RemoveAllListeners();
                startSkipButton.onClick.AddListener(OnClickStartOrSkip);
                startSkipButton.gameObject.SetActive(true);
            }
            if (startSkipButtonText != null) startSkipButtonText.text = "Start First Wave";
            if (enemiesRemainingText)
            {
                _enemiesDefaultScale = enemiesRemainingText.transform.localScale;
                _enemiesDefaultColor = enemiesRemainingText.color;
            }
            StartCoroutine(MonitorEnemiesRemaining());
        }

        private void SetupArenaFromContext()
        {
            GameDB.EnsureLoaded();
            
            
            ArenaDefinition arenaById = null;
            if (!string.IsNullOrEmpty(TR.Systems.MatchContext.ArenaId))
            {
                arenaById = TR.Systems.GameDB.GetArenaById(TR.Systems.MatchContext.ArenaId);
            }

            _arena = overrideArena != null ? overrideArena : (arenaById != null ? arenaById : ArenaService.GetCurrentArena());
            if (_arena == null)
            {
                Debug.LogWarning("[BattleSceneController] No current arena found. Falling back to first available.");
                var list = GameDB.GetArenasSortedByRequirement();
                if (list != null && list.Count > 0) _arena = list[0];
            }
            CurrentArena = _arena;
            if (arenaNameText) arenaNameText.text = _arena != null ? _arena.DisplayName : "Arena -";
            if (waveSpawner != null) waveSpawner.Configure(_arena);
        }

        private void UpdateTopBar()
        {
            if (waveText)
            {
                int current = Mathf.Clamp(_wavesCleared + 1, 1, _arena != null ? _arena.WaveCount : 1);
                waveText.text = _arena != null ? $"Wave {current}/{_arena.WaveCount}" : "Wave -";
            }
        }

        private int GetRoomWaveProperty()
        {
            var room = Photon.Pun.PhotonNetwork.CurrentRoom;
            if (room != null && room.CustomProperties != null
                && room.CustomProperties.TryGetValue(PROP_DUO_WAVE, out object v) && v != null)
            {
                try
                {
                    return Mathf.Max(0, System.Convert.ToInt32(v));
                }
                catch
                {
                    return 0;
                }
            }
            return 0;
        }

        private void SetRoomWaveProperty(int wave)
        {
            if (!MatchContext.IsDuo || !Photon.Pun.PhotonNetwork.IsMasterClient) return;
            var room = Photon.Pun.PhotonNetwork.CurrentRoom;
            if (room == null) return;
            room.SetCustomProperties(new Hashtable { { PROP_DUO_WAVE, wave } });
        }

        private void SetupDeckAndPlacement()
        {
            if (economy != null)
            {
                economy.BeginMatch();
            }
            
            TR.Systems.EffectLimitService.Initialize(_arena);
            
            if (_arena != null && _arena.BattleToastPrefab != null)
            {
                TR.UI.BattleToast.SetPrefab(_arena.BattleToastPrefab);
            }
            if (deckBar != null)
            {
                deckBar.BindFromPlayerDeck();
            }
            if (placement != null)
            {
                placement.Configure(economy);
            }
        }

        private IEnumerator RunMatch(int startWave = 0)
        {
            _running = true;
            _wavesCleared = Mathf.Max(0, startWave);
            SetRoomWaveProperty(_wavesCleared);
            _ended = false;
            ClearWaveTracking();
            _totalWaves = _arena != null ? _arena.WaveCount : 10;
            if (resultsPanel) resultsPanel.SetActive(false);

            int total = _totalWaves;
            float interval = _arena != null ? _arena.WaveInterval : 60f;

            for (int i = Mathf.Clamp(startWave, 0, total); i < total; i++)
            {
                _wavesCleared = i; 
                SetRoomWaveProperty(_wavesCleared);
                UpdateTopBar();

                
                if (waveSpawner != null)
                    waveSpawner.SpawnWave(i + 1);

                
                
                bool isFinalWave = (i == total - 1);
                if (timerText) timerText.gameObject.SetActive(!isFinalWave);
                if (enemiesRemainingText) enemiesRemainingText.gameObject.SetActive(true);
                if (!isFinalWave)
                {
                    
                    _skipRequested = false;
                    if (startSkipButton != null)
                    {
                        startSkipButton.gameObject.SetActive(true);
                        if (startSkipButtonText != null) startSkipButtonText.text = "Skip Wait";
                    }
                    yield return StartCoroutine(Countdown(interval));
                }
                else
                {
                    
                    if (timerText) timerText.text = string.Empty;
                    if (startSkipButton != null) startSkipButton.gameObject.SetActive(false);
                    
                    BroadcastWaveState(i + 1, total, 0f, TR.Net.DuoBattleCoordinator.PHASE_FINAL, false);
                    yield return StartCoroutine(WaitForAllEnemiesCleared());
                }
            }

            
            if (!_ended)
            {
                _running = false;
                
                if (MatchContext.IsDuo && _coordinator != null) _coordinator.BroadcastVictory();
                ShowResultsVictory();
            }
        }

        
        private void BroadcastWaveState(int wave, int total, float timer, int phase, bool allowSkip)
        {
            if (MatchContext.IsDuo && _coordinator != null && Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                _coordinator.BroadcastWaveState(wave, total, timer, phase, allowSkip);
            }
        }

        private IEnumerator Countdown(float seconds)
        {
            
            ResetSkipVotesForNewWave();

            float t = Mathf.Max(0f, seconds);
            while (t > 0f)
            {
                
                bool allowSkip = CanSkipNow();
                if (startSkipButton != null)
                {
                    startSkipButton.interactable = !_localVotedSkip && allowSkip;
                }
                if (_skipRequested)
                {
                    
                    if (allowSkip) break;
                    
                    _skipRequested = false;
                    
                    if (MatchContext.IsDuo && _coordinator != null && Photon.Pun.PhotonNetwork.IsMasterClient)
                        _coordinator.ResetSkipVotes();
                }
                if (timerText) timerText.text = $"Next wave in {Mathf.CeilToInt(t)}s";
                
                BroadcastWaveState(_wavesCleared + 1, _arena != null ? _arena.WaveCount : 1, t, TR.Net.DuoBattleCoordinator.PHASE_COUNTDOWN, allowSkip);
                UpdateEnemiesRemainingText();
                yield return null;
                t -= Time.deltaTime;
            }
            if (timerText) timerText.text = "Spawning...";
            
            BroadcastWaveState(_wavesCleared + 1, _arena != null ? _arena.WaveCount : 1, 0f, TR.Net.DuoBattleCoordinator.PHASE_SPAWNING, false);
            _skipRequested = false;
            
            UpdateEnemiesRemainingText();
        }

        private void ShowResultsVictory()
        {
            
            TR.Net.DuoRejoinService.EndMatch();
            MarkRoomMatchEnded();
            var rewards = ArenaService.AwardMatchCompletion(_arena, _arena != null ? _arena.WaveCount : _wavesCleared);
            if (resultsPanel) resultsPanel.SetActive(false); 
            if (resultsText)
            {
                string trophyLine = rewards.trophiesCapped && rewards.trophiesEarned <= 0
                    ? $"Trophies: Maxed (Total {rewards.totalTrophiesAfter})"
                    : $"Trophies: +{rewards.trophiesEarned} (Total {rewards.totalTrophiesAfter})";
                resultsText.text =
                    $"Victory!\n" +
                    trophyLine + "\n" +
                    $"Money: +{rewards.moneyEarned}\n" +
                    $"Castle XP: +{rewards.castleXPEarned}\n" +
                    (rewards.arenaAfter != rewards.arenaBefore && rewards.arenaAfter != null
                        ? $"Unlocked: {rewards.arenaAfter.DisplayName}!"
                        : "");
            }
            StartCoroutine(FadeInResultsPanelSimple());
        }

        private void ShowResultsDefeat()
        {
            
            TR.Net.DuoRejoinService.EndMatch();
            MarkRoomMatchEnded();
            var rewards = ArenaService.AwardMatchDefeat(_arena, _wavesCleared);
            if (resultsPanel) resultsPanel.SetActive(false); 
            if (resultsText)
            {
                string trophyLine;
                if (rewards.trophiesEarned < 0)
                {
                    trophyLine = $"Trophies: {rewards.trophiesEarned} (Total {rewards.totalTrophiesAfter})";
                }
                else
                {
                    
                    trophyLine = rewards.trophiesCapped
                        ? $"Trophies: -0 (Total {rewards.totalTrophiesAfter})"
                        : $"Trophies: 0 (Total {rewards.totalTrophiesAfter})";
                }
                resultsText.text =
                    $"Defeat\n" +
                    trophyLine + "\n" +
                    $"Castle XP: +{rewards.castleXPEarned}";
            }
            StartCoroutine(FadeInResultsPanelSimple());
        }

        private void MarkRoomMatchEnded()
        {
            if (MatchContext.IsDuo && Photon.Pun.PhotonNetwork.IsMasterClient && Photon.Pun.PhotonNetwork.CurrentRoom != null)
            {
                var room = Photon.Pun.PhotonNetwork.CurrentRoom;
                room.IsOpen = false;
                room.IsVisible = false;
                var props = new ExitGames.Client.Photon.Hashtable();
                props["DuoMatchEnded"] = true;
                room.SetCustomProperties(props);
            }
        }

        
        private void SetupDuoCoordinator()
        {
            var room = Photon.Pun.PhotonNetwork.CurrentRoom;
            bool roomEnded = room != null && room.CustomProperties != null && room.CustomProperties.TryGetValue("DuoMatchEnded", out object endedVal) && endedVal is bool endedBool && endedBool;
            bool ended = TR.Net.DuoRejoinService.IsMatchEnded || roomEnded;
            if (ended)
            {
                Debug.Log("[BattleSceneController] Match has already ended; returning to lobby.");
                _matchEndedReturnToLobby = true;
                TR.Net.DuoRejoinService.EndMatch();
                MatchContext.Reset();
                if (SceneFader.Instance != null) _ = SceneFader.Instance.LoadSceneWithFade("Lobby");
                else UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
                return;
            }

            _coordinator = TR.Net.DuoBattleCoordinator.Instance;
            if (_coordinator == null)
            {
                _coordinator = FindFirstObjectByType<TR.Net.DuoBattleCoordinator>(FindObjectsInactive.Include);
            }
            if (_coordinator == null)
            {
                Debug.LogError("[BattleSceneController] Duo mode but no DuoBattleCoordinator found in scene. Add one with a scene PhotonView.");
                return;
            }
            _isDuoClient = !Photon.Pun.PhotonNetwork.IsMasterClient;

            
            _wavesCleared = Mathf.Max(_wavesCleared, GetRoomWaveProperty());
            _coordinator.OnMatchStarted += OnDuoMatchStarted;
            _coordinator.OnWaveStateReceived += OnDuoWaveStateReceived;
            _coordinator.OnVictoryReceived += OnDuoVictoryReceived;
            _coordinator.OnDefeatReceived += OnDuoDefeatReceived;
            _coordinator.OnSkipVoteChanged += OnDuoSkipVoteChanged;
            _coordinator.OnSkipConfirmed += OnDuoSkipConfirmed;
            _coordinator.OnSpawnPortalsChanged += OnDuoSpawnPortalsChanged;
            _coordinator.OnPartnerDeckReceived += OnDuoPartnerDeckReceived;
            _coordinator.OnMasterClientSwitchedEvent += OnDuoMasterSwitched;
            _coordinator.OnEnemySyncRequested += OnDuoEnemySyncRequested;
            _coordinator.OnEnemySyncReceived += OnDuoEnemySyncReceived;
            _coordinator.OnEnemyRespawnRequested += OnDuoEnemyRespawnRequested;

            
            
            TR.Net.DuoRejoinService.SaveActiveMatch();

            
            if (TR.Net.DuoRejoinService.IsRejoinAttempt && _coordinator != null && !Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                _coordinator.RequestTowerSync();
                _coordinator.RequestEnemySync();
            }
            TR.Net.DuoRejoinService.IsRejoinAttempt = false;

            
            BroadcastLocalDeck();
            
            if (_coordinator.HasPartnerDeck)
                OnDuoPartnerDeckReceived(_coordinator.PartnerDeckIds, _coordinator.PartnerDeckLevels);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_coordinator != null)
            {
                _coordinator.OnMatchStarted -= OnDuoMatchStarted;
                _coordinator.OnWaveStateReceived -= OnDuoWaveStateReceived;
                _coordinator.OnVictoryReceived -= OnDuoVictoryReceived;
                _coordinator.OnDefeatReceived -= OnDuoDefeatReceived;
                _coordinator.OnSkipVoteChanged -= OnDuoSkipVoteChanged;
                _coordinator.OnSkipConfirmed -= OnDuoSkipConfirmed;
                _coordinator.OnSpawnPortalsChanged -= OnDuoSpawnPortalsChanged;
                _coordinator.OnPartnerDeckReceived -= OnDuoPartnerDeckReceived;
                _coordinator.OnMasterClientSwitchedEvent -= OnDuoMasterSwitched;
                _coordinator.OnEnemySyncRequested -= OnDuoEnemySyncRequested;
                _coordinator.OnEnemySyncReceived -= OnDuoEnemySyncReceived;
                _coordinator.OnEnemyRespawnRequested -= OnDuoEnemyRespawnRequested;
            }
        }

        
        
        
        private bool _tookOverAsMaster;
        private void OnDuoMasterSwitched()
        {
            if (_ended || _tookOverAsMaster) return;
            if (!Photon.Pun.PhotonNetwork.IsMasterClient) return;

            
            _tookOverAsMaster = true;
            _isDuoClient = false;
            _coordinator?.MarkMatchStarted();

            
            
            int resumeWave = Mathf.Max(0, Mathf.Max(_wavesCleared, GetRoomWaveProperty()));
            _started = true;
            _running = true;
            if (resultsPanel) resultsPanel.SetActive(false);
            StartCoroutine(RunMatch(resumeWave));
            TR.UI.BattleToast.Show("You are now the host \u2014 resuming the match...", 2.5f);
        }

        
        private void OnDuoMatchStarted()
        {
            if (_ended) return;
            _started = true;
            if (startSkipButtonText != null) startSkipButtonText.text = "Skip Wait";
            if (Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                
                StartCoroutine(RunMatch());
            }
            else
            {
                
                _running = true;
                if (resultsPanel) resultsPanel.SetActive(false);
            }
        }

        
        private void OnDuoWaveStateReceived(int wave, int total, float timer, int phase, bool allowSkip)
        {
            if (!_isDuoClient || _ended) return;

            
            
            if (!_started || !_running)
            {
                _started = true;
                _running = true;
                if (resultsPanel) resultsPanel.SetActive(false);
            }

            _remoteAllowSkip = allowSkip;
            _wavesCleared = Mathf.Clamp(wave - 1, 0, Mathf.Max(0, total));
            _wavesCleared = Mathf.Clamp(Mathf.Max(_wavesCleared, GetRoomWaveProperty()), 0, Mathf.Max(0, total - 1));
            if (waveText) waveText.text = $"Wave {Mathf.Clamp(_wavesCleared + 1, 1, total)}/{total}";
            bool isFinal = phase == TR.Net.DuoBattleCoordinator.PHASE_FINAL;
            if (timerText)
            {
                timerText.gameObject.SetActive(!isFinal);
                if (phase == TR.Net.DuoBattleCoordinator.PHASE_COUNTDOWN)
                    timerText.text = $"Next wave in {Mathf.CeilToInt(timer)}s";
                else if (phase == TR.Net.DuoBattleCoordinator.PHASE_SPAWNING)
                    timerText.text = "Spawning...";
                else
                    timerText.text = string.Empty;
            }
            
            if (startSkipButton != null)
            {
                bool showSkip = !isFinal;
                startSkipButton.gameObject.SetActive(showSkip);
                
                if (showSkip) UpdateSkipButtonLabel();
            }
        }

        
        
        private void BroadcastLocalDeck()
        {
            if (_coordinator == null) return;
            var deck = TR.Systems.PlayerProfile.Data.deck;
            if (deck == null || deck.Count == 0) return;
            var ids = new string[deck.Count];
            var levels = new int[deck.Count];
            for (int i = 0; i < deck.Count; i++)
            {
                ids[i] = deck[i];
                var cp = TR.Systems.PlayerProfile.GetOrCreateCard(deck[i]);
                levels[i] = Mathf.Max(1, cp != null ? cp.level : 1);
            }
            Debug.Log($"[BattleSceneController] Broadcasting local deck ({ids.Length} cards) to partner.");
            _coordinator.BroadcastLocalDeck(ids, levels);
        }

        private void OnDuoPartnerDeckReceived(string[] cardIds, int[] levels)
        {
            Debug.Log($"[BattleSceneController] Received partner deck ({(cardIds != null ? cardIds.Length : 0)} cards). partnerDeckBar assigned: {partnerDeckBar != null}");
            if (partnerDeckBar != null) partnerDeckBar.BindFromPartnerDeck(cardIds, levels);
        }

        private void OnDuoEnemySyncRequested(int targetActor)
        {
            if (!Photon.Pun.PhotonNetwork.IsMasterClient) return;
            var all = new System.Collections.Generic.List<EnemyBase2D>(EnemyBase2D.All);
            int count = all.Count;
            int[] viewIds = new int[count];
            string[] enemyIds = new string[count];
            Vector3[] positions = new Vector3[count];
            float[] hMul = new float[count];
            float[] dMul = new float[count];
            float[] sMul = new float[count];
            float[] health = new float[count];
            int[] waveNumbers = new int[count];
            int[] waypointIndices = new int[count];

            for (int i = 0; i < count; i++)
            {
                var e = all[i];
                if (e == null) continue;
                var pv = e.GetComponent<Photon.Pun.PhotonView>();
                viewIds[i] = pv != null ? pv.ViewID : 0;
                enemyIds[i] = e.Definition != null ? e.Definition.EnemyId : "";
                positions[i] = e.transform.position;
                hMul[i] = e.HealthMultiplier;
                dMul[i] = e.DamageMultiplier;
                sMul[i] = e.SpeedMultiplier;
                health[i] = e.CurrentHealth;
                waveNumbers[i] = e.WaveNumber;
                waypointIndices[i] = e.WaypointIndex;
            }

            _coordinator.SendEnemySync(targetActor, viewIds, enemyIds, positions, hMul, dMul, sMul, health, waveNumbers, waypointIndices);
            Debug.Log($"[BattleSceneController] Sent enemy sync to actor {targetActor}: {count} enemies.");
        }

        private bool _enemyRespawnRequested;
        private Coroutine _pendingEnemySyncRetry;

        private void OnDuoEnemySyncReceived(int[] viewIds, string[] enemyIds, Vector3[] positions, float[] hMul, float[] dMul, float[] sMul, float[] health, int[] waveNumbers, int[] waypointIndices)
        {
            if (Photon.Pun.PhotonNetwork.IsMasterClient) return;

            int initialized = ApplyEnemySync(viewIds, enemyIds, positions, hMul, dMul, sMul, health, waveNumbers, waypointIndices);
            int count = viewIds != null ? viewIds.Length : 0;

            if (initialized > 0 && _pendingEnemySyncRetry != null)
            {
                StopCoroutine(_pendingEnemySyncRetry);
                _pendingEnemySyncRetry = null;
            }

            if (count > 0 && initialized == 0 && !_enemyRespawnRequested && _pendingEnemySyncRetry == null)
            {
                _pendingEnemySyncRetry = StartCoroutine(TryEnemySyncRetry(viewIds, enemyIds, positions, hMul, dMul, sMul, health, waveNumbers, waypointIndices));
            }
        }

        private int ApplyEnemySync(int[] viewIds, string[] enemyIds, Vector3[] positions, float[] hMul, float[] dMul, float[] sMul, float[] health, int[] waveNumbers, int[] waypointIndices)
        {
            if (viewIds == null) return 0;
            int count = viewIds.Length;
            var path = FindFirstObjectByType<Path2D>(FindObjectsInactive.Include);
            int initialized = 0;
            for (int i = 0; i < count; i++)
            {
                if (viewIds[i] == 0) continue;
                var pv = Photon.Pun.PhotonNetwork.GetPhotonView(viewIds[i]);
                if (pv == null)
                {
                    Debug.LogWarning($"[BattleSceneController] Enemy sync: PhotonView {viewIds[i]} not found.");
                    continue;
                }
                var enemy = pv.GetComponent<EnemyBase2D>();
                if (enemy == null)
                {
                    Debug.LogWarning($"[BattleSceneController] Enemy sync: PhotonView {viewIds[i]} has no EnemyBase2D.");
                    continue;
                }
                var def = GameDB.GetEnemyById(enemyIds[i]);
                if (def == null)
                {
                    Debug.LogWarning($"[BattleSceneController] Enemy sync: unknown enemyId '{enemyIds[i]}'.");
                    continue;
                }
                enemy.Initialize(def, path);
                enemy.SetWaveNumber(waveNumbers[i]);
                enemy.SetWaypointIndex(waypointIndices[i]);
                enemy.transform.position = positions[i];
                enemy.RecalculateWaypointFromPosition();
                enemy.ApplyBossScaling(hMul[i], dMul[i], sMul[i]);
                enemy.SetNetworkedHealth(health[i]);
                initialized++;
            }
            Debug.Log($"[BattleSceneController] Enemy sync applied: {initialized}/{count} enemies.");
            return initialized;
        }

        private System.Collections.IEnumerator TryEnemySyncRetry(int[] viewIds, string[] enemyIds, Vector3[] positions, float[] hMul, float[] dMul, float[] sMul, float[] health, int[] waveNumbers, int[] waypointIndices)
        {
            float[] delays = { 0.3f, 0.5f, 0.8f };
            for (int i = 0; i < delays.Length; i++)
            {
                if (Photon.Pun.PhotonNetwork.IsMasterClient) yield break;
                yield return new WaitForSeconds(delays[i]);
                if (Photon.Pun.PhotonNetwork.IsMasterClient) yield break;

                int initialized = ApplyEnemySync(viewIds, enemyIds, positions, hMul, dMul, sMul, health, waveNumbers, waypointIndices);
                if (initialized > 0) yield break;
            }

            if (Photon.Pun.PhotonNetwork.IsMasterClient) yield break;

            if (!_enemyRespawnRequested)
            {
                _enemyRespawnRequested = true;
                Debug.LogWarning("[BattleSceneController] Enemy sync still found no PhotonViews after retries; requesting respawn from master.");
                _coordinator?.RequestEnemyRespawn();
            }
        }

        private struct EnemyRespawnState
        {
            public string EnemyId;
            public Vector3 Position;
            public float HMul;
            public float DMul;
            public float SMul;
            public float Health;
            public int WaveNumber;
            public int WaypointIndex;
        }

        private void OnDuoEnemyRespawnRequested(int requesterActor)
        {
            if (!Photon.Pun.PhotonNetwork.IsMasterClient || waveSpawner == null) return;

            var all = new System.Collections.Generic.List<EnemyBase2D>(EnemyBase2D.All);
            var states = new System.Collections.Generic.List<EnemyRespawnState>(all.Count);
            foreach (var e in all)
            {
                if (e == null || e.Definition == null) continue;
                states.Add(new EnemyRespawnState
                {
                    EnemyId = e.Definition.EnemyId,
                    Position = e.transform.position,
                    HMul = e.HealthMultiplier,
                    DMul = e.DamageMultiplier,
                    SMul = e.SpeedMultiplier,
                    Health = e.CurrentHealth,
                    WaveNumber = e.WaveNumber,
                    WaypointIndex = e.WaypointIndex
                });
                UnregisterWaveEnemy(e.WaveNumber);
                Photon.Pun.PhotonNetwork.Destroy(e.gameObject);
            }

            Debug.Log($"[BattleSceneController] Respawning {states.Count} enemies for rejoiner actor {requesterActor}.");
            StartCoroutine(RespawnEnemiesCo(states));
        }

        private System.Collections.IEnumerator RespawnEnemiesCo(System.Collections.Generic.List<EnemyRespawnState> states)
        {
            yield return null;
            if (waveSpawner == null) yield break;
            var path = FindFirstObjectByType<Path2D>(FindObjectsInactive.Include);
            foreach (var s in states)
            {
                var def = GameDB.GetEnemyById(s.EnemyId);
                if (def == null) continue;
                var enemy = waveSpawner.SpawnEnemyNetworked(def, s.Position, s.HMul, s.DMul, s.SMul, s.WaveNumber);
                if (enemy != null)
                {
                    enemy.Initialize(def, path);
                    enemy.SetWaveNumber(s.WaveNumber);
                    enemy.SetWaypointIndex(s.WaypointIndex);
                    enemy.transform.position = s.Position;
                    enemy.RecalculateWaypointFromPosition();
                    enemy.ApplyBossScaling(s.HMul, s.DMul, s.SMul);
                    enemy.SetNetworkedHealth(s.Health);
                }
            }
            Debug.Log($"[BattleSceneController] Respawned {states.Count} enemies.");
        }

        private void OnDuoSpawnPortalsChanged(bool open)
        {
            if (!_isDuoClient || waveSpawner == null) return;
            if (open) waveSpawner.OpenSpawnPortals();
            else waveSpawner.CloseAllPortals();
        }

        private void OnDuoVictoryReceived()
        {
            if (_ended) return;
            _ended = true;
            _running = false;
            MarkRoomMatchEnded();
            TR.Net.DuoRejoinService.EndMatch();
            ShowResultsVictory();
        }

        
        private void OnDuoDefeatReceived()
        {
            if (_ended) return;
            _ended = true;
            _running = false;
            MarkRoomMatchEnded();
            TR.Net.DuoRejoinService.EndMatch();
            StopAllCoroutines();
            StartCoroutine(DefeatCleanup());
            ShowResultsDefeat();
        }

        
        private void OnDuoSkipVoteChanged(int votes, int needed)
        {
            _skipVotes = votes;
            _skipNeeded = Mathf.Max(1, needed);
            
            if (votes <= 0) _localVotedSkip = false;
            UpdateSkipButtonLabel();
        }

        
        private void OnDuoSkipConfirmed()
        {
            _localVotedSkip = false;
            
            if (Photon.Pun.PhotonNetwork.IsMasterClient) _skipRequested = true;
        }

        
        private void UpdateSkipButtonLabel()
        {
            if (!_started || _ended) return;
            if (startSkipButtonText == null) return;
            if (_skipVotes > 0 && _skipVotes < _skipNeeded)
                startSkipButtonText.text = $"Skip vote ({_skipVotes}/{_skipNeeded})";
            else
                startSkipButtonText.text = "Skip Wait";
            if (startSkipButton != null)
                startSkipButton.interactable = !_localVotedSkip && CanSkipNowEffective();
        }

        
        
        private bool CanSkipNowEffective()
        {
            
            if (MatchContext.IsDuo && _isDuoClient) return _remoteAllowSkip;
            return CanSkipNow();
        }

        
        private void ResetSkipVotesForNewWave()
        {
            _localVotedSkip = false;
            _skipVotes = 0;
            if (MatchContext.IsDuo && _coordinator != null && Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                _coordinator.ResetSkipVotes();
            }
        }

        private void HookCastle()
        {
            var castle = FindFirstObjectByType<BaseCastle>(FindObjectsInactive.Include);
            if (castle != null)
            {
                castle.OnCastleDestroyed += OnCastleDestroyed;
            }
        }

        private void OnCastleDestroyed()
        {
            if (_ended) return;
            _ended = true;
            _running = false;
            MarkRoomMatchEnded();
            TR.Net.DuoRejoinService.EndMatch();

            if (MatchContext.IsDuo && _coordinator != null && Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                _coordinator.BroadcastDefeat();
            }
            StopAllCoroutines();
            StartCoroutine(DefeatCleanup());
            ShowResultsDefeat();
        }

        
        public void OnClickReturnToLobby()
        {
            
            TR.Net.DuoRejoinService.ClearActiveMatch();
            MatchContext.Reset();
            _ = SceneFader.Instance.LoadSceneWithFade("Lobby");
        }

        
        private void OnClickStartOrSkip()
        {
            if (_ended) return;

            
            if (MatchContext.IsDuo && _coordinator != null)
            {
                if (!_started)
                {
                    
                    _coordinator.LocalReadyUp();
                    if (startSkipButtonText != null) startSkipButtonText.text = "Waiting for partner...";
                    if (startSkipButton != null) startSkipButton.interactable = false;
                }
                else
                {
                    
                    if (_localVotedSkip) return;
                    if (!CanSkipNowEffective()) return;
                    _localVotedSkip = true;
                    if (startSkipButton != null) startSkipButton.interactable = false;
                    _coordinator.CastSkipVote();
                }
                return;
            }

            if (!_started)
            {
                
                _started = true;
                if (startSkipButtonText != null) startSkipButtonText.text = "Skip Wait";
                StartCoroutine(RunMatch());
            }
            else
            {
                
                _skipRequested = true;
            }
        }

        private IEnumerator WaitForAllEnemiesCleared()
        {
            
            while (true)
            {
                int active = EnemyBase2D.All != null ? EnemyBase2D.All.Count : 0;
                int pending = waveSpawner != null ? waveSpawner.GetPendingSpawns() : 0;
                if (active <= 0 && pending <= 0) break;
                UpdateEnemiesRemainingText();
                yield return null;
            }
            if (enemiesRemainingText) enemiesRemainingText.text = "All clear!";
        }

        private void UpdateEnemiesRemainingText()
        {
            if (!enemiesRemainingText) return;
            int active = EnemyBase2D.All != null ? EnemyBase2D.All.Count : 0;
            int pending = waveSpawner != null ? waveSpawner.GetPendingSpawns() : 0;
            int remaining = active + pending;
            enemiesRemainingText.text = $"Enemies remaining: {remaining}";

            if (startSkipButton != null)
            {
                startSkipButton.interactable = !_localVotedSkip && CanSkipNowEffective();
            }
        }

        private bool CanSkipNow()
        {
            
            if (EnemyBase2D.All != null)
            {
                foreach (var e in EnemyBase2D.All)
                {
                    if (e == null) continue;
                    if (e.GetTier() == ArenaDefinition.EnemyTier.Boss) return false;
                }
            }
            int active = EnemyBase2D.All != null ? EnemyBase2D.All.Count : 0;
            int pending = waveSpawner != null ? waveSpawner.GetPendingSpawns() : 0;
            int totalPressure = active + pending;
            return totalPressure <= Mathf.Max(0, maxEnemiesToAllowSkip);
        }

        private IEnumerator MonitorEnemiesRemaining()
        {
            while (true)
            {
                if (_running && !_ended && enemiesRemainingText)
                {
                    int active = EnemyBase2D.All != null ? EnemyBase2D.All.Count : 0;
                    int pending = waveSpawner != null ? waveSpawner.GetPendingSpawns() : 0;
                    int remaining = active + pending;
                    if (remaining != _lastEnemiesCount)
                    {
                        _lastEnemiesCount = remaining;
                        UpdateEnemiesRemainingText();
                        TriggerEnemiesPulse();
                    }
                }
                yield return null;
            }
        }

        private void TriggerEnemiesPulse()
        {
            if (enemiesRemainingText == null) return;
            if (_enemiesPulseCo != null) StopCoroutine(_enemiesPulseCo);
            _enemiesPulseCo = StartCoroutine(PulseEnemiesRemaining());
        }

        private IEnumerator DefeatCleanup()
        {
            
            if (waveSpawner != null) waveSpawner.StopAllCoroutines();
            
            var enemyList = new System.Collections.Generic.List<EnemyBase2D>();
            if (EnemyBase2D.All != null)
            {
                foreach (var e in EnemyBase2D.All) enemyList.Add(e);
            }
            for (int i = 0; i < enemyList.Count; i++)
            {
                var e = enemyList[i];
                if (e == null) continue;
                e.TakeDamage(Mathf.Max(1f, e.CurrentHealth));
            }
            
            var towerList = new System.Collections.Generic.List<TowerBase>();
            if (TowerBase.All != null)
            {
                foreach (var t in TowerBase.All) towerList.Add((TowerBase)t);
            }
            for (int i = 0; i < towerList.Count; i++)
            {
                var t = towerList[i];
                if (t == null || t.Definition == null) continue;
                string vfxKey = t.Definition.GetDefeatDestroyVfxKey();
                string sfxKey = t.Definition.GetDefeatDestroySfxKey();
                if (!string.IsNullOrEmpty(vfxKey))
                {
                    ParticleManager.SpawnOneShot(vfxKey, t.transform.position);
                }
                if (!string.IsNullOrEmpty(sfxKey) && TR.Audio.SFXManager.Instance != null)
                {
                    TR.Audio.SFXManager.Instance.Play(sfxKey);
                }
                Destroy(t.gameObject);
            }
            yield return null;
        }

        private IEnumerator PulseEnemiesRemaining()
        {
            
            Transform tr = enemiesRemainingText.transform;
            Color fromColor = _enemiesDefaultColor;
            Color toColor = new Color(1f, 0.95f, 0.4f, fromColor.a); 
            Vector3 fromScale = _enemiesDefaultScale;
            Vector3 toScale = _enemiesDefaultScale * 1.12f;

            float t = 0f;
            const float upTime = 0.1f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, upTime);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                tr.localScale = Vector3.Lerp(fromScale, toScale, e);
                enemiesRemainingText.color = Color.Lerp(fromColor, toColor, e);
                yield return null;
            }

            t = 0f;
            const float downTime = 0.12f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, downTime);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                tr.localScale = Vector3.Lerp(toScale, fromScale, e);
                enemiesRemainingText.color = Color.Lerp(toColor, fromColor, e);
                yield return null;
            }
            tr.localScale = _enemiesDefaultScale;
            enemiesRemainingText.color = _enemiesDefaultColor;
            _enemiesPulseCo = null;
        }

        private IEnumerator FadeInResultsPanelSimple()
        {
            if (resultsPanel == null)
                yield break;
            
            var cg = resultsPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = resultsPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            resultsPanel.SetActive(true);
            float t = 0f;
            const float dur = 2.0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
                cg.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                yield return null;
            }
            cg.alpha = 1f;
        }

        private void ClearWaveTracking()
        {
            _waveRemainingEnemies.Clear();
            _waveKillMoney.Clear();
        }

        public void RegisterWaveEnemy(int wave)
        {
            if (wave <= 0) return;
            if (!TR.Net.DuoRuntime.IsSimulationAuthority) return;
            _waveRemainingEnemies.TryGetValue(wave, out int remaining);
            _waveRemainingEnemies[wave] = remaining + 1;
            if (!_waveKillMoney.ContainsKey(wave)) _waveKillMoney[wave] = 0;
        }

        public void UnregisterWaveEnemy(int wave)
        {
            if (wave <= 0) return;
            if (!TR.Net.DuoRuntime.IsSimulationAuthority) return;
            if (!_waveRemainingEnemies.ContainsKey(wave)) return;
            _waveRemainingEnemies[wave] = Mathf.Max(0, _waveRemainingEnemies[wave] - 1);
            if (_waveRemainingEnemies[wave] == 0)
            {
                _waveRemainingEnemies.Remove(wave);
                _waveKillMoney.Remove(wave);
            }
        }

        public void RecordWaveKill(int wave, int amount)
        {
            if (wave <= 0 || amount <= 0) return;
            if (!TR.Net.DuoRuntime.IsSimulationAuthority) return;
            if (!_waveRemainingEnemies.ContainsKey(wave)) return;

            _waveKillMoney.TryGetValue(wave, out int earned);
            _waveKillMoney[wave] = earned + amount;

            _waveRemainingEnemies.TryGetValue(wave, out int remaining);
            remaining = Mathf.Max(0, remaining - 1);
            _waveRemainingEnemies[wave] = remaining;

            if (remaining == 0)
            {
                if (waveSpawner != null && waveSpawner.IsWaveSpawning(wave))
                {
                    // Defer payout until the spawner has finished this wave.
                }
                else
                {
                    int bonus = _waveKillMoney[wave];
                    _waveKillMoney.Remove(wave);
                    _waveRemainingEnemies.Remove(wave);
                    if (bonus > 0) PayWaveBonus(wave, bonus);
                }
            }
        }

        public void OnWaveSpawnComplete(int wave)
        {
            if (wave <= 0) return;
            if (!_waveRemainingEnemies.ContainsKey(wave)) return;
            if (_waveRemainingEnemies[wave] != 0) return;

            int bonus = _waveKillMoney[wave];
            _waveKillMoney.Remove(wave);
            _waveRemainingEnemies.Remove(wave);
            if (bonus > 0) PayWaveBonus(wave, bonus);
        }

        private void PayWaveBonus(int wave, int total)
        {
            if (total <= 0) return;
            if (wave >= _totalWaves) return;

            if (!TR.Net.DuoRuntime.IsDuo)
            {
                economy?.Earn(total);
                TR.UI.BattleToast.Show($"Wave {wave} cleared! +${total}");
            }
            else
            {
                _coordinator?.AwardWaveBonus(wave, total);
            }
        }
    }
}
