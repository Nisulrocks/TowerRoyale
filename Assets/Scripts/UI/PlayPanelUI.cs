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

        private void OnEnable()
        {
            Refresh();
            PlayerProfile.OnSoftCurrencyChanged += HandleSoftCurrencyChanged;
            if (deckWarningText != null) _deckWarnBaseColor = deckWarningText.color;
            if (playButton != null) _playBtnBaseScale = playButton.transform.localScale;
            // Start a lightweight updater to refresh ban countdown every second
            if (_banCountdownCo != null) StopCoroutine(_banCountdownCo);
            _banCountdownCo = StartCoroutine(BanCountdownUpdater());
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

            // Castle info
            if (castleLevelText)
            {
                castleLevelText.text = $"Castle Lv {castleLevel}";
            }
            if (castleXPText)
            {
                if (castleCfg != null)
                {
                    int maxL = Mathf.Max(1, castleCfg.MaxLevel);
                    if (castleLevel >= maxL)
                    {
                        castleXPText.text = $"XP: MAX";
                    }
                    else
                    {
                        int needed = castleCfg.GetXPForLevel(castleLevel);
                        castleXPText.text = $"XP: {castleXP}/{needed}";
                    }
                }
                else
                {
                    castleXPText.text = $"XP: {castleXP}";
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
