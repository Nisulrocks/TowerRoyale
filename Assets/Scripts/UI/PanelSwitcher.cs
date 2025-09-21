using UnityEngine;
using UnityEngine.UI;

namespace TR.UI
{
    // Tiny utility to switch between panels in the Lobby.
    public class PanelSwitcher : MonoBehaviour
    {
        [System.Serializable]
        public class Panel
        {
            public string name;
            public GameObject root;
            public Button tabButton; // optional
        }

        [SerializeField] private Panel[] panels;
        private int _activeIndex = -1;

        private void Awake()
        {
            // Wire tab buttons
            for (int i = 0; i < panels.Length; i++)
            {
                int idx = i;
                if (panels[i].tabButton != null)
                {
                    panels[i].tabButton.onClick.AddListener(() => Show(idx));
                }
            }
            if (panels.Length > 0) Show(0);
        }

        public void Show(int index)
        {
            if (panels == null || panels.Length == 0) return;
            index = Mathf.Clamp(index, 0, panels.Length - 1);
            _activeIndex = index;
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i].root)
                    panels[i].root.SetActive(i == index);
            }
        }

        public void ShowByName(string panelName)
        {
            if (string.IsNullOrEmpty(panelName) || panels == null) return;
            int idx = GetIndexByName(panelName);
            if (idx >= 0) Show(idx);
        }

        public int GetIndexByName(string panelName)
        {
            if (panels == null) return -1;
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null && panels[i].name == panelName) return i;
            }
            return -1;
        }
    }
}
