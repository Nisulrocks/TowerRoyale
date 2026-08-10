using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data.Progression;
using TR.Systems;
using TR.UI.TrophyRoad;

namespace TR.UI
{
    public class RewardClaimFX : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float dimFade = 0.2f;
        [SerializeField] private float cardPopSeconds = 0.35f;
        [SerializeField] private float autoCollectAfter = 0f;  
        [SerializeField] private float burstSeconds = 0.3f;
        [SerializeField] private float flySeconds = 0.55f;
        [SerializeField] private float coinStagger = 0.045f;

        [Header("Look")]
        [SerializeField] private int maxCoins = 16;
        [SerializeField] private float dimAlpha = 0.68f;
        [SerializeField] private Color coinColor = new Color(1f, 0.82f, 0.24f);
        [SerializeField] private float coinSize = 54f;
        [SerializeField] private float burstRadius = 190f;

        [Header("Counter Wait")]
        [SerializeField] private float counterWaitTimeout = 120f;

        [Header("SFX")]
        [SerializeField] private string collectSfxKey = "ui_popup_show";
        [SerializeField] private string coinLandSfxKey = "ui_level_up";

        private static RewardClaimFX _instance;
        private static Sprite _coinSprite;

        private RectTransform _root;
        private Image _dim;

        private struct Pending { public RewardDefinition reward; public int gained; }
        private readonly Queue<Pending> _queue = new Queue<Pending>();

        private Coroutine _cardPump;
        private Coroutine _coinFlight;

        private bool _hasPendingCoins;
        private int _pendingFrom;
        private int _pendingTo;

        public static void Present(RewardDefinition reward, int coinsBefore, int coinsAfter)
        {
            var fx = Ensure();
            if (fx == null) return;

            fx.gameObject.SetActive(true);

            fx._queue.Enqueue(new Pending
            {
                reward = reward,
                gained = Mathf.Max(0, coinsAfter - coinsBefore)
            });

            if (!fx._hasPendingCoins)
            {
                fx._pendingFrom = coinsBefore;
                fx._hasPendingCoins = true;
            }
            fx._pendingTo = coinsAfter;

            if (fx._pendingTo > fx._pendingFrom)
                PlayPanelUI.SetCoinDisplayOverride(fx._pendingFrom);

            Debug.Log($"[RewardClaimFX] Queued reward (coins {coinsBefore} -> {coinsAfter}); " +
                      $"queue={fx._queue.Count}, pending={fx._pendingFrom}->{fx._pendingTo}.");

            if (fx._cardPump == null) fx._cardPump = fx.StartCoroutine(fx.CardPump());
            if (fx._coinFlight == null) fx._coinFlight = fx.StartCoroutine(fx.CoinFlight());
        }

        private static RewardClaimFX Ensure()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("RewardClaimFX");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RewardClaimFX>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _root = (RectTransform)transform;

            _dim = NewImage("Dim", _root);
            Stretch(_dim.rectTransform);
            _dim.color = new Color(0f, 0f, 0f, 0f);
            _dim.raycastTarget = false;

