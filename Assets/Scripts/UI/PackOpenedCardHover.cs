using UnityEngine;
using UnityEngine.EventSystems;

namespace TR.UI
{
    // Attach to each revealed card instance to notify the controller about hover events.
    public class PackOpenedCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private PackOpeningSceneController _controller;
        private int _index;

        public void Init(PackOpeningSceneController controller, int index)
        {
            _controller = controller;
            _index = index;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _controller?.StartHover(_index);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _controller?.EndHover(_index);
        }
    }
}
