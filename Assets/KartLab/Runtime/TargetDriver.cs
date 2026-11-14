using UnityEngine;

namespace KartLab
{
    /// <summary>Test fixture only: slow straight-line moving target, not race AI.</summary>
    public class TargetDriver : MonoBehaviour
    {
        public KartMotor motor;
        public float travel = 35;
        Vector3 start;
        void Start() { start = transform.position; motor.input.useExternalInput = true; }
        void Update()
        {
            motor.input.externalInput = new DriveInput { throttle = .7f };
            if (Vector3.Distance(start, transform.position) > travel) motor.BeginReset();
        }
    }
}
