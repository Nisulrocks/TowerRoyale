using UnityEngine;

namespace TR.Battle
{
    
    
    public class TowerSnapBinding : MonoBehaviour
    {
        private Transform _snap;
        private TowerPlacementController _controller;

        public void Bind(Transform snap, TowerPlacementController controller)
        {
            _snap = snap;
            _controller = controller;
        }

        private void OnDestroy()
        {
            if (_controller != null && _snap != null)
            {
                _controller.FreeSnap(_snap);
            }
        }
    }
}
