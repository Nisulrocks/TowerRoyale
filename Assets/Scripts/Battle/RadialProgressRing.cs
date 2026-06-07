using UnityEngine;

namespace TR.Battle
{
    
    
    [RequireComponent(typeof(LineRenderer))]
    public class RadialProgressRing : MonoBehaviour
    {
        [Header("Appearance")] 
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private float thickness = 0.06f;
        [SerializeField] private int segments = 64;
        [SerializeField] private Color startColor = new Color(0.2f, 0.9f, 0.2f, 1f); 
        [SerializeField] private Color endColor = new Color(0.9f, 0.2f, 0.2f, 1f);   
        [SerializeField] private bool clockwise = true;
        [SerializeField] private int sortingOrder = 2100;
        [SerializeField] private string sortingLayerName = "Default";

        [Header("Progress")]
        [Range(0f, 1f)] [SerializeField] private float progress = 1f; 

        private LineRenderer _lr;
        private bool _dirty = true;

        public float Radius { get => radius; set { radius = Mathf.Max(0f, value); _dirty = true; } }
        public float Thickness { get => thickness; set { thickness = Mathf.Max(0.001f, value); _dirty = true; } }
        public int Segments { get => segments; set { segments = Mathf.Clamp(value, 8, 256); _dirty = true; } }
        public float Progress => progress;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();
            _lr.loop = false;
            _lr.useWorldSpace = false;
            _lr.textureMode = LineTextureMode.Stretch;
            _lr.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(sortingLayerName)) _lr.sortingLayerName = sortingLayerName;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lr.receiveShadows = false;
            _lr.alignment = LineAlignment.TransformZ;
            if (_lr.sharedMaterial == null)
            {
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = Color.white;
                _lr.sharedMaterial = mat;
            }
            _dirty = true;
        }

        private void Update()
        {
            if (_dirty)
            {
                Rebuild();
                _dirty = false;
            }
        }

        public void SetProgress(float value)
        {
            float clamped = Mathf.Clamp01(value);
            if (!Mathf.Approximately(progress, clamped))
            {
                progress = clamped;
                _dirty = true;
            }
        }

        private void Rebuild()
        {
            if (_lr == null) return;
            int count = Mathf.Max(8, segments);
            int used = Mathf.Max(2, Mathf.RoundToInt(count * progress));
            float totalAng = Mathf.PI * 2f * progress;
            float step = (used - 1) > 0 ? totalAng / (used - 1) : totalAng;
            _lr.positionCount = used;
            _lr.startWidth = thickness;
            _lr.endWidth = thickness;
            Color col = Color.Lerp(endColor, startColor, progress); 
            _lr.startColor = col;
            _lr.endColor = col;
            for (int i = 0; i < used; i++)
            {
                float ang = (clockwise ? -1f : 1f) * i * step;
                _lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f));
            }
        }
    }
}
