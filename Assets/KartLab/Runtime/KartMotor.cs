using System;
using System.Collections.Generic;
using UnityEngine;

namespace KartLab
{
    [RequireComponent(typeof(Rigidbody))]
    public class KartMotor : MonoBehaviour
    {
        public KartInput input;
        public Transform visual;
        public float topSpeed = 18, acceleration = 12, braking = 22, reverseSpeed = 4;
        public float steerDegrees = 110, jumpHeight = .4f;
        public float lowSpeedTurnRadius = 3.8f, highSpeedTurnRadius = 6.5f, boostMultiplier = 1.45f;
        public float driftSteerDegrees = 150, steeringResponse = 300, lateralGrip = 36;
        [Range(0,.4f)] public float maxStepHeight = .32f;
        [Range(.4f,1)] public float vrSpeedScale = .72f;
        public LayerMask groundMask = 1;
        [Header("Hors-piste")]
        [Tooltip("Ralentit le kart dès qu'il s'éloigne de toutes les routes (TrackRoute) de la scène, quel que soit le sol.")] public bool offTrackSlowdown = true;
        [Range(.2f,1)] public float offTrackSpeedFactor = .5f;
        [Tooltip("Marge (m) au-delà de la demi-largeur de la route avant de commencer à ralentir, puis distance sur laquelle le ralentissement s'installe.")] public float offTrackMargin = 1.5f, offTrackBlend = 3f;
        public bool OffTrack => offTrackFactor < .99f;
        public bool assistedDriving=true;
        [HideInInspector] public bool driveLocked;
        [Tooltip("En dessous de cette hauteur, le kart est considéré tombé et remis sur la piste.")] public float fallResetY = -6f;
        public float SlipSpeed {get;private set;}
        public Rigidbody Body { get; private set; }
        public float Speed => Body ? Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up).magnitude : 0;
        public bool Grounded { get; private set; }
        public bool Drifting => skillMode == 1;
        public bool Charging => skillMode == 2;
        public float Charge { get; private set; }
        public bool Boosted => Time.time < boostUntil || Invincible;
        public bool Invincible => Time.time < starUntil;
        public bool Frozen => Time.time < frozenUntil;
        public float StarRemaining => Mathf.Max(0,starUntil-Time.time);
        public float BoostRemaining => Mathf.Max(0,boostUntil-Time.time);
        public float EffectiveTopSpeed => topSpeed * (input && input.XRActive ? vrSpeedScale : 1);
        public int DriftTier => Charge >= 2.1f ? 3 : Charge >= 1.25f ? 2 : Charge >= .65f ? 1 : 0;
        public float Steering => Drifting ? driftDirection * Mathf.Lerp(.3f,1, Mathf.InverseLerp(-1,1,(input ? input.Current.steering : 0)*driftDirection)) : input ? input.Current.steering : 0;
        public bool Shielded => Time.time < shieldUntil;
        public bool Protected => Time.time < protectedUntil;
        public int HitCount { get; private set; }
        public string LastEvent { get; private set; } = "READY TO DRIVE";
        public event Action<string> Feedback;
        public Pose SafePose { get; private set; }
        public bool Resetting => resetUntil > Time.time;
        readonly Dictionary<UnityEngine.Object, float> surfaces = new();
        readonly List<Vector2> slows = new();
        int skillMode;
        float driftDirection;
        bool previousSkill, previousUse, resetLatched;
        float boostUntil, shieldUntil, protectedUntil, jumpReady, reverseHold, resetHold, collisionUntil, resetUntil;
        float groundFactor = 1, offTrackFactor = 1, offTrackCheckAt;
        float starUntil, frozenUntil, yawRate, collisionFeedbackAt;
        readonly List<Vector3> wallNormals = new();
        readonly List<ContactPoint> stepContacts = new();
        Collider chassis;
        bool supportedByContact;
        float contactSurfaceFactor=1;
        PhysicsMaterial contactMaterial;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = 180;chassis=GetComponent<Collider>();
            Body.linearDamping = 0;
            contactMaterial = new PhysicsMaterial("Kart contact - grip controlled by motor")
            { staticFriction = 0, dynamicFriction = 0, bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum };
            foreach (var collider in GetComponents<Collider>()) collider.sharedMaterial = contactMaterial;
            // Contact impulses must not spin the player camera. Steering is authored below.
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.maxDepenetrationVelocity = 2;
            Body.maxAngularVelocity = 0;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            SafePose = new Pose(transform.position, transform.rotation);
        }
        void Update()
        {
            if (!input || Time.timeScale == 0) return;
            var i = input.Current;
            if (i.use && !previousUse && !Resetting) GetComponent<KartInventory>()?.Use(i.backward);
            if (i.skill && !previousSkill && Grounded && Speed >= 6 && Time.time >= jumpReady && !Resetting && !driveLocked)
            { skillMode = Mathf.Abs(i.steering) > .2f ? 1 : 2; Charge = 0; driftDirection = Mathf.Sign(i.steering); if(skillMode==1)Notify("DRIFT"); }
            if (i.skill && skillMode != 0)
            {
                bool valid = Grounded && Speed >= 6 && Time.time > collisionUntil;
                Charge = valid ? Mathf.Min(Charge + Time.deltaTime, skillMode == 1 ? 2.1f : .6f) : 0;
            }
            if (!i.skill && previousSkill)
            {
                if (Grounded && Speed >= 6 && Time.time > collisionUntil)
                {
                    if (skillMode == 1 && DriftTier > 0) { ApplyBoost(DriftTier==3 ? 1.8f : DriftTier==2 ? 1.1f : .65f); Notify("MINI TURBO " + DriftTier); }
                    if (skillMode == 2 && Charge >= .6f)
                    {
                        var v = Body.linearVelocity;
                        v.y = Mathf.Sqrt(2 * Physics.gravity.magnitude * jumpHeight);
                        Body.linearVelocity = v;
                        jumpReady = Time.time + 1.55f;
                        Notify("CHARGE JUMP");
                    }
                }
                skillMode = 0; Charge = 0;
            }
            previousSkill = i.skill; previousUse = i.use;
            resetHold = i.reset ? resetHold + Time.deltaTime : 0;
            if (!i.reset) resetLatched = false;
            if (resetHold >= 1 && !resetLatched) { resetLatched = true; BeginReset(); }
            if (transform.position.y < fallResetY && !Resetting) BeginReset();
            if (visual) visual.localRotation = Quaternion.Lerp(visual.localRotation,
                Quaternion.Euler(0, Drifting ? Steering * -20 : -i.steering*3, -i.steering * (Drifting?5:2)), Time.deltaTime * 10);
        }
        void FixedUpdate()
        {
            Body.angularVelocity = Vector3.zero;
            UpdateGroundContact();
            UpdateOffTrack();
            if (Resetting || driveLocked) { Body.linearVelocity = Vector3.zero; yawRate=0;wallNormals.Clear();stepContacts.Clear();return; }
            var i = input ? input.Current : default;
            if(Grounded)TryClimbLowEdge(i);
            Vector3 planar = Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up);
            float speed = Vector3.Dot(planar, transform.forward);
            float limit = EffectiveTopSpeed * (Time.time < boostUntil ? boostMultiplier : Invincible ? 1.25f : 1) * SpeedFactor();
            if (Grounded)
            {
                float radius=Mathf.Lerp(lowSpeedTurnRadius,highSpeedTurnRadius,Mathf.Clamp01(Mathf.Abs(speed)/10));
                if(Drifting)radius*=.8f;
                float maxYaw=Drifting?driftSteerDegrees:steerDegrees;
                float steer=Steering;
                // Tight turns shed speed, as tyres cannot hold an arbitrarily fast corner.
                float cornerLimit=maxYaw*Mathf.Deg2Rad*radius/Mathf.Max(.1f,Mathf.Abs(steer));
                if(assistedDriving)limit=Mathf.Min(limit,cornerLimit);
                if (i.brake > .1f)
                {
                    if (Mathf.Abs(speed) < .3f || speed < 0) reverseHold += Time.fixedDeltaTime;
                    else reverseHold = 0;
                    speed = reverseHold > .5f && i.throttle < .1f
                        ? Mathf.MoveTowards(speed, -reverseSpeed * i.brake, acceleration * Time.fixedDeltaTime)
                        : Mathf.MoveTowards(speed, 0, braking * i.brake * Time.fixedDeltaTime);
                }
                else
                {
                    reverseHold = 0;
                    float target = i.throttle > .01f ? limit * i.throttle : 0;
                    speed = Mathf.MoveTowards(speed, target, (i.throttle > .01f ? acceleration * (Boosted ? 2.5f : 1) : 1.6f) * Time.fixedDeltaTime);
                }
                if (speed > limit) speed = Mathf.MoveTowards(speed, limit, 24 * Time.fixedDeltaTime);
                float desiredYaw=steer*Mathf.Clamp(speed/radius*Mathf.Rad2Deg,-maxYaw,maxYaw);
                if(wallNormals.Count>0 && Mathf.Abs(speed)<3)
                    desiredYaw=steer*55*(i.throttle-i.brake);
                if(Frozen)desiredYaw*=.6f;
                yawRate=Mathf.MoveTowards(yawRate,desiredYaw,steeringResponse*Time.fixedDeltaTime);
                Quaternion nextRotation=Body.rotation*Quaternion.Euler(0,yawRate*Time.fixedDeltaTime,0);
                Body.MoveRotation(nextRotation);
                float sideways=Vector3.Dot(planar,transform.right);
                float slipTarget=Drifting?-Steering*Mathf.Min(2.3f,Mathf.Abs(speed)*.13f):0;
                sideways=Mathf.MoveTowards(sideways,slipTarget,(Drifting?9:lateralGrip)*Time.fixedDeltaTime);SlipSpeed=sideways;
                Vector3 velocity=nextRotation*new Vector3(sideways,0,speed)+Vector3.up*Body.linearVelocity.y;
                // Preserve tangential movement while removing all motion into a contacted wall.
                foreach(var normal in wallNormals)
                {float into=Vector3.Dot(velocity,normal);if(into<0)velocity-=normal*into;}
                Body.linearVelocity=velocity;
            }
            wallNormals.Clear();stepContacts.Clear();
        }
        void UpdateGroundContact()
        {
            Grounded=false;groundFactor=1;
            float bottom=chassis?chassis.bounds.min.y:Body.position.y;
            float highest=float.NegativeInfinity;
            // Centre plus four tyre areas: a kerb under a wheel must still support the kart.
            for(int n=0;n<5;n++)
            {
                Vector3 local=n==0?Vector3.zero:new Vector3(n<=2?-.48f:.48f,0,n%2==0?-.68f:.68f);
                Vector3 origin=Body.position+Body.rotation*local+Vector3.up*.5f;
                if(!Physics.Raycast(origin,Vector3.down,out var hit,.8f,groundMask,QueryTriggerInteraction.Ignore))continue;
                float gap=bottom-hit.point.y;
                if(hit.normal.y<.6f || gap<-.06f || gap>.18f || hit.point.y<=highest)continue;
                highest=hit.point.y;Grounded=true;
                groundFactor=hit.collider.TryGetComponent<GroundSurface>(out var surface)?surface.speedFactor:1;
            }
            if(!Grounded && supportedByContact && Body.linearVelocity.y<=.5f)
            {Grounded=true;groundFactor=contactSurfaceFactor;}
            supportedByContact=false;
        }
        /// <summary>Distance à la route la plus proche (toutes les TrackRoute actives, raccourcis compris), à hauteur comparable.</summary>
        void UpdateOffTrack()
        {
            if (!offTrackSlowdown || TrackRoute.All.Count == 0 || !Grounded) { if (!offTrackSlowdown) offTrackFactor = 1; return; }
            if (Time.time < offTrackCheckAt) return;
            offTrackCheckAt = Time.time + .1f;
            Vector3 pos = Body.position;
            float best = float.PositiveInfinity;   // distance au bord de la route (négative = sur la route)
            foreach (var route in TrackRoute.All)
            {
                if (!route || !route.isActiveAndEnabled || route.points == null || route.points.Length < 2) continue;
                var tr = route.transform; var pts = route.points;
                int segs = route.closed ? pts.Length : pts.Length - 1;
                for (int n = 0; n < segs; n++)
                {
                    Vector3 a = tr.TransformPoint(pts[n]), b = tr.TransformPoint(pts[(n + 1) % pts.Length]);
                    Vector3 ab = b - a; ab.y = 0; float len2 = ab.sqrMagnitude; if (len2 < 1e-4f) continue;
                    Vector3 ap = pos - a; ap.y = 0;
                    float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / len2);
                    Vector3 c = Vector3.Lerp(a, b, t);
                    if (Mathf.Abs(pos.y - c.y) > 4f) continue;   // viaduc au-dessus / route en dessous
                    float dx = pos.x - c.x, dz = pos.z - c.z;
                    float edge = Mathf.Sqrt(dx * dx + dz * dz) - route.roadHalfWidth;
                    if (edge < best) best = edge;
                }
            }
            if (float.IsPositiveInfinity(best)) { offTrackFactor = 1; return; }   // aucune route à cette hauteur (saut, zone non tracée)
            offTrackFactor = Mathf.Lerp(1, offTrackSpeedFactor, Mathf.InverseLerp(offTrackMargin, offTrackMargin + offTrackBlend, best));
        }
        void TryClimbLowEdge(DriveInput drive)
        {
            if(!chassis || maxStepHeight<=0 || Body.linearVelocity.y>.5f)return;
            float intention=drive.throttle>.05f?drive.throttle:drive.brake>.05f?-drive.brake:0;
            if(Mathf.Abs(intention)<.05f)return;
            Vector3 direction=Body.rotation*Vector3.forward*Mathf.Sign(intention);
            Bounds bounds=chassis.bounds;
            foreach(var contact in stepContacts)
            {
                Vector3 normal=Vector3.ProjectOnPlane(contact.normal,Vector3.up).normalized;
                if(Vector3.Dot(direction,normal)>-.15f)continue;
                // Look just beyond the blocking face. A full-height wall has no reachable top.
                Vector3 probe=contact.point-normal*.16f;
                probe.y=bounds.min.y+maxStepHeight+.08f;
                if(!Physics.Raycast(probe,Vector3.down,out var top,maxStepHeight+.1f,groundMask,QueryTriggerInteraction.Ignore)||top.normal.y<.85f)continue;
                float rise=top.point.y-bounds.min.y+.02f;
                if(rise<.025f || rise>maxStepHeight+.02f)continue;
                Vector3 extents=bounds.extents-Vector3.one*.015f;
                // Never lift the chassis into a wall/roof; only clear a genuinely low ledge.
                if(Physics.CheckBox(bounds.center+Vector3.up*rise,extents,Quaternion.identity,groundMask,QueryTriggerInteraction.Ignore))continue;
                Body.position+=Vector3.up*rise;
                Vector3 velocity=Body.linearVelocity;velocity.y=Mathf.Min(velocity.y,0);Body.linearVelocity=velocity;
                wallNormals.Clear();return;
            }
        }
        public float SpeedFactor()
        {
            if(Invincible)return 1;
            float factor = Mathf.Min(groundFactor, offTrackFactor);
            foreach (float surface in surfaces.Values) factor = Mathf.Min(factor, surface);
            for (int n = slows.Count - 1; n >= 0; n--)
                if (Time.time >= slows[n].y) slows.RemoveAt(n); else factor = Mathf.Min(factor, slows[n].x);
            return factor;
        }
        public void SetSurface(UnityEngine.Object id, float factor) => surfaces[id] = Mathf.Clamp(factor, .1f, 1);
        public void ClearSurface(UnityEngine.Object id) => surfaces.Remove(id);
        public void ApplyBoost(float duration) { boostUntil = Mathf.Max(boostUntil, Time.time + duration); Notify("TURBO"); }
        public void ApplyShield(float duration = 5) { shieldUntil = Time.time + duration; Notify("SHIELD READY"); }
        public void ApplyStar(float duration = 5)
        { starUntil=Time.time+duration; frozenUntil=0;slows.Clear();Notify("STAR POWER"); }
        public bool ApplyFreeze()
        { if(!ApplyHit(.45f,1.5f))return false;frozenUntil=Time.time+1.5f;Notify("FROZEN");return true; }
        public bool ApplyHit(float factor = .6f, float duration = 1.2f)
        {
            if (Resetting || Protected || Invincible) return false;
            if (Shielded) { shieldUntil = 0; protectedUntil = Time.time + 2; Notify("BLOCKED"); return false; }
            slows.Add(new Vector2(Mathf.Clamp(factor, .1f, 1), Time.time + duration));
            protectedUntil = Time.time + 2; HitCount++;
            var v = Body.linearVelocity; Body.linearVelocity = new Vector3(v.x * factor, v.y, v.z * factor);
            skillMode = 0; Charge = 0; Notify("HIT - SLOW DOWN"); return true;
        }
        public void SetCheckpoint(Vector3 position, Quaternion rotation) => SafePose = new Pose(position, rotation);
        public void BeginReset()
        {
            if (Resetting) return;
            resetUntil = Time.time + .55f;
            skillMode = 0; Charge = 0; boostUntil = shieldUntil = protectedUntil = starUntil = frozenUntil = 0;
            slows.Clear(); surfaces.Clear(); wallNormals.Clear();stepContacts.Clear();supportedByContact=false;yawRate=0; Body.linearVelocity = Body.angularVelocity = Vector3.zero;
            Invoke(nameof(FinishReset), .25f); Notify("RETURNING TO CHECKPOINT");
        }
        void FinishReset()
        { Body.position = SafePose.position; Body.rotation = SafePose.rotation; Body.linearVelocity = Vector3.zero; }
        public void Notify(string message) { LastEvent = message; Feedback?.Invoke(message);
            float strength=message.Contains("HIT")||message.Contains("WALL")?.55f:message.Contains("TURBO")||message.Contains("START")?.35f:message.Contains("DRIFT")?.12f:.2f;
            input?.Haptic(strength,message.Contains("TURBO")?.18f:.07f); }
        void OnDestroy() { if (contactMaterial) Destroy(contactMaterial); }
        void OnCollisionEnter(Collision collision) => ResolveContact(collision,true);
        void OnCollisionStay(Collision collision) => ResolveContact(collision,false);
        void ResolveContact(Collision collision,bool entering)
        {
            var rival=collision.collider.GetComponentInParent<KartMotor>();
            if(entering && rival && Invincible)rival.ApplyHit(.5f,1.2f);
            bool wall=false;
            foreach(var contact in collision.contacts)
            {
                if(contact.normal.y>.6f)
                {supportedByContact=true;contactSurfaceFactor=collision.collider.TryGetComponent<GroundSurface>(out var surface)?surface.speedFactor:1;}
                if(Mathf.Abs(contact.normal.y)>=.5f)continue;
                if(!collision.rigidbody)stepContacts.Add(contact);
                Vector3 normal=Vector3.ProjectOnPlane(contact.normal,Vector3.up).normalized;
                wallNormals.Add(normal);wall=true;
                Vector3 velocity=Body.linearVelocity;
                float into=Vector3.Dot(velocity,normal);if(into<0)velocity-=normal*into;
                if(Grounded)velocity.y=Mathf.Min(velocity.y,0);
                Body.linearVelocity=Vector3.ClampMagnitude(velocity,EffectiveTopSpeed*boostMultiplier);
            }
            if(!wall)return;
            Body.angularVelocity=Vector3.zero;
            collisionUntil=Time.time+.12f;Charge=0;
            if(entering && Time.time>=collisionFeedbackAt)
            {collisionFeedbackAt=Time.time+.4f;Notify(rival?"BUMP":"WALL CONTACT");}
        }
    }
}
