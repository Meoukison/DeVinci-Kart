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
        void Start(){if(!route)foreach(var r in TrackRoute.All)if(!r.alternative){route=r;break;}if(!route)route=FindFirstObjectByType<TrackRoute>();}
        // Parmi la route principale et les itinéraires secondaires, garde celle sur laquelle le kart roule vraiment.
        bool Closest(Vector3 p,out Pose pose,out float distance,out float half)
        {
            pose=default;distance=float.PositiveInfinity;half=route?route.roadHalfWidth:4.5f;float best=float.PositiveInfinity;bool any=false;
            foreach(var r in TrackRoute.All)
            {
                if(!r||!r.ClosestPose(p,out Pose c,out float d))continue;
                float score=d/Mathf.Max(.5f,r.roadHalfWidth)+(Mathf.Abs(p.y-c.position.y)<1.2f?0f:50f);
                if(score<best){best=score;pose=c;distance=d;half=r.roadHalfWidth;any=true;}
            }
            if(!any&&route&&route.ClosestPose(p,out pose,out distance)){half=route.roadHalfWidth;any=true;}
            return any;
        }
        void Update()
        {
            if((!route&&TrackRoute.All.Count==0)||!motor.Body||Time.timeScale==0)return;
            if(motor.Resetting){outsideTime=stuckTime=0;graceUntil=Time.time+.3f;return;}
            if(!Closest(motor.Body.position,out Pose centre,out float distance,out float half))return;
            bool onRoad=distance<=half && Mathf.Abs(motor.Body.position.y-centre.position.y)<1.2f;
            // Once outside, freeze the last safe road projection; do not teleport to a later section.
            if(onRoad || !hasRoadPose)
            {LastRoadPose=centre;hasRoadPose=true;motor.SetCheckpoint(centre.position,centre.rotation);}
            if(Time.time<graceUntil)return;
            outsideTime=onRoad?0:outsideTime+Time.deltaTime;
            var input=motor.input?motor.input.Current:default;
            bool trying=input.throttle>.15f||input.brake>.15f;
            bool edge=distance>half-1.4f;
            stuckTime=onRoad&&edge&&trying&&motor.Speed<.7f?stuckTime+Time.deltaTime:0;
            if(outsideTime<offTrackDelay&&stuckTime<stuckEdgeDelay)return;
            motor.SetCheckpoint(LastRoadPose.position,LastRoadPose.rotation);
            motor.BeginReset();motor.Notify("RETURNING TO TRACK");RecoveryCount++;
            outsideTime=stuckTime=0;
        }
    }
}
