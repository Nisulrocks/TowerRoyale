using UnityEngine;

namespace TR.Battle
{
    
    
    public class TowerSnapBinding : MonoBehaviour
    {
        private TowerPlacementController _controller;
        private int _placementId = -1;
        private bool _isMirror;

        public int PlacementId => _placementId;

        public void Bind(TowerPlacementController controller, int placementId = -1, bool isMirror = false)
        {
            _controller = controller;
            _placementId = placementId;
            _isMirror = isMirror;
        }

        public void SetMirror(bool isMirror)
        {
            _isMirror = isMirror;
        }

        public void Unbind()
        {
            _controller = null;
        }

        private void OnDestroy()
        {
            if (_controller != null && _placementId >= 0)
            {
                _controller.NotifyTowerDestroyed(_placementId, _isMirror);
            }
        }
    }
}
