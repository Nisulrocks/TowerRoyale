using UnityEngine;

namespace TR.Battle
{
    // Draws a simple circle ring using a LineRenderer.
    // Set properties (Radius/Color/Segments/Thickness) and the ring updates automatically.
    [RequireComponent(typeof(LineRenderer))]
    public class RangeRing : MonoBehaviour
    {
        [SerializeField] private float radius = 1f;
        [SerializeField] private float thickness = 0.05f;
        [SerializeField] private int segments = 48;
        [SerializeField] private Color color = new Color(0.2f, 1f, 0.2f, 0.6f);
        [SerializeField] private bool useWorldSpace = false;
        [SerializeField] private int sortingOrder = 2000;
        [SerializeField] private string sortingLayerName = "Default";

        private LineRenderer _lr;
        private float _lastRadius;
        private int _lastSegments;
        private float _lastThickness;
        private Color _lastColor;

        public float Radius { get => radius; set { radius = Mathf.Max(0f, value); MarkDirty(); } }
        public float Thickness { get => thickness; set { thickness = Mathf.Max(0.001f, value); MarkDirty(); } }
        public int Segments { get => segments; set { segments = Mathf.Clamp(value, 8, 256); MarkDirty(); } }
        public Color Color { get => color; set { color = value; MarkDirty(); } }

        private bool _dirty = true;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();
            _lr.loop = true;
            _lr.useWorldSpace = useWorldSpace;
            _lr.textureMode = LineTextureMode.Stretch;
            _lr.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(sortingLayerName)) _lr.sortingLayerName = sortingLayerName;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lr.receiveShadows = false;
            _lr.alignment = LineAlignment.TransformZ;
            // Material
            if (_lr.sharedMaterial == null)
            {
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = Color.white;
                _lr.sharedMaterial = mat;
            }
            MarkDirty();
        }

        private void OnEnable()
        {
            MarkDirty();
        }

        private void Update()
        {
            if (_dirty || PropertiesChanged())
            {
                Rebuild();
                _dirty = false;
            }
        }

        private bool PropertiesChanged()
        {
            return !Mathf.Approximately(_lastRadius, radius) ||
                   _lastSegments != segments ||
                   !Mathf.Approximately(_lastThickness, thickness) ||
                   _lastColor != color;
        }

        private void Rebuild()
        {
            if (_lr == null) return;
            _lastRadius = radius;
            _lastSegments = segments;
            _lastThickness = thickness;
            _lastColor = color;

            _lr.positionCount = Mathf.Max(segments, 8);
            _lr.startWidth = thickness;
            _lr.endWidth = thickness;
            _lr.startColor = color;
            _lr.endColor = color;

            float step = Mathf.PI * 2f / _lr.positionCount;
            for (int i = 0; i < _lr.positionCount; i++)
            {
                float ang = i * step;
                _lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f));
            }
        }

        public void MarkDirty()
        {
            _dirty = true;
        }
    }
}
