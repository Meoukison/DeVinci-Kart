using UnityEngine;

namespace DeVinciKart
{
    /// <summary>Ligne centrale de la piste (pour l'IA / la minimap plus tard). Dessinée en jaune dans la vue Scène.</summary>
    public class TrackPath : MonoBehaviour
    {
        public Vector3[] points;
        public Vector3[] forwards;
        public float length;

        public int Nearest(Vector3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                float d = (points[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void OnDrawGizmos()
        {
            if (points == null || points.Length < 2) return;
            Gizmos.color = Color.yellow;
            for (int i = 1; i < points.Length; i++)
                Gizmos.DrawLine(points[i - 1] + Vector3.up * 0.5f, points[i] + Vector3.up * 0.5f);
        }
    }
}
