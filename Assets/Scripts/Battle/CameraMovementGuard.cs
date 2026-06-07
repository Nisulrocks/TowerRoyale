using UnityEngine;
using TR.Systems;

namespace TR.Battle
{
    
    
    public class CameraMovementGuard : MonoBehaviour
    {
        [SerializeField] private Behaviour cameraMovementScript; 

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
