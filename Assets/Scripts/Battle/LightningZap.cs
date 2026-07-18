using UnityEngine;

namespace TR.Battle
{
    
    
    [RequireComponent(typeof(LineRenderer))]
    public class LightningZap : MonoBehaviour
    {
        [SerializeField] private float duration = 0.12f;
        [SerializeField] private float width = 0.06f;
        [SerializeField] private float jitter = 0.18f;
        [SerializeField] private int segments = 12;
        [SerializeField] private Color color = new Color(0.7f, 0.9f, 1f, 1f);
        [SerializeField] private bool glowEnabled = true;
        [SerializeField] private float glowBoost = 1000.0f;
        [SerializeField] private float animFrequency = 40f; 
        [SerializeField] private Material materialOverride;

        private Vector3 _start;
        private Vector3 _end;
        private float _t;
        private LineRenderer _lr;

        
        public static void Spawn(Vector3 start, Vector3 end, float duration, float width, float jitter, int segments, Color color)
        {
            var go = new GameObject("LightningZap");
            go.SetActive(false);
            var zap = go.AddComponent<LightningZap>();
            zap._start = start;
            zap._end = end;
            zap.duration = Mathf.Max(0.02f, duration);
            zap.width = Mathf.Max(0.001f, width);
            zap.jitter = Mathf.Max(0f, jitter);
            zap.segments = Mathf.Clamp(segments, 2, 128);
            zap.color = color;
            go.SetActive(true);
        }

        public static void Spawn(Vector3 start, Vector3 end, float duration, float width, float jitter, int segments, Color color, Material material)
        {
            var go = new GameObject("LightningZap");
            go.SetActive(false);
            var zap = go.AddComponent<LightningZap>();
            zap._start = start;
            zap._end = end;
            zap.duration = Mathf.Max(0.02f, duration);
            zap.width = Mathf.Max(0.001f, width);
            zap.jitter = Mathf.Max(0f, jitter);
            zap.segments = Mathf.Clamp(segments, 2, 128);
            zap.color = color;
            zap.materialOverride = material;
            go.SetActive(true);
        }

        
        public static void Spawn(Vector3 start, Vector3 end, float duration, float width, float jitter, int segments, Color color, bool glowEnabled, float glowBoost)
        {
            var go = new GameObject("LightningZap");
            go.SetActive(false);
            var zap = go.AddComponent<LightningZap>();
            zap._start = start;
            zap._end = end;
            zap.duration = Mathf.Max(0.02f, duration);
            zap.width = Mathf.Max(0.001f, width);
            zap.jitter = Mathf.Max(0f, jitter);
            zap.segments = Mathf.Clamp(segments, 2, 128);
            zap.color = color;
            zap.glowEnabled = glowEnabled;
            zap.glowBoost = glowBoost;
            go.SetActive(true);
        }

        
        public static void Spawn(Vector3 start, Vector3 end, float duration, float width, float jitter, int segments, Color color, Material material, bool glowEnabled, float glowBoost)
        {
            var go = new GameObject("LightningZap");
            go.SetActive(false);
            var zap = go.AddComponent<LightningZap>();
            zap._start = start;
            zap._end = end;
            zap.duration = Mathf.Max(0.02f, duration);
            zap.width = Mathf.Max(0.001f, width);
            zap.jitter = Mathf.Max(0f, jitter);
            zap.segments = Mathf.Clamp(segments, 2, 128);
            zap.color = color;
            zap.materialOverride = material;
            zap.glowEnabled = glowEnabled;
            zap.glowBoost = glowBoost;
            go.SetActive(true);
        }

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();
            
            _lr.positionCount = Mathf.Max(2, segments);
            _lr.startWidth = width;
            _lr.endWidth = width * 0.8f;
            _lr.numCapVertices = 4;
            _lr.numCornerVertices = 2;
            _lr.alignment = LineAlignment.View;
            
            _lr.textureMode = LineTextureMode.Stretch;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lr.receiveShadows = false;
            _lr.useWorldSpace = true;
            _lr.sortingOrder = 5000;

            Material mat = CreateZapMaterial();
            if (mat != null)
            {
                _lr.material = mat;
                ApplyMaterialColor(mat);
            }

            _lr.startColor = Color.white;
            _lr.endColor = Color.white;
            
            var grad = new Gradient();
            grad.mode = GradientMode.Blend;
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            _lr.colorGradient = grad;
            _lr.textureMode = LineTextureMode.Stretch;
        }

        private Material CreateZapMaterial()
        {
            if (materialOverride != null)
            {
                Material m = new Material(materialOverride);
                MakeAdditiveIfURP(m);
                return m;
            }

            Shader shader = FindZapShader();
            if (shader == null)
            {
                Debug.LogWarning("[LightningZap] No additive shader found. Lightning may not render.");
                return null;
            }

            Material mat = new Material(shader);
            MakeAdditiveIfURP(mat);
            return mat;
        }

        private Shader FindZapShader()
        {
            return Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Particles/SimpleLit")
                ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
                ?? Shader.Find("Particles/Additive")
                ?? Shader.Find("Legacy Shaders/Particles/Additive")
                ?? Shader.Find("Mobile/Particles/Additive")
                ?? Shader.Find("Sprites/Default");
        }

        private void MakeAdditiveIfURP(Material mat)
        {
            if (mat == null || mat.shader == null) return;
            if (!mat.shader.name.Contains("Universal Render Pipeline/Particles")) return;

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 1f);
            mat.SetFloat("_SrcBlend", 1f);
            mat.SetFloat("_DstBlend", 1f);
            mat.SetFloat("_SrcBlendAlpha", 1f);
            mat.SetFloat("_DstBlendAlpha", 1f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.renderQueue = 3000;
        }

        private void ApplyMaterialColor(Material mat)
        {
            if (mat == null) return;

            float boost = glowEnabled ? Mathf.Max(1f, glowBoost) : 1f;
            Color final = color * boost;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", final);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", final);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", final);
            if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", final);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= duration)
            {
                Destroy(gameObject);
                return;
            }
            
            if (_lr.positionCount != segments) _lr.positionCount = segments;
            Vector3 dir = _end - _start;
            float len = dir.magnitude;
            Vector3 fwd = len > 1e-5f ? dir / len : Vector3.right;
            
            Vector3 perp = new Vector3(-fwd.y, fwd.x, 0f);
            float phase = Time.time * animFrequency;
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)(segments - 1);
                Vector3 basePos = Vector3.Lerp(_start, _end, t);
                
                float amp = Mathf.Sin(t * Mathf.PI) * jitter;
                float noise = (Mathf.PerlinNoise(phase + i * 0.31f, phase * 0.73f) - 0.5f) * 2f; 
                Vector3 offset = perp * (amp * noise);
                _lr.SetPosition(i, basePos + offset);
            }
        }
    }
}
