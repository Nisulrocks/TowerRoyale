using UnityEngine;

namespace TR.Battle
{
    
    
    [RequireComponent(typeof(LineRenderer))]
    public class BeamController : MonoBehaviour
    {
        [SerializeField] private LineRenderer lr;
        [SerializeField] private Color startColor = new Color(1f, 0.6f, 0.2f, 1f);
        [SerializeField] private Color endColor = new Color(1f, 0.2f, 0f, 1f);
        [SerializeField] private float baseWidth = 0.04f;
        [SerializeField] private float maxWidth = 0.1f;
        [SerializeField] private bool jitter = true;
        [SerializeField] private float jitterAmplitude = 0.05f;
        [SerializeField] private int segments = 6; 

        private Vector3 _from;
        private Vector3 _to;
        private float _intensity; 

        private void Awake()
        {
            if (lr == null) lr = GetComponent<LineRenderer>();
            lr.positionCount = Mathf.Max(2, segments);
            lr.useWorldSpace = true;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            UpdateAppearance();
        }

        public void Configure(Color cStart, Color cEnd, float widthBase, float widthMax, bool doJitter, float amp)
        {
            startColor = cStart; endColor = cEnd;
            baseWidth = widthBase; maxWidth = Mathf.Max(widthBase, widthMax);
            jitter = doJitter; jitterAmplitude = Mathf.Max(0f, amp);
            UpdateAppearance();
        }

        public void SetEndpoints(Vector3 from, Vector3 to)
        {
            _from = from; _to = to;
        }

        public void SetIntensity01(float t)
        {
            _intensity = Mathf.Clamp01(t);
            UpdateAppearance();
        }

        private void UpdateAppearance()
        {
            if (lr == null) return;
            float w = Mathf.Lerp(baseWidth, maxWidth, _intensity);
            lr.startWidth = w; lr.endWidth = w * 0.85f;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.Lerp(startColor, Color.white, _intensity * 0.2f), 0f), new GradientColorKey(Color.Lerp(endColor, Color.white, _intensity * 0.1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(Mathf.Lerp(0.9f, 0.6f, _intensity), 1f) }
            );
            lr.colorGradient = grad;
        }

        private void LateUpdate()
        {
            if (lr == null) return;
            
            int count = Mathf.Max(2, segments);
            lr.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                Vector3 p = Vector3.Lerp(_from, _to, t);
                if (jitter && jitterAmplitude > 1e-4f && i > 0 && i < count - 1)
                {
                    
                    float n = Mathf.PerlinNoise(Time.time * 10f + i * 0.37f, 0.123f + i * 0.77f) * 2f - 1f;
                    Vector3 perp = Vector3.Cross((_to - _from).normalized, Vector3.forward);
                    p += perp * n * jitterAmplitude * Mathf.Lerp(0.2f, 1f, _intensity);
                }
                lr.SetPosition(i, p);
            }
        }
    }
}
