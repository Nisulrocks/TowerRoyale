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
            if (glowEnabled)
            {
                
                var add = Shader.Find("Particles/Additive");
                if (add == null) add = Shader.Find("Legacy Shaders/Particles/Additive");
                if (add != null)
                {
                    var addMat = new Material(add);
                    _lr.material = addMat;
                    float boost = Mathf.Max(1f, glowEnabled ? glowBoost : 1f);
                    if (addMat.HasProperty("_TintColor")) addMat.SetColor("_TintColor", color * boost);
                    if (addMat.HasProperty("_Color")) addMat.SetColor("_Color", color * boost);
                }
            }
            else
            {
                
                var def = Shader.Find("Sprites/Default");
                if (def != null)
                {
                    var mat = new Material(def);
                    mat.color = color; 
                    _lr.material = mat;
                }
            }
            _lr.startColor = color;
            _lr.endColor = color;
            
            var grad = new Gradient();
            grad.mode = GradientMode.Blend;
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) }
            );
            _lr.colorGradient = grad;
            _lr.textureMode = LineTextureMode.Stretch;

            
            var pb = new MaterialPropertyBlock();
            _lr.GetPropertyBlock(pb);
            var matNow = _lr.sharedMaterial;
            if (matNow != null)
            {
                if (matNow.HasProperty("_BaseColor")) pb.SetColor("_BaseColor", color);
                if (matNow.HasProperty("_Color")) pb.SetColor("_Color", color);
                if (matNow.HasProperty("_Tint")) pb.SetColor("_Tint", color);
                if (matNow.HasProperty("_EmissionColor"))
                {
                    pb.SetColor("_EmissionColor", color);
                    matNow.EnableKeyword("_EMISSION");
                }
            }
            _lr.SetPropertyBlock(pb);

            
            
            if (glowEnabled)
            {
                var instMat = _lr.material; 
                if (instMat != null)
                {
                    
                    if (instMat.HasProperty("_EmissionColor"))
                    {
                        float boost = Mathf.Max(0f, glowBoost);
                        instMat.SetColor("_EmissionColor", color * boost);
                        instMat.EnableKeyword("_EMISSION");
                    }
                    else if (instMat.HasProperty("_Color"))
                    {
                        
                        float boost = Mathf.Max(1f, glowBoost);
                        instMat.SetColor("_Color", color * boost);
                    }
                }
            }
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
