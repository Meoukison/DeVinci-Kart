using UnityEngine;
using UnityEngine.InputSystem;

namespace DeVinciKart
{
    public enum KartItem { Aucun, Champignon, Plume }

    /// <summary>
    /// Kart arcade façon Mario Kart : accélération, dérapage + mini-turbo, objets (champignon / plume).
    /// Clavier : ZQSD/WASD ou flèches, Espace = dérapage, E ou Shift = objet, R = replacer le kart.
    /// Manette : RT accélère, LT freine, stick gauche, RB = dérapage, A/LB = objet, Select = replacer.
    /// Pour la VR : mettre useExternalInput = true et appeler SetInput(...) depuis vos contrôleurs XR.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KartController : MonoBehaviour
    {
        [Header("Vitesse (m/s)")]
        public float maxSpeed = 24f;
        public float acceleration = 15f;
        public float brakeForce = 28f;
        public float reverseSpeed = 8f;
        public float coastDecel = 5f;

        [Header("Direction")]
        public float turnRate = 95f;
        public float grip = 9f;
        public float driftGrip = 2.2f;
        public float driftTurnBoost = 1.35f;

        [Header("Boost / saut")]
        public float boostSpeed = 34f;
        public float plumeJump = 9f;
        public float extraGravity = 22f;

        [Header("Sol")]
        public LayerMask groundMask = ~(1 << 2);
        public float groundRay = 1.4f;

        [Header("Références")]
        public Transform visual;
        public GameObject plumeDisplay;
        public Transform cameraAnchor;

        [Header("État")]
        public bool controlsLocked = true;
        public KartItem heldItem = KartItem.Aucun;

        [Header("Entrée externe (VR)")]
        public bool useExternalInput;

        public bool IsGrounded { get; private set; }
        public bool IsDrifting => drifting;
        public float Speed => rb ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;

        Rigidbody rb;
        float throttle, brake, steer;
        bool driftHeld, driftPressed, itemPressed;
        float extThrottle, extBrake, extSteer;
        bool extDrift, extDriftPrev, extItem;
        float boostTimer, driftTime;
        bool drifting;
        int driftDir;
        Vector3 groundNormal = Vector3.up;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = 0f;
        }

        /// <summary>Entrée depuis un autre système (ex : volant / manettes VR).</summary>
        public void SetInput(float throttle01, float brake01, float steerMinus1To1, bool drift, bool useItem)
        {
            extThrottle = throttle01; extBrake = brake01; extSteer = steerMinus1To1;
            extDrift = drift; extItem |= useItem;
        }

