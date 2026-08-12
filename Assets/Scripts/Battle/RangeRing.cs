using UnityEngine;
using UnityEngine.Rendering;

namespace TR.Battle
{








    public class RangeRing : MonoBehaviour
    {
        [SerializeField] private float radius = 1f;
        [SerializeField] private Color color = new Color(0.2f, 1f, 0.2f, 0.6f);
        [SerializeField] private int sortingOrder = 2000;
        [SerializeField] private string sortingLayerName = "Default";

        [Header("Rim")]

        [SerializeField] private float rimPixels = 2.5f;
        [SerializeField] private int rimSegments = 192;

        [Header("Ticks")]
        [SerializeField] private bool showTicks = true;
        [SerializeField] private int tickCount = 36;

        [SerializeField] private float tickInner = 0.885f;
        [SerializeField] private float tickOuter = 0.945f;

        [SerializeField] private float tickFill = 0.4f;
        [SerializeField] private float tickSpinSpeed = -12f;

        [Header("Sweep")]
        [SerializeField] private bool showSweep = true;
        [SerializeField] private float sweepSpinSpeed = 55f;
        [SerializeField] private float sweepAlpha = 0.45f;

        [Header("Motion")]
        [SerializeField] private float popInSeconds = 0.22f;
        [SerializeField] private float popOvershoot = 1.06f;
        [SerializeField] private float breathAmount = 0.012f;
        [SerializeField] private float breathSpeed = 2.2f;

        public float Radius { get => radius; set => radius = Mathf.Max(0f, value); }
        public Color Color { get => color; set => color = value; }

        private const float FillAlpha = 0.30f;
        private const int TexSize = 256;

        private static Sprite s_fill;
        private static Sprite s_sweep;
        private static Material s_lineMat;

        private SpriteRenderer _fill;
        private SpriteRenderer _sweep;
        private LineRenderer _rim;
        private MeshRenderer _tickRenderer;
        private Mesh _tickMesh;
        private Transform _tickTr;

        private Vector3[] _rimPoints;
        private Vector3[] _tickVerts;
        private Color[] _tickColors;
        private int[] _tickTris;
        private float _builtRim = -1f;
        private float _builtTicks = -1f;
        private bool _tickTrisSet;
        private Color _builtTickColor = new Color(-1f, -1f, -1f, -1f);
        private float _popT;
        private float _spin;

        private void Awake()
        {


            var stale = GetComponent<LineRenderer>();
            if (stale != null) Destroy(stale);

            var sg = GetComponent<SortingGroup>();
            if (sg == null) sg = gameObject.AddComponent<SortingGroup>();
            sg.sortingOrder = 100;

            BuildFill();
            BuildSweep();
            BuildRim();
            BuildTicks();
        }

        private void OnEnable()
        {

            _popT = 0f;
            _spin = 0f;
            _builtRim = -1f;
            _builtTicks = -1f;
            Apply(0f);
        }

        private void Update()
        {
            if (_popT < 1f)
                _popT = Mathf.Clamp01(_popT + Time.deltaTime / Mathf.Max(0.01f, popInSeconds));

            _spin += Time.deltaTime;
            Apply(_popT);
        }





        private void LateUpdate()
        {
            transform.rotation = Quaternion.identity;
        }

