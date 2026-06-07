using System;

namespace TR.Systems
{
    
    public static class InputLocks
    {
        private static bool _placementDragging;
        public static bool IsPlacementDragging => _placementDragging;
        public static event Action<bool> OnPlacementDraggingChanged;

        public static void SetPlacementDragging(bool value)
        {
            if (_placementDragging == value) return;
            _placementDragging = value;
            OnPlacementDraggingChanged?.Invoke(_placementDragging);
        }
    }
}
