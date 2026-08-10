using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;

namespace TR.UI
{
    // Cinematic reveal for high-rarity pack pulls.
    //
    // Everything here is generated at runtime — rays, rings, glows and sparks are procedural
    // textures tinted with the rarity colour — so it needs no art assets and automatically matches
    // any rarity palette you set later.
    //
    // The whole sequence runs on unscaled time because it deliberately drives Time.timeScale for
    // the slow-motion beat; using scaled time would make the effect slow itself down.
    public class RarityRevealFX : MonoBehaviour
    {
        private static RarityRevealFX _instance;

        [Header("Sorting")]
        [Tooltip("Sorting order of the effect canvas. Must sit BELOW the hero card's order so the rays render behind it.")]
        [SerializeField] private int effectSortingOrder = 200;
        [Tooltip("Sorting order the revealed card is lifted to for the duration of the effect.")]
        [SerializeField] private int heroSortingOrder = 500;

        [Header("Timing")]
        [SerializeField] private float flashOut = 0.22f;
        [SerializeField] private float holdSeconds = 1.15f;
        [SerializeField] private float settleSeconds = 0.55f;

        [Header("Slow Motion")]
        [Tooltip("Time scale during the anticipation and impact beats.")]
        [SerializeField] private float slowMoScale = 0.4f;
        [Tooltip("How long the world takes to ease DOWN into slow motion as the card starts flipping.")]
        [SerializeField] private float slowMoEaseIn = 0.35f;
        [Tooltip("How long the world takes to ease back up to full speed after the reveal.")]
        [SerializeField] private float slowMoRecover = 0.9f;

        [Header("Intensity")]
        [SerializeField] private float shakeStrength = 26f;
        [SerializeField] private int sparkCount = 22;

        [Header("Neighbouring Cards")]
        [Tooltip("How far the other cards are shoved aside to clear space around the hero card.")]
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

        // ---- generated art, built once and shared ----
        private static Sprite _raysSprite, _ringSprite, _glowSprite, _sparkSprite, _solidSprite;

        // Called as the card STARTS flipping. Eases the world into slow motion so the reveal is
        // anticipated rather than reacted to — the slowdown has to precede the payoff to read.
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

