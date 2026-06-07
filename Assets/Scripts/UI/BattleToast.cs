using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace TR.UI
{
    
    public class BattleToast : MonoBehaviour
    {
        private static BattleToast _instance;
        private static GameObject _prefab;
        private static Canvas _overlayCanvas; 
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private CanvasGroup _group;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            if (_text == null)
            {
                _text = GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (_canvas == null)
            {
                _canvas = GetComponentInChildren<Canvas>(true);
            }
            if (_group == null)
            {
                _group = GetComponentInChildren<CanvasGroup>(true);
            }
            if (_text == null)
            {
                
                if (_canvas == null)
                {
                    var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    cgo.transform.SetParent(transform, false);
                    _canvas = cgo.GetComponent<Canvas>();
                    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                var tgo = new GameObject("ToastText", typeof(TextMeshProUGUI));
                tgo.transform.SetParent(_canvas.transform, false);
                _text = tgo.GetComponent<TextMeshProUGUI>();
                _text.alignment = TextAlignmentOptions.Center;
                _text.fontSize = 32f;
                _text.color = new Color(1f, 0.95f, 0.4f, 0f); 
                var rect = _text.rectTransform;
                
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(900, 120);
            }
            if (_group == null)
            {
                
                _group = gameObject.GetComponent<CanvasGroup>();
                if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            }
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        
        public static void SetPrefab(GameObject prefab)
        {
            _prefab = prefab;
        }

        private static Canvas GetOrCreateOverlayCanvas()
        {
            if (_overlayCanvas != null) return _overlayCanvas;
            
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].isRootCanvas && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    _overlayCanvas = canvases[i];
                    break;
                }
            }
            if (_overlayCanvas == null)
            {
                
                var cgo = new GameObject("BattleToastCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _overlayCanvas = cgo.GetComponent<Canvas>();
                _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _overlayCanvas.sortingOrder = 5000; 
                var scaler = cgo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            return _overlayCanvas;
        }

        public static void Show(string message, float duration = 1.25f)
        {
            if (_instance == null)
            {
                if (_prefab != null)
                {
                    
                    var parent = GetOrCreateOverlayCanvas().transform;
                    var go = Object.Instantiate(_prefab, parent, false);
                    
                    go.name = "BattleToast";
                    _instance = go.GetComponent<BattleToast>();
                    if (_instance == null) _instance = go.AddComponent<BattleToast>();
                    _instance.Awake();
                }
                else
                {
                    var go = new GameObject("BattleToast");
                    
                    go.transform.SetParent(GetOrCreateOverlayCanvas().transform, false);
                    _instance = go.AddComponent<BattleToast>();
                    _instance.Awake();
                }
            }
            _instance.StopAllCoroutines();
            _instance.StartCoroutine(_instance.RunToast(message, duration));
        }

        private System.Collections.IEnumerator RunToast(string message, float duration)
        {
            if (_text != null) _text.text = message;
            
            if (_group == null)
            {
                _group = gameObject.GetComponent<CanvasGroup>();
                if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }

            
            Color textFrom = _text != null ? _text.color : Color.white;
            textFrom.a = 0f;
            Color textTo = _text != null ? new Color(textFrom.r, textFrom.g, textFrom.b, 1f) : Color.white;

            
            float t = 0f;
            const float inTime = 0.18f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, inTime);
                float e = Mathf.SmoothStep(0f, 1f, t);
                _group.alpha = e;
                if (_text != null) _text.color = Color.Lerp(textFrom, textTo, e);
                yield return null;
            }
            _group.alpha = 1f;
            if (_text != null) _text.color = textTo;

            
            float hold = Mathf.Max(0f, duration - inTime - 0.22f);
            float elapsed = 0f;
            while (elapsed < hold)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            
            t = 0f;
            const float outTime = 0.22f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, outTime);
                float e = Mathf.SmoothStep(0f, 1f, t);
                _group.alpha = 1f - e;
                if (_text != null) _text.color = Color.Lerp(textTo, textFrom, e);
                yield return null;
            }
            _group.alpha = 0f;
            if (_text != null) _text.color = textFrom;
        }
    }
}
