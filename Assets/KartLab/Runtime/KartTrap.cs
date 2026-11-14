using UnityEngine;

namespace KartLab
{
    public class KartTrap : MonoBehaviour
    {
        float armedAt;
        bool consumed;
        void OnEnable() { armedAt = Time.time + .5f; Destroy(gameObject, 15); }
        void OnTriggerStay(Collider other)
        {
            if (consumed || Time.time < armedAt) return;
            var motor = other.GetComponentInParent<KartMotor>();
            if (!motor) return;
            consumed = true; motor.ApplyHit(.6f, 1.5f); Destroy(gameObject);
        }
    }
}
