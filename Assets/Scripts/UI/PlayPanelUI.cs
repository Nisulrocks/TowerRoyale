using UnityEngine;
using TMPro;
using TR.Systems;
using TR.Data;
using UnityEngine.SceneManagement;
using TR.Infrastructure;
using UnityEngine.UI;

namespace TR.UI
{
    
    public class PlayPanelUI : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text trophiesText;
        [SerializeField] private TMP_Text arenaNameText;
        [SerializeField] private TMP_Text nextArenaText;
        [SerializeField] private TMP_Text castleLevelText;   
        [SerializeField] private TMP_Text castleXPText;      
        [SerializeField] private TMP_Text softCurrencyText;  
        [Header("Castle Progress UI")]
        [Tooltip("Slider for castle XP progress (0-1 value)")]
        [SerializeField] private Slider castleProgressSlider;

        [SerializeField] private TMP_Text castleXPGainText;
        [Tooltip("Delay before showing the '+XP' splash (seconds)")]
        [SerializeField] private float castleXPGainDelay = 0.25f;
        [Tooltip("Castle XP bar animation speed (XP per second, visual only)")]
        [SerializeField] private float castleXpAnimSpeed = 250f;
        [Header("Castle Level-Up VFX")]
        [Tooltip("Particle/VFX key to play when castle levels up (uses ParticleManager via reflection)")]
        [SerializeField] private string levelUpVfxKey = "";

        [SerializeField] private Transform levelUpVfxParent;

        [SerializeField] private bool debugTriggerLevelUpVfxWithL = true;
        [Tooltip("Delay between each VFX burst (seconds)")]
        [SerializeField] private float levelUpVfxBurstStagger = 0.08f;
        [Header("SFX")]
        [Tooltip("SFX key to play when castle levels up (uses SFXManager)")]
        [SerializeField] private string levelUpSfxKey = "";
        [Header("Arena Image")]
        [SerializeField] private Image arenaImage;           

        [Header("Scene Naming")] 
        [SerializeField] private string battleSceneNameFormat = "Arena{0}BattleScene"; 

        [Header("Play Gating")]
        [SerializeField] private Button playButton;              
        [SerializeField] private TMP_Text deckWarningText;       

        
        private Coroutine _deckFlashCo;
        private Color _deckWarnBaseColor = Color.white;
        private Vector3 _playBtnBaseScale = Vector3.one;
        
        private Coroutine _xpGainFadeCo;

