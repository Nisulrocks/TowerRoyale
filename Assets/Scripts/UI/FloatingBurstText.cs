using UnityEngine;
using TMPro;

namespace TR.UI
{
    // Simple one-shot burst text that floats and fades, not merging
    public class FloatingBurstText : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.8f;
        [SerializeField] private float upVelocity = 300f;
        [SerializeField] private float gravity = -900f;
        [SerializeField] private Color color = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private float startScale = 1.4f;
        [SerializeField] private float endScale = 1.0f;
        [SerializeField] private float startZRotation = 12f; // degrees, signed randomly
        [SerializeField] private float endZRotation = 0f;

        private RectTransform _rect;
        private TextMeshProUGUI _text;
        private Canvas _canvas;
        private Camera _cam;
        private Transform _target;
        private Vector2 _vel;
        private float _t;
        private float _life;
        private float _zStart;

        public void Init(Transform target, string msg, Canvas canvas)
        {
            _target = target;
            _canvas = canvas;
            _cam = _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : Camera.main;
            _rect = GetComponent<RectTransform>();
            _text = GetComponentInChildren<TextMeshProUGUI>(true);
            if (_text != null)
            {
                _text.text = msg;
                _text.color = color;
                _text.alignment = TextAlignmentOptions.Center;
                _text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            // Place once at current target position (no follow after)
            if (_rect != null && _canvas != null && target != null)
            {
                Vector3 worldPos = target.position + Vector3.up * 1.6f;
                Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(_cam != null ? _cam : Camera.main, worldPos);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas.transform as RectTransform, screenPos, _cam, out var canvasPos);
                _rect.anchoredPosition = canvasPos;
            }
            // Vertical motion only (no horizontal jitter)
            _vel = new Vector2(0f, upVelocity);
            _t = 0f;
            _life = Mathf.Max(0.01f, lifetime);
            if (_rect != null)
            {
                _rect.localScale = Vector3.one * startScale;
                // Randomize initial Z tilt
                float sign = Random.value < 0.5f ? -1f : 1f;
                _zStart = sign * Mathf.Abs(startZRotation);
                _rect.localRotation = Quaternion.Euler(0f, 0f, _zStart);
            }
        }

        private void LateUpdate()
        {
            _t += Time.unscaledDeltaTime;
            // Do not follow target after spawn; simply animate position from initial anchored point
            _vel += new Vector2(0f, gravity) * Time.unscaledDeltaTime;
            _rect.anchoredPosition += _vel * Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(1f - (_t / _life));
            if (_text != null)
            {
                var c = _text.color; c.a = a; _text.color = c;
            }
            // Smoothly scale down from startScale to endScale over lifetime
            float s = Mathf.Lerp(endScale, startScale, a);
            _rect.localScale = Vector3.one * s;
            // Rotate back to 0 over lifetime
            float z = Mathf.Lerp(endZRotation, _zStart, a);
            _rect.localRotation = Quaternion.Euler(0f, 0f, z);
            if (_t >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
