using UnityEngine;

namespace TR.Battle
{
    
    
    public class TowerSnapBinding : MonoBehaviour
    {
        private Transform _snap;
        private TowerPlacementController _controller;
        private int _snapIndex = -1;
        private bool _isMirror;

        public int SnapIndex => _snapIndex;

        public void Bind(Transform snap, TowerPlacementController controller, int snapIndex = -1, bool isMirror = false)
        {
            _snap = snap;
            _controller = controller;
            _snapIndex = snapIndex;
            _isMirror = isMirror;
        }

        public void SetMirror(bool isMirror)
        {
            _isMirror = isMirror;
        }

        private void OnDestroy()
        {
            if (_controller != null && _snap != null)
            {
                _controller.NotifyTowerDestroyed(_snap, _snapIndex, _isMirror);
            }
        }
    }
}
