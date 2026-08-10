using UnityEngine;
using UnityEngine.UI;

namespace TR.UI
{
    [RequireComponent(typeof(Button))]
    public class OpenPanelButton : MonoBehaviour
    {
        [SerializeField] private string panelName = "Profile";

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null) _button.onClick.AddListener(Open);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(Open);
        }

        private void Open()
        {
            if (string.IsNullOrEmpty(panelName)) return;

            var switcher = GetComponentInParent<PanelSwitcher>();
            if (switcher == null) switcher = FindFirstObjectByType<PanelSwitcher>(FindObjectsInactive.Include);

            if (switcher == null)
            {
                Debug.LogWarning("[OpenPanelButton] No PanelSwitcher found; cannot open '" + panelName + "'.");
                return;
            }

            if (switcher.GetIndexByName(panelName) < 0)
            {
                Debug.LogWarning($"[OpenPanelButton] PanelSwitcher has no panel named '{panelName}'. " +
                                 "Add it to the switcher's Panels list (a tab button is not required).");
                return;
            }

            switcher.ShowByName(panelName);
        }
    }
}
