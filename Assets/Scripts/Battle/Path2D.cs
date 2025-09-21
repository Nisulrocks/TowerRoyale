using UnityEngine;

namespace TR.Battle
{
    // Defines a simple waypoint path for 2D enemies to follow in order.
    public class Path2D : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        public Transform[] Waypoints => waypoints;

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                var wp = waypoints[i];
                if (wp == null) continue;
                Gizmos.DrawSphere(wp.position, 0.08f);
                if (i + 1 < waypoints.Length && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(wp.position, waypoints[i + 1].position);
                }
            }
        }
    }
}
