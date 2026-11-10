using UnityEngine;

namespace DeVinciKart
{
    /// <summary>Fait défiler la texture (eau de la rivière).</summary>
    [RequireComponent(typeof(Renderer))]
    public class UVScroller : MonoBehaviour
    {
        public Vector2 speed = new Vector2(0.01f, 0.03f);
        Material mat;

        void Start() => mat = GetComponent<Renderer>().material;

        void Update()
        {
            if (mat) mat.SetTextureOffset("_BaseMap", speed * Time.time);
        }
    }
}
