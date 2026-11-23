using KartLab;
using UnityEngine;

namespace DeVinciKart.Upgrade
{
    [DefaultExecutionOrder(25)]
    [AddComponentMenu("")]
    internal sealed class KartAccelerationView : MonoBehaviour
    {
        private KartMotor motor;
        private KartView view;
        private float previousSpeed, impulse;
        private bool sampled;

        internal void Initialize(KartMotor source, KartView driverView)
        {
            motor = source;
            view = driverView;
        }

        private void FixedUpdate()
        {
            if (!motor || !motor.Body) return;
            float forwardSpeed = Mathf.Max(0f, Vector3.Dot(motor.Body.linearVelocity, motor.Body.rotation * Vector3.forward));
            var input = motor.input.Current;
            bool pulling = sampled && motor.Grounded && !motor.Resetting && !motor.driveLocked
                && input.throttle > 0.12f && input.brake <= 0.1f;
            float acceleration = pulling ? Mathf.Max(0f, forwardSpeed - previousSpeed) / Time.fixedDeltaTime : 0f;
            float target = Mathf.InverseLerp(1.5f, 16f, acceleration);
            float response = target > impulse ? 10f : 4f;
            impulse = Mathf.Lerp(impulse, target, 1f - Mathf.Exp(-response * Time.fixedDeltaTime));
            previousSpeed = forwardSpeed;
            sampled = !motor.Resetting;
        }

        private void LateUpdate()
        {
            if (!motor || !view || !view.view || !view.isActiveAndEnabled || motor.input.XRActive || motor.Resetting) return;

            // KartView first places the desktop camera at its normal pose each frame.
            Camera camera = view.view;
            camera.fieldOfView += impulse * (view.thirdPerson ? 4f : 6f);
            if (!view.thirdPerson)
                camera.transform.position -= motor.transform.forward * (impulse * 0.075f);
        }
    }
}
