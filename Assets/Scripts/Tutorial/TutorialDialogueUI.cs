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

        [Header("Anchor Presets")]
        [SerializeField] private AnchorPreset leftPreset = new AnchorPreset();
        [SerializeField] private AnchorPreset rightPreset = new AnchorPreset();

        [Tooltip("Top-centre. Use in battle when the sides would cover the deck bar or currency.")]
        [SerializeField] private AnchorPreset topPreset = new AnchorPreset
        {
            anchorMin = new Vector2(0.5f, 1f),
            anchorMax = new Vector2(0.5f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(0f, -160f)
        };

        [Tooltip("Screen centre. Clear of every HUD edge, at the cost of covering the board.")]
        [SerializeField] private AnchorPreset centerPreset = new AnchorPreset
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero
        };

        [Tooltip("Bottom-centre, lifted above the deck bar.")]
        [SerializeField] private AnchorPreset bottomPreset = new AnchorPreset
        {
            anchorMin = new Vector2(0.5f, 0f),
            anchorMax = new Vector2(0.5f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = new Vector2(0f, 260f)
        };

        [Tooltip("Right edge, vertically centred. Clears the deck bar and the top-corner currency.")]
        [SerializeField] private AnchorPreset middleRightPreset = new AnchorPreset
        {
            anchorMin = new Vector2(1f, 0.5f),
            anchorMax = new Vector2(1f, 0.5f),
            pivot = new Vector2(1f, 0.5f),
            anchoredPosition = new Vector2(-140f, 0f)
        };

        [Tooltip("Left edge, vertically centred.")]
        [SerializeField] private AnchorPreset middleLeftPreset = new AnchorPreset
        {
            anchorMin = new Vector2(0f, 0.5f),
            anchorMax = new Vector2(0f, 0.5f),
            pivot = new Vector2(0f, 0.5f),
            anchoredPosition = new Vector2(40f, 0f)
        };

        [Header("SFX")]
        [Tooltip("SFX Library key played when the dialogue box pops in. Leave empty to disable.")]
        [SerializeField] private string popupSfxKey = "ui_dialogue_popup";
        [Tooltip("SFX Library key looped while text is being typed. Starts and stops with the typewriter.")]
        [SerializeField] private string typingSfxKey = "ui_dialogue_typing";

        [Header("Input")]
        [Tooltip("Clicking (or pressing Space/Enter) while text is typing finishes the line instantly instead of making the player wait it out.")]
        [SerializeField] private bool clickToCompleteTyping = true;
        [Tooltip("Ignore clicks for this long after the box appears, so the same click that advanced the previous step does not skip this one's text too.")]
        [SerializeField] private float completeInputGrace = 0.15f;

        [Header("Animation")]
        [SerializeField] private float popDuration = 0.25f;
        [SerializeField] private AnimationCurve popScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Guide Sprite")]
        [SerializeField] private Image guideImage;
        [SerializeField] private Vector2 guideImageSize = new Vector2(80f, 80f);

        private Coroutine _typing;
        private Coroutine _pop;

        private RectTransform _dialogueTransform;

        // Full line for the current Show, so a skip can jump straight to it.
        private string _pendingContent;
        private float _shownAt;
        private bool _hiding;

        /// True while the pop-in or the typewriter is still running.
        public bool IsBusy => _typing != null || (_pop != null && !_hiding);

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

            _pendingContent = content ?? string.Empty;
            _shownAt = Time.unscaledTime;
            _hiding = false;

            _pop = StartCoroutine(AnimateShow(_pendingContent, charDelay > 0f ? charDelay : defaultCharDelay));
        }

        private void ApplyAnchor(DialogueAnchor anchor)
        {
            AnchorPreset preset;
            switch (anchor)
            {
                case DialogueAnchor.Left: preset = leftPreset; break;
                case DialogueAnchor.Right: preset = rightPreset; break;
                case DialogueAnchor.Top: preset = topPreset; break;
                case DialogueAnchor.Center: preset = centerPreset; break;
                case DialogueAnchor.Bottom: preset = bottomPreset; break;
                case DialogueAnchor.MiddleLeft: preset = middleLeftPreset; break;
                case DialogueAnchor.MiddleRight: preset = middleRightPreset; break;
                default: preset = leftPreset; break;
            }
            if (preset == null || _dialogueTransform == null) return;

            _dialogueTransform.anchorMin = preset.anchorMin;
            _dialogueTransform.anchorMax = preset.anchorMax;
            _dialogueTransform.pivot = preset.pivot;
            _dialogueTransform.anchoredPosition = preset.anchoredPosition;

            ClampInsideParent();
        }

        [Tooltip("Minimum gap kept between the dialogue box and the screen edge.")]
        [SerializeField] private float screenEdgeMargin = 24f;

        // A preset combined with the panel's own width can push the box past the screen edge, which
        // clips the text. Nudge it back inside rather than relying on hand-tuned offsets that only
        // hold for one panel size and resolution.
        private void ClampInsideParent()
        {
            if (_dialogueTransform == null) return;
            var parent = _dialogueTransform.parent as RectTransform;
            if (parent == null) return;

            Vector2 parentSize = parent.rect.size;
            if (parentSize.x <= 0f || parentSize.y <= 0f) return;

            Vector2 size = _dialogueTransform.rect.size;
            Vector2 pivot = _dialogueTransform.pivot;
            Vector2 anchor = _dialogueTransform.anchorMin;

            // Only single-point anchors have a meaningful anchoredPosition to clamp.
            if (anchor != _dialogueTransform.anchorMax) return;

            // Panel bounds expressed in the parent's local space.
            Vector2 anchorLocal = (anchor - parent.pivot) * parentSize;
            Vector2 pos = _dialogueTransform.anchoredPosition;
            Vector2 min = anchorLocal + pos - new Vector2(size.x * pivot.x, size.y * pivot.y);
            Vector2 max = min + size;

            Vector2 parentMin = -parent.pivot * parentSize + new Vector2(screenEdgeMargin, screenEdgeMargin);
            Vector2 parentMax = (Vector2.one - parent.pivot) * parentSize - new Vector2(screenEdgeMargin, screenEdgeMargin);

            Vector2 shift = Vector2.zero;
            if (size.x <= parentMax.x - parentMin.x)
            {
                if (min.x < parentMin.x) shift.x = parentMin.x - min.x;
                else if (max.x > parentMax.x) shift.x = parentMax.x - max.x;
            }
            if (size.y <= parentMax.y - parentMin.y)
            {
                if (min.y < parentMin.y) shift.y = parentMin.y - min.y;
                else if (max.y > parentMax.y) shift.y = parentMax.y - max.y;
            }

            if (shift != Vector2.zero)
                _dialogueTransform.anchoredPosition = pos + shift;
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
            if (!string.IsNullOrEmpty(popupSfxKey))
                TR.Audio.SFXManager.Instance?.Play(popupSfxKey);

            yield return Pop(true);
            _pop = null;
            _typing = StartCoroutine(Typewriter(content, delay));
        }

        /// Jumps straight to the finished line: skips the pop-in and the remaining typing.
        public void CompleteTyping()
        {
            if (_hiding) return;
            if (_typing == null && _pop == null) return;

            if (_typing != null) { StopCoroutine(_typing); _typing = null; }
            if (_pop != null) { StopCoroutine(_pop); _pop = null; }

            if (_dialogueTransform != null) _dialogueTransform.localScale = Vector3.one;
            if (text != null && _pendingContent != null) text.text = _pendingContent;
            StopTypingLoop();
        }

        private void Update()
        {
            if (!clickToCompleteTyping || _hiding) return;
            if (_typing == null && _pop == null) return;
            if (Time.unscaledTime - _shownAt < completeInputGrace) return;

            // Raw input rather than the EventSystem: the tutorial blocker swallows pointer events
            // everywhere except the target, and a skip has to work anywhere on screen.
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                CompleteTyping();
        }

        public void Hide()
        {
            _hiding = true;
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
            StopTypingLoop(0f);
            if (text != null) text.text = string.Empty;
            ApplyGuideSprite(null);
            _dialogueTransform.localScale = Vector3.zero;
            _pendingContent = null;
            _hiding = false;
            gameObject.SetActive(false);
        }

        private void StopAnimations()
        {
            if (_typing != null) StopCoroutine(_typing);
            _typing = null;
            if (_pop != null) StopCoroutine(_pop);
            _pop = null;
            // Stopping the coroutine skips its own cleanup, so the loop has to be cut here too.
            StopTypingLoop(0f);
        }

        private int _typingLoop = -1;

        private void StartTypingLoop(string content)
        {
            StopTypingLoop(0f);
            if (string.IsNullOrEmpty(typingSfxKey) || string.IsNullOrEmpty(content)) return;

            var sfx = TR.Audio.SFXManager.Instance;
            if (sfx == null) return;
            _typingLoop = sfx.PlayLoop(typingSfxKey, 1f);
        }

        private void StopTypingLoop(float fadeSeconds = 0.06f)
        {
            if (_typingLoop < 0) return;
            TR.Audio.SFXManager.Instance?.StopLoop(_typingLoop, fadeSeconds);
            _typingLoop = -1;
        }

        private void OnDisable()
        {
            // Never leave the loop running if the dialogue is switched off mid-type.
            StopTypingLoop(0f);
        }

        private IEnumerator Typewriter(string content, float delay)
        {
            if (text != null) text.text = string.Empty;

            // DialogueTyping is a continuous ~2.3s loop, not a per-key click. Firing it per
            // character started a new 2.3s clip on every letter, which is why the sound ran on
            // after the text finished. Run it as a loop for exactly as long as text is appearing.
            StartTypingLoop(content);

            for (int i = 0; i < content.Length; i++)
            {
                if (text != null) text.text = content.Substring(0, i + 1);
                yield return new WaitForSecondsRealtime(delay);
            }

            StopTypingLoop();
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
