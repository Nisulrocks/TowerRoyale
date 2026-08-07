using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TR.UI
{
    public class DeckPresetNodeUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;
        [SerializeField] private Button button;
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.6f, 1f, 1f);
        [SerializeField] private Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        private int _index;
        private System.Action<int> _onClick;
        private Color _baseColor;

        public void Bind(int index, bool selected, System.Action<int> onClick)
        {
            _index = index;
            _onClick = onClick;
            if (label) label.text = (index + 1).ToString();
            SetSelected(selected);
            if (button)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke(_index));
            }
        }

        public void SetSelected(bool selected)
        {
            if (background)
            {
                if (_baseColor == default) _baseColor = background.color;
                background.color = selected ? selectedColor : (_baseColor != default ? _baseColor : unselectedColor);
            }
        }
    }
}
