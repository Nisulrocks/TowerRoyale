using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Net;
using TR.UI;

namespace TR.Battle
{
    
    
    
    public class DuoNetStatsUI : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Vector2 topLeftOffset = new Vector2(16f, 16f);
        [SerializeField] private float fontSize = 20f;

        [Header("Toast")]
        [SerializeField] private string localWeakMessage = "Connection weak";
        [SerializeField] private string partnerWeakMessage = "Partner's connection weak";

        private DuoNetStats _stats;
        private GameObject _root;
        private GameObject _createdCanvas;
        private TextMeshProUGUI _localText;
        private TextMeshProUGUI _partnerText;

        private void Start()
        {
            if (!DuoRuntime.IsDuo)
            {
                enabled = false;
                return;
            }

            _stats = DuoNetStats.EnsureExists();
            _stats.OnLocalWeakChanged += HandleLocalWeak;
            _stats.OnPartnerWeakChanged += HandlePartnerWeak;

            BuildUI();
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.OnLocalWeakChanged -= HandleLocalWeak;
                _stats.OnPartnerWeakChanged -= HandlePartnerWeak;
            }
            
            if (_root != null) Destroy(_root);
            if (_createdCanvas != null) Destroy(_createdCanvas);
        }

        private void Update()
        {
            if (_stats == null) return;

            int local = _stats.LocalPing;
            _localText.text = $"You: {local} ms";
            _localText.color = DuoNetStats.QualityColor(local);

            if (_stats.HasPartner)
            {
                if (!_partnerText.gameObject.activeSelf) _partnerText.gameObject.SetActive(true);
                int partner = _stats.PartnerPing;
                _partnerText.text = $"Partner: {partner} ms";
                _partnerText.color = DuoNetStats.QualityColor(partner);
            }
            else if (_partnerText.gameObject.activeSelf)
            {
                _partnerText.gameObject.SetActive(false);
            }
        }

        private void HandleLocalWeak(bool weak)
        {
            if (weak) BattleToast.Show(localWeakMessage, 2f);
        }

        private void HandlePartnerWeak(bool weak)
        {
            if (weak) BattleToast.Show(partnerWeakMessage, 2f);
        }

        private void BuildUI()
        {
            var canvas = ResolveCanvas();

            _root = new GameObject("DuoNetStats", typeof(RectTransform));
            _root.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)_root.transform;
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0f, 1f);
            rootRt.anchoredPosition = new Vector2(topLeftOffset.x, -topLeftOffset.y);
            rootRt.sizeDelta = new Vector2(240f, 60f);

            var vlg = _root.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2f;

            _localText = MakeLine("NetStat_You");
            _partnerText = MakeLine("NetStat_Partner");
        }

        private TextMeshProUGUI MakeLine(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.color = Color.white;
            t.fontStyle = FontStyles.Bold;
            
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private Canvas ResolveCanvas()
        {
            if (targetCanvas != null) return targetCanvas;
            var existing = FindFirstObjectByType<Canvas>();
            if (existing != null) return existing.rootCanvas != null ? existing.rootCanvas : existing;

            var go = new GameObject("DuoNetStatsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _createdCanvas = go;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4000;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }
    }
}