        private void OnEnable()
        {
            Refresh();
            PlayerProfile.OnSoftCurrencyChanged += HandleSoftCurrencyChanged;
            if (deckWarningText != null) _deckWarnBaseColor = deckWarningText.color;
            if (playButton != null) _playBtnBaseScale = playButton.transform.localScale;
            
            if (_banCountdownCo != null) StopCoroutine(_banCountdownCo);
            _banCountdownCo = StartCoroutine(BanCountdownUpdater());
            
            if (castleXPGainText)
            {
                var cg = castleXPGainText.GetComponent<CanvasGroup>();
                if (cg == null) cg = castleXPGainText.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                castleXPGainText.gameObject.SetActive(false);
            }
        }

        
        public void OnClickArenaImage()
        {
            var panel = FindFirstObjectByType<TR.UI.TrophyRoad.TrophyRoadPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogWarning("[PlayPanelUI] TrophyRoadPanel not found in scene. Add it to the Lobby scene.");
                return;
            }
            panel.Show();
        }

        private void OnDisable()
        {
            PlayerProfile.OnSoftCurrencyChanged -= HandleSoftCurrencyChanged;
            StopDeckFlash();
            RestoreDeckWarningVisuals();
            if (_banCountdownCo != null) { StopCoroutine(_banCountdownCo); _banCountdownCo = null; }
            
            if (_xpGainFadeCo != null)
            {
                StopCoroutine(_xpGainFadeCo);
                _xpGainFadeCo = null;
            }
            if (castleXPGainText)
            {
                var cg = castleXPGainText.GetComponent<CanvasGroup>();
                if (cg == null) cg = castleXPGainText.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                castleXPGainText.gameObject.SetActive(false);
            }
        }

        private void HandleSoftCurrencyChanged(int newBalance)
        {
            if (softCurrencyText)
            {
                softCurrencyText.text = $"Coins: {newBalance}";
            }
            UpdateBanGatingUI();
        }

        public void Refresh()
        {
            GameDB.EnsureLoaded();
            int trophies = PlayerProfile.GetTrophies();
            int soft = PlayerProfile.GetSoftCurrency();
            var currentArena = ArenaService.GetArenaForTrophies(trophies);
            var nextArena = ArenaService.GetNextArena();
            var castleCfg = GameDB.GetCastleProgression();
            int castleLevel = PlayerProfile.GetCastleLevel();
            int castleXP = PlayerProfile.GetCastleXP();

            if (trophiesText)
                trophiesText.text = $"Trophies: {trophies}";
            if (softCurrencyText)
                softCurrencyText.text = $"Coins: {soft}";

            if (arenaNameText)
                arenaNameText.text = currentArena != null ? $"Arena: {currentArena.DisplayName}" : "Arena: -";

            if (arenaImage)
            {
                arenaImage.sprite = currentArena != null ? currentArena.ArenaImage : null;
                arenaImage.enabled = arenaImage.sprite != null;
            }

            if (nextArenaText)
            {
                if (nextArena != null)
                {
                    int need = Mathf.Max(0, nextArena.TrophyRequirement - trophies);
                    nextArenaText.text = $"Next: {nextArena.DisplayName} in {need} trophies";
                }
                else
                {
                    nextArenaText.text = "Next: Max Arena";
                }
            }

            
            
            
            if (PlayerProfile.TryConsumePendingCastleXp(out var pendingDelta) && pendingDelta > 0 && castleCfg != null)
            {
                var prev = ComputePreviousCastleState(castleCfg, castleLevel, castleXP, pendingDelta);
                
                UpdateCastleUIInstant(castleCfg, prev.level, prev.xp);
                
                if (_castleAnimCo != null) StopCoroutine(_castleAnimCo);
                _castleAnimCo = StartCoroutine(AnimateCastleXpGain(castleCfg, prev.level, prev.xp, castleLevel, castleXP, pendingDelta));
            }
            else
            {
                UpdateCastleUIInstant(castleCfg, castleLevel, castleXP);
                
                if (castleXPGainText)
                {
                    var cg = castleXPGainText.GetComponent<CanvasGroup>();
                    if (cg == null) cg = castleXPGainText.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    castleXPGainText.gameObject.SetActive(false);
                }
            }

            
            UpdateBanGatingUI();
        }

        
        public void ForceRefreshFromButton() => Refresh();

        
        public async void OnClickPlay()
        {
            
            if (PlayerProfile.Data == null || PlayerProfile.Data.deck == null || PlayerProfile.Data.deck.Count == 0)
            {
                Debug.LogWarning("[PlayPanelUI] Cannot play: deck is empty. Build a deck first.");
                
                ShowAndFlashDeckWarning();
                return;
            }
            GameDB.EnsureLoaded();
            int trophies = PlayerProfile.GetTrophies();
            var currentArena = ArenaService.GetArenaForTrophies(trophies);
            var arenas = GameDB.GetArenasSortedByRequirement();
            if (currentArena == null || arenas == null || arenas.Count == 0)
            {
                Debug.LogWarning("[PlayPanelUI] No arenas available or current arena not found.");
                return;
            }
            int index = 0;
            for (int i = 0; i < arenas.Count; i++)
            {
                if (arenas[i] == currentArena) { index = i; break; }
            }
            
            string arenaIdOrIndex = !string.IsNullOrEmpty(currentArena.ArenaId) ? currentArena.ArenaId : (index + 1).ToString();
            string sceneName = string.Format(battleSceneNameFormat, arenaIdOrIndex);
            Debug.Log($"[PlayPanelUI] Loading battle scene '{sceneName}' for arena '{arenaIdOrIndex}' ({currentArena.DisplayName}).");
            
            if (currentArena != null && !string.IsNullOrEmpty(currentArena.DisplayName))
            {
                TR.Infrastructure.SceneFader.Instance.SetNextTransitionMessage(currentArena.DisplayName, 1.0f);
            }
            await SceneFader.Instance.LoadSceneWithFade(sceneName);
        }

        private void ShowAndFlashDeckWarning()
        {
            if (deckWarningText)
            {
                deckWarningText.gameObject.SetActive(true);
                if (_deckWarnBaseColor == default(Color)) _deckWarnBaseColor = deckWarningText.color;
                deckWarningText.text = "Build a deck to play";
            }
            if (playButton) playButton.interactable = false;
            StopDeckFlash();
            _deckFlashCo = StartCoroutine(DeckFlashCo());
        }

        private System.Collections.IEnumerator DeckFlashCo()
        {
            const float dur = 0.6f;
            float t = 0f;
            if (playButton != null && _playBtnBaseScale == Vector3.one)
                _playBtnBaseScale = playButton.transform.localScale;
            Color pulse = new Color(1f, 0.3f, 0.3f, deckWarningText != null ? deckWarningText.color.a : 1f);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float ping = Mathf.PingPong(u * 2f, 1f);
                if (deckWarningText) deckWarningText.color = Color.Lerp(_deckWarnBaseColor, pulse, ping);
                if (playButton) playButton.transform.localScale = _playBtnBaseScale * (1f + 0.06f * ping);
                yield return null;
            }
            RestoreDeckWarningVisuals();
            _deckFlashCo = null;
        }

        private void StopDeckFlash()
        {
            if (_deckFlashCo != null)
            {
                StopCoroutine(_deckFlashCo);
                _deckFlashCo = null;
            }
        }

        private void RestoreDeckWarningVisuals()
        {
            if (deckWarningText) deckWarningText.color = _deckWarnBaseColor == default(Color) ? deckWarningText.color : _deckWarnBaseColor;
            if (playButton) playButton.transform.localScale = _playBtnBaseScale;
        }

        
        private Coroutine _castleAnimCo;
        private struct CastleState { public int level; public int xp; }

        private void UpdateCastleUIInstant(TR.Data.CastleProgression castleCfg, int level, int xp)
        {
            if (castleLevelText) castleLevelText.text = $"Castle Lv {level}";
            if (castleXPText)
            {
                if (castleCfg != null)
                {
                    int maxL = Mathf.Max(1, castleCfg.MaxLevel);
                    if (level >= maxL)
                    {
                        castleXPText.text = "XP: MAX";
                    }
                    else
                    {
                        int needed = castleCfg.GetXPForLevel(level);
                        castleXPText.text = $"XP: {xp}/{needed}";
                    }
                }
                else castleXPText.text = $"XP: {xp}";
            }
            if (castleProgressSlider)
            {
                if (castleCfg != null)
                {
                    int maxL = Mathf.Max(1, castleCfg.MaxLevel);
                    if (level >= maxL)
                    {
                        castleProgressSlider.value = 1f;
                    }
                    else
                    {
                        int needed = Mathf.Max(1, castleCfg.GetXPForLevel(level));
                        castleProgressSlider.value = Mathf.Clamp01((float)xp / needed);
                    }
                }
                else castleProgressSlider.value = 0f;
            }
        }

        private CastleState ComputePreviousCastleState(TR.Data.CastleProgression castleCfg, int currentLevel, int currentXp, int addedDelta)
        {
            int level = currentLevel;
            int xp = currentXp;
            int remain = Mathf.Max(0, addedDelta);
            while (remain > 0 && level > 1)
            {
                if (xp >= remain)
                {
                    xp -= remain; remain = 0; break;
                }
                else
                {
                    remain -= xp;
                    int prevLevel = level - 1;
                    int prevNeeded = Mathf.Max(1, castleCfg.GetXPForLevel(prevLevel));
                    xp = prevNeeded;
                    level = prevLevel;
                }
            }
            if (remain > 0)
            {
                xp = Mathf.Max(0, xp - remain);
                remain = 0;
            }
            return new CastleState { level = level, xp = xp };
        }

        private System.Collections.IEnumerator AnimateCastleXpGain(TR.Data.CastleProgression castleCfg,
            int fromLevel, int fromXp, int toLevel, int toXp, int totalDelta)
        {
            
            if (castleXPGainText)
            {
                float d = Mathf.Max(0f, castleXPGainDelay);
                if (d > 0f) yield return new WaitForSecondsRealtime(d);
                castleXPGainText.gameObject.SetActive(true);
                castleXPGainText.text = $"+{totalDelta} XP";
                
                var cg = castleXPGainText.GetComponent<CanvasGroup>();
                if (cg == null) cg = castleXPGainText.gameObject.AddComponent<CanvasGroup>();
                
                if (_xpGainFadeCo != null) { StopCoroutine(_xpGainFadeCo); _xpGainFadeCo = null; }
                cg.alpha = 1f;
                _xpGainFadeCo = StartCoroutine(FadeOutXPGain(cg, 0.9f));
            }

            int curLevel = fromLevel;
            float curXpF = fromXp; 
            UpdateCastleUIInstant(castleCfg, curLevel, Mathf.FloorToInt(curXpF));

            
            float speed = Mathf.Max(1f, castleXpAnimSpeed); 
            float remainingVisual = ComputeVisualDistance(castleCfg, fromLevel, fromXp, toLevel, toXp);
            float progressed = 0f;
            while (progressed < remainingVisual - 0.5f)
            {
                float step = speed * Time.unscaledDeltaTime;
                progressed += step;
                
                curXpF += step;
                int needed = curLevel < castleCfg.MaxLevel ? Mathf.Max(1, castleCfg.GetXPForLevel(curLevel)) : 1;
                while (curLevel < castleCfg.MaxLevel && curXpF >= needed)
                {
                    curXpF -= needed;
                    curLevel++;
                    
                    TryPlayLevelUpVfx(0f);
                    needed = curLevel < castleCfg.MaxLevel ? Mathf.Max(1, castleCfg.GetXPForLevel(curLevel)) : 1;
                }
                if (curLevel >= toLevel)
                {
                    
                    if (curLevel == toLevel && curXpF > toXp) curXpF = toXp;
                }
                UpdateCastleUIInstant(castleCfg, curLevel, Mathf.FloorToInt(curXpF));
                yield return null;
            }
            UpdateCastleUIInstant(castleCfg, toLevel, toXp);
            _castleAnimCo = null;
        }

        
        
        private void TryPlayLevelUpVfx(float delaySeconds)
        {
            if (string.IsNullOrWhiteSpace(levelUpVfxKey)) return;
            float d = Mathf.Max(0f, delaySeconds);
            StartCoroutine(SpawnLevelUpVfxAfterDelay(d));
        }

        private System.Collections.IEnumerator SpawnLevelUpVfxAfterDelay(float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            
            if (!string.IsNullOrWhiteSpace(levelUpSfxKey) && TR.Audio.SFXManager.Instance != null)
            {
                TR.Audio.SFXManager.Instance.Play(levelUpSfxKey);
            }
            
            
            var cam = Camera.main;
            var positions = new System.Collections.Generic.List<Vector3>(3);
            Transform parent = null;
            if (levelUpVfxParent != null)
            {
                Vector3 basePos = levelUpVfxParent.position;
                parent = levelUpVfxParent;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 jitter = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.2f), 0f);
                    positions.Add(basePos + jitter);
                }
            }
            else if (cam != null)
            {
                float z = 5f; 
                float[] xs = new float[] { 0.2f, 0.5f, 0.8f };
                for (int i = 0; i < xs.Length; i++)
                {
                    float vx = xs[i] + Random.Range(-0.05f, 0.05f);
                    float vy = 0.55f + Random.Range(-0.1f, 0.1f);
                    Vector3 wp = cam.ViewportToWorldPoint(new Vector3(Mathf.Clamp01(vx), Mathf.Clamp01(vy), z));
                    positions.Add(wp);
                }
                parent = null;
            }
            else
            {
                
                Vector3 center = transform.position + Vector3.up * 1.5f;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 offset = new Vector3((i - 1) * 1.2f, 0f, 0f) + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0f);
                    positions.Add(center + offset);
                }
                parent = null;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                try
                {
                    TR.VFX.ParticleManager.Spawn(levelUpVfxKey, positions[i], Quaternion.identity, parent, true);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PlayPanelUI] Failed to spawn level-up VFX with key '{levelUpVfxKey}': {ex.Message}");
                }
                if (levelUpVfxBurstStagger > 0f && i < positions.Count - 1)
                    yield return new WaitForSecondsRealtime(levelUpVfxBurstStagger);
            }
        }

        private float ComputeVisualDistance(TR.Data.CastleProgression castleCfg, int fromLevel, int fromXp, int toLevel, int toXp)
        {
            if (toLevel < fromLevel || (toLevel == fromLevel && toXp < fromXp)) return 0f;
            float dist = 0f;
            int l = fromLevel;
            int x = fromXp;
            while (l < toLevel)
            {
                int need = Mathf.Max(1, castleCfg.GetXPForLevel(l));
                dist += Mathf.Max(0, need - x);
                l++;
                x = 0;
            }
            dist += Mathf.Max(0, toXp - x);
            return dist;
        }

        private void Update()
        {
            if (debugTriggerLevelUpVfxWithL && Input.GetKeyDown(KeyCode.L))
            {
                
                TryPlayLevelUpVfx(0f);
                Debug.Log("Level up vfx played");
            }
        }

        private System.Collections.IEnumerator FadeOut(CanvasGroup cg, float duration)
        {
            float t = 0f; float d = Mathf.Max(0.05f, duration);
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / d);
                yield return null;
            }
            cg.alpha = 0f;
            cg.gameObject.SetActive(false);
        }

        
        private System.Collections.IEnumerator FadeOutXPGain(CanvasGroup cg, float duration)
        {
            float t = 0f; float d = Mathf.Max(0.05f, duration);
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / d);
                yield return null;
            }
            cg.alpha = 0f;
            cg.gameObject.SetActive(false);
            _xpGainFadeCo = null;
        }

        private Coroutine _banCountdownCo;
        private System.Collections.IEnumerator BanCountdownUpdater()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (isActiveAndEnabled)
            {
                UpdateBanGatingUI();
                yield return wait;
            }
        }

        private void UpdateBanGatingUI()
        {
            bool hasDeck = PlayerProfile.Data != null && PlayerProfile.Data.deck != null && PlayerProfile.Data.deck.Count > 0;
            bool banned = PlayerProfile.IsBanned(out var remaining);
            if (playButton)
            {
                playButton.interactable = hasDeck && !banned;
            }
            if (deckWarningText)
            {
                if (banned)
                {
                    deckWarningText.gameObject.SetActive(true);
                    deckWarningText.text = $"Temporarily restricted: {FormatRemaining(remaining)} remaining";
                }
                else
                {
                    deckWarningText.gameObject.SetActive(!hasDeck);
                    if (!hasDeck) deckWarningText.text = "Build a deck to play";
                }
            }
        }

        private static string FormatRemaining(System.TimeSpan remaining)
        {
            if (remaining.TotalHours >= 1.0)
            {
                int h = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalHours));
                int m = Mathf.Max(0, Mathf.CeilToInt((float)(remaining.TotalMinutes % 60)));
                return m > 0 ? $"{h}h {m}m" : $"{h}h";
            }
            else if (remaining.TotalMinutes >= 1.0)
            {
                int m = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalMinutes));
                return $"{m}m";
            }
            else
            {
                int s = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));
                return $"{s}s";
            }
        }
    }
}
