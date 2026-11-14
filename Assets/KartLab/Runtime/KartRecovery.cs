using UnityEngine;

namespace KartLab
{
    [RequireComponent(typeof(KartMotor))]
    public class KartRecovery : MonoBehaviour
    {
        public TrackRoute route;
        public float offTrackDelay=1, stuckEdgeDelay=1.5f;
        public Pose LastRoadPose {get;private set;}
        public int RecoveryCount {get;private set;}
        KartMotor motor;
        bool hasRoadPose;
        float outsideTime,stuckTime,graceUntil;
        void Awake(){motor=GetComponent<KartMotor>();}
        void OnEnable(){hasRoadPose=false;outsideTime=stuckTime=0;graceUntil=Time.time+.3f;}
        void Start(){if(!route)route=FindFirstObjectByType<TrackRoute>();}
        void Update()
        {
            if(!route||!motor.Body||Time.timeScale==0)return;
            if(motor.Resetting){outsideTime=stuckTime=0;graceUntil=Time.time+.3f;return;}
            if(!route.ClosestPose(motor.Body.position,out Pose centre,out float distance))return;
            bool onRoad=distance<=route.roadHalfWidth && Mathf.Abs(motor.Body.position.y-centre.position.y)<1.2f;
            // Once outside, freeze the last safe road projection; do not teleport to a later section.
            if(onRoad || !hasRoadPose)
            {LastRoadPose=centre;hasRoadPose=true;motor.SetCheckpoint(centre.position,centre.rotation);}
            if(Time.time<graceUntil)return;
            outsideTime=onRoad?0:outsideTime+Time.deltaTime;
            var input=motor.input?motor.input.Current:default;
            bool trying=input.throttle>.15f||input.brake>.15f;
            bool edge=distance>route.roadHalfWidth-1.4f;
            stuckTime=onRoad&&edge&&trying&&motor.Speed<.7f?stuckTime+Time.deltaTime:0;
            if(outsideTime<offTrackDelay&&stuckTime<stuckEdgeDelay)return;
            motor.SetCheckpoint(LastRoadPose.position,LastRoadPose.rotation);
            motor.BeginReset();motor.Notify("RETURNING TO TRACK");RecoveryCount++;
            outsideTime=stuckTime=0;
        }
    }
}
