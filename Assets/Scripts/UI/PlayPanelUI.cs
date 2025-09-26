using UnityEngine;
using TMPro;
using TR.Systems;
using TR.Data;
using UnityEngine.SceneManagement;
using TR.Infrastructure;
using UnityEngine.UI;

namespace TR.UI
{
    // Attach this to the Play tab root in the Lobby scene and assign the text fields.
    public class PlayPanelUI : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text trophiesText;
        [SerializeField] private TMP_Text arenaNameText;
        [SerializeField] private TMP_Text nextArenaText;
        [SerializeField] private TMP_Text castleLevelText;   // new
        [SerializeField] private TMP_Text castleXPText;      // new
        [SerializeField] private TMP_Text softCurrencyText;  // new
        [Header("Castle Progress UI")]
        [Tooltip("Slider for castle XP progress (0-1 value)")]
        [SerializeField] private Slider castleProgressSlider;
        [Tooltip("Optional text for showing '+XP' when gains are applied")]
        [SerializeField] private TMP_Text castleXPGainText;
        [Tooltip("Delay before showing the '+XP' splash (seconds)")]
        [SerializeField] private float castleXPGainDelay = 0.25f;
        [Tooltip("Castle XP bar animation speed (XP per second, visual only)")]
        [SerializeField] private float castleXpAnimSpeed = 250f;
        [Header("Castle Level-Up VFX")]
        [Tooltip("Particle/VFX key to play when castle levels up (uses ParticleManager via reflection)")]
        [SerializeField] private string levelUpVfxKey = "";
        [Tooltip("Optional transform to use as spawn parent/anchor for level-up VFX")]
        [SerializeField] private Transform levelUpVfxParent;
        [Tooltip("If true, pressing 'L' will trigger the level-up VFX for testing")] 
        [SerializeField] private bool debugTriggerLevelUpVfxWithL = true;
        [Tooltip("Delay between each VFX burst (seconds)")]
        [SerializeField] private float levelUpVfxBurstStagger = 0.08f;
        [Header("SFX")]
        [Tooltip("SFX key to play when castle levels up (uses SFXManager)")]
        [SerializeField] private string levelUpSfxKey = "";
        [Header("Arena Image")]
        [SerializeField] private Image arenaImage;           // shows current arena image and opens Trophy Road

        [Header("Scene Naming")] 
        [SerializeField] private string battleSceneNameFormat = "Arena{0}BattleScene"; // {0} = Arena ID (falls back to 1-based index if empty)

        [Header("Play Gating")]
        [SerializeField] private Button playButton;              // optional: assign to control interactivity
        [SerializeField] private TMP_Text deckWarningText;       // optional: "Build a deck to play" message

        // Flash state for deck warning feedback
        private Coroutine _deckFlashCo;
        private Color _deckWarnBaseColor = Color.white;
        private Vector3 _playBtnBaseScale = Vector3.one;
        // Castle XP splash fade state
        private Coroutine _xpGainFadeCo;

        private void OnEnable()
        {
            Refresh();
            PlayerProfile.OnSoftCurrencyChanged += HandleSoftCurrencyChanged;
            if (deckWarningText != null) _deckWarnBaseColor = deckWarningText.color;
            if (playButton != null) _playBtnBaseScale = playButton.transform.localScale;
            // Start a lightweight updater to refresh ban countdown every second
            if (_banCountdownCo != null) StopCoroutine(_banCountdownCo);
            _banCountdownCo = StartCoroutine(BanCountdownUpdater());
            // Ensure XP splash starts hidden on enable
            if (castleXPGainText)
            {
                var cg = castleXPGainText.GetComponent<CanvasGroup>();
                if (cg == null) cg = castleXPGainText.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                castleXPGainText.gameObject.SetActive(false);
            }
        }

        // Hook this to the arena image/button to open the Trophy Road panel
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
            // Stop any active XP splash fade and hide it to avoid getting stuck
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

            // Castle info + progress bar
            // If there is pending XP to present, render PREVIOUS state first to avoid a visual snap-back,
            // then animate forward to current. Otherwise, just show current instantly.
            if (PlayerProfile.TryConsumePendingCastleXp(out var pendingDelta) && pendingDelta > 0 && castleCfg != null)
            {
                var prev = ComputePreviousCastleState(castleCfg, castleLevel, castleXP, pendingDelta);
                // Show previous state immediately
                UpdateCastleUIInstant(castleCfg, prev.level, prev.xp);
                // Then animate to current
                if (_castleAnimCo != null) StopCoroutine(_castleAnimCo);
                _castleAnimCo = StartCoroutine(AnimateCastleXpGain(castleCfg, prev.level, prev.xp, castleLevel, castleXP, pendingDelta));
            }
            else
            {
                UpdateCastleUIInstant(castleCfg, castleLevel, castleXP);
                // No pending XP: ensure splash is hidden
                if (castleXPGainText)
                {
                    var cg = castleXPGainText.GetComponent<CanvasGroup>();
                    if (cg == null) cg = castleXPGainText.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    castleXPGainText.gameObject.SetActive(false);
                }
            }

