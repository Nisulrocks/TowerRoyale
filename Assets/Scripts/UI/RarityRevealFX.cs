using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;

namespace TR.UI
{
    public class RarityRevealFX : MonoBehaviour
    {
        private static RarityRevealFX _instance;

        [Header("Sorting")]
        [SerializeField] private int effectSortingOrder = 200;
        [SerializeField] private int heroSortingOrder = 500;

        [Header("Timing")]
        [SerializeField] private float flashOut = 0.22f;
        [SerializeField] private float holdSeconds = 1.15f;
        [SerializeField] private float settleSeconds = 0.55f;

        [Header("Slow Motion")]
        [SerializeField] private float slowMoScale = 0.4f;
        [SerializeField] private float slowMoEaseIn = 0.35f;
        [SerializeField] private float slowMoRecover = 0.9f;

        [Header("Intensity")]
        [SerializeField] private float shakeStrength = 26f;
        [SerializeField] private int sparkCount = 22;

        [Header("Neighbouring Cards")]
        [SerializeField] private float pushDistance = 190f;
        [SerializeField] private float pushOutSeconds = 0.28f;
        [SerializeField] private float pushReturnSeconds = 0.45f;
        [SerializeField] private float neighbourShake = 14f;

        private Canvas _canvas;
        private RectTransform _root;
        private Image _dim;
        private Image _flash;
        private Coroutine _running;
        private float _restoreTimeScale = 1f;
        private bool _timeScaleHeld;

        private static Sprite _raysSprite, _ringSprite, _glowSprite, _sparkSprite, _solidSprite;

        public static void BeginAnticipation(RarityDefinition rarity)
        {
            if (rarity == null || rarity.RevealTier <= 0) return;
            EnsureInstance();
            _instance.StartAnticipation(rarity);
        }

        public static void Play(RectTransform card, RarityDefinition rarity, System.Collections.Generic.IList<RectTransform> others = null)
        {
            if (rarity == null || rarity.RevealTier <= 0) return;
            EnsureInstance();
            _instance.Begin(card, rarity, others);
        }

        public static bool IsPlaying =>
            _instance != null && (_instance._running != null || _instance._pushed.Count > 0);

        public static void CancelAll()
        {
            if (_instance == null) return;
            _instance.Stop();
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("RarityRevealFX");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RarityRevealFX>();
            _instance.BuildCanvas();
        }

        private void BuildCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = effectSortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _root = (RectTransform)transform;

            _dim = CreateImage("Dim", _root, Solid());
            Stretch(_dim.rectTransform);
            _dim.color = new Color(0f, 0f, 0f, 0f);
            _dim.raycastTarget = false;

            _flash = CreateImage("Flash", _root, Solid());
            Stretch(_flash.rectTransform);
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _flash.raycastTarget = false;

            gameObject.SetActive(true);
        }

        private Coroutine _anticipation;

        private void StartAnticipation(RarityDefinition rarity)
        {
            if (_anticipation != null) StopCoroutine(_anticipation);
            _anticipation = StartCoroutine(AnticipationRoutine(rarity));
        }

