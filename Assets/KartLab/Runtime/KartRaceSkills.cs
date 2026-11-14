using UnityEngine;
using UnityEngine.InputSystem;

namespace KartLab
{
    /// <summary>Optional practice start, landing trick and slipstream. Race manager can call BeginCountdown.</summary>
    public class KartRaceSkills : MonoBehaviour
    {
        public KartMotor motor;
        public bool slipstreamEnabled=true;
        public float draftDistance=14, draftChargeTime=1.8f;
        public bool CountingDown {get;private set;}
        public string CountdownLabel => CountingDown?Mathf.CeilToInt(Mathf.Max(0,startAt-Time.time)).ToString():"GO";
        public float DraftCharge {get;private set;}
        public bool TrickArmed {get;private set;}
        public int SuccessfulTricks {get;private set;}
        public int RocketStarts {get;private set;}
        float startAt, throttleAt=-1, airTime, lastTrick, draftCooldown;
        bool oldThrottle,oldSkill,wasGrounded;
        KartMotor[] rivals=System.Array.Empty<KartMotor>();float refreshAt;
        public void BeginCountdown()
        {if(motor.Resetting)return;CountingDown=true;startAt=Time.time+3;throttleAt=-1;oldThrottle=motor.input.Current.throttle>.5f;motor.driveLocked=true;motor.Notify("START IN 3");}
        public void CancelCountdown(){CountingDown=false;motor.driveLocked=false;}
        void Update()
        {
            if(Time.timeScale==0||!motor.input)return;
            var i=motor.input.Current;
            if(!motor.input.useExternalInput&&!motor.input.XRActive&&Keyboard.current!=null&&Keyboard.current.enterKey.wasPressedThisFrame&&!CountingDown)BeginCountdown();
            if(motor.Resetting){CancelCountdown();TrickArmed=false;airTime=0;DraftCharge=0;wasGrounded=false;return;}
            if(CountingDown)
            {
                bool throttle=i.throttle>.5f;
                if(throttle&&!oldThrottle)throttleAt=Time.time;
                if(!throttle)throttleAt=-1;
                oldThrottle=throttle;
                if(Time.time>=startAt)
                {CancelCountdown();if(throttleAt>=startAt-.8f&&throttleAt<=startAt-.15f&&throttle){RocketStarts++;motor.ApplyBoost(1.4f);motor.Notify("ROCKET START");}else motor.Notify(throttleAt>0&&throttleAt<startAt-.8f?"EARLY THROTTLE / NORMAL START":"GO");}
            }
            if(!motor.Grounded){airTime+=Time.deltaTime;if(i.skill&&!oldSkill&&airTime>.08f&&Time.time>lastTrick+.8f){TrickArmed=true;motor.Notify("TRICK READY");}}
            if(motor.Grounded&&!wasGrounded)
            {if(TrickArmed&&airTime>.16f&&motor.Speed>4){motor.ApplyBoost(.8f);motor.Notify("PERFECT LANDING");SuccessfulTricks++;lastTrick=Time.time;}TrickArmed=false;airTime=0;}
            oldSkill=i.skill;wasGrounded=motor.Grounded;
            if(Time.time>=refreshAt){rivals=FindObjectsByType<KartMotor>();refreshAt=Time.time+1;}
            bool drafting=false;
            if(slipstreamEnabled&&motor.Grounded&&motor.Speed>6&&!CountingDown&&Time.time>draftCooldown)
                foreach(var other in rivals)
                {
                    if(!other||other==motor||other.Resetting||other.Speed<4)continue;
                    Vector3 delta=other.transform.position-motor.transform.position;float ahead=Vector3.Dot(delta,motor.transform.forward);
                    if(ahead<3||ahead>draftDistance||Mathf.Abs(Vector3.Dot(delta,motor.transform.right))>1.8f||Mathf.Abs(delta.y)>1.5f||Vector3.Dot(other.transform.forward,motor.transform.forward)<.85f)continue;
                    if(Physics.Linecast(motor.transform.position+Vector3.up*.7f,other.transform.position+Vector3.up*.7f,motor.groundMask,QueryTriggerInteraction.Ignore))continue;
                    drafting=true;break;
                }
            DraftCharge=Mathf.MoveTowards(DraftCharge,drafting?draftChargeTime:0,Time.deltaTime*(drafting?1:2));
            if(DraftCharge>=draftChargeTime){DraftCharge=0;draftCooldown=Time.time+3.5f;motor.ApplyBoost(1);motor.Notify("SLIPSTREAM BOOST");}
        }
        void OnDisable(){if(motor)CancelCountdown();}
    }
}
