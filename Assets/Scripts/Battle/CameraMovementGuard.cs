using UnityEngine;
using TR.Systems;

namespace TR.Battle
{
    // Attach this to the same GameObject as your camera movement script.
    // It will enable/disable the movement script whenever a placement drag starts/ends.
    public class CameraMovementGuard : MonoBehaviour
    {
        [SerializeField] private Behaviour cameraMovementScript; // any component to toggle (e.g., CinemachineInputProvider or custom script)

        private void Awake()
        {
            if (cameraMovementScript == null)
            {
                cameraMovementScript = GetComponent<Behaviour>();
            }
        }

        private void OnEnable()
        {
            InputLocks.OnPlacementDraggingChanged += HandlePlacementDraggingChanged;
            HandlePlacementDraggingChanged(InputLocks.IsPlacementDragging);
        }

        private void OnDisable()
        {
            InputLocks.OnPlacementDraggingChanged -= HandlePlacementDraggingChanged;
        }

        private void HandlePlacementDraggingChanged(bool dragging)
        {
            if (cameraMovementScript != null)
            {
                cameraMovementScript.enabled = !dragging;
            }
        }
    }
}
