using UnityEngine;
using UnityEngine.UI;

namespace TR.UI
{
    /// Opens a PanelSwitcher panel by name. Lets a panel be reachable from somewhere other than the
    /// tab strip — the profile hangs off the name plate on the Play panel, because the tab row is full.
    [RequireComponent(typeof(Button))]
    public class OpenPanelButton : MonoBehaviour
    {
        [Tooltip("Panel name as registered in the PanelSwitcher.")]
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

            // The switcher lives above the panels, and this button sits inside one of them.
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
