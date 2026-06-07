using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TR.Infrastructure;
using TR.VFX;
using TMPro;
using TR.Systems;
using TR.Data;

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
        [SerializeField] private TowerPlacementController placement;
        [SerializeField] private MatchEconomy economy;
        [Header("Arena Override (Optional)")]
        [SerializeField] private ArenaDefinition overrideArena; 

        private ArenaDefinition _arena;
        private int _wavesCleared;
        private bool _running;
        private bool _ended;
        private int _lastEnemiesCount = -1;
        private Coroutine _enemiesPulseCo;
        private Vector3 _enemiesDefaultScale = Vector3.one;
        private Color _enemiesDefaultColor = Color.white;
        private bool _started = false;       
        private bool _skipRequested = false; 
        [Header("Skip Settings")]
        [Tooltip("Player can only skip the wait if (active enemies + pending spawns this wave) are less than or equal to this number.")]
        [SerializeField] private int maxEnemiesToAllowSkip = 5;

        private void Start()
        {
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
            _arena = overrideArena != null ? overrideArena : ArenaService.GetCurrentArena();
            if (_arena == null)
            {
                Debug.LogWarning("[BattleSceneController] No current arena found. Falling back to first available.");
                var list = GameDB.GetArenasSortedByRequirement();
                if (list != null && list.Count > 0) _arena = list[0];
            }
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

        private IEnumerator RunMatch()
        {
            _running = true;
            _wavesCleared = 0;
            _ended = false;
            if (resultsPanel) resultsPanel.SetActive(false);

            int total = _arena != null ? _arena.WaveCount : 10;
            float interval = _arena != null ? _arena.WaveInterval : 60f;

            for (int i = 0; i < total; i++)
            {
                _wavesCleared = i; 
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
                    yield return StartCoroutine(WaitForAllEnemiesCleared());
                }
            }

            
            if (!_ended)
            {
                _running = false;
                ShowResultsVictory();
            }
        }

        private IEnumerator Countdown(float seconds)
        {
            float t = Mathf.Max(0f, seconds);
            while (t > 0f)
            {
                
                if (startSkipButton != null)
                {
                    startSkipButton.interactable = CanSkipNow();
                }
                if (_skipRequested)
                {
                    
                    if (CanSkipNow()) break;
                    
                    _skipRequested = false;
                }
                if (timerText) timerText.text = $"Next wave in {Mathf.CeilToInt(t)}s";
                UpdateEnemiesRemainingText();
                yield return null;
                t -= Time.deltaTime;
            }
            if (timerText) timerText.text = "Spawning...";
            _skipRequested = false;
            
            UpdateEnemiesRemainingText();
        }

        private void ShowResultsVictory()
        {
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
            StopAllCoroutines();
            StartCoroutine(DefeatCleanup());
            ShowResultsDefeat();
        }

        
        public void OnClickReturnToLobby()
        {
            _ = SceneFader.Instance.LoadSceneWithFade("Lobby");
        }

        
        private void OnClickStartOrSkip()
        {
            if (_ended) return;
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
            int remaining = EnemyBase2D.All != null ? EnemyBase2D.All.Count : 0;
            enemiesRemainingText.text = $"Enemies remaining: {remaining}";
            
            if (startSkipButton != null)
            {
                startSkipButton.interactable = CanSkipNow();
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
                    int remaining = EnemyBase2D.All != null ? EnemyBase2D.All.Count : 0;
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
    }
}
