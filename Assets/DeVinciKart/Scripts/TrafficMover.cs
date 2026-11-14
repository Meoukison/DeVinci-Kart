using UnityEngine;

namespace DeVinciKart
{
    /// <summary>Fait circuler un véhicule en boucle entre deux points (repère local du parent). Utilisé pour le trafic du boulevard circulaire.</summary>
    public class TrafficMover : MonoBehaviour
    {
        public Vector3 localA, localB;
        public float speed = 15f;
        [Range(0f, 1f)] public float phase;
        float t;

        void Start() { t = phase; }

        void Update()
        {
            float len = Vector3.Distance(localA, localB);
            if (len < 0.01f) return;
            t += speed * Time.deltaTime / len;
            if (t > 1f) t -= 1f;
            transform.localPosition = Vector3.Lerp(localA, localB, t);
        }
    }
}