            // Deck + ban gating on refresh as well (so UI is correct on open)
            UpdateBanGatingUI();
        }

        // Optional: call this from a button to re-pull data after a match or debug grant
        public void ForceRefreshFromButton() => Refresh();

        // Hook this to the Play button
        public async void OnClickPlay()
        {
            // Hard guard in case button wasn't wired
            if (PlayerProfile.Data == null || PlayerProfile.Data.deck == null || PlayerProfile.Data.deck.Count == 0)
            {
                Debug.LogWarning("[PlayPanelUI] Cannot play: deck is empty. Build a deck first.");
                // Visible feedback: show and flash the warning text, pulse the Play button
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
            // Prefer ArenaId string; if empty/null, fallback to 1-based index
            string arenaIdOrIndex = !string.IsNullOrEmpty(currentArena.ArenaId) ? currentArena.ArenaId : (index + 1).ToString();
            string sceneName = string.Format(battleSceneNameFormat, arenaIdOrIndex);
            Debug.Log($"[PlayPanelUI] Loading battle scene '{sceneName}' for arena '{arenaIdOrIndex}' ({currentArena.DisplayName}).");
            // Show arena name on the fade screen briefly
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

        // ===== Castle Progress Animation =====
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
            // Show +XP splash after a small delay
            if (castleXPGainText)
            {
                float d = Mathf.Max(0f, castleXPGainDelay);
                if (d > 0f) yield return new WaitForSecondsRealtime(d);
                castleXPGainText.gameObject.SetActive(true);
                castleXPGainText.text = $"+{totalDelta} XP";
                // quick fade/pulse
                var cg = castleXPGainText.GetComponent<CanvasGroup>();
                if (cg == null) cg = castleXPGainText.gameObject.AddComponent<CanvasGroup>();
                // Cancel any previous fade
                if (_xpGainFadeCo != null) { StopCoroutine(_xpGainFadeCo); _xpGainFadeCo = null; }
                cg.alpha = 1f;
                _xpGainFadeCo = StartCoroutine(FadeOutXPGain(cg, 0.9f));
            }

            int curLevel = fromLevel;
            float curXpF = fromXp; // use float accumulator for smooth fill and counting
            UpdateCastleUIInstant(castleCfg, curLevel, Mathf.FloorToInt(curXpF));

            // Animate forward to target, handling level-ups
            float speed = Mathf.Max(1f, castleXpAnimSpeed); // XP per second visual speed
            float remainingVisual = ComputeVisualDistance(castleCfg, fromLevel, fromXp, toLevel, toXp);
            float progressed = 0f;
            while (progressed < remainingVisual - 0.5f)
            {
                float step = speed * Time.unscaledDeltaTime;
                progressed += step;
                // advance xp/levels accordingly
                curXpF += step;
                int needed = curLevel < castleCfg.MaxLevel ? Mathf.Max(1, castleCfg.GetXPForLevel(curLevel)) : 1;
                while (curLevel < castleCfg.MaxLevel && curXpF >= needed)
                {
                    curXpF -= needed;
                    curLevel++;
                    // VFX should play after the splash delay has already occurred, so do not add extra delay here
                    TryPlayLevelUpVfx(0f);
                    needed = curLevel < castleCfg.MaxLevel ? Mathf.Max(1, castleCfg.GetXPForLevel(curLevel)) : 1;
                }
                if (curLevel >= toLevel)
                {
                    // clamp to target at the end
                    if (curLevel == toLevel && curXpF > toXp) curXpF = toXp;
                }
                UpdateCastleUIInstant(castleCfg, curLevel, Mathf.FloorToInt(curXpF));
                yield return null;
            }
            UpdateCastleUIInstant(castleCfg, toLevel, toXp);
            _castleAnimCo = null;
        }

        // Trigger level-up VFX using the project's ParticleManager static API
        // delaySeconds is an additional delay; pass 0 during the XP animation since we already waited for the splash delay.
        private void TryPlayLevelUpVfx(float delaySeconds)
        {
            if (string.IsNullOrWhiteSpace(levelUpVfxKey)) return;
            float d = Mathf.Max(0f, delaySeconds);
            StartCoroutine(SpawnLevelUpVfxAfterDelay(d));
        }

        private System.Collections.IEnumerator SpawnLevelUpVfxAfterDelay(float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            // Play level-up SFX once
            if (!string.IsNullOrWhiteSpace(levelUpSfxKey) && TR.Audio.SFXManager.Instance != null)
            {
                TR.Audio.SFXManager.Instance.Play(levelUpSfxKey);
            }
            // Default: world-space spawns under ParticleManager (parent = null)
            // If a parent is explicitly provided, use its position for all three spawns
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
                float z = 5f; // distance in front of camera
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
                // Fallback: around this transform in world space
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
                // For debug trigger, spawn immediately
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

        // Same as FadeOut but also clears the _xpGainFadeCo reference when done
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
