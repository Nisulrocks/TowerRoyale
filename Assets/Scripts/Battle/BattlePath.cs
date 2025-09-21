using UnityEngine;

namespace TR.Battle
{
    // Holds a series of waypoints for enemies to follow in 2D.
    public class BattlePath : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;

        public int Count => waypoints != null ? waypoints.Length : 0;
        public Transform Get(int index) => (waypoints != null && index >= 0 && index < waypoints.Length) ? waypoints[index] : null;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.1f);
                if (i + 1 < waypoints.Length && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                }
            }
        }
#endif
    }
}
