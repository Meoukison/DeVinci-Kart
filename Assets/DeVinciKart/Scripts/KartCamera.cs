using UnityEngine;

namespace DeVinciKart
{
    /// <summary>Caméra 3e personne pour tester la piste sur écran (en VR, utilisez plutôt kart.cameraAnchor).</summary>
    public class KartCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 6.5f;
        public float height = 2.6f;
        public float lookHeight = 1.2f;
        public float followSharpness = 6f;

        void LateUpdate()
        {
            if (!target) return;
            Vector3 fwd = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 desired = target.position - fwd * distance + Vector3.up * height;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(target.position + Vector3.up * lookHeight - transform.position, Vector3.up);
        }
    }
}