        void ReadInput()
        {
            float t = 0, b = 0, s = 0;
            bool dh = false, dp = false, ip = false;

            if (useExternalInput)
            {
                t = extThrottle; b = extBrake; s = extSteer;
                dh = extDrift; dp = extDrift && !extDriftPrev; ip = extItem;
                extDriftPrev = extDrift; extItem = false;
            }
            else
            {
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.wKey.isPressed || k.zKey.isPressed || k.upArrowKey.isPressed) t = 1f;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) b = 1f;
                    if (k.aKey.isPressed || k.qKey.isPressed || k.leftArrowKey.isPressed) s -= 1f;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) s += 1f;
                    dh |= k.spaceKey.isPressed;
                    dp |= k.spaceKey.wasPressedThisFrame;
                    ip |= k.eKey.wasPressedThisFrame || k.leftShiftKey.wasPressedThisFrame;
                }
                var g = Gamepad.current;
                if (g != null)
                {
                    t = Mathf.Max(t, g.rightTrigger.ReadValue());
                    b = Mathf.Max(b, g.leftTrigger.ReadValue());
                    float gs = g.leftStick.x.ReadValue();
                    if (Mathf.Abs(gs) > 0.15f) s = gs;
                    dh |= g.rightShoulder.isPressed;
                    dp |= g.rightShoulder.wasPressedThisFrame;
                    ip |= g.buttonSouth.wasPressedThisFrame || g.leftShoulder.wasPressedThisFrame;
                }
            }

            if (controlsLocked) { t = b = s = 0f; dh = dp = ip = false; }
            throttle = t; brake = b; steer = Mathf.Clamp(s, -1f, 1f);
            driftHeld = dh; driftPressed = dp; itemPressed = ip;
        }

        void Update()
        {
            ReadInput();

            if (driftPressed && !drifting && IsGrounded && Mathf.Abs(steer) > 0.3f && Speed > 8f)
            {
                drifting = true;
                driftDir = steer > 0 ? 1 : -1;
                driftTime = 0f;
                rb.AddForce(transform.up * 3.5f, ForceMode.VelocityChange); // petit saut
            }
            if (drifting)
            {
                driftTime += Time.deltaTime;
                if (!driftHeld || Speed < 5f)
                {
                    if (driftTime > 2.2f) Boost(1.2f);      // turbo orange
                    else if (driftTime > 1.1f) Boost(0.7f); // turbo bleu
                    drifting = false;
                }
            }

            if (itemPressed) UseItem();
            if (plumeDisplay) plumeDisplay.SetActive(heldItem == KartItem.Plume);

            if (visual)
            {
                float lean = drifting ? driftDir * 10f : steer * 4f;
                float yaw = drifting ? driftDir * 18f : 0f;
                var target = Quaternion.Euler(0f, yaw, -lean);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 origin = rb.position + transform.up * 0.5f;
            IsGrounded = Physics.Raycast(origin, -transform.up, out RaycastHit hit, groundRay, groundMask, QueryTriggerInteraction.Ignore);
            Vector3 targetUp = IsGrounded ? hit.normal : Vector3.up;
            groundNormal = Vector3.Slerp(groundNormal, targetUp, 1f - Mathf.Exp(-12f * dt)).normalized;

            float speed = Vector3.Dot(rb.linearVelocity, transform.forward);
            float speedFactor = Mathf.Clamp01(Mathf.Abs(speed) / 5f) * (speed < -0.1f ? -1f : 1f);
            float turn = steer * turnRate * speedFactor;
            if (drifting) turn = (driftDir * 0.55f + steer * 0.6f) * turnRate * driftTurnBoost * Mathf.Abs(speedFactor);

            Vector3 fwd = Quaternion.AngleAxis(turn * dt, groundNormal) * transform.forward;
            fwd = Vector3.ProjectOnPlane(fwd, groundNormal);
            if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
            Quaternion rot = Quaternion.LookRotation(fwd.normalized, groundNormal);
            rb.MoveRotation(rot);

            if (IsGrounded)
            {
                Vector3 local = Quaternion.Inverse(rot) * rb.linearVelocity;
                if (boostTimer > 0f)
                    local.z = Mathf.MoveTowards(local.z, boostSpeed, 50f * dt);
                else if (throttle > 0.05f)
                    local.z = local.z < maxSpeed
                        ? Mathf.MoveTowards(local.z, maxSpeed, acceleration * throttle * dt)
                        : Mathf.MoveTowards(local.z, maxSpeed, 12f * dt);
                else if (brake > 0.05f)
                    local.z = Mathf.MoveTowards(local.z, -reverseSpeed, brakeForce * brake * dt);
                else
                    local.z = Mathf.MoveTowards(local.z, 0f, coastDecel * dt);

                float g = drifting ? driftGrip : grip;
                local.x = Mathf.Lerp(local.x, 0f, 1f - Mathf.Exp(-g * dt));
                rb.linearVelocity = rot * local;
                rb.AddForce(-groundNormal * extraGravity, ForceMode.Acceleration);
            }
            else
            {
                rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
            }

            if (boostTimer > 0f) boostTimer -= dt;
        }

        public void Boost(float duration) => boostTimer = Mathf.Max(boostTimer, duration);

        public void UseItem()
        {
            switch (heldItem)
            {
                case KartItem.Champignon:
                    Boost(1.6f);
                    break;
                case KartItem.Plume:
                    if (!IsGrounded) return;
                    rb.AddForce(groundNormal * plumeJump, ForceMode.VelocityChange);
                    break;
                default:
                    return;
            }
            heldItem = KartItem.Aucun;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            groundNormal = rotation * Vector3.up;
            drifting = false;
            boostTimer = 0f;
        }
    }
}
