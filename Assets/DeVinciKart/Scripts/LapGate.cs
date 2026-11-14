using KartLab;
using UnityEngine;

namespace DeVinciKart
{
    /// <summary>Porte de passage (trigger) pour le comptage des tours. Ne compte que dans le sens de la course.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LapGate : MonoBehaviour
    {
        public int index;
        public LapRaceManager manager;

        void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<KartMotor>();
            if (!kart || !manager) return;
            var body = kart.Body;
            if (body && Vector3.Dot(body.linearVelocity, transform.forward) < 0.5f) return;   // marche arrière : ignoré
            manager.OnGate(index, kart);
        }
    }
}
