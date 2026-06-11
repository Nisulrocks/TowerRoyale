using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.Tutorial
{
    
    public class TutorialNameInputUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text errorLabel;

        private Action<string> _onConfirmed;
        private bool _initialized;

        private void Awake()
        {
            EnsureWired();
            Hide();
        }

        private void EnsureWired()
        {
            if (_initialized) return;
            _initialized = true;

            if (inputField != null)
            {
                inputField.characterLimit = PlayerProfile.PlayerNameMaxLength;
                inputField.onValueChanged.AddListener(OnValueChanged);
                inputField.onSubmit.AddListener(OnSubmit);
            }
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnClickConfirm);
            }
        }

        
        public void Show(string prompt, string placeholder, Action<string> onConfirmed)
        {
            EnsureWired();
            _onConfirmed = onConfirmed;
            gameObject.SetActive(true);
            if (panel != null) panel.gameObject.SetActive(true);

            if (promptLabel != null && !string.IsNullOrEmpty(prompt)) promptLabel.text = prompt;

            if (inputField != null)
            {
                
                inputField.text = PlayerProfile.HasPlayerName() ? PlayerProfile.GetPlayerName() : string.Empty;
                if (inputField.placeholder is TMP_Text ph && !string.IsNullOrEmpty(placeholder))
                {
                    ph.text = placeholder;
                }
                inputField.Select();
                inputField.ActivateInputField();
            }
            if (errorLabel != null) errorLabel.text = string.Empty;
            UpdateConfirmInteractable();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnValueChanged(string _)
        {
            if (errorLabel != null) errorLabel.text = string.Empty;
            UpdateConfirmInteractable();
        }

        private void UpdateConfirmInteractable()
        {
            if (confirmButton == null) return;
            confirmButton.interactable = inputField != null && PlayerProfile.IsValidPlayerName(inputField.text);
        }

        private void OnSubmit(string _)
        {
            OnClickConfirm();
        }

        private void OnClickConfirm()
        {
            string raw = inputField != null ? inputField.text : string.Empty;
            if (!PlayerProfile.IsValidPlayerName(raw))
            {
                if (errorLabel != null)
                    errorLabel.text = $"Name must be {PlayerProfile.PlayerNameMinLength}-{PlayerProfile.PlayerNameMaxLength} characters.";
                return;
            }
            string clean = PlayerProfile.SanitizePlayerName(raw);
            PlayerProfile.SetPlayerName(clean);
            var cb = _onConfirmed;
            _onConfirmed = null;
            Hide();
            cb?.Invoke(clean);
        }
    }
}
