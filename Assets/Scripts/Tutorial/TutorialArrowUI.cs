using UnityEngine;
using TMPro;

namespace TR.Tutorial
{
    
    public class TutorialArrowUI : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private Vector2 screenOffset = new Vector2(0, 60);
        [SerializeField] private float bobAmplitude = 8f;
        [SerializeField] private float bobSpeed = 3f;
        [SerializeField] private string arrowGlyph = "➤"; 
        [SerializeField] private float arrowSize = 36f;
        [SerializeField] private Vector2 pivot = new Vector2(0.5f, 0f);

        private RectTransform _rt;
        private TextMeshProUGUI _text;
        private float _t;
        private bool _hasTarget;

        private void Awake()
        {
            
            _rt = GetComponent<RectTransform>();
            if (_rt == null) _rt = gameObject.AddComponent<RectTransform>();

            
            _text = GetComponentInChildren<TextMeshProUGUI>(true);
            if (_text == null)
            {
                var go = new GameObject("ArrowText", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                _text = go.AddComponent<TextMeshProUGUI>();
                _text.alignment = TextAlignmentOptions.Center;
                _text.raycastTarget = false;
                var tr = _text.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
                tr.pivot = new Vector2(0.5f, 0.5f);
            }
            
            _text.text = arrowGlyph;
            _text.fontSize = arrowSize;
            gameObject.name = "TutorialArrowUI";

            
            _hasTarget = false;
            gameObject.SetActive(false);
        }

        public void Follow(RectTransform newTarget, Vector2 offset)
        {
            target = newTarget;
            screenOffset = offset;
            _hasTarget = (target != null);
            if (_rt != null)
            {
                _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
                _rt.pivot = pivot;
            }
            
            if (_hasTarget)
            {
                var pos = GetWorldToCanvasPosition(target) + screenOffset;
                _rt.anchoredPosition = pos;
                if (!gameObject.activeSelf) gameObject.SetActive(true);
            }
            else
            {
                
                if (gameObject.activeSelf) gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_hasTarget || target == null || _rt == null) return;
            _t += Time.unscaledDeltaTime * bobSpeed;
            float bob = Mathf.Sin(_t) * bobAmplitude;
            
            Vector2 pos = GetWorldToCanvasPosition(target) + screenOffset + new Vector2(0f, bob);
            _rt.anchoredPosition = pos;
        }

        private Vector2 GetWorldToCanvasPosition(RectTransform rt)
        {
            
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            var canvasRT = transform.parent as RectTransform;
            Vector2 localPoint;
            if (canvasRT == null)
            {
                return Vector2.zero;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, RectTransformUtility.WorldToScreenPoint(null, center), null, out localPoint);
            return localPoint;
        }
    }
}
