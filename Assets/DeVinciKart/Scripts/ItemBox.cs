using UnityEngine;

namespace DeVinciKart
{
    /// <summary>Boîte à objets : donne un Champignon (boost) ou une Plume (saut), réapparaît après quelques secondes.</summary>
    [RequireComponent(typeof(Collider))]
    public class ItemBox : MonoBehaviour
    {
        public float respawnTime = 3f;
        Renderer[] renderers;
        Collider col;
        float hiddenUntil;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            col = GetComponent<Collider>();
        }

        void Update()
        {
            if (hiddenUntil > 0f && Time.time > hiddenUntil)
            {
                SetVisible(true);
                hiddenUntil = 0f;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (hiddenUntil > 0f) return;
            var kart = other.GetComponentInParent<KartController>();
            if (!kart) return;
            if (kart.heldItem == KartItem.Aucun)
                kart.heldItem = Random.value < 0.55f ? KartItem.Champignon : KartItem.Plume;
            SetVisible(false);
            hiddenUntil = Time.time + respawnTime;
        }

        void SetVisible(bool visible)
        {
            foreach (var r in renderers) r.enabled = visible;
            col.enabled = visible;
        }
    }
}
