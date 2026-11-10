using UnityEngine;

namespace DeVinciKart
{
    /// <summary>Ligne de passage obligatoire. La dernière (isFinish) est la ligne d'arrivée.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Checkpoint : MonoBehaviour
    {
        public int index;
        public bool isFinish;

        void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<KartController>();
            if (kart && RaceManager.Instance) RaceManager.Instance.PassCheckpoint(this, kart);
        }
    }
}