        private void Apply(float pop)
        {
            if (_fill == null) return;




            float c1 = Mathf.Max(0f, popOvershoot - 1f) * 17f;
            float t = pop - 1f;
            float ease = 1f + (c1 + 1f) * t * t * t + c1 * t * t;

            float shown = radius * Mathf.LerpUnclamped(0.65f, 1f, ease);
            float breath = 1f + Mathf.Sin(_spin * breathSpeed) * breathAmount * pop;

            float a = Mathf.Clamp01(color.a) * pop;
            var rgb = new Color(color.r, color.g, color.b);

            _fill.transform.localScale = Vector3.one * shown;
            _fill.color = new Color(rgb.r, rgb.g, rgb.b, a);

            _sweep.transform.localScale = Vector3.one * shown;
            _sweep.transform.localRotation = Quaternion.Euler(0f, 0f, _spin * sweepSpinSpeed);
            _sweep.color = new Color(rgb.r, rgb.g, rgb.b, a * (showSweep ? sweepAlpha : 0f));




            RebuildRim(shown);
            _rim.startColor = new Color(rgb.r, rgb.g, rgb.b, a);
            _rim.endColor = _rim.startColor;
            _rim.widthMultiplier = RimWorldWidth();

            _tickRenderer.enabled = showTicks;
            if (showTicks)
            {
                RebuildTicks(shown * breath);
                _tickTr.localRotation = Quaternion.Euler(0f, 0f, _spin * tickSpinSpeed);



                TintTicks(new Color(rgb.r, rgb.g, rgb.b, a));
            }
        }

        private void TintTicks(Color c)
        {
            if (c == _builtTickColor) return;
            _builtTickColor = c;

            for (int i = 0; i < _tickColors.Length; i++) _tickColors[i] = c;
            _tickMesh.colors = _tickColors;
        }






        private float RimWorldWidth()
        {
            float scale = Mathf.Abs(transform.lossyScale.x);
            if (scale < 1e-4f) scale = 1f;

            var cam = Camera.main;
            if (cam == null || !cam.orthographic || Screen.height <= 0)
                return Mathf.Max(0.01f, radius * 0.012f);

            float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;
            return Mathf.Max(0.004f, rimPixels * worldPerPixel / scale);
        }





        private void BuildRim()
        {
            var go = new GameObject("Rim");
            go.transform.SetParent(transform, false);

            _rim = go.AddComponent<LineRenderer>();
            _rim.useWorldSpace = false;
            _rim.loop = true;
            _rim.alignment = LineAlignment.TransformZ;
            _rim.textureMode = LineTextureMode.Stretch;
            _rim.numCapVertices = 0;
            _rim.numCornerVertices = 0;
            _rim.shadowCastingMode = ShadowCastingMode.Off;
            _rim.receiveShadows = false;
            _rim.sortingOrder = sortingOrder + 2;
            if (!string.IsNullOrEmpty(sortingLayerName)) _rim.sortingLayerName = sortingLayerName;
            _rim.sharedMaterial = LineMaterial();

            int n = Mathf.Clamp(rimSegments, 32, 512);
            _rimPoints = new Vector3[n];
            _rim.positionCount = n;
        }

        private void RebuildRim(float r)
        {
            if (Mathf.Abs(r - _builtRim) < 0.0005f) return;
            _builtRim = r;

            int n = _rimPoints.Length;
            float step = Mathf.PI * 2f / n;
            for (int i = 0; i < n; i++)
                _rimPoints[i] = new Vector3(Mathf.Cos(i * step) * r, Mathf.Sin(i * step) * r, 0f);

            _rim.SetPositions(_rimPoints);
        }





        private void BuildTicks()
        {
            var go = new GameObject("Ticks");
            go.transform.SetParent(transform, false);
            _tickTr = go.transform;

            _tickMesh = new Mesh { name = "RangeRingTicks" };
            _tickMesh.MarkDynamic();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _tickMesh;

            _tickRenderer = go.AddComponent<MeshRenderer>();
            _tickRenderer.sharedMaterial = LineMaterial();
            _tickRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _tickRenderer.receiveShadows = false;
            _tickRenderer.sortingOrder = sortingOrder + 1;
            if (!string.IsNullOrEmpty(sortingLayerName)) _tickRenderer.sortingLayerName = sortingLayerName;

            int count = Mathf.Max(1, tickCount);
            _tickVerts = new Vector3[count * 4];
            _tickColors = new Color[count * 4];
            _tickTris = new int[count * 6];
            for (int i = 0; i < _tickColors.Length; i++) _tickColors[i] = Color.white;
            for (int i = 0; i < count; i++)
            {
                int v = i * 4, tri = i * 6;
                _tickTris[tri] = v; _tickTris[tri + 1] = v + 1; _tickTris[tri + 2] = v + 2;
                _tickTris[tri + 3] = v; _tickTris[tri + 4] = v + 2; _tickTris[tri + 5] = v + 3;
            }
        }

