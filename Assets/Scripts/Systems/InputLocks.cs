using System;

namespace TR.Systems
{
    // Global input lock flags. Use to temporarily disable systems like camera movement during placement drag.
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
