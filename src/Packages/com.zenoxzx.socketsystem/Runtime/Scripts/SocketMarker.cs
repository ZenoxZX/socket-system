using UnityEngine;

namespace ZenoxZX.SocketSystem
{
    [DisallowMultipleComponent]
    public class SocketMarker : MonoBehaviour
    {
        private const float k_SphereRadius = 0.03f;
        private const float k_AxisLength = 0.1f;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Transform t = transform;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(t.position, k_SphereRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(t.position, t.position + t.right * k_AxisLength);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(t.position, t.position + t.up * k_AxisLength);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(t.position, t.position + t.forward * k_AxisLength);

            UnityEditor.Handles.Label(t.position + Vector3.up * (k_SphereRadius + 0.02f), t.name);
        }
#endif
    }
}
