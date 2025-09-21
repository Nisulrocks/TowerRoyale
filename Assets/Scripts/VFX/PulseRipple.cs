using UnityEngine;

namespace TR.VFX
{
    // Simple procedural ripple ring using a LineRenderer. Expands from small to target radius and fades out, then destroys itself.
    public class PulseRipple : MonoBehaviour
    {
        [SerializeField] private float duration = 0.25f;
        [SerializeField] private float startRadius = 0.1f;
        [SerializeField] private float endRadius = 3f;
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private Color color = new Color(0.6f, 0.9f, 1f, 0.9f);
        [SerializeField] private int segments = 64;

        private LineRenderer _lr;
        private float _t;

        public static void Spawn(Vector3 position, float endRadius, Color color, float duration, float lineWidth = 0.06f, int segments = 64)
        {
            var go = new GameObject("PulseRipple");
            go.transform.position = position;
            var pr = go.AddComponent<PulseRipple>();
            pr.duration = Mathf.Max(0.01f, duration);
            pr.endRadius = Mathf.Max(0.01f, endRadius);
            pr.lineWidth = Mathf.Max(0.001f, lineWidth);
            pr.color = color;
            pr.segments = Mathf.Clamp(segments, 12, 256);
        }

        private void Awake()
        {
            _lr = gameObject.AddComponent<LineRenderer>();
            _lr.loop = true;
            _lr.positionCount = Mathf.Max(12, segments);
            _lr.useWorldSpace = true;
            _lr.widthMultiplier = 1f;
            _lr.startWidth = lineWidth;
            _lr.endWidth = lineWidth;
            _lr.material = new Material(Shader.Find("Sprites/Default"));
            _lr.startColor = color;
            _lr.endColor = color;
            _t = 0f;
            UpdateRing(startRadius);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float a = Mathf.Clamp01(_t / Mathf.Max(0.01f, duration));
            float r = Mathf.Lerp(startRadius, endRadius, Mathf.SmoothStep(0f, 1f, a));
            // Fade out alpha over time
            Color c = color; c.a = Mathf.Lerp(color.a, 0f, a);
            _lr.startColor = c; _lr.endColor = c;
            // Slightly reduce width towards end
            float w = Mathf.Lerp(lineWidth, lineWidth * 0.4f, a);
            _lr.startWidth = w; _lr.endWidth = w;

            UpdateRing(r);
            if (_t >= duration)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateRing(float r)
        {
            int count = Mathf.Max(12, segments);
            if (_lr.positionCount != count) _lr.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
                _lr.SetPosition(i, p);
            }
        }
    }
}
