using UnityEngine;
using UnityEngine.EventSystems;

namespace TR.Tutorial
{
    
    public class TutorialDragListener : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public bool Dragged { get; private set; }

        public float minPixels = 30f;

        public bool requireExitRect = false;

        private Vector2 _startScreenPos;
        private RectTransform _rt;

        // Only a gesture that BEGINS after arming counts. Dragged is raised mid-gesture, as soon as
        // the pointer passes the threshold, so when a step re-arms a listener the player's finger is
        // usually still down on the same card. Without this the in-flight drag immediately satisfies
        // the next wait as well — and since ResetFlag clears _startScreenPos to zero, it measured
        // from the screen corner and tripped no matter what the threshold was.
        private bool _armed;

        public void ResetFlag()
        {
            Dragged = false;
            _armed = false;
            _startScreenPos = Vector2.zero;
            if (_rt == null) _rt = GetComponent<RectTransform>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Dragged = false;
            _armed = true;
            _startScreenPos = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_armed || Dragged) return;
            float dist = Vector2.Distance(_startScreenPos, eventData.position);
            if (dist < Mathf.Max(1f, minPixels)) return;
            if (requireExitRect && _rt != null)
            {
                Vector2 lp;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, eventData.position, eventData.pressEventCamera, out lp))
                {
                    if (_rt.rect.Contains(lp)) return; 
                }
            }
            Dragged = true;
        }

        public void OnEndDrag(PointerEventData eventData) { }
    }
}