        private void RebuildTicks(float r)
        {
            if (Mathf.Abs(r - _builtTicks) < 0.0005f) return;
            _builtTicks = r;

            int count = Mathf.Max(1, tickCount);
            float step = Mathf.PI * 2f / count;
            float half = step * Mathf.Clamp01(tickFill) * 0.5f;
            float inner = r * tickInner;
            float outer = r * tickOuter;

            for (int i = 0; i < count; i++)
            {
                float mid = i * step;
                float a0 = mid - half, a1 = mid + half;
                float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0);
                float c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);

                int v = i * 4;
                _tickVerts[v] = new Vector3(c0 * inner, s0 * inner, 0f);
                _tickVerts[v + 1] = new Vector3(c0 * outer, s0 * outer, 0f);
                _tickVerts[v + 2] = new Vector3(c1 * outer, s1 * outer, 0f);
                _tickVerts[v + 3] = new Vector3(c1 * inner, s1 * inner, 0f);
            }





            _tickMesh.vertices = _tickVerts;
            if (!_tickTrisSet)
            {
                _tickMesh.triangles = _tickTris;
                _tickMesh.colors = _tickColors;
                _tickTrisSet = true;
            }
            _tickMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (_tickMesh != null) Destroy(_tickMesh);
        }





        private void BuildFill()
        {
            _fill = MakeSpriteLayer("Fill", FillSprite(), 0);
        }

        private void BuildSweep()
        {
            _sweep = MakeSpriteLayer("Sweep", SweepSprite(), 3);
        }

        private SpriteRenderer MakeSpriteLayer(string layerName, Sprite sprite, int orderOffset)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder + orderOffset;
            if (!string.IsNullOrEmpty(sortingLayerName)) sr.sortingLayerName = sortingLayerName;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return sr;
        }

        private static Material LineMaterial()
        {
            if (s_lineMat != null) return s_lineMat;

            s_lineMat = new Material(Shader.Find("Sprites/Default"));
            s_lineMat.name = "RangeRingLine";



            s_lineMat.mainTexture = Texture2D.whiteTexture;
            return s_lineMat;
        }

        public static void Warm()
        {
            FillSprite();
            SweepSprite();
            LineMaterial();
        }





        public static Sprite FillSprite()
        {
            if (s_fill != null) return s_fill;

            var tex = NewTexture();
            var px = new Color[TexSize * TexSize];
            float half = TexSize * 0.5f;

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = 0f;
                    if (r <= 1f)
                    {


                        a = Mathf.Lerp(0.18f, 1f, r * r * r) * FillAlpha;
                        a *= Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.93f, 1f, r));
                    }

                    px[y * TexSize + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, true);
            s_fill = ToSprite(tex);
            return s_fill;
        }




        public static Sprite SweepSprite()
        {
            if (s_sweep != null) return s_sweep;

            var tex = NewTexture();
            var px = new Color[TexSize * TexSize];
            float half = TexSize * 0.5f;

            const float arc = 0.30f;

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = 0f;
                    if (r <= 0.93f)
                    {
                        float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f);
                        if (ang < 0f) ang += 1f;

                        if (ang < arc)
                        {
                            float trail = 1f - (ang / arc);
                            a = trail * trail * trail;
                            a *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.55f, r));
                            a *= Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.86f, 0.93f, r));
                        }
                    }

                    px[y * TexSize + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, true);
            s_sweep = ToSprite(tex);
            return s_sweep;
        }

        private static Texture2D NewTexture()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Sprite ToSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0f, 0f, TexSize, TexSize),
                                 new Vector2(0.5f, 0.5f), TexSize * 0.5f);
        }
    }
}
