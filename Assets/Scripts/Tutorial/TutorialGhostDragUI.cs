using UnityEngine;
using UnityEngine.UI;

namespace TR.Tutorial
{
    public class TutorialGhostDragUI : MonoBehaviour
    {
        [SerializeField] private Image ghostImage;

        [Header("Timing")]
        [SerializeField] private float travelSeconds = 1.1f;
        [SerializeField] private float holdAtEndSeconds = 0.35f;
        [SerializeField] private float restartDelaySeconds = 0.45f;

        [Header("Look")]
        [SerializeField] private float ghostSize = 96f;
        [SerializeField] private Color ghostTint = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private float arcHeight = 40f;

        private RectTransform _rect;
        private Canvas _canvas;

        public RectTransform Rect => _rect;
        private bool _playing;
        private float _t;
        private float _phaseTimer;
        private Vector2 _from;
        private Vector2 _to;

        private enum Phase { Travel, Hold, Restart }
        private Phase _phase;

        public static TutorialGhostDragUI Create(Transform parent)
        {
            var go = new GameObject("TutorialGhostDrag", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var ghost = go.AddComponent<TutorialGhostDragUI>();
            ghost._rect = (RectTransform)go.transform;
            ghost._canvas = go.GetComponentInParent<Canvas>();

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;   
            ghost.ghostImage = img;

            ghost._rect.sizeDelta = new Vector2(ghost.ghostSize, ghost.ghostSize);
            go.SetActive(false);
            return ghost;
        }

        public void Play(Vector2 fromScreen, Vector2 toScreen, Sprite sprite)
        {
            _from = fromScreen;
            _to = toScreen;

            if (ghostImage != null)
            {
                ghostImage.sprite = sprite;
                ghostImage.color = ghostTint;
                ghostImage.enabled = sprite != null;
                ghostImage.preserveAspect = true;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _playing = true;
            _phase = Phase.Travel;
            _t = 0f;
            _phaseTimer = 0f;
            Apply(0f, 1f);
        }

        public void UpdateEndpoints(Vector2 fromScreen, Vector2 toScreen)
        {
            _from = fromScreen;
            _to = toScreen;
        }

        public void StopAndHide()
        {
            _playing = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_playing) return;

            switch (_phase)
            {
                case Phase.Travel:
                    _t += Time.unscaledDeltaTime / Mathf.Max(0.05f, travelSeconds);
                    if (_t >= 1f)
                    {
                        _t = 1f;
                        _phase = Phase.Hold;
                        _phaseTimer = 0f;
                    }
                    Apply(Mathf.SmoothStep(0f, 1f, _t), 1f);
                    break;

                case Phase.Hold:
                    _phaseTimer += Time.unscaledDeltaTime;
                    Apply(1f, Mathf.Clamp01(1f - _phaseTimer / Mathf.Max(0.05f, holdAtEndSeconds)));
                    if (_phaseTimer >= holdAtEndSeconds)
                    {
                        _phase = Phase.Restart;
                        _phaseTimer = 0f;
                    }
                    break;

                case Phase.Restart:
                    _phaseTimer += Time.unscaledDeltaTime;
                    Apply(1f, 0f);
                    if (_phaseTimer >= restartDelaySeconds)
                    {
                        _phase = Phase.Travel;
                        _t = 0f;
                    }
                    break;
            }
        }

        private void Apply(float progress, float alpha)
        {
            if (_rect == null) return;

            Vector2 screen = Vector2.Lerp(_from, _to, progress);
            screen.y += Mathf.Sin(progress * Mathf.PI) * arcHeight;

            var parent = _rect.parent as RectTransform;
            if (parent != null)
            {
                Camera cam = null;
                if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    cam = _canvas.worldCamera;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out Vector2 local))
                    _rect.anchoredPosition = local;
            }

            if (ghostImage != null)
            {
                var c = ghostTint;
                c.a *= alpha;
                ghostImage.color = c;
            }
        }
    }
}
