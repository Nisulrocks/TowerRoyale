using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TR.UI
{
    public class MatchResultBannerFX : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float buildUpSeconds = 0.45f;
        [SerializeField] private float flyInSeconds = 0.45f;
        [SerializeField] private float centreHoldSeconds = 0.2f;
        [SerializeField] private float glideSeconds = 0.5f;

        [Header("Header Rest Position")]
        [SerializeField] private float topOffsetY = 260f;
        [SerializeField] private float topScale = 0.8f;

        [Header("Camera Rumble")]
        [SerializeField] private float buildUpShake = 0.30f;
        [SerializeField] private float impactCameraShake = 0.55f;

        [Header("Depth")]
        [SerializeField] private int depthLayers = 9;
        [SerializeField] private float depthStep = 5.5f;

        [Header("Feel")]
        [SerializeField] private float shakeStrength = 34f;
        [SerializeField] private int sparkCount = 26;

        [Header("SFX")]
        [SerializeField] private string victorySfxKey = "game_victory";
        [SerializeField] private string defeatSfxKey = "game_defeat";
        [Range(0f, 1f)] [SerializeField] private float bgmDuckLevel = 0.25f;

        private AudioSource _resultSfx;
        private AudioClip _resultSfxClip;

        private static MatchResultBannerFX _instance;
        private static Sprite _glow, _ring, _star;

        private RectTransform _root;
        private Image _dim;
        private RectTransform _stage;      
        private RectTransform _word;       
        private readonly List<TMP_Text> _layers = new List<TMP_Text>();
        private Coroutine _running;

        public static bool IsPlaying => _instance != null && _instance._running != null;

        public static void Show(bool victory, System.Action onResultsCue)
        {
            var fx = Ensure();
            if (fx == null) { onResultsCue?.Invoke(); return; }

            fx.gameObject.SetActive(true);
            if (fx._running != null) fx.StopCoroutine(fx._running);
            fx._running = fx.StartCoroutine(fx.Run(victory, onResultsCue));
        }

        private static MatchResultBannerFX Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("MatchResultBannerFX");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MatchResultBannerFX>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 7000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _root = (RectTransform)transform;

            _dim = NewImage("Dim", _root);
            Stretch(_dim.rectTransform);
            _dim.color = new Color(0f, 0f, 0f, 0f);
            _dim.raycastTarget = false;   

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private IEnumerator Run(bool victory, System.Action onResultsCue)
        {
            ClearStage();

            Color front = victory ? new Color(1f, 0.84f, 0.28f) : new Color(0.92f, 0.26f, 0.26f);
            Color deep = victory ? new Color(0.42f, 0.24f, 0.02f) : new Color(0.24f, 0.04f, 0.04f);
            string word = victory ? "VICTORY" : "DEFEAT";
            float dimTarget = victory ? 0.5f : 0.62f;

            _stage = NewRect("Stage", _root);
            Center(_stage, Vector2.zero, 0f);

            var glow = NewImage("Glow", _stage);
            glow.sprite = Glow();
            Center(glow.rectTransform, Vector2.zero, 300f);
            glow.color = new Color(front.r, front.g, front.b, 0f);
            glow.raycastTarget = false;

            BuildWord(word, front, deep);

            SetWord(0f, 0f, Vector2.zero, 0f);
            StartCoroutine(CameraRumble(buildUpShake, buildUpSeconds, rampUp: true));

            float build = 0f;
            while (build < 1f)
            {
                build += Time.unscaledDeltaTime / Mathf.Max(0.01f, buildUpSeconds);
                float e = Mathf.Clamp01(build);
                _dim.color = new Color(0f, 0f, 0f, dimTarget * 0.35f * e);
                yield return null;
            }

            float dimAtSlam = _dim.color.a;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, flyInSeconds);
                float e = Mathf.Clamp01(t);

                _dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(dimAtSlam, dimTarget, e));

                if (victory)
                {
                    float ease = 1f - Mathf.Pow(1f - e, 3f);
                    SetWord(Mathf.Lerp(4.5f, 1f, ease), Mathf.Lerp(-75f, 0f, ease), Vector2.zero, ease);
                }
                else
                {
                    float ease = e * e;
                    SetWord(Mathf.Lerp(1.5f, 1f, ease), Mathf.Lerp(42f, 8f, ease),
                            new Vector2(0f, Mathf.Lerp(680f, 0f, ease)), ease);
                }

                float gs = Mathf.Lerp(300f, victory ? 1500f : 1000f, e);
                glow.rectTransform.sizeDelta = new Vector2(gs, gs);
                glow.color = new Color(front.r, front.g, front.b, (victory ? 0.42f : 0.3f) * e);
                yield return null;
            }

            string sfxKey = victory ? victorySfxKey : defeatSfxKey;
            if (!string.IsNullOrEmpty(sfxKey))
            {
                _resultSfx = TR.Audio.SFXManager.Instance?.Play(sfxKey);
                _resultSfxClip = _resultSfx != null ? _resultSfx.clip : null;

                var bgm = TR.Audio.BGMManager.Active;
                if (bgm != null && _resultSfxClip != null)
                    bgm.DuckFor(_resultSfxClip.length, bgmDuckLevel);
            }

            StartCoroutine(Shake(victory ? shakeStrength : shakeStrength * 1.35f, victory ? 0.35f : 0.5f));
            StartCoroutine(CameraRumble(victory ? impactCameraShake : impactCameraShake * 1.3f,
                                        victory ? 0.4f : 0.55f, rampUp: false));
            StartCoroutine(Shockwave(victory ? front : new Color(0.6f, 0.6f, 0.62f), victory ? 1700f : 1200f));
            if (victory) SpawnSparks(front);

            StartCoroutine(FrontFlash(front));

            float hold = 0f;
            float restTilt = victory ? 0f : 10f;
            while (hold < centreHoldSeconds)
            {
                hold += Time.unscaledDeltaTime;
                SetWord(1f, victory ? 0f : Mathf.Lerp(8f, 10f, hold / Mathf.Max(0.01f, centreHoldSeconds)),
                        Vector2.zero, 1f);
                yield return null;
            }

            float dimFrom = _dim.color.a;
            float glowFrom = glow.color.a;
            float glowSizeFrom = glow.rectTransform.sizeDelta.x;

            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, glideSeconds);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

                SetWord(Mathf.Lerp(1f, topScale, e),
                        Mathf.Lerp(restTilt, 0f, e),
                        new Vector2(0f, Mathf.Lerp(0f, topOffsetY, e)),
                        1f);

                _dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(dimFrom, 0f, e));

                float gs = Mathf.Lerp(glowSizeFrom, 900f, e);
                glow.rectTransform.sizeDelta = new Vector2(gs, gs);
                glow.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, topOffsetY, e));
                glow.color = new Color(front.r, front.g, front.b, Mathf.Lerp(glowFrom, 0.14f, e));
                yield return null;
            }

            onResultsCue?.Invoke();

            float idle = 0f;
            while (true)
            {
                idle += Time.unscaledDeltaTime;
                if (victory)
                {
                    float sway = Mathf.Sin(idle * 1.3f) * 4f;
                    float bob = Mathf.Sin(idle * 1.7f) * 3f;
                    SetWord(topScale, sway * 0.4f, new Vector2(0f, topOffsetY + bob), 1f, sway);
                }
                else
                {
                    SetWord(topScale, 0f, new Vector2(0f, topOffsetY), 1f);
                }
                yield return null;
            }
        }

        public static void Dismiss()
        {
            if (_instance == null) return;
            _instance.StopAllCoroutines();
            _instance._running = null;

            TR.Audio.SFXManager.Instance?.FadeOutOneShot(_instance._resultSfx, _instance._resultSfxClip, 0.25f);
            _instance._resultSfx = null;
            _instance._resultSfxClip = null;

            TR.Audio.BGMManager.Active?.ClearDuck(0.4f);

            _instance.ClearStage();
            if (_instance._dim != null) _instance._dim.color = new Color(0f, 0f, 0f, 0f);
            _instance.gameObject.SetActive(false);
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Dismiss();
        }

        private void BuildWord(string word, Color front, Color deep)
        {
            _layers.Clear();

            _word = NewRect("Word", _stage);
            Center(_word, Vector2.zero, 0f);
            _word.sizeDelta = new Vector2(1700f, 320f);

            var font = ResolveFont();

            for (int i = depthLayers; i >= 0; i--)
            {
                var rt = NewRect("Layer" + i, _word);
                Stretch(rt);
                var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.text = word;
                tmp.fontSize = 190f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.raycastTarget = false;
                if (font != null) tmp.font = font;

                float k = depthLayers <= 0 ? 0f : i / (float)depthLayers;
                tmp.color = i == 0 ? front : Color.Lerp(front, deep, Mathf.Lerp(0.35f, 1f, k));
                _layers.Add(tmp);
            }
            _layers.Reverse();   
        }

        private void SetWord(float scale, float tiltDeg, Vector2 offset, float alpha, float yawDeg = 0f)
        {
            if (_word == null) return;

            _word.localScale = Vector3.one * scale;
            _word.localRotation = Quaternion.Euler(tiltDeg, yawDeg, 0f);
            _word.anchoredPosition = offset;

            float rad = tiltDeg * Mathf.Deg2Rad;
            float yawRad = yawDeg * Mathf.Deg2Rad;

            for (int i = 0; i < _layers.Count; i++)
            {
                var tmp = _layers[i];
                if (tmp == null) continue;

                float d = i * depthStep;
                var rt = (RectTransform)tmp.transform;
                rt.anchoredPosition = new Vector2(-Mathf.Sin(yawRad) * d, -Mathf.Cos(rad) * d);

                var c = tmp.color;
                c.a = alpha;
                tmp.color = c;
            }
        }

        private IEnumerator FrontFlash(Color settle)
        {
            if (_layers.Count == 0) yield break;
            var face = _layers[0];
            float t = 0f;
            while (t < 1f)
            {
                if (face == null) yield break;
                t += Time.unscaledDeltaTime / 0.28f;
                float e = Mathf.Clamp01(t);
                var c = Color.Lerp(Color.white, settle, e);
                c.a = face.color.a;
                face.color = c;
                yield return null;
            }
        }

        private IEnumerator CameraRumble(float strength, float seconds, bool rampUp)
        {
            if (!TR.Systems.ShakeSettings.ScreenShakeEnabled) yield break;

            var cam = Camera.main;
            if (cam == null) yield break;
            var tr = cam.transform;

            Vector3 applied = Vector3.zero;
            float t = 0f;
            while (t < 1f)
            {
                if (tr == null) yield break;
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
                float e = Mathf.Clamp01(t);

                float amp = rampUp
                    ? strength * (e * e)        
                    : strength * (1f - e) * (1f - e);

                Vector3 want = (Vector3)(Random.insideUnitCircle * amp);
                tr.localPosition += want - applied;
                applied = want;
                yield return null;
            }

            if (tr != null) tr.localPosition -= applied;
        }

        private IEnumerator Shake(float strength, float seconds)
        {
            float t = 0f;
            while (t < 1f)
            {
                if (_stage == null) yield break;
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
                float damp = 1f - Mathf.Clamp01(t);
                _stage.anchoredPosition = new Vector2(
                    Random.Range(-1f, 1f) * strength * damp,
                    Random.Range(-1f, 1f) * strength * damp);
                yield return null;
            }
            if (_stage != null) _stage.anchoredPosition = Vector2.zero;
        }

        private IEnumerator Shockwave(Color tint, float maxSize)
        {
            var ring = NewImage("Shockwave", _stage);
            ring.sprite = Ring();
            ring.raycastTarget = false;
            Center(ring.rectTransform, Vector2.zero, 220f);

            float t = 0f;
            while (t < 1f)
            {
                if (ring == null) yield break;
                t += Time.unscaledDeltaTime / 0.55f;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                float s = Mathf.Lerp(220f, maxSize, e);
                ring.rectTransform.sizeDelta = new Vector2(s, s);
                ring.color = new Color(tint.r, tint.g, tint.b, 0.85f * (1f - e));
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }

        private void SpawnSparks(Color tint)
        {
            for (int i = 0; i < sparkCount; i++)
            {
                var img = NewImage("Spark", _stage);
                img.sprite = Star();
                img.color = tint;
                img.raycastTarget = false;
                float size = Random.Range(18f, 46f);
                Center(img.rectTransform, Vector2.zero, size);

                float ang = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.65f);
                StartCoroutine(SparkFly(img, dir * Random.Range(320f, 900f), Random.Range(0.5f, 0.95f)));
            }
        }

        private IEnumerator SparkFly(Image img, Vector2 to, float seconds)
        {
            var rt = img.rectTransform;
            Vector2 from = rt.anchoredPosition;
            Color c0 = img.color;
            float t = 0f;
            while (t < 1f)
            {
                if (img == null) yield break;
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
                float e = Mathf.Clamp01(t);
                float ease = 1f - Mathf.Pow(1f - e, 2f);
                rt.anchoredPosition = Vector2.Lerp(from, to, ease) + new Vector2(0f, -260f * e * e);
                rt.localRotation = Quaternion.Euler(0f, 0f, e * 220f);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, e);
                img.color = new Color(c0.r, c0.g, c0.b, 1f - e);
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
        }

        private void ClearStage()
        {
            StopAllCoroutinesExceptRun();
            _layers.Clear();
            _word = null;
            if (_root == null) return;
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                var child = _root.GetChild(i);
                if (_dim != null && child == _dim.transform) continue;
                Destroy(child.gameObject);
            }
            _stage = null;
        }

        private void StopAllCoroutinesExceptRun()
        {

        }


        private static TMP_FontAsset _font;

        private static TMP_FontAsset ResolveFont()
        {
            if (_font != null) return _font;

            var existing = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in existing)
            {
                if (t == null || t.font == null) continue;
                if (_instance != null && t.transform.IsChildOf(_instance.transform)) continue;
                _font = t.font;
                break;
            }

            if (_font == null) _font = TMP_Settings.defaultFontAsset;
            return _font;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<Image>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt, Vector2 pos, float size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            if (size > 0f) rt.sizeDelta = new Vector2(size, size);
        }


        private static Sprite Glow()
        {
            if (_glow != null) return _glow;
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color[S * S];
            var c = new Vector2(S * 0.5f, S * 0.5f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (S * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * S + x] = new Color(1f, 1f, 1f, a * a * a);
                }
            tex.SetPixels(px); tex.Apply();
            _glow = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _glow;
        }

        private static Sprite Ring()
        {
            if (_ring != null) return _ring;
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color[S * S];
            var c = new Vector2(S * 0.5f, S * 0.5f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (S * 0.5f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.09f);
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            _ring = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _ring;
        }

        private static Sprite Star()
        {
            if (_star != null) return _star;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color[S * S];
            var c = new Vector2(S * 0.5f, S * 0.5f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) - c;
                    float n = (Mathf.Abs(p.x) + Mathf.Abs(p.y)) / (S * 0.5f);   // 4-point diamond
                    float a = Mathf.Clamp01(1f - n);
                    px[y * S + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(px); tex.Apply();
            _star = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _star;
        }
    }
}