        private IEnumerator AnticipationRoutine(RarityDefinition rarity)
        {
            bool mythic = rarity.RevealTier >= 2;
            float target = mythic ? slowMoScale * 0.85f : slowMoScale;
            float from = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, slowMoEaseIn);
                HoldTimeScale(Mathf.Lerp(from, target, Mathf.Clamp01(t)));
                if (_dim != null) _dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.18f, Mathf.Clamp01(t)));
                yield return null;
            }
            _anticipation = null;
        }

        private void Begin(RectTransform card, RarityDefinition rarity, System.Collections.Generic.IList<RectTransform> others)
        {
            if (_anticipation != null) { StopCoroutine(_anticipation); _anticipation = null; }
            if (_running != null) { StopCoroutine(_running); _running = null; }
            ClearTransients();
            LowerCard();
            RestoreNeighbours(true);

            _running = StartCoroutine(Sequence(card, rarity, others));
        }

        private void Stop()
        {
            if (_anticipation != null) { StopCoroutine(_anticipation); _anticipation = null; }
            if (_running != null) { StopCoroutine(_running); _running = null; }
            ReleaseTimeScale();
            LowerCard();
            RestoreNeighbours(true);
            ClearTransients();
            if (_dim != null) _dim.color = new Color(0f, 0f, 0f, 0f);
            if (_flash != null) _flash.color = new Color(1f, 1f, 1f, 0f);
        }


        private Canvas _heroCanvas;
        private GraphicRaycaster _heroRaycaster;
        private bool _addedHeroCanvas, _addedHeroRaycaster;
        private int _heroPrevOrder;
        private bool _heroPrevOverride;

        private void RaiseCard(RectTransform card)
        {
            if (card == null) return;
            LowerCard();

            _heroCanvas = card.GetComponent<Canvas>();
            if (_heroCanvas == null)
            {
                _heroCanvas = card.gameObject.AddComponent<Canvas>();
                _addedHeroCanvas = true;
                if (card.GetComponent<GraphicRaycaster>() == null)
                {
                    _heroRaycaster = card.gameObject.AddComponent<GraphicRaycaster>();
                    _addedHeroRaycaster = true;
                }
            }
            else
            {
                _heroPrevOverride = _heroCanvas.overrideSorting;
                _heroPrevOrder = _heroCanvas.sortingOrder;
            }

            _heroCanvas.overrideSorting = true;
            _heroCanvas.sortingOrder = heroSortingOrder;
        }

        private void LowerCard()
        {
            if (_heroCanvas == null) { _addedHeroCanvas = false; _addedHeroRaycaster = false; return; }

            if (_addedHeroCanvas)
            {
                if (_addedHeroRaycaster && _heroRaycaster != null) Destroy(_heroRaycaster);
                Destroy(_heroCanvas);
            }
            else
            {
                _heroCanvas.overrideSorting = _heroPrevOverride;
                _heroCanvas.sortingOrder = _heroPrevOrder;
            }

            _heroCanvas = null;
            _heroRaycaster = null;
            _addedHeroCanvas = false;
            _addedHeroRaycaster = false;
        }


        private readonly System.Collections.Generic.List<RectTransform> _pushed = new System.Collections.Generic.List<RectTransform>();
        private readonly System.Collections.Generic.List<Vector2> _pushApplied = new System.Collections.Generic.List<Vector2>();
        private readonly System.Collections.Generic.List<Vector2> _pushTarget = new System.Collections.Generic.List<Vector2>();

        private void ApplyPushOffset(int i, Vector2 want)
        {
            var rt = _pushed[i];
            if (rt == null) { _pushApplied[i] = want; return; }
            rt.anchoredPosition += want - _pushApplied[i];
            _pushApplied[i] = want;
        }

        private void RestoreNeighbours(bool instant)
        {
            for (int i = 0; i < _pushed.Count; i++) ApplyPushOffset(i, Vector2.zero);
            _pushed.Clear();
            _pushApplied.Clear();
            _pushTarget.Clear();
        }

        private IEnumerator PushNeighbours(RectTransform hero, System.Collections.Generic.IList<RectTransform> others, bool mythic)
        {
            if (others == null || hero == null) yield break;

            RestoreNeighbours(true);
            float heroX = hero.anchoredPosition.x;
            float dist = pushDistance * (mythic ? 1.35f : 1f);

            foreach (var other in others)
            {
                if (other == null || other == hero) continue;
                float dx = other.anchoredPosition.x - heroX;
                float dir = Mathf.Abs(dx) < 0.01f ? 1f : Mathf.Sign(dx);
                _pushed.Add(other);
                _pushApplied.Add(Vector2.zero);
                _pushTarget.Add(new Vector2(dir * dist, 0f));
            }
            if (_pushed.Count == 0) yield break;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, pushOutSeconds);
                float e = Mathf.Clamp01(t);
                float ease = 1f - Mathf.Pow(1f - e, 3f);
                float rattle = neighbourShake * (1f - e);

                for (int i = 0; i < _pushed.Count; i++)
                {
                    Vector2 want = Vector2.Lerp(Vector2.zero, _pushTarget[i], ease)
                                 + new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * rattle;
                    ApplyPushOffset(i, want);
                }
                yield return null;
            }
            for (int i = 0; i < _pushed.Count; i++) ApplyPushOffset(i, _pushTarget[i]);
        }

        private IEnumerator ReturnNeighbours()
        {
            if (_pushed.Count == 0) yield break;

            var from = new System.Collections.Generic.List<Vector2>(_pushApplied);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, pushReturnSeconds);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                for (int i = 0; i < _pushed.Count; i++)
                    ApplyPushOffset(i, Vector2.Lerp(from[i], Vector2.zero, e));
                yield return null;
            }
            RestoreNeighbours(true);
        }

        private void ClearTransients()
        {
            if (_root == null) return;
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                var child = _root.GetChild(i);
                if (child == _dim.transform || child == _flash.transform) continue;
                Destroy(child.gameObject);
            }
        }

        private void OnDisable() => ReleaseTimeScale();
        private void OnDestroy() { ReleaseTimeScale(); if (_instance == this) _instance = null; }

        private void HoldTimeScale(float scale)
        {
            if (!_timeScaleHeld)
            {
                _restoreTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
                _timeScaleHeld = true;
            }
            Time.timeScale = scale;
        }

        private void ReleaseTimeScale()
        {
            if (!_timeScaleHeld) return;
            Time.timeScale = _restoreTimeScale <= 0f ? 1f : _restoreTimeScale;
            _timeScaleHeld = false;
        }

        private IEnumerator Sequence(RectTransform card, RarityDefinition rarity, System.Collections.Generic.IList<RectTransform> others)
        {
            bool mythic = rarity.RevealTier >= 2;
            Color tint = rarity.Color;
            Vector2 focus = ScreenPointOf(card);

            float dimTarget = mythic ? 0.72f : 0.55f;
            int rayCount = mythic ? 24 : 16;
            float shake = shakeStrength * (mythic ? 1.6f : 1f);

            ClearTransients();
            RaiseCard(card);
            StartCoroutine(PushNeighbours(card, others, mythic));

            _flash.color = new Color(1f, 1f, 1f, mythic ? 0.95f : 0.75f);

            const float innerRayScale = 0.85f;

            var rays = CreateImage("Rays", _root, Rays(rayCount));
            Center(rays.rectTransform, focus, FullScreenRaySize(focus));
            rays.color = new Color(tint.r, tint.g, tint.b, 0f);

            var rays2 = mythic ? CreateImage("Rays2", _root, Rays(rayCount / 2)) : null;
            if (rays2 != null)
            {
                Center(rays2.rectTransform, focus, FullScreenRaySize(focus) * innerRayScale);
                rays2.color = new Color(1f, 1f, 1f, 0f);
            }

            var glow = CreateImage("Glow", _root, Glow());
            Center(glow.rectTransform, focus, 900f);
            glow.color = new Color(tint.r, tint.g, tint.b, 0f);

            var ring = CreateImage("Ring", _root, Ring());
            Center(ring.rectTransform, focus, 200f);
            ring.color = new Color(tint.r, tint.g, tint.b, 0.9f);

            SpawnSparks(focus, tint, mythic ? sparkCount * 2 : sparkCount);
            var plate = BuildNameplate(rarity, mythic);

            StartCoroutine(Shake(card, mythic ? 0.5f : 0.35f, shake));

            float t = 0f;
            float swell = mythic ? 0.55f : 0.45f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / swell;
                float e = Mathf.Clamp01(t);
                float ease = 1f - Mathf.Pow(1f - e, 3f);   

                focus = ScreenPointOf(card);
                rays.rectTransform.anchoredPosition = focus;
                if (rays2 != null) rays2.rectTransform.anchoredPosition = focus;
                glow.rectTransform.anchoredPosition = focus;
                ring.rectTransform.anchoredPosition = focus;

                _dim.color = new Color(0f, 0f, 0f, dimTarget * ease);
                _flash.color = new Color(1f, 1f, 1f, Mathf.Lerp(mythic ? 0.95f : 0.75f, 0f, Mathf.Clamp01(t * swell / flashOut)));

                float rayFull = FullScreenRaySize(focus);

                float raySize = Mathf.Lerp(200f, rayFull, ease);
                rays.rectTransform.sizeDelta = new Vector2(raySize, raySize);
                rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, e * 40f);
                rays.color = new Color(tint.r, tint.g, tint.b, 0.55f * ease);

                if (rays2 != null)
                {
                    float s2 = Mathf.Lerp(200f, rayFull * innerRayScale, ease);
                    rays2.rectTransform.sizeDelta = new Vector2(s2, s2);
                    rays2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -e * 65f);
                    rays2.color = new Color(1f, 1f, 1f, 0.22f * ease);
                }

                float glowSize = Mathf.Lerp(300f, 1000f, ease);
                glow.rectTransform.sizeDelta = new Vector2(glowSize, glowSize);
                glow.color = new Color(tint.r, tint.g, tint.b, 0.75f * ease);

                float ringSize = Mathf.Lerp(200f, mythic ? 2400f : 1800f, ease);
                ring.rectTransform.sizeDelta = new Vector2(ringSize, ringSize);
                ring.color = new Color(tint.r, tint.g, tint.b, 0.9f * (1f - e));

                if (plate != null) DriveNameplate(plate, e);

                yield return null;
            }

            float hold = 0f;
            float holdFor = holdSeconds * (mythic ? 1.25f : 1f);
            float startScale = Time.timeScale;
            float restoreTo = _restoreTimeScale <= 0f ? 1f : _restoreTimeScale;

            while (hold < holdFor)
            {
                hold += Time.unscaledDeltaTime;

                if (_timeScaleHeld)
                {
                    float k = Mathf.Clamp01(hold / Mathf.Max(0.01f, slowMoRecover));
                    Time.timeScale = Mathf.Lerp(startScale, restoreTo, k * k);
                    if (k >= 1f) ReleaseTimeScale();
                }

                focus = ScreenPointOf(card);
                rays.rectTransform.anchoredPosition = focus;
                if (rays2 != null) rays2.rectTransform.anchoredPosition = focus;
                glow.rectTransform.anchoredPosition = focus;

                float holdRayFull = FullScreenRaySize(focus);
                rays.rectTransform.sizeDelta = new Vector2(holdRayFull, holdRayFull);
                rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 40f + hold * 10f);
                if (rays2 != null)
                {
                    float h2 = holdRayFull * innerRayScale;
                    rays2.rectTransform.sizeDelta = new Vector2(h2, h2);
                    rays2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -65f - hold * 16f);
                }

                float pulse = 0.75f + Mathf.Sin(hold * 6f) * 0.12f;
                glow.color = new Color(tint.r, tint.g, tint.b, pulse);
                yield return null;
            }
            ReleaseTimeScale();

            float s = 0f;
            float dimFrom = _dim.color.a;
            while (s < 1f)
            {
                s += Time.unscaledDeltaTime / Mathf.Max(0.01f, settleSeconds);
                float e = Mathf.Clamp01(s);

                focus = ScreenPointOf(card);
                rays.rectTransform.anchoredPosition = focus;
                if (rays2 != null) rays2.rectTransform.anchoredPosition = focus;
                glow.rectTransform.anchoredPosition = focus;

                _dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(dimFrom, 0f, e));
                rays.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.55f, 0f, e));
                if (rays2 != null) rays2.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.22f, 0f, e));
                glow.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.75f, 0f, e));
                if (plate != null) plate.alpha = 1f - e;
                yield return null;
            }

            LowerCard();
            ClearTransients();

            yield return ReturnNeighbours();

            _running = null;
        }


        private CanvasGroup BuildNameplate(RarityDefinition rarity, bool mythic)
        {
            var go = new GameObject("Nameplate", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, mythic ? 330f : 300f);
            rt.sizeDelta = new Vector2(1400f, 200f);

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = (rarity.DisplayName ?? "LEGENDARY").ToUpperInvariant();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = mythic ? 150f : 130f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = rarity.Color;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            return cg;
        }

        private void DriveNameplate(CanvasGroup plate, float e)
        {
            plate.alpha = Mathf.Clamp01(e * 2.2f);
            var rt = (RectTransform)plate.transform;
            float scale = Mathf.Lerp(2.1f, 1f, 1f - Mathf.Pow(1f - e, 4f));
            rt.localScale = new Vector3(scale, scale, 1f);

            var tmp = plate.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.characterSpacing = Mathf.Lerp(-14f, 6f, e);
        }


        private void SpawnSparks(Vector2 focus, Color tint, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var img = CreateImage("Spark", _root, Star());
                float size = Random.Range(18f, 54f);
                Center(img.rectTransform, focus, size);
                img.color = Color.Lerp(tint, Color.white, Random.value * 0.6f);

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(240f, 780f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                StartCoroutine(FlySpark(img, focus, focus + dir, Random.Range(0.5f, 1.1f)));
            }
        }

        private IEnumerator FlySpark(Image img, Vector2 from, Vector2 to, float duration)
        {
            float t = 0f;
            Color start = img.color;
            float spin = Random.Range(-260f, 260f);
            while (t < 1f && img != null)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.05f, duration);
                float e = Mathf.Clamp01(t);
                float ease = 1f - Mathf.Pow(1f - e, 3f);

                var rt = img.rectTransform;
                Vector2 pos = Vector2.Lerp(from, to, ease);
                pos.y -= 260f * e * e;
                rt.anchoredPosition = pos;
                rt.localRotation = Quaternion.Euler(0f, 0f, spin * e);

                img.color = new Color(start.r, start.g, start.b, 1f - e);
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
        }

        private IEnumerator Shake(RectTransform target, float duration, float strength)
        {
            if (target == null) yield break;
            Vector2 origin = target.anchoredPosition;
            float t = 0f;
            while (t < duration && target != null)
            {
                t += Time.unscaledDeltaTime;
                float falloff = 1f - (t / duration);
                target.anchoredPosition = origin + new Vector2(
                    Random.Range(-1f, 1f) * strength * falloff,
                    Random.Range(-1f, 1f) * strength * falloff);
                yield return null;
            }
            if (target != null) target.anchoredPosition = origin;
        }


        private Vector2 ScreenPointOf(RectTransform card)
        {
            if (card == null || _root == null) return Vector2.zero;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, card.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
            return local;
        }

        private float FullScreenRaySize(Vector2 focus)
        {
            if (_root == null) return 2600f;
            Rect r = _root.rect;
            float dx = Mathf.Max(Mathf.Abs(focus.x - r.xMin), Mathf.Abs(focus.x - r.xMax));
            float dy = Mathf.Max(Mathf.Abs(focus.y - r.yMin), Mathf.Abs(focus.y - r.yMax));
            return 2f * Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static Image CreateImage(string name, RectTransform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
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
            rt.sizeDelta = new Vector2(size, size);
        }


        private static Sprite Solid()
        {
            if (_solidSprite != null) return _solidSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }

        private static Sprite Rays(int spokes)
        {
            if (_raysSprite != null && _raysCached == spokes) return _raysSprite;

            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half, dy = y - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    if (r > 1f) { px[y * size + x] = Color.clear; continue; }

                    float ang = Mathf.Atan2(dy, dx);
                    float beam = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * spokes * 0.5f)), 14f);
                    float radial = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);
                    px[y * size + x] = new Color(1f, 1f, 1f, beam * radial);
                }
            }
            tex.SetPixels(px); tex.Apply();
            _raysSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _raysCached = spokes;
            return _raysSprite;
        }
        private static int _raysCached = -1;

        private static Sprite Ring()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half, dy = y - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    float band = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.86f) / 0.1f);
                    px[y * size + x] = new Color(1f, 1f, 1f, band * band);
                }
            }
            tex.SetPixels(px); tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _ringSprite;
        }

        private static Sprite Glow()
        {
            if (_glowSprite != null) return _glowSprite;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half, dy = y - half;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / half);
                    float a = Mathf.Pow(1f - r, 2.4f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px); tex.Apply();
            _glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _glowSprite;
        }

        private static Sprite Star()
        {
            if (_sparkSprite != null) return _sparkSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - half) / half;
                    float dy = Mathf.Abs(y - half) / half;
                    float a = Mathf.Clamp01(1f - (dx * dx + dy * dy)) * Mathf.Clamp01(1f - Mathf.Min(dx, dy) * 6f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px); tex.Apply();
            _sparkSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _sparkSprite;
        }
    }
}
