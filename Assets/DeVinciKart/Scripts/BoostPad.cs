using UnityEngine;

namespace DeVinciKart
{
    /// <summary>Plaque d'accélération.</summary>
    [RequireComponent(typeof(Collider))]
    public class BoostPad : MonoBehaviour
    {
        public float duration = 1.1f;

        void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<KartController>();
            if (kart) kart.Boost(duration);
        }
    }
}
