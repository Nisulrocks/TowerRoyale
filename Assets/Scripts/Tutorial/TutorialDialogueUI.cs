using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TR.Tutorial
{
    
    public class TutorialDialogueUI : MonoBehaviour
    {
        [System.Serializable]
        public class AnchorPreset
        {
            public Vector2 anchorMin = new Vector2(0.5f, 0f);
            public Vector2 anchorMax = new Vector2(0.5f, 0f);
            public Vector2 pivot = new Vector2(0.5f, 0f);
            public Vector2 anchoredPosition = new Vector2(0f, 40f);
        }

        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private float defaultCharDelay = 0.03f;

        [Header("Screen Transform")]
        [Tooltip("If true, overrides the prefab RectTransform with the values below on Awake.")]
        [SerializeField] private bool applyTransformOnAwake = true;
        [SerializeField] private Vector2 anchorMin = new Vector2(0.5f, 0f);
        [SerializeField] private Vector2 anchorMax = new Vector2(0.5f, 0f);
        [SerializeField] private Vector2 pivot = new Vector2(0.5f, 0f);
        [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 40f);
        [SerializeField] private Vector2 size = new Vector2(720f, 140f);

        [Header("Left / Right Presets")]
        [SerializeField] private AnchorPreset leftPreset = new AnchorPreset();
        [SerializeField] private AnchorPreset rightPreset = new AnchorPreset();

        [Header("Animation")]
        [SerializeField] private float popDuration = 0.25f;
        [SerializeField] private AnimationCurve popScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Guide Sprite")]
        [SerializeField] private Image guideImage;
        [SerializeField] private Vector2 guideImageSize = new Vector2(80f, 80f);

        private Coroutine _typing;
        private Coroutine _pop;

        private RectTransform _dialogueTransform;

        private void Awake()
        {
            _dialogueTransform = panel != null ? panel : GetComponent<RectTransform>();
            if (_dialogueTransform == null) _dialogueTransform = gameObject.AddComponent<RectTransform>();

            if (applyTransformOnAwake)
            {
                _dialogueTransform.anchorMin = anchorMin;
                _dialogueTransform.anchorMax = anchorMax;
                _dialogueTransform.pivot = pivot;
                _dialogueTransform.anchoredPosition = anchoredPosition;
                _dialogueTransform.sizeDelta = size;
            }

            if (text == null)
            {
                text = GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (text == null)
            {
                var textGO = new GameObject("DialogueText", typeof(RectTransform));
                textGO.transform.SetParent(transform, false);
                text = textGO.AddComponent<TextMeshProUGUI>();
                text.raycastTarget = false;
                text.alignment = TextAlignmentOptions.Midline;
                text.fontSize = 26f;
                text.textWrappingMode = TextWrappingModes.Normal;
                var tr = text.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.sizeDelta = size;
            }

            EnsureGuideImage();

            gameObject.name = "TutorialDialogueUI";
            InstantHide();
        }

        private void EnsureGuideImage()
        {
            if (guideImage != null) return;

            var guideTf = transform.Find("GuideImage");
            if (guideTf != null)
            {
                guideImage = guideTf.GetComponent<Image>();
            }

            if (guideImage == null)
            {
                var guideGO = new GameObject("GuideImage", typeof(RectTransform));
                guideGO.transform.SetParent(_dialogueTransform, false);
                guideImage = guideGO.AddComponent<Image>();
                guideImage.preserveAspect = true;

                var guideRt = guideImage.rectTransform;
                guideRt.anchorMin = new Vector2(0f, 0.5f);
                guideRt.anchorMax = new Vector2(0f, 0.5f);
                guideRt.pivot = new Vector2(0f, 0.5f);
                guideRt.anchoredPosition = new Vector2(20f, 0f);
                guideRt.sizeDelta = guideImageSize;
            }
        }

        public void Show(string content, float charDelay, Sprite guideSprite = null)
        {
            Show(content, charDelay, DialogueAnchor.Left, guideSprite);
        }

        public void Show(string content, float charDelay, DialogueAnchor anchor, Sprite guideSprite = null)
        {
            StopAnimations();
            if (text != null) text.text = string.Empty;
            gameObject.SetActive(true);
            _dialogueTransform.localScale = Vector3.zero;

            ApplyAnchor(anchor);
            ApplyGuideSprite(guideSprite);

            _pop = StartCoroutine(AnimateShow(content ?? string.Empty, charDelay > 0f ? charDelay : defaultCharDelay));
        }

        private void ApplyAnchor(DialogueAnchor anchor)
        {
            var preset = anchor == DialogueAnchor.Left ? leftPreset : rightPreset;
            if (preset == null || _dialogueTransform == null) return;

            _dialogueTransform.anchorMin = preset.anchorMin;
            _dialogueTransform.anchorMax = preset.anchorMax;
            _dialogueTransform.pivot = preset.pivot;
            _dialogueTransform.anchoredPosition = preset.anchoredPosition;
        }

        private void ApplyGuideSprite(Sprite guideSprite)
        {
            if (guideImage == null) EnsureGuideImage();
            if (guideImage == null) return;

            guideImage.sprite = guideSprite;
            guideImage.gameObject.SetActive(guideSprite != null);
        }

        private IEnumerator AnimateShow(string content, float delay)
        {
            yield return Pop(true);
            _pop = null;
            _typing = StartCoroutine(Typewriter(content, delay));
        }

        public void Hide()
        {
            StopAnimations();
            if (gameObject.activeInHierarchy)
            {
                _pop = StartCoroutine(PopOutAndDeactivate());
            }
            else
            {
                InstantHide();
            }
        }

        private void InstantHide()
        {
            if (_typing != null) StopCoroutine(_typing);
            _typing = null;
            _pop = null;
            if (text != null) text.text = string.Empty;
            ApplyGuideSprite(null);
            _dialogueTransform.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }

        private void StopAnimations()
        {
            if (_typing != null) StopCoroutine(_typing);
            _typing = null;
            if (_pop != null) StopCoroutine(_pop);
            _pop = null;
        }

        private IEnumerator Typewriter(string content, float delay)
        {
            if (text != null) text.text = string.Empty;
            for (int i = 0; i < content.Length; i++)
            {
                if (text != null) text.text = content.Substring(0, i + 1);
                yield return new WaitForSecondsRealtime(delay);
            }
            _typing = null;
        }

        private IEnumerator Pop(bool inDirection)
        {
            float elapsed = 0f;
            Vector3 from = inDirection ? Vector3.zero : _dialogueTransform.localScale;
            Vector3 to = inDirection ? Vector3.one : Vector3.zero;
            _dialogueTransform.localScale = from;

            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, popDuration));
                float scale = popScaleCurve != null && popScaleCurve.length > 0
                    ? popScaleCurve.Evaluate(t)
                    : Mathf.SmoothStep(0f, 1f, t);
                _dialogueTransform.localScale = Vector3.Lerp(from, to, scale);
                yield return null;
            }
            _dialogueTransform.localScale = to;
        }

        private IEnumerator PopOutAndDeactivate()
        {
            yield return Pop(false);
            InstantHide();
        }
    }
}