        // Called when the player skips ahead: kill the effect and never leave time dilated.
        // True while the celebration still owns card positions. Callers must not reposition the
        // cards until this clears, or their layout and this effect's offsets fight each other.
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
            // Deliberately BELOW the hero card: the rays must render behind it, not over it.
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
                // A touch of dim during the wind-up hints that something big is coming.
                if (_dim != null) _dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.18f, Mathf.Clamp01(t)));
                yield return null;
            }
            _anticipation = null;
        }

        private void Begin(RectTransform card, RarityDefinition rarity, System.Collections.Generic.IList<RectTransform> others)
        {
            // Keep whatever the anticipation ramp did to time scale; only cancel its coroutine.
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

        // ---------- lifting the hero card above the effect ----------

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
                // A nested Canvas swallows raycasts for its children unless it has its own raycaster.
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

        // ---------- shoving the other cards aside ----------

        // The push is tracked as an OFFSET we own, never as absolute positions. The pack controller
        // moves these same cards while the effect runs (it compresses the row into its final
        // layout), so restoring a position captured at push time would undo that repositioning —
        // which is exactly how the cards "uncompressed" after a legendary reveal.
        private readonly System.Collections.Generic.List<RectTransform> _pushed = new System.Collections.Generic.List<RectTransform>();
        private readonly System.Collections.Generic.List<Vector2> _pushApplied = new System.Collections.Generic.List<Vector2>();
        private readonly System.Collections.Generic.List<Vector2> _pushTarget = new System.Collections.Generic.List<Vector2>();

        // Moves a card by the difference between the offset we want applied and the one already
        // applied, leaving any movement the controller made in between untouched.
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
                // Shove away from the hero; cards sitting exactly on it break ties to the right.
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
                    // Rattle is folded into the offset so it unwinds with everything else.
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
            // Lift the card above the effect canvas so the rays sit behind it.
            RaiseCard(card);
            StartCoroutine(PushNeighbours(card, others, mythic));

            // --- 1. impact ---------------------------------------------------------------------
            _flash.color = new Color(1f, 1f, 1f, mythic ? 0.95f : 0.75f);

            // Second sheet stays slightly inside the first, as it always did (2200 of 2600).
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

            // Card sits above the backdrop art but below sparks and the nameplate.
            var ring = CreateImage("Ring", _root, Ring());
            Center(ring.rectTransform, focus, 200f);
            ring.color = new Color(tint.r, tint.g, tint.b, 0.9f);

            SpawnSparks(focus, tint, mythic ? sparkCount * 2 : sparkCount);
            var plate = BuildNameplate(rarity, mythic);

            StartCoroutine(Shake(card, mythic ? 0.5f : 0.35f, shake));

            // --- 2. the swell ----------------------------------------------------------------
            float t = 0f;
            float swell = mythic ? 0.55f : 0.45f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / swell;
                float e = Mathf.Clamp01(t);
                float ease = 1f - Mathf.Pow(1f - e, 3f);   // easeOutCubic

                // The card keeps moving (it settles, then the row compresses), so re-anchor every
                // frame instead of pinning the effect to where it was at reveal time.
                focus = ScreenPointOf(card);
                rays.rectTransform.anchoredPosition = focus;
                if (rays2 != null) rays2.rectTransform.anchoredPosition = focus;
                glow.rectTransform.anchoredPosition = focus;
                ring.rectTransform.anchoredPosition = focus;

                _dim.color = new Color(0f, 0f, 0f, dimTarget * ease);
                _flash.color = new Color(1f, 1f, 1f, Mathf.Lerp(mythic ? 0.95f : 0.75f, 0f, Mathf.Clamp01(t * swell / flashOut)));

                // Re-measured each frame: the card shakes, and the reach it needs changes with it.
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

                // Shockwave outruns everything else and fades as it goes.
                float ringSize = Mathf.Lerp(200f, mythic ? 2400f : 1800f, ease);
                ring.rectTransform.sizeDelta = new Vector2(ringSize, ringSize);
                ring.color = new Color(tint.r, tint.g, tint.b, 0.9f * (1f - e));

                if (plate != null) DriveNameplate(plate, e);

                yield return null;
            }

            // --- 3. hold: rays keep turning while the world eases back to full speed ----------
            float hold = 0f;
            float holdFor = holdSeconds * (mythic ? 1.25f : 1f);
            float startScale = Time.timeScale;
            float restoreTo = _restoreTimeScale <= 0f ? 1f : _restoreTimeScale;

            while (hold < holdFor)
            {
                hold += Time.unscaledDeltaTime;

                // Recovering across the hold rather than in its own blocking loop is what makes the
                // speed-up feel natural instead of a gear change.
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

            // --- 4. settle -------------------------------------------------------------------
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

            // The effect has faded to nothing and the other cards are still shoved aside, so
            // nothing overlaps the hero right now — this is the only moment its sorting order can
            // drop without showing. Dropping it after the row had already closed in is what made
            // the card visibly snap from in front of its neighbours to behind them.
            LowerCard();
            ClearTransients();

            // Only now let the row close back up, so the card is already sitting in its normal
            // order as the others slide back over it. IsPlaying stays true until this finishes,
            // which keeps the pack controller from compressing the row mid-slide.
            yield return ReturnNeighbours();

            _running = null;
        }

        // ---------- nameplate ----------

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
            // Slams in oversized and settles, with the letters spreading as it lands.
            float scale = Mathf.Lerp(2.1f, 1f, 1f - Mathf.Pow(1f - e, 4f));
            rt.localScale = new Vector3(scale, scale, 1f);

            var tmp = plate.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.characterSpacing = Mathf.Lerp(-14f, 6f, e);
        }

        // ---------- sparks ----------

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
                // Gravity sag so they arc instead of travelling dead straight.
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

        // ---------- helpers ----------

        private Vector2 ScreenPointOf(RectTransform card)
        {
            if (card == null || _root == null) return Vector2.zero;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, card.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
            return local;
        }

        // The ray sheet is a square centred on the card, and it spins — so the only region
        // guaranteed to stay covered through every rotation is the disc inscribed in it. Sizing it
        // off the farthest corner of the screen keeps the rays reaching every edge no matter how
        // far off-centre the card sits; a fixed size only worked for cards near the middle.
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

        // ---------- procedural textures ----------

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
            // Regenerated when the spoke count differs; the common case is cached.
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
                    // cos^k across the spokes gives clean tapering beams.
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
                    // Soft band near the rim.
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
                    // Four-point sparkle: bright along both axes, dark diagonally.
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
