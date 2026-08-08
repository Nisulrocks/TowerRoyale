using UnityEngine;
using UnityEngine.UI;

namespace TR.UI
{
    
    public class PanelSwitcher : MonoBehaviour
    {
        [System.Serializable]
        public class Panel
        {
            public string name;
            public GameObject root;
            public Button tabButton; 
        }

        [SerializeField] private Panel[] panels;
        private int _activeIndex = -1;

        private static string _returnPanelName;
        private static bool _skipSave;

        private void Awake()
        {
            
            for (int i = 0; i < panels.Length; i++)
            {
                int idx = i;
                if (panels[i].tabButton != null)
                {
                    panels[i].tabButton.onClick.AddListener(() => Show(idx));
                }
            }

            if (!string.IsNullOrEmpty(_returnPanelName))
            {
                string saved = _returnPanelName;
                _returnPanelName = null;
                int savedIdx = GetIndexByName(saved);
                if (savedIdx >= 0)
                {
                    Show(savedIdx);
                    return;
                }
            }

            if (panels.Length > 0) Show(0);
        }

        private void OnDestroy()
        {
            if (_skipSave)
            {
                _skipSave = false;
                return;
            }
            if (panels != null && _activeIndex >= 0 && _activeIndex < panels.Length)
            {
                var panel = panels[_activeIndex];
                if (panel != null && !string.IsNullOrEmpty(panel.name))
                    _returnPanelName = panel.name;
            }
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

        public static void ClearReturnPanel()
        {
            _returnPanelName = null;
            _skipSave = true;
        }
    }
}
