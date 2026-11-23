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
        private const float OutsideGrace = 1f;
        private const float ShoulderMargin = 3f;
        private const float PavedMargin = 8f;
        private float originalDelay, originalStuckDelay, outsideGroundedTime;
        private bool useCitySurfaceChecks;
        private bool active;

        internal void Initialize(KartMotor source)
        {
            motor = source;
            chassis = source.GetComponent<BoxCollider>();
            recovery = source.GetComponent<KartRecovery>();
            useCitySurfaceChecks = source.gameObject.scene.name == "Piste_CitySlicker";
            if (recovery && useCitySurfaceChecks)
            {
                originalDelay = recovery.offTrackDelay;
                originalStuckDelay = recovery.stuckEdgeDelay;
                recovery.offTrackDelay = recovery.stuckEdgeDelay = float.PositiveInfinity;
            }
            active = true;
        }

        private void UpdateCityRecovery(Rigidbody body)
        {
            if (!useCitySurfaceChecks || !recovery || !recovery.isActiveAndEnabled) return;

            bool outside = !motor.Resetting && !motor.driveLocked && motor.Grounded && recovery.route
                && recovery.route.ClosestPose(body.position, out _, out float distance)
                && distance > recovery.route.roadHalfWidth + ShoulderMargin
                && (distance > recovery.route.roadHalfWidth + PavedMargin || !TouchesPavement(body));

            outsideGroundedTime = outside ? outsideGroundedTime + Time.fixedDeltaTime : 0f;
            // KartRecovery still owns the saved centreline pose and the actual reset.
            // Only let its width timer fire after a continuous, clearly off-course departure.
            recovery.offTrackDelay = outsideGroundedTime >= OutsideGrace ? OutsideGrace : float.PositiveInfinity;
        }

        private bool TouchesPavement(Rigidbody body)
        {
            Vector3 origin = body.position + Vector3.up * 0.44f;
            Vector3 sideways = body.rotation * Vector3.right * 0.45f;
            return IsPavedAt(origin) || IsPavedAt(origin + sideways) || IsPavedAt(origin - sideways);
        }

        private bool IsPavedAt(Vector3 origin)
        {
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 0.86f, motor.groundMask,
                QueryTriggerInteraction.Ignore) || hit.normal.y < 0.6f) return false;

            string name = hit.collider.name;
            return name.Contains("t_city_asphalt", StringComparison.OrdinalIgnoreCase)
                || name.Contains("t_city_bike_way", StringComparison.OrdinalIgnoreCase)
                || name.Contains("t_startLine", StringComparison.OrdinalIgnoreCase);
        }

        private void FixedUpdate()
        {
            if (!active || !motor.Body) return;
            var body = motor.Body;
            UpdateCityRecovery(body);
            if (!chassis || motor.Resetting || motor.driveLocked) return;

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
            if (active && recovery && useCitySurfaceChecks)
            {
                recovery.offTrackDelay = originalDelay;
                recovery.stuckEdgeDelay = originalStuckDelay;
            }
            active = false;
        }
    }
}
