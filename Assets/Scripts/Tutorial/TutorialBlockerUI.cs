using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TR.Tutorial
{
    // Full-screen, invisible blocker that captures raycasts outside the target rect.
    // Inside the target rect, raycasts pass through (so the user can click the highlighted control).
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class TutorialBlockerUI : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField] private RectTransform passThroughTarget;
        [SerializeField] private System.Collections.Generic.List<RectTransform> passThroughTargets;
        [SerializeField] private bool _enabled;

        private RectTransform _rt;
        private Image _img;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _img = GetComponent<Image>();
            // Stretch full screen
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = Vector2.zero;
            _rt.sizeDelta = Vector2.zero;
            // Invisible but raycast-targetable
            _img.color = new Color(0, 0, 0, 0);
            _img.raycastTarget = true;
            passThroughTargets = new System.Collections.Generic.List<RectTransform>();
            Disable();
        }

        public void AttachToCanvas(Canvas canvas)
        {
            if (canvas == null) return;
            transform.SetParent(canvas.transform, false);
            // Ensure it is the topmost graphic so it blocks correctly
            transform.SetAsLastSibling();
        }

        public void Enable(RectTransform target)
        {
            passThroughTarget = target;
            passThroughTargets.Clear();
            _enabled = true;
            gameObject.SetActive(true);
            // Keep this as topmost
            transform.SetAsLastSibling();
            if (_img != null) _img.raycastTarget = true;
        }

        public void EnableMany(System.Collections.Generic.List<RectTransform> targets)
        {
            passThroughTarget = null;
            passThroughTargets = targets ?? new System.Collections.Generic.List<RectTransform>();
            _enabled = true;
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
            gameObject.SetActive(false);
        }

        // Return true to allow this graphic to receive the raycast (i.e., block underlying UI).
        // Return false to ignore the raycast so underlying UI can receive it.
        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!_enabled) return false; // do not block when disabled
            RectTransform canvasRT = transform.parent as RectTransform;
            if (canvasRT == null) return true;

            // Helper to test a single target
            bool InsideTarget(RectTransform rt)
            {
                if (rt == null) return false;
                Vector2 lp;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, sp, eventCamera, out lp)) return false;
                return rt.rect.Contains(lp);
            }

            // If a single target is set
            if (passThroughTarget != null)
            {
                return !InsideTarget(passThroughTarget);
            }
            // If multiple targets are set
            if (passThroughTargets != null && passThroughTargets.Count > 0)
            {
                for (int i = 0; i < passThroughTargets.Count; i++)
                {
                    if (InsideTarget(passThroughTargets[i])) return false; // allow inside any
                }
                return true; // block otherwise
            }
            // No targets: block everything
            return true;
        }
    }
}
