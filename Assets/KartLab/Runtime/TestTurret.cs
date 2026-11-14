using UnityEngine;

namespace KartLab
{
    public class TestTurret : MonoBehaviour
    {
        public KartMotor target;
        public GameObject projectilePrefab;
        public float interval = 4, range = 22;
        float next;
        void Update()
        {
            if (!target || !projectilePrefab || Time.time < next || Vector3.Distance(transform.position, target.transform.position) > range) return;
            next = Time.time + interval;
            Vector3 aim = target.transform.position + Vector3.up * .4f - transform.position;
            var shot = Instantiate(projectilePrefab, transform.position, Quaternion.LookRotation(aim));
            shot.GetComponent<KartProjectile>().Launch(null);
        }
    }
}
