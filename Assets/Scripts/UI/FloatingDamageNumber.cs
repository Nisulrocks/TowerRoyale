using System.Collections;
using UnityEngine;
using TMPro;
 

namespace TR.UI
{
    
    public class FloatingDamageNumber : MonoBehaviour
    {
        [Header("Runtime State")]
        public Transform worldTarget; 
        public FloatingDamageStyle style;

        private RectTransform _rect;
        private TextMeshProUGUI _text;
        private Canvas _canvas;
        private Camera _cam;

        private float _lastAddTime;
        private Vector3 _baseScale = Vector3.one;
        private float _stackedScale = 1f; 
        private Vector2 _velocity; 
        private float _alpha = 1f;
        private float _valueF; 
        private bool _dead;
        private bool _detached; 
        private bool _hasAnchored; 

        public void Init(Transform target, int initialValue, FloatingDamageStyle sty, Canvas canvas)
        {
            worldTarget = target;
            style = sty;
            _rect = GetComponent<RectTransform>();
            _text = GetComponentInChildren<TextMeshProUGUI>(true);
            _canvas = canvas;
            _cam = _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : Camera.main;
            _lastAddTime = Time.unscaledTime;
            _alpha = 1f;
            _baseScale = Vector3.one;
            _valueF = 0f;
            _dead = false;
            _stackedScale = 1f;
            _velocity = Vector2.zero;
            _detached = false;
            _hasAnchored = false;

            if (_text != null)
            {
                _text.fontSize = style != null ? style.baseFontSize : 36;
                _text.textWrappingMode = TextWrappingModes.NoWrap;
                _text.alignment = TextAlignmentOptions.Center;
                _text.raycastTarget = false; 
            }
            
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            AddValue(initialValue);
        }

        public void AddValue(int amount)
        {
            AddValueFloat(amount);
        }

        public void AddValueFloat(float amount)
        {
            _valueF += Mathf.Max(0f, amount);
            _lastAddTime = Time.unscaledTime;
            _alpha = 1f; 
            
            float mul = (style != null ? Mathf.Max(1f, style.popScale) : 1.15f);
            float maxS = (style != null ? Mathf.Max(1f, style.popMaxScale) : 2f);
            _stackedScale = Mathf.Min(maxS, _stackedScale * mul);
            if (_text != null)
            {
                _text.text = Mathf.CeilToInt(_valueF).ToString();
                var col = (style != null) ? style.GetColorForDamage(_valueF) : Color.white;
                col.a = _alpha;
                _text.color = col;
            }
        }

        private void LateUpdate()
        {
            if (_dead) return;

            Vector2 canvasPos = _rect.anchoredPosition;
            bool targetValid = worldTarget != null && worldTarget.gameObject.activeInHierarchy;
            if (targetValid && !_detached)
            {
                
                Vector3 worldPos = worldTarget.position + Vector3.up * 1.5f; 
                Vector3 screenPos;
                screenPos = RectTransformUtility.WorldToScreenPoint(_cam != null ? _cam : Camera.main, worldPos);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas.transform as RectTransform, screenPos, _cam, out canvasPos);
            }
            else
            {
                
                _detached = true;
                if (!_hasAnchored)
                {
                    canvasPos = _rect.anchoredPosition;
                    _hasAnchored = true;
                }
            }
            float mergeWindow = style != null ? style.mergeWindow : 0.4f;
            float timeSinceLastAdd = Time.unscaledTime - _lastAddTime;
            if (!_detached && timeSinceLastAdd <= mergeWindow)
            {
                
                float yOff = style != null ? style.followOffsetY : 120f;
                float popReturnA = style != null ? Mathf.Max(0.0001f, style.popReturnTime) : 0.2f;
                float lerpTA = Mathf.Clamp01((Time.unscaledTime - _lastAddTime) / popReturnA);
                _rect.anchoredPosition = canvasPos + new Vector2(0f, yOff);
                _velocity = Vector2.zero; 
            }
            else
            {
                
                if (_velocity == Vector2.zero)
                {
                    
                    float upV = style != null ? style.initialUpVelocity : 240f;
                    float xV = style != null ? style.endKickHorizontal : 80f;
                    float xSign = (Random.value < 0.5f ? -1f : 1f);
                    _velocity = new Vector2(xV * xSign, upV);
                }
                float g = style != null ? style.gravity : -1400f;
                _velocity += new Vector2(0f, g) * Time.unscaledDeltaTime;
                _rect.anchoredPosition = (_detached ? _rect.anchoredPosition : (canvasPos + new Vector2(0f, 0f))) + _velocity * Time.unscaledDeltaTime;
            }

            
            float popReturn = style != null ? Mathf.Max(0.0001f, style.popReturnTime) : 0.2f;
            float tSinceAdd = Time.unscaledTime - _lastAddTime;
            float lerpT = Mathf.Clamp01(tSinceAdd / popReturn);
            float scaleNow = Mathf.Lerp(_stackedScale, 1f, lerpT);
            _rect.localScale = _baseScale * scaleNow;
            
            if (lerpT >= 1f) _stackedScale = 1f;

            
            float lifetime = style != null ? Mathf.Max(0.01f, style.lifetime) : 1f;
            
            if (timeSinceLastAdd > mergeWindow)
            {
                float fadeSpeed = style != null ? Mathf.Max(0f, style.fadeSpeed) : 1f;
                _alpha = Mathf.Clamp01(1f - ((timeSinceLastAdd - mergeWindow) / lifetime) * fadeSpeed);
                if (_text != null)
                {
                    
                    var c = (style != null) ? style.GetColorForDamage(_valueF) : _text.color;
                    c.a = _alpha; 
                    _text.color = c;
                }
                if (_alpha <= 0.01f)
                {
                    _dead = true;
                    Destroy(gameObject);
                }
            }
        }

        private float ComputeFloatOffset() { return 0f; }
    }
}
