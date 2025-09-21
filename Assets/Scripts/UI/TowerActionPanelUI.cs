using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Battle;

namespace TR.UI
{
    // Simple panel that appears when a tower is selected. Shows basic info and a Destroy button with refund.
    public class TowerActionPanelUI : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject root; // enable/disable to show/hide

        [Header("Texts")] 
        [SerializeField] private TMP_Text refundText; // shows exact refund amount

        [Header("Actions")] 
        [SerializeField] private Button destroyButton;
        [SerializeField] private Button confirmButton; // hidden by default; shown when confirming

        [Header("Settings")]
        [Range(0f,1f)] [SerializeField] private float refundPercent = 0.6f; // 60% default
        [SerializeField] private bool requireConfirm = false;
        [SerializeField] private string confirmMessage = "Destroy this tower for a {0}% refund?";
        [SerializeField] private bool showDetails = false; // if true, also show details via optional fields below

        [Header("Optional Details (only if showDetails)")]
        [SerializeField] private TMP_Text nameTextOptional;
        [SerializeField] private TMP_Text levelTextOptional;
        [SerializeField] private TMP_Text costTextOptional;

        private TowerBase _current;
        private bool _awaitingConfirm = false;

        private void Awake()
        {
            if (root == null) root = gameObject;
            if (destroyButton != null)
            {
                destroyButton.onClick.AddListener(OnClickDestroy);
            }
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnClickConfirm);
                confirmButton.gameObject.SetActive(false);
            }
            SetVisible(false);
        }

        private void OnEnable()
        {
            TowerSelectable.OnTowerSelectionChanged += HandleSelectionChanged;
        }

        private void OnDisable()
        {
            TowerSelectable.OnTowerSelectionChanged -= HandleSelectionChanged;
            _current = null;
            SetVisible(false);
        }

        private void HandleSelectionChanged(TowerBase tower, bool selected)
        {
            if (selected)
            {
                _current = tower;
                RefreshInfo();
                SetVisible(true);
                _awaitingConfirm = false;
                if (confirmButton != null) confirmButton.gameObject.SetActive(false);
            }
            else
            {
                if (_current == tower)
                {
                    _current = null;
                    SetVisible(false);
                }
            }
        }

        private void RefreshInfo()
        {
            if (_current == null)
            {
                SetVisible(false);
                return;
            }
            int placementCost = _current.GetPlacementCost();
            int refundAmount = Mathf.RoundToInt(placementCost * Mathf.Clamp01(refundPercent));
            if (refundText) refundText.text = $"Refund: {refundAmount}";

            if (showDetails)
            {
                var def = _current.Definition;
                if (nameTextOptional) nameTextOptional.text = def != null ? def.DisplayName : "Tower";
                if (levelTextOptional) levelTextOptional.text = $"Lv {_current.Level}";
                if (costTextOptional) costTextOptional.text = $"Cost: {placementCost}";
            }
        }

        private void SetVisible(bool show)
        {
            if (root != null) root.SetActive(show);
            if (!show)
            {
                _awaitingConfirm = false;
                if (confirmButton != null) confirmButton.gameObject.SetActive(false);
            }
        }

        private void OnClickDestroy()
        {
            if (_current == null) return;
            float pct = Mathf.Clamp01(refundPercent);
            if (requireConfirm)
            {
                // Toggle confirmation state
                _awaitingConfirm = !_awaitingConfirm;
                if (_awaitingConfirm)
                {
                    // Show confirm prompt and button
                    if (refundText) refundText.text = string.Format(confirmMessage, Mathf.RoundToInt(pct * 100f));
                    if (confirmButton != null) confirmButton.gameObject.SetActive(true);
                }
                else
                {
                    // Cancel confirmation, restore refund info
                    RefreshInfo();
                    if (confirmButton != null) confirmButton.gameObject.SetActive(false);
                }
                return;
            }

            // No confirm path: destroy immediately
            var tower = _current;
            _current = null;
            SetVisible(false);
            tower.DestroyForRefund(pct);
        }

        private void OnClickConfirm()
        {
            if (_current == null) return;
            float pct = Mathf.Clamp01(refundPercent);
            var tower = _current;
            _current = null;
            _awaitingConfirm = false;
            if (confirmButton != null) confirmButton.gameObject.SetActive(false);
            SetVisible(false);
            tower.DestroyForRefund(pct);
        }
    }
}
