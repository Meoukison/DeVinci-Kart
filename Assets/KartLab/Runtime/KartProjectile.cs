using UnityEngine;

namespace KartLab
{
    public class KartProjectile : MonoBehaviour
    {
        public float speed = 30, lifetime = 6;
        public int maxBounces=3;
        public bool ice;
        public Transform spinningArt;
        public int BounceCount {get;private set;}
        KartMotor owner;
        float launched;
        public void Launch(KartMotor source) { owner = source;launched=Time.time; Destroy(gameObject, lifetime); }
        void Update(){if(spinningArt)spinningArt.Rotate(0,Time.deltaTime*480,0);}
        void FixedUpdate()
        {
            float distance = speed * Time.fixedDeltaTime;
            var hits = Physics.SphereCastAll(transform.position, .15f, transform.forward, distance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var target = hit.collider.GetComponentInParent<KartMotor>();
                if (target && target == owner && (BounceCount==0||Time.time-launched<.5f)) continue;
                if (target)
                {bool applied=ice?target.ApplyFreeze():target.ApplyHit(.55f,1.2f);if(owner&&target!=owner)owner.Notify(applied?"TARGET HIT":"TARGET PROTECTED");Destroy(gameObject);return;}
                if(Mathf.Abs(hit.normal.y)>.7f)continue;
                if(ice||BounceCount>=maxBounces){Destroy(gameObject);return;}
                BounceCount++;
                Vector3 reflected=Vector3.ProjectOnPlane(Vector3.Reflect(transform.forward,hit.normal),Vector3.up).normalized;
                transform.position += transform.forward*Mathf.Max(0,hit.distance-.03f)+hit.normal*.05f;
                transform.rotation=Quaternion.LookRotation(reflected);
                return;
            }
            transform.position += transform.forward * distance;
        }
    }
}
