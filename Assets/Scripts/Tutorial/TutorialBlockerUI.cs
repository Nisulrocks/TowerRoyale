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
        [SerializeField] private System.Collections.Generic.List<RectTransform> passThroughTargets;
        [SerializeField] private bool _enabled;

        private RectTransform _rt;
        private Image _img;

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
            passThroughTargets = new System.Collections.Generic.List<RectTransform>();
            Disable();
        }

        public void AttachToCanvas(Canvas canvas)
        {
            if (canvas == null) return;
            transform.SetParent(canvas.transform, false);
            
            transform.SetAsLastSibling();
        }

        public void Enable(RectTransform target)
        {
            passThroughTarget = target;
            passThroughTargets.Clear();
            _enabled = true;
            gameObject.SetActive(true);
            
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

        
        
        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!_enabled) return false; 
            RectTransform canvasRT = transform.parent as RectTransform;
            if (canvasRT == null) return true;

            
            bool InsideTarget(RectTransform rt)
            {
                if (rt == null) return false;
                Vector2 lp;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, sp, eventCamera, out lp)) return false;
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
