using System;
using KartLab;
using UnityEngine;

namespace DeVinciKart.Upgrade
{
    // Runs after KartMotor so the sweep uses the velocity that physics will integrate.
    [DefaultExecutionOrder(20)]
    [AddComponentMenu("")]
    internal sealed class KartBoundaryAssist : MonoBehaviour
    {
        private const float Skin = 0.035f;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private KartMotor motor;
        private BoxCollider chassis;
        private KartRecovery recovery;
        private float originalDelay;
        private bool active;

        internal void Initialize(KartMotor source)
        {
            motor = source;
            chassis = source.GetComponent<BoxCollider>();
            recovery = source.GetComponent<KartRecovery>();
            if (recovery) originalDelay = recovery.offTrackDelay;
            active = true;
        }

        private void Update()
        {
            if (!active || !recovery) return;
            // Keep the scene's extra airborne time for jumps and ramps.
            recovery.offTrackDelay = motor.Grounded ? Mathf.Min(originalDelay, 0.6f) : originalDelay;
        }

        private void FixedUpdate()
        {
            if (!active || !chassis || !motor.Body || motor.Resetting || motor.driveLocked) return;

            var body = motor.Body;
            Vector3 velocity = body.linearVelocity;
            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float speed = horizontal.magnitude;
            if (speed < 0.05f) return;

            Vector3 scale = chassis.transform.lossyScale;
            Vector3 half = Vector3.Scale(chassis.size * 0.5f, scale);
            Vector3 centre = body.position + body.rotation * Vector3.Scale(chassis.center, scale);
            Vector3 direction = horizontal / speed;
            float step = speed * Time.fixedDeltaTime;
            int count;
            // Imported rail meshes may only expose a triangle from one side. Limit
            // the backface setting to this query and restore it before other scripts run.
            bool previousBackfaces = Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces = true;
                count = Physics.BoxCastNonAlloc(centre, half, direction, hits, body.rotation,
                    step + Skin, motor.groundMask, QueryTriggerInteraction.Ignore);
            }
            finally
            {
                Physics.queriesHitBackfaces = previousBackfaces;
            }

            Vector3 normal = Vector3.zero;
            float nearest = step + Skin;
            for (int index = 0; index < count; index++)
            {
                var hit = hits[index];
                if (hit.distance <= 0.0001f || hit.distance >= nearest || !IsRail(hit.collider) || Mathf.Abs(hit.normal.y) > 0.65f) continue;
                var side = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                // Backfaces can report the opposite normal. Use the one facing the kart.
                if (Vector3.Dot(side, direction) > 0f) side = -side;
                if (Vector3.Dot(side, direction) > -0.08f) continue;

                nearest = hit.distance;
                normal = side;
            }

            if (normal == Vector3.zero) return;
            float travel = Mathf.Clamp01((nearest - Skin) / step);
            float into = Vector3.Dot(horizontal, normal);
            body.linearVelocity = velocity - normal * (into * (1f - travel));
        }

        private static bool IsRail(Collider obstacle)
        {
            if (!obstacle || obstacle.attachedRigidbody) return false;
            string name = obstacle.name;
            return name.Contains("gardrail", StringComparison.OrdinalIgnoreCase)
                || name.Contains("guardrail", StringComparison.OrdinalIgnoreCase)
                || name.Contains("garde-corps", StringComparison.OrdinalIgnoreCase)
                || name.Contains("t_city_fence", StringComparison.OrdinalIgnoreCase);
        }

        private void OnDisable()
        {
            if (active && recovery) recovery.offTrackDelay = originalDelay;
            active = false;
        }
    }
}
