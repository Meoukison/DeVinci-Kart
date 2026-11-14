using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace KartLab
{
    [System.Serializable]
    public struct DriveInput
    {
        public float steering, throttle, brake;
        public bool skill, use, reset, pause, camera, backward, horn;
    }

    /// <summary>Two-hand virtual wheel; replace this adapter to integrate another input provider.</summary>
    public class KartInput : MonoBehaviour
    {
        public float wheelRange = 65f;
        public float steeringSmoothing = 8f;
        public DriveInput Current { get; private set; }
        public bool WheelHeld { get; private set; }
        public bool XRActive => XRSettings.isDeviceActive;
        public bool useExternalInput;
        public DriveInput externalInput;
        public Vector3 leftPosition, rightPosition;
        public Quaternion leftRotation, rightRotation;
        public bool leftTracked, rightTracked;
        float neutral, steering;
        bool wasHeld;
        Quaternion wheelHeading=Quaternion.identity;

        public void Recalibrate(){wasHeld=false;steering=0;}

        public static float WheelAngle(Vector3 left, Vector3 right)
        {
            Vector3 delta = right - left;
            return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        }

        void Update()
        {
            if (useExternalInput) { Current = externalInput; return; }
            DriveInput input = default;
            if (XRActive)
            {
                XRDevice l = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                XRDevice r = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                leftTracked = ReadPose(l, out leftPosition, out leftRotation);
                rightTracked = ReadPose(r, out rightPosition, out rightRotation);
                bool grips = Button(l, CommonUsages.gripButton) && Button(r, CommonUsages.gripButton);
                WheelHeld = leftTracked && rightTracked && grips && Vector3.Distance(leftPosition, rightPosition) > .12f;
                float target = 0;
                if (WheelHeld)
                {
                    if(!wasHeld)
                    {var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);if(head.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion heading))wheelHeading=Quaternion.Euler(0,heading.eulerAngles.y,0);}
                    float angle = WheelAngle(Quaternion.Inverse(wheelHeading)*leftPosition, Quaternion.Inverse(wheelHeading)*rightPosition);
                    if (!wasHeld) neutral = angle;
                    target = Mathf.Clamp(-Mathf.DeltaAngle(neutral, angle) / wheelRange, -1, 1);
                    input.throttle = Axis(r, CommonUsages.trigger);
                }
                // Release either grip / lose tracking: remove throttle and gently return steering.
                steering = Mathf.Lerp(steering, target, 1 - Mathf.Exp(-steeringSmoothing * Time.unscaledDeltaTime));
                input.steering = steering;
                input.brake = leftTracked ? Axis(l, CommonUsages.trigger) : 0;
                input.skill = rightTracked && Button(r, CommonUsages.primaryButton);
                input.use = leftTracked && Button(l, CommonUsages.primaryButton);
                input.backward=l.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 stick)&&stick.y<-.5f;
                input.reset = leftTracked && Button(l, CommonUsages.secondaryButton);
                input.pause = rightTracked && Button(r, CommonUsages.secondaryButton);
                input.horn = rightTracked && Button(r, CommonUsages.primary2DAxisClick);
                wasHeld = WheelHeld;
            }
            else
            {
                var k = Keyboard.current;
                if (k != null)
                {
                    float target = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
                    steering = Mathf.MoveTowards(steering, target, Time.unscaledDeltaTime * (target==0 ? 6 : 4));
                    input.steering = steering;
                    input.throttle = k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0;
                    input.brake = k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0;
                    input.skill = k.spaceKey.isPressed;
                    input.use = k.eKey.isPressed || k.eKey.wasPressedThisFrame || k.qKey.isPressed || k.qKey.wasPressedThisFrame;
                    input.backward=k.qKey.isPressed||k.qKey.wasPressedThisFrame;
                    input.reset = k.rKey.isPressed;
                    input.pause = k.escapeKey.isPressed || k.escapeKey.wasPressedThisFrame;
                    input.camera = k.cKey.isPressed || k.cKey.wasPressedThisFrame;
                    input.horn = k.hKey.isPressed || k.hKey.wasPressedThisFrame;
                }
            }
            Current = input;
        }

        static bool ReadPose(XRDevice d, out Vector3 p, out Quaternion q)
        {
            bool a = d.TryGetFeatureValue(CommonUsages.devicePosition, out p);
            bool b = d.TryGetFeatureValue(CommonUsages.deviceRotation, out q);
            return d.isValid && a && b && Button(d, CommonUsages.isTracked);
        }
        static bool Button(XRDevice d, InputFeatureUsage<bool> feature) => d.TryGetFeatureValue(feature, out bool v) && v;
        static float Axis(XRDevice d, InputFeatureUsage<float> feature) => d.TryGetFeatureValue(feature, out float v) ? v : 0;
        public void Haptic(float amplitude = .25f, float duration = .1f)
        {
            foreach (var node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                var d = InputDevices.GetDeviceAtXRNode(node);
                if (d.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                    d.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }
}
