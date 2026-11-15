using KartLab;
using UnityEngine;

namespace DeVinciKart
{
    /// <summary>
    /// Obstacle « dégommable » (plot, barrière de chantier) : quand le kart le touche, il s'envole,
    /// le kart perd un peu de vitesse, puis l'obstacle revient à sa place au bout de quelques secondes.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Knockable : MonoBehaviour
    {
        [Range(0.3f, 1f)] public float slowFactor = 0.8f;
        public float respawnSeconds = 7f;
        Vector3 homePos; Quaternion homeRot;
        Rigidbody rb; BoxCollider trigger; SphereCollider flight;
        bool down; float upAt;

        void Start()
        {
            homePos = transform.position; homeRot = transform.rotation;
            trigger = GetComponent<BoxCollider>(); trigger.isTrigger = true;
            rb = GetComponent<Rigidbody>();
            if (!rb) rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 4f; rb.isKinematic = true; rb.useGravity = true;
            flight = gameObject.AddComponent<SphereCollider>();
            flight.radius = Mathf.Max(0.2f, Mathf.Min(trigger.size.x, trigger.size.z) * 0.3f);
            flight.center = trigger.center;
            flight.enabled = false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (down) return;
            var motor = other.GetComponentInParent<KartMotor>();
            if (!motor || !motor.Body) return;
            down = true; upAt = Time.time + respawnSeconds;
            Vector3 v = motor.Body.linearVelocity;
            motor.Body.linearVelocity = new Vector3(v.x * slowFactor, v.y, v.z * slowFactor);
            motor.Notify("PLOT DÉGOMMÉ !");
            foreach (var c in motor.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(flight, c, true);
            trigger.enabled = false; flight.enabled = true;
            rb.isKinematic = false;
            Vector3 dir = new Vector3(v.x, 0f, v.z);
            dir = dir.sqrMagnitude > 0.1f ? dir.normalized : transform.forward;
            float speed = Mathf.Clamp(v.magnitude, 6f, 25f);
            rb.AddForce((dir * speed * 0.6f + Vector3.up * (3f + speed * 0.25f)) * rb.mass, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 10f * rb.mass, ForceMode.Impulse);
        }

        void Update()
        {
            if (!down || Time.time < upAt) return;
            down = false;
            rb.isKinematic = true;
            flight.enabled = false;
            transform.SetPositionAndRotation(homePos, homeRot);
            trigger.enabled = true;
        }
    }
}
