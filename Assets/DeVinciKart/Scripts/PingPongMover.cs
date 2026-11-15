using UnityEngine;

namespace DeVinciKart
{
    /// <summary>
    /// Obstacle mobile : va-et-vient sinusoïdal autour de sa position de départ (repère du parent).
    /// Avec un Rigidbody cinématique, le kart le percute proprement (barrières coulissantes, camion qui manœuvre…).
    /// </summary>
    public class PingPongMover : MonoBehaviour
    {
        public Vector3 localOffset = new Vector3(3f, 0f, 0f);
        public float period = 3f;
        [Range(0f, 1f)] public float phase;
        Vector3 basePos;
        Rigidbody rb;

        void Start()
        {
            basePos = transform.localPosition;
            rb = GetComponent<Rigidbody>();
            if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate; }
        }

        void FixedUpdate()
        {
            float s = Mathf.Sin((Time.time / Mathf.Max(0.1f, period) + phase) * 2f * Mathf.PI);
            Vector3 lp = basePos + localOffset * s;
            if (rb) rb.MovePosition(transform.parent ? transform.parent.TransformPoint(lp) : lp);
            else transform.localPosition = lp;
        }
    }
}
