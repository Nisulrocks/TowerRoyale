using UnityEngine;

namespace TR.Battle
{
    // Lives on a placed tower to remember which snap point it occupies.
    // Automatically frees the snap point in the placement controller when the tower is destroyed.
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
