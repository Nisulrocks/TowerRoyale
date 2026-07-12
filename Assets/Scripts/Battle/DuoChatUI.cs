using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using TR.Net;

namespace TR.Battle
{
    
    
    
    public class DuoChatUI : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Optional parent Canvas for the generated chat UI. If null, a Canvas is created automatically.")]
        [SerializeField] private Canvas targetCanvas;
        [Tooltip("Anchored size of the chat panel (width x height) in pixels.")]
        [SerializeField] private Vector2 panelSize = new Vector2(420f, 260f);
        [Tooltip("Offset from the bottom-left corner of the screen, in pixels.")]
        [SerializeField] private Vector2 bottomLeftOffset = new Vector2(20f, 20f);

        [Header("Behaviour")]
        [Tooltip("Max number of message lines kept in the log.")]
        [SerializeField] private int maxLines = 50;
        [Tooltip("Whether the chat log starts visible.")]
        [SerializeField] private bool startVisible = false;
        [Tooltip("The chat toggle button GameObject. Hidden automatically in single player.")]
        [SerializeField] private GameObject toggleButton;

        private DuoBattleCoordinator _coordinator;
        private readonly List<TMP_Text> _lines = new();

        
        private GameObject _panel;         
        private RectTransform _content;    
        private ScrollRect _scrollRect;
        private TMP_InputField _inputField;
        private bool _visible;

        private void Start()
        {
            if (!DuoRuntime.IsDuo)
            {
                
                if (toggleButton != null) toggleButton.SetActive(false);
                enabled = false;
                return;
            }

            
            if (toggleButton != null) toggleButton.SetActive(true);

            _coordinator = FindFirstObjectByType<DuoBattleCoordinator>(FindObjectsInactive.Include);
            if (_coordinator == null)
            {
                Debug.LogWarning("[DuoChatUI] No DuoBattleCoordinator found; chat disabled.");
                if (toggleButton != null) toggleButton.SetActive(false);
                enabled = false;
                return;
            }
            _coordinator.OnChatMessageReceived += OnChatMessageReceived;

            BuildUI();
            SetVisible(startVisible);
        }

        private void OnDestroy()
        {
            if (_coordinator != null) _coordinator.OnChatMessageReceived -= OnChatMessageReceived;
        }

        
        
        public void ToggleChat()
        {
            SetVisible(!_visible);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_panel != null) _panel.SetActive(visible);
            if (visible && _inputField != null)
            {
                _inputField.ActivateInputField();
                StartCoroutine(ScrollToBottomNextFrame());
            }
        }

        
        
        private void BuildUI()
        {
            var canvas = ResolveCanvas();

            
            _panel = new GameObject("DuoChatPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var panelRt = (RectTransform)_panel.transform;
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(0f, 0f);
            panelRt.pivot = new Vector2(0f, 0f);
            panelRt.sizeDelta = panelSize;
            panelRt.anchoredPosition = bottomLeftOffset;
            var panelImg = _panel.GetComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.55f);

            
            float inputHeight = 40f;
            var scrollGO = new GameObject("ChatScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
            scrollGO.transform.SetParent(_panel.transform, false);
            var scrollRt = (RectTransform)scrollGO.transform;
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(6f, inputHeight + 6f);
            scrollRt.offsetMax = new Vector2(-6f, -6f);
            scrollGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
            scrollGO.GetComponent<Mask>().showMaskGraphic = true;
            _scrollRect = scrollGO.GetComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 20f;
            _scrollRect.viewport = scrollRt;

            
            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(scrollGO.transform, false);
            _content = (RectTransform)contentGO.transform;
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 0f);
            var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(6, 6, 4, 4);
            var fitter = contentGO.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scrollRect.content = _content;

            
            var inputGO = new GameObject("ChatInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputGO.transform.SetParent(_panel.transform, false);
            var inputRt = (RectTransform)inputGO.transform;
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 0f);
            inputRt.pivot = new Vector2(0.5f, 0f);
            inputRt.sizeDelta = new Vector2(-12f, inputHeight);
            inputRt.anchoredPosition = new Vector2(0f, 6f);
            inputGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            
            var textAreaGO = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            textAreaGO.transform.SetParent(inputGO.transform, false);
            var textAreaRt = (RectTransform)textAreaGO.transform;
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(10f, 4f);
            textAreaRt.offsetMax = new Vector2(-10f, -4f);

            var placeholderGO = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var placeholder = placeholderGO.AddComponent<TextMeshProUGUI>();
            StretchFull((RectTransform)placeholderGO.transform);
            placeholder.text = "Press Enter to chat...";
            placeholder.fontSize = 20f;
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(textAreaGO.transform, false);
            var inputText = textGO.AddComponent<TextMeshProUGUI>();
            StretchFull((RectTransform)textGO.transform);
            inputText.fontSize = 20f;
            inputText.color = Color.white;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;

            _inputField = inputGO.GetComponent<TMP_InputField>();
            _inputField.textViewport = textAreaRt;
            _inputField.textComponent = inputText;
            _inputField.placeholder = placeholder;
            _inputField.characterLimit = 200;
            _inputField.lineType = TMP_InputField.LineType.SingleLine;
            _inputField.onSubmit.AddListener(_ => SendCurrentInput());
        }

        private Canvas ResolveCanvas()
        {
            if (targetCanvas != null) return targetCanvas;
            
            var existing = FindFirstObjectByType<Canvas>();
            if (existing != null) return existing.rootCanvas != null ? existing.rootCanvas : existing;

            
            var go = new GameObject("DuoChatCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        
        
        private void SendCurrentInput()
        {
            if (_inputField == null || _coordinator == null) return;
            string msg = _inputField.text;
            if (string.IsNullOrWhiteSpace(msg)) return;
            _coordinator.SendChat(msg.Trim());
            _inputField.text = string.Empty;
            _inputField.ActivateInputField();
        }

        private void OnChatMessageReceived(string sender, string message, bool isOwn)
        {
            AppendLine(sender, message, isOwn);
        }

        private void AppendLine(string sender, string message, bool isOwn)
        {
            if (_content == null) return;

            
            int actor = isOwn
                ? (PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 1)
                : GetPartnerActorNumber();
            Color col = DuoPlayerColors.GetColorForActor(actor);

            var go = new GameObject("ChatLine", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var line = go.AddComponent<TextMeshProUGUI>();
            line.fontSize = 20f;
            line.color = Color.white;
            line.richText = true;
            line.enableWordWrapping = true;
            line.alignment = TextAlignmentOptions.TopLeft;
            line.margin = Vector4.zero;
            line.overflowMode = TextOverflowModes.Overflow;

            string safeSender = string.IsNullOrEmpty(sender) ? (isOwn ? "You" : "Partner") : sender;
            string hex = DuoPlayerColors.ToHex(col);
            line.text = $"<b><color=#{hex}>{safeSender}:</color></b> {message}";

            _lines.Add(line);
            while (_lines.Count > maxLines)
            {
                var old = _lines[0];
                _lines.RemoveAt(0);
                if (old) Destroy(old.gameObject);
            }

            if (isActiveAndEnabled) StartCoroutine(ScrollToBottomNextFrame());
        }

        private static int GetPartnerActorNumber()
        {
            foreach (var p in PhotonNetwork.PlayerListOthers) return p.ActorNumber;
            return 2;
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
