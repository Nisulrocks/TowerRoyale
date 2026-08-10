using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TR.Tutorial
{


    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class TutorialBlockerUI : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField] private RectTransform passThroughTarget;
        [SerializeField] private List<RectTransform> passThroughTargets;
        [SerializeField] private bool _enabled;

        [Header("Spotlight")]
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private float focusSeconds = 0.55f;
        [SerializeField] private Vector2 holePadding = new Vector2(18f, 18f);
        [SerializeField] private float holePulse = 6f;
        [SerializeField] private float holePulseSpeed = 3.2f;

        private RectTransform _rt;
        private Image _img;
        private Camera _uiCam;

        private readonly RectTransform[] _dimParts = new RectTransform[4];
        private readonly Image[] _dimImages = new Image[4];
        private readonly Vector3[] _corners = new Vector3[4];

        private bool _spotlight;
        private float _focusT;

        public bool BlockInput { get; set; } = true;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _img = GetComponent<Image>();

            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = Vector2.zero;
            _rt.sizeDelta = Vector2.zero;

            _img.color = new Color(0, 0, 0, 0);
            _img.raycastTarget = true;
            passThroughTargets = new List<RectTransform>();
            Disable();
        }

        public void AttachToCanvas(Canvas canvas)
        {
            if (canvas == null) return;
            transform.SetParent(canvas.transform, false);

            transform.SetAsLastSibling();
            _uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        public void Enable(RectTransform target)
        {
            passThroughTarget = target;
            passThroughTargets.Clear();
            _enabled = true;
            BlockInput = true;
            gameObject.SetActive(true);

            transform.SetAsLastSibling();
            if (_img != null) _img.raycastTarget = true;
        }

        public void EnableMany(List<RectTransform> targets)
        {
            passThroughTarget = null;
            passThroughTargets = targets ?? new List<RectTransform>();
            _enabled = true;
            BlockInput = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (_img != null) _img.raycastTarget = true;
        }

        public void Disable()
        {
            _enabled = false;
            passThroughTarget = null;
            passThroughTargets.Clear();
            if (_img != null) _img.raycastTarget = false;
            SetSpotlight(false);
            gameObject.SetActive(false);
        }


        public void SetSpotlight(bool on, bool restart = false)
        {
            if (_spotlight == on && !restart)
            {
                if (on) EnsureDimParts();
                return;
            }

            _spotlight = on;
            _focusT = 0f;
            if (!on)
            {
                for (int i = 0; i < _dimParts.Length; i++)
                    if (_dimImages[i] != null) _dimImages[i].color = Color.clear;
            }
            else
            {
                EnsureDimParts();
            }
        }

        private void EnsureDimParts()
        {
            if (_dimParts[0] != null) return;
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Dim" + i, typeof(RectTransform));
                go.transform.SetParent(transform, false);

                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                var img = go.AddComponent<Image>();
                img.raycastTarget = false; 
                img.color = Color.clear;

                _dimParts[i] = rt;
                _dimImages[i] = img;
            }
        }

        private void LateUpdate()
        {
            if (!_spotlight || _rt == null) return;
            EnsureDimParts();

            _focusT = Mathf.MoveTowards(_focusT, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, focusSeconds));
            float e = 1f - Mathf.Pow(1f - _focusT, 3f);

            Rect full = _rt.rect;
            Rect hole = full;

            if (TryGetTargetRect(out Rect targetRect))
            {
                float pulse = holePulse * Mathf.Sin(Time.unscaledTime * holePulseSpeed) * e;
                targetRect = Expand(targetRect, holePadding.x + pulse, holePadding.y + pulse);

                hole = LerpRect(full, targetRect, e);
            }

            ApplyHole(full, hole);

            var c = dimColor;
            c.a *= e;
            for (int i = 0; i < 4; i++)
                if (_dimImages[i] != null) _dimImages[i].color = c;
        }

        private static Rect Expand(Rect r, float x, float y)
            => Rect.MinMaxRect(r.xMin - x, r.yMin - y, r.xMax + x, r.yMax + y);

        private static Rect LerpRect(Rect a, Rect b, float t) => Rect.MinMaxRect(
            Mathf.Lerp(a.xMin, b.xMin, t), Mathf.Lerp(a.yMin, b.yMin, t),
            Mathf.Lerp(a.xMax, b.xMax, t), Mathf.Lerp(a.yMax, b.yMax, t));

        private bool TryGetTargetRect(out Rect rect)
        {
            rect = default;
            bool any = false;

            if (passThroughTarget != null)
                any |= Accumulate(passThroughTarget, ref rect, any);

            if (passThroughTargets != null)
            {
                for (int i = 0; i < passThroughTargets.Count; i++)
                    any |= Accumulate(passThroughTargets[i], ref rect, any);
            }

            return any;
        }

        private bool Accumulate(RectTransform target, ref Rect rect, bool alreadyHave)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;

            Camera targetCam = null;
            var targetCanvas = target.GetComponentInParent<Canvas>();
            if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                targetCam = targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main;

            target.GetWorldCorners(_corners);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(targetCam, _corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, sp, _uiCam, out Vector2 lp))
                    return false;
                min = Vector2.Min(min, lp);
                max = Vector2.Max(max, lp);
            }

            if (max.x - min.x <= 0.01f || max.y - min.y <= 0.01f) return false;

            var r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            rect = alreadyHave
                ? Rect.MinMaxRect(Mathf.Min(rect.xMin, r.xMin), Mathf.Min(rect.yMin, r.yMin),
                                  Mathf.Max(rect.xMax, r.xMax), Mathf.Max(rect.yMax, r.yMax))
                : r;
            return true;
        }

        private void ApplyHole(Rect full, Rect hole)
        {
            float hx0 = Mathf.Clamp(hole.xMin, full.xMin, full.xMax);
            float hx1 = Mathf.Clamp(hole.xMax, full.xMin, full.xMax);
            float hy0 = Mathf.Clamp(hole.yMin, full.yMin, full.yMax);
            float hy1 = Mathf.Clamp(hole.yMax, full.yMin, full.yMax);

            SetPart(0, full.xMin, hy1, full.xMax, full.yMax); 
            SetPart(1, full.xMin, full.yMin, full.xMax, hy0); 
            SetPart(2, full.xMin, hy0, hx0, hy1);             
            SetPart(3, hx1, hy0, full.xMax, hy1);             
        }

        private void SetPart(int index, float xMin, float yMin, float xMax, float yMax)
        {
            var rt = _dimParts[index];
            if (rt == null) return;

            float w = Mathf.Max(0f, xMax - xMin);
            float h = Mathf.Max(0f, yMax - yMin);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
        }


        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!_enabled || !BlockInput) return false;
            RectTransform canvasRT = transform.parent as RectTransform;
            if (canvasRT == null) return true;


            bool InsideTarget(RectTransform rt)
            {
                if (rt == null) return false;
                Camera targetCam = eventCamera;
                var targetCanvas = rt.GetComponentInParent<Canvas>();
                if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    targetCam = targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main;
                Vector2 lp;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, sp, targetCam, out lp)) return false;
                return rt.rect.Contains(lp);
            }


            if (passThroughTarget != null)
            {
                return !InsideTarget(passThroughTarget);
            }

            if (passThroughTargets != null && passThroughTargets.Count > 0)
            {
                for (int i = 0; i < passThroughTargets.Count; i++)
                {
                    if (InsideTarget(passThroughTargets[i])) return false;
                }
                return true;
            }

            return true;
        }
    }
}
