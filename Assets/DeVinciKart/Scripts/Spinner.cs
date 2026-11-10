using UnityEngine;

namespace DeVinciKart
{
    /// <summary>Rotation + flottement (boîtes à objets, Lakitu, plume...).</summary>
    public class Spinner : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0f, 90f, 0f);
        public bool worldSpace = true;
        public float bobAmplitude;
        public float bobFrequency = 0.5f;
        Vector3 basePos;

        void Start() => basePos = transform.localPosition;

        void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, worldSpace ? Space.World : Space.Self);
            if (bobAmplitude > 0f)
                transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * bobFrequency * 2f * Mathf.PI) * bobAmplitude;
        }
    }
}
