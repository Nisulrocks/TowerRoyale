using UnityEngine;
using UnityEngine.EventSystems;

namespace TR.Tutorial
{
    // Lightweight drag detector for UI elements
    public class TutorialDragListener : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public bool Dragged { get; private set; }
        [Tooltip("Pixels of cursor movement required before considering it a drag.")]
        public float minPixels = 30f;
        [Tooltip("If true, only mark as dragged once the pointer has moved outside this element's rect.")]
        public bool requireExitRect = false;

        private Vector2 _startScreenPos;
        private RectTransform _rt;

        public void ResetFlag()
        {
            Dragged = false;
            _startScreenPos = Vector2.zero;
            if (_rt == null) _rt = GetComponent<RectTransform>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Dragged = false;
            _startScreenPos = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Dragged) return;
            float dist = Vector2.Distance(_startScreenPos, eventData.position);
            if (dist < Mathf.Max(1f, minPixels)) return;
            if (requireExitRect && _rt != null)
            {
                Vector2 lp;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, eventData.position, eventData.pressEventCamera, out lp))
                {
                    if (_rt.rect.Contains(lp)) return; // still inside, keep waiting
                }
            }
            Dragged = true;
        }

        public void OnEndDrag(PointerEventData eventData) { }
    }
}