            gameObject.SetActive(false);
        }

        private IEnumerator CardPump()
        {
            _dim.raycastTarget = true;
            yield return StartCoroutine(FadeDim(dimAlpha));

            while (_queue.Count > 0)
            {
                var pending = _queue.Dequeue();
                var card = BuildCard(pending.reward, pending.gained);

                yield return StartCoroutine(PopIn(card, cardPopSeconds));

                if (!string.IsNullOrEmpty(collectSfxKey)) TR.Audio.SFXManager.Instance?.Play(collectSfxKey);

                yield return StartCoroutine(WaitForCollect());
                yield return StartCoroutine(FadeOutCard(card));

                if (card != null) Destroy(card.gameObject);
            }

            yield return StartCoroutine(FadeDim(0f));
            _dim.raycastTarget = false;

            _cardPump = null;
            TryIdle();
        }

        private IEnumerator CoinFlight()
        {
            RectTransform counter = null;
            float waited = 0f;

            while (true)
            {
                bool cardsDone = _cardPump == null && _queue.Count == 0;

                if (cardsDone && !TrophyRoadPanel.IsOpen)
                {
                    counter = PlayPanelUI.CoinCounterRect;
                    if (counter != null) break;

                    waited += Time.unscaledDeltaTime;
                    if (waited > counterWaitTimeout)
                    {
                        Debug.LogWarning("[RewardClaimFX] Coin counter never became visible; skipping the fly-in.");
                        PlayPanelUI.SetCoinDisplayOverride(null);
                        _hasPendingCoins = false;
                        _coinFlight = null;
                        TryIdle();
                        yield break;
                    }
                }
                yield return null;
            }

            int from = _pendingFrom, to = _pendingTo;
            if (to > from) yield return StartCoroutine(FlyCoins(counter, from, to));

            PlayPanelUI.SetCoinDisplayOverride(null);
            _hasPendingCoins = false;
            _coinFlight = null;
            TryIdle();
        }

        private void TryIdle()
        {
            if (_cardPump == null && _coinFlight == null && _queue.Count == 0)
                gameObject.SetActive(false);
        }

        private IEnumerator WaitForCollect()
        {
            float waited = 0f;
            while (true)
            {
                waited += Time.unscaledDeltaTime;
                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    yield break;
                if (autoCollectAfter > 0f && waited >= autoCollectAfter)
                    yield break;
                yield return null;
            }
        }

        private IEnumerator FlyCoins(RectTransform counter, int coinsBefore, int coinsAfter)
        {
            if (counter == null) yield break;

            int gained = coinsAfter - coinsBefore;
            Vector2 origin = Vector2.zero;                 
            Vector2 target = UiToLocal(counter);

            int count = Mathf.Clamp(gained, 1, maxCoins);
            var coins = new List<RectTransform>(count);
            var bursts = new List<Vector2>(count);

            for (int i = 0; i < count; i++)
            {
                var img = NewImage("Coin", _root);
                var art = CoinArt();
                img.sprite = art;
                img.color = art == _coinSprite ? coinColor : Color.white;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                Center(rt, origin, coinSize);
                coins.Add(rt);

                float ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                bursts.Add(origin + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * burstRadius * Random.Range(0.6f, 1.1f));
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, burstSeconds);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                for (int i = 0; i < coins.Count; i++)
                    if (coins[i] != null) coins[i].anchoredPosition = Vector2.Lerp(origin, bursts[i], e);
                yield return null;
            }

            int landed = 0;
            var done = new bool[coins.Count];
            float elapsed = 0f;
            float lastDuration = coinStagger * (coins.Count - 1) + flySeconds;

            while (landed < coins.Count && elapsed < lastDuration + 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;

                var live = PlayPanelUI.CoinCounterRect;
                if (live != null) target = UiToLocal(live);

                for (int i = 0; i < coins.Count; i++)
                {
                    if (done[i] || coins[i] == null) continue;

                    float local = (elapsed - i * coinStagger) / Mathf.Max(0.01f, flySeconds);
                    if (local <= 0f) continue;

                    if (local >= 1f)
                    {
                        done[i] = true;
                        landed++;

                        int shown = Mathf.RoundToInt(Mathf.Lerp(coinsBefore, coinsAfter, landed / (float)coins.Count));
                        PlayPanelUI.SetCoinDisplayOverride(shown);
                        if (live != null) StartCoroutine(PunchRoutine(live));
                        if (!string.IsNullOrEmpty(coinLandSfxKey) && (i % 3 == 0))
                            TR.Audio.SFXManager.Instance?.Play(coinLandSfxKey);

                        Destroy(coins[i].gameObject);
                        coins[i] = null;
                        continue;
                    }

                    float e = local * local * (3f - 2f * local);
                    Vector2 p0 = bursts[i];
                    Vector2 ctrl = Vector2.Lerp(p0, target, 0.5f) + new Vector2(0f, 220f);
                    Vector2 pos = (1 - e) * (1 - e) * p0 + 2 * (1 - e) * e * ctrl + e * e * target;
                    coins[i].anchoredPosition = pos;
                    coins[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, e);
                }
                yield return null;
            }

            for (int i = 0; i < coins.Count; i++)
                if (coins[i] != null) Destroy(coins[i].gameObject);
        }

        private IEnumerator PunchRoutine(RectTransform rt)
        {
            float t = 0f;
            while (t < 1f)
            {
                if (rt == null) yield break;
                t += Time.unscaledDeltaTime / 0.16f;
                float e = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                rt.localScale = Vector3.one * (1f + 0.18f * e);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        private IEnumerator FadeDim(float to)
        {
            float from = _dim.color.a;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dimFade);
                _dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t)));
                yield return null;
            }
            _dim.color = new Color(0f, 0f, 0f, to);
        }

        private RectTransform BuildCard(RewardDefinition reward, int gained)
        {
            var panel = NewImage("RewardCard", _root);
            panel.color = new Color(0.10f, 0.12f, 0.22f, 0.96f);
            Center(panel.rectTransform, Vector2.zero, 0f);
            panel.rectTransform.sizeDelta = new Vector2(560f, 620f);

            panel.rectTransform.localScale = Vector3.zero;

            panel.gameObject.AddComponent<CanvasGroup>();

            var title = NewText("Title", panel.rectTransform, "REWARD", 34f, TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -32f);
            title.rectTransform.sizeDelta = new Vector2(0f, 50f);
            title.color = new Color(1f, 0.82f, 0.24f);

            var icon = NewImage("Icon", panel.rectTransform);
            icon.sprite = reward != null ? reward.GetIcon() : null;
            if (icon.sprite == null)
            {
                var art = CoinArt();
                icon.sprite = art;
                icon.color = art == _coinSprite ? coinColor : Color.white;
            }
            icon.preserveAspect = true;
            Center(icon.rectTransform, new Vector2(0f, 40f), 260f);

            string label = reward != null ? reward.GetDisplayName() : (gained > 0 ? $"Coins x{gained}" : "Reward");
            var name = NewText("Name", panel.rectTransform, label, 30f, TextAlignmentOptions.Center);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 0f);
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.rectTransform.anchoredPosition = new Vector2(0f, 120f);
            name.rectTransform.sizeDelta = new Vector2(0f, 60f);

            var hint = NewText("Hint", panel.rectTransform, "Tap to collect", 20f, TextAlignmentOptions.Center);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 46f);
            hint.rectTransform.sizeDelta = new Vector2(0f, 40f);
            hint.color = new Color(1f, 1f, 1f, 0.6f);

            return panel.rectTransform;
        }

        private IEnumerator PopIn(RectTransform rt, float seconds)
        {
            float t = 0f;
            while (t < 1f)
            {
                if (rt == null) yield break;
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
                float e = Mathf.Clamp01(t);
                float s = 1f - Mathf.Pow(1f - e, 3f);
                s += Mathf.Sin(e * Mathf.PI) * 0.12f;
                rt.localScale = Vector3.one * s;
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        private IEnumerator FadeOutCard(RectTransform rt)
        {
            if (rt == null) yield break;

            var group = rt.gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();

            float t = 0f;
            while (t < 1f)
            {
                if (rt == null || group == null) yield break;
                t += Time.unscaledDeltaTime / 0.22f;
                float e = Mathf.Clamp01(t);
                group.alpha = 1f - e;
                rt.localScale = Vector3.one * (1f - 0.15f * e);
                yield return null;
            }
        }

        private Vector2 UiToLocal(RectTransform source)
        {
            if (source == null) return Vector2.zero;

            var canvas = source.GetComponentInParent<Canvas>();
            Camera srcCam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                srcCam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(srcCam, source.position);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
            return local;
        }

        private static Image NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<Image>();
        }

        private static TMP_Text NewText(string name, Transform parent, string content, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = size;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt, Vector2 pos, float size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            if (size > 0f) rt.sizeDelta = new Vector2(size, size);
        }

        private static Sprite CoinArt()
        {
            var real = PlayPanelUI.CoinIcon;
            return real != null ? real : CoinSprite();
        }

        private static Sprite CoinSprite()
        {
            if (_coinSprite != null) return _coinSprite;

            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color[S * S];
            Vector2 c = new Vector2(S * 0.5f, S * 0.5f);
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Vector2.Distance(p, c) / (S * 0.5f);

                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.94f, 1.0f, d));
                    float rim = Mathf.SmoothStep(0.72f, 0.94f, d);
                    float shade = Mathf.Lerp(1f, 0.62f, rim);

                    float spec = 1f - Mathf.Clamp01(Vector2.Distance(p, c + new Vector2(-S * 0.16f, S * 0.16f)) / (S * 0.26f));
                    shade += Mathf.SmoothStep(0f, 1f, spec) * 0.35f;

                    shade = Mathf.Clamp01(shade);
                    px[y * S + x] = new Color(shade, shade, shade, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _coinSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _coinSprite;
        }
    }
}
