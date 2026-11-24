#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KartLab
{
    // Runtime integration checks, instantiated only by the editor verification command.
    public class DemoVerification : MonoBehaviour
    {
        public bool Done { get; private set; }
        public int Failures { get; private set; }
        readonly List<string> results = new();
        KartMotor motor;
        KartInput input;
        readonly Vector3 testStart = new(150,.05f,0);
        void Check(string name,bool passed,string detail="")
        { results.Add((passed?"PASS":"FAIL")+" | "+name+" | "+detail); if(!passed)Failures++;Debug.Log(results[^1]); }
        IEnumerator Start()
        {
            motor=GameObject.Find("Player Kart").GetComponent<KartMotor>();input=motor.input;input.useExternalInput=true;
            foreach(var skills in FindObjectsByType<KartRaceSkills>())skills.enabled=false;
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-kartHornOnly")>=0)
            {yield return HornChecks();File.WriteAllLines("Evidence/horn-verification.txt",results);Done=true;yield break;}
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-kartCockpitOnly")>=0)
            {
                yield return new WaitForSeconds(3);
                var target=GameObject.Find("Moving target - no race AI").GetComponent<KartMotor>();
                Check("Practice target meets slipstream speed threshold",target.Speed>4,target.Speed.ToString("F2"));
                var driverView=motor.GetComponent<KartView>();
                Capture("Evidence/cockpit-preview.png",driverView.view);
                driverView.thirdPerson=true;yield return new WaitForSeconds(.1f);
                Capture("Evidence/chase-preview.png",driverView.view);
                var inspection=new GameObject("Complete kart inspection").AddComponent<Camera>();
                inspection.CopyFrom(driverView.view);inspection.fieldOfView=40;
                inspection.transform.position=motor.transform.TransformPoint(new Vector3(2.6f,1.9f,3));
                inspection.transform.LookAt(motor.transform.TransformPoint(new Vector3(0,.45f,0)));
                Capture("Evidence/complete-kart-preview.png",inspection);Destroy(inspection.gameObject);
                driverView.thirdPerson=false;
                input.externalInput=new DriveInput{throttle=.3f,steering=.4f};yield return new WaitForSeconds(.7f);
                Capture("Evidence/cockpit-turn-preview.png",driverView.view);
                input.externalInput=default;
                motor.GetComponent<KartInventory>().Pickup(ItemKind.Mushroom);yield return new WaitForSeconds(.8f);
                Capture("Evidence/cockpit-item-preview.png",driverView.view);
                File.WriteAllLines("Evidence/cockpit-verification.txt",results);Done=true;yield break;
            }
            foreach(var turret in FindObjectsByType<TestTurret>())turret.enabled=false;
            var recovery=motor.GetComponent<KartRecovery>();if(recovery)recovery.enabled=false;
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-kartRampOnly")>=0)
            {yield return RampChecks();File.WriteAllLines("Evidence/ramp-verification.txt",results);Done=true;yield break;}
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-kartRecoveryOnly")>=0)
            {yield return RecoveryChecks();File.WriteAllLines("Evidence/recovery-verification.txt",results);Done=true;yield break;}
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-kartEdgeOnly")>=0)
            {yield return ReentryChecks();File.WriteAllLines("Evidence/reentry-verification.txt",results);Done=true;yield break;}
            var floor=new GameObject("Verification surface").AddComponent<BoxCollider>();floor.transform.position=new Vector3(150,-.5f,55);floor.size=new Vector3(80,1,200);
            motor.Body.position=testStart;motor.Body.rotation=Quaternion.identity;motor.Body.linearVelocity=Vector3.zero;
            motor.SetCheckpoint(testStart,Quaternion.identity);
            yield return new WaitForSeconds(.3f);
            Check("Ground contact",motor.Grounded);
            Check("Wheel neutral",Mathf.Abs(KartInput.WheelAngle(new Vector3(-.2f,0,0),new Vector3(.2f,0,0)))<.01f);
            Check("Wheel signed tilt",KartInput.WheelAngle(new Vector3(-.2f,0,0),new Vector3(.2f,.2f,0))>20);
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(3.3f);
            Check("Acceleration and cap",motor.Speed>17.5f && motor.Speed<18.4f,motor.Speed.ToString("F2"));
            float z=motor.transform.position.z;Check("Forward displacement",z>15,z.ToString("F2"));
            var view=motor.GetComponent<KartView>();
            Check("Speed widens desktop FOV",view.view.fieldOfView>75&&view.view.fieldOfView<77,view.view.fieldOfView.ToString("F1"));
            motor.Body.position=new Vector3(150,.05f,40);motor.Body.rotation=Quaternion.identity;motor.Body.linearVelocity=Vector3.forward*18;
            input.externalInput=new DriveInput{throttle=1,steering=1};yield return new WaitForSeconds(.6f);
            Check("Tight steering sheds speed",motor.Speed<13.2f&&motor.Speed>10,motor.Speed.ToString("F2"));
            Vector3 cornerStart=motor.Body.position;Quaternion cornerHeading=motor.Body.rotation;
            yield return new WaitForSeconds(.82f);
            float yaw=Quaternion.Angle(cornerHeading,motor.Body.rotation);
            Check("Controlled 90 degree corner",yaw>84&&yaw<96,"yaw="+yaw.ToString("F1")+", speed="+motor.Speed.ToString("F1"));
            var cornerDelta=Quaternion.Inverse(cornerHeading)*(motor.Body.position-cornerStart);
            Check("Settled corner fits seven metre radius",Mathf.Abs(cornerDelta.x)<7.2f&&Mathf.Abs(cornerDelta.z)<7.2f,cornerDelta.ToString());
            motor.Body.position=new Vector3(150,.05f,20);motor.Body.rotation=Quaternion.identity;motor.Body.linearVelocity=Vector3.forward*18;
            input.externalInput=new DriveInput{throttle=1};
            motor.ApplyBoost(2);yield return new WaitForSeconds(.6f);
            Check("Turbo raises cap",motor.Speed>25 && motor.Speed<=26.3f,motor.Speed.ToString("F2"));
            Check("Boost camera widens further",view.view.fieldOfView>84,view.view.fieldOfView.ToString("F1"));
            motor.ApplyBoost(.6f);yield return new WaitForSeconds(.7f);
            Check("Short boost does not truncate active turbo",motor.Boosted);
            input.externalInput=new DriveInput{brake=1};yield return new WaitForSeconds(2.2f);
            Check("Brake then reverse",Vector3.Dot(motor.Body.linearVelocity,motor.transform.forward)<-.5f);
            input.externalInput=default;motor.SetCheckpoint(new Vector3(150,.05f,40),Quaternion.identity);motor.BeginReset();yield return new WaitForSeconds(.65f);
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(1.8f);
            input.externalInput=new DriveInput{throttle=1,steering=.45f,skill=true};yield return new WaitForSeconds(1.8f);
            Check("Drift second tier",motor.Drifting&&motor.Charge>=1.25f,"charge="+motor.Charge.ToString("F2")+", speed="+motor.Speed.ToString("F2")+", grounded="+motor.Grounded+", position="+motor.transform.position);
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(.1f);
            Check("Release drift awards boost",motor.Boosted);
            input.externalInput=default;motor.BeginReset();yield return new WaitForSeconds(.65f);
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(1.8f);
            input.externalInput=new DriveInput{throttle=1,steering=.7f,skill=true};yield return new WaitForSeconds(2.4f);
            Check("Drift third tier",motor.Drifting&&motor.DriftTier==3,motor.Charge.ToString("F2"));
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(.1f);
            Check("Third tier gives longer turbo",motor.BoostRemaining>1.55f,motor.BoostRemaining.ToString("F2"));
            input.externalInput=default;motor.SetCheckpoint(testStart,Quaternion.identity);motor.BeginReset();yield return new WaitForSeconds(.65f);
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(1.1f);
            input.externalInput=new DriveInput{throttle=1,skill=true};yield return new WaitForSeconds(.7f);
            Check("Straight hold charges jump",motor.Charging&&motor.Charge>=.59f);
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(.17f);
            Check("Jump leaves surface",motor.transform.position.y>.17f,motor.transform.position.y.ToString("F2"));
            yield return new WaitForSeconds(.9f);Check("Jump lands",motor.Grounded);
            input.externalInput=default;
            motor.ApplyShield();int hits=motor.HitCount;
            Check("Shield consumes one hit",!motor.ApplyHit()&&!motor.Shielded&&motor.HitCount==hits);
            Check("Protection prevents repeated hit",!motor.ApplyHit());yield return new WaitForSeconds(2.1f);
            Check("Attack applies slowdown",motor.ApplyHit()&&motor.SpeedFactor()<=.61f);
            yield return new WaitForSeconds(2.1f);Check("Slowdown expires",motor.SpeedFactor()>.99f);
            var a=new GameObject("Surface A");var b=new GameObject("Surface B");
            motor.SetSurface(a,.6f);motor.SetSurface(b,.8f);Check("Slow surfaces use strongest, not multiply",Mathf.Abs(motor.SpeedFactor()-.6f)<.01f);
            motor.ClearSurface(a);Check("Leaving strongest surface preserves remaining",Mathf.Abs(motor.SpeedFactor()-.8f)<.01f);motor.ClearSurface(b);
            var inventory=motor.GetComponent<KartInventory>();
            Check("Empty slot accepts item",inventory.Pickup(ItemKind.Star));Check("Roulette blocks another pickup",!inventory.Pickup(ItemKind.Mushroom));yield return new WaitForSeconds(.7f);
            Check("Roll delivers item",inventory.Held==ItemKind.Star);
            Check("Full slot refuses overwrite",!inventory.Pickup(ItemKind.Mushroom));Check("Use star consumes inventory",inventory.Use()&&motor.Invincible&&inventory.Held==ItemKind.None);
            Check("Star blocks damage",!motor.ApplyHit()&&motor.Invincible);
            inventory.Pickup(ItemKind.Mushroom);yield return new WaitForSeconds(.7f);Check("Use turbo",inventory.Use()&&motor.Boosted);
            inventory.Pickup(ItemKind.GreenShell);yield return new WaitForSeconds(.7f);Check("Fire projectile",inventory.Use()&&FindObjectsByType<KartProjectile>().Length>0);
            inventory.Pickup(ItemKind.Banana);yield return new WaitForSeconds(.7f);Check("Place trap",inventory.Use()&&FindObjectsByType<KartTrap>().Length>0);
            // A fresh target verifies the projectile's swept hit against a vehicle collider.
            var victim=new GameObject("Verification victim");victim.layer=8;victim.transform.position=new Vector3(160,.05f,20);
            var vc=victim.AddComponent<BoxCollider>();vc.center=Vector3.up*.4f;vc.size=new Vector3(1.2f,.8f,1.6f);
            victim.AddComponent<Rigidbody>();var vm=victim.AddComponent<KartMotor>();
            var shot=Instantiate(inventory.boltPrefab,new Vector3(160,.4f,15),Quaternion.identity);shot.GetComponent<KartProjectile>().Launch(motor);
            yield return new WaitForSeconds(.4f);Check("Projectile hits target",vm.HitCount==1);
            yield return new WaitForSeconds(2.1f);
            var trap=Instantiate(inventory.trapPrefab,vm.transform.position,Quaternion.identity);
            yield return new WaitForSeconds(.7f);Check("Trap arms and hits target",vm.HitCount==2);
            yield return new WaitForSeconds(2.1f);
            var ice=Instantiate(inventory.icePrefab,vm.transform.position-Vector3.forward*5+Vector3.up*.4f,Quaternion.identity);ice.GetComponent<KartProjectile>().Launch(motor);
            yield return new WaitForSeconds(.4f);Check("Ice shot freezes target",vm.Frozen&&vm.HitCount==3);
            inventory.Pickup(ItemKind.IceFlower);yield return new WaitForSeconds(.7f);
            Check("Ice flower begins with three shots",inventory.Charges==3&&inventory.Held==ItemKind.IceFlower);
            inventory.Use();Check("Ice retains two shots",inventory.Charges==2&&inventory.Held==ItemKind.IceFlower);
            yield return new WaitForSeconds(.3f);inventory.Use();yield return new WaitForSeconds(.3f);inventory.Use();Check("Third ice shot empties slot",inventory.Held==ItemKind.None);
            var wall=new GameObject("Shell test wall").AddComponent<BoxCollider>();wall.transform.position=new Vector3(175,.5f,30);wall.size=new Vector3(6,1,.4f);
            var bouncing=Instantiate(inventory.boltPrefab,new Vector3(175,.4f,26),Quaternion.identity).GetComponent<KartProjectile>();bouncing.Launch(motor);
            yield return new WaitForSeconds(.2f);Check("Green shell ricochets from wall",bouncing&&bouncing.BounceCount==1&&bouncing.transform.forward.z<-.9f);Destroy(wall.gameObject);
            inventory.Pickup(ItemKind.Star);yield return new WaitForSeconds(.7f);
            motor.Body.position=testStart+new Vector3(6,0,6);motor.ApplyBoost(2);motor.BeginReset();yield return new WaitForSeconds(.65f);
            Check("Reset returns to saved checkpoint",Vector3.Distance(motor.transform.position,testStart)<.2f);
            Check("Reset clears effects and speed",!motor.Boosted&&!motor.Shielded&&motor.Speed<.05f);
            Check("Reset preserves unused item",inventory.Held==ItemKind.Star);
            motor.Body.position=new Vector3(150,-7,0);yield return new WaitForSeconds(.7f);Check("Falling triggers reset",motor.transform.position.y>=-.1f);
            // Actual PhysX contacts: head-on, glancing and overlapping wall starts.
            var crashWall=new GameObject("Crash regression wall").AddComponent<BoxCollider>();crashWall.transform.position=new Vector3(150,1,20);crashWall.size=new Vector3(50,2,1);
            motor.SetCheckpoint(new Vector3(150,.05f,12),Quaternion.identity);motor.BeginReset();yield return new WaitForSeconds(.65f);
            motor.Body.linearVelocity=Vector3.forward*18;input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(1.2f);
            Check("Head-on wall does not launch kart",motor.Speed<1&&Mathf.Abs(motor.transform.position.y)<.15f,"speed="+motor.Speed.ToString("F2")+", y="+motor.transform.position.y.ToString("F2"));
            Check("Wall impact does not spin kart",Quaternion.Angle(motor.Body.rotation,Quaternion.identity)<1&&motor.Body.angularVelocity.magnitude<.01f);
            input.externalInput=new DriveInput{throttle=1,steering=1};yield return new WaitForSeconds(1.8f);
            Check("Steering can escape a stopped wall contact",motor.Body.position.x>150.5f&&motor.Body.rotation.eulerAngles.y>60&&motor.Body.rotation.eulerAngles.y<135,"position="+motor.Body.position+", yaw="+motor.Body.rotation.eulerAngles.y.ToString("F1"));
            input.externalInput=default;motor.SetCheckpoint(new Vector3(150,.05f,12),Quaternion.Euler(0,45,0));motor.BeginReset();yield return new WaitForSeconds(.65f);
            motor.Body.linearVelocity=motor.transform.forward*18;input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(1.1f);
            Check("Glancing hit slides along wall",motor.Body.linearVelocity.x>2&&motor.Body.linearVelocity.z<.5f,motor.Body.linearVelocity.ToString());
            Check("Glancing hit preserves steering heading",Quaternion.Angle(motor.Body.rotation,Quaternion.Euler(0,45,0))<1&&Mathf.Abs(motor.transform.position.y)<.15f);
            input.externalInput=default;motor.SetCheckpoint(new Vector3(150,.05f,19.1f),Quaternion.identity);motor.BeginReset();yield return new WaitForSeconds(.65f);
            yield return new WaitForSeconds(.6f);Check("Overlapping start separates gently",motor.Speed<2.1f&&Vector3.Distance(motor.Body.position,new Vector3(150,.05f,19.1f))<1.5f);
            Destroy(crashWall.gameObject);
            motor.SetCheckpoint(new Vector3(150,.05f,10),Quaternion.identity);motor.BeginReset();yield return new WaitForSeconds(.65f);
            motor.Body.linearVelocity=Vector3.right*6;yield return new WaitForSeconds(.25f);
            Check("Tyre grip damps sideways impulse",Mathf.Abs(motor.Body.linearVelocity.x)<.2f&&motor.Body.position.x<151.2f,motor.Body.linearVelocity.ToString());
            yield return RampChecks();
            yield return DriverFeatureChecks();
            yield return ReentryChecks();
            yield return RecoveryChecks();
            motor.SetCheckpoint(new Vector3(24,.27f,-24),Quaternion.identity);motor.BeginReset();yield return new WaitForSeconds(.65f);
            motor.GetComponent<KartView>().thirdPerson=true;
            yield return null;
            Capture("Evidence/chase-preview.png",motor.GetComponent<KartView>().view);
            motor.GetComponent<KartView>().thirdPerson=false;yield return null;yield return null;
            Capture("Evidence/cockpit-preview.png",motor.GetComponent<KartView>().view);
            var overview=new GameObject("Overview").AddComponent<Camera>();overview.transform.position=new Vector3(78,100,-100);overview.transform.LookAt(Vector3.zero);overview.fieldOfView=48;overview.backgroundColor=new Color(.7f,.83f,.86f);overview.clearFlags=CameraClearFlags.SolidColor;
            Capture("Evidence/course-preview.png",overview);
            File.WriteAllLines("Evidence/verification.txt",results);
            Done=true;
        }
        IEnumerator RampChecks()
        {
            var others=new List<GameObject>();foreach(var kart in FindObjectsByType<KartMotor>())if(kart!=motor){others.Add(kart.gameObject);kart.gameObject.SetActive(false);}
            var recovery=motor.GetComponent<KartRecovery>();bool active=recovery&&recovery.enabled;if(recovery)recovery.enabled=false;
            bool surface=Physics.Raycast(new Vector3(-24,2,12.1f),Vector3.down,out var rampHit,4,1,QueryTriggerInteraction.Ignore)&&Physics.Raycast(new Vector3(-24,2,11.8f),Vector3.down,out var roadHit,4,1,QueryTriggerInteraction.Ignore);
            Physics.Raycast(new Vector3(-24,2,11.8f),Vector3.down,out var before,4,1,QueryTriggerInteraction.Ignore);
            Check("Ramp entrance meets road within 5mm",surface&&Mathf.Abs(rampHit.point.y-before.point.y)<.005f,"ramp="+rampHit.point.y.ToString("F3")+", road="+before.point.y.ToString("F3"));
            for(int n=0;n<4;n++)
            {
                float start=n==1?23:11,yaw=n==1?180:0;
                input.externalInput=default;motor.SetCheckpoint(new Vector3(n==3?-21.8f:-24,.2f,start),Quaternion.Euler(0,yaw,0));motor.BeginReset();yield return new WaitForSeconds(.65f);
                input.externalInput=new DriveInput{throttle=.12f};float end=Time.time+9,maxY=0;bool stopped=false;
                while(Time.time<end)
                {
                    maxY=Mathf.Max(maxY,motor.Body.position.y);
                    if(n==2&&!stopped&&motor.Body.position.z>16.3f){input.externalInput=new DriveInput{brake=1};yield return new WaitForSeconds(.3f);input.externalInput=new DriveInput{throttle=.12f};stopped=true;}
                    if((n==1&&motor.Body.position.z<11)||(n!=1&&motor.Body.position.z>23))break;
                    yield return null;
                }
                Check(n==0?"Low speed ramp forward crossing":n==1?"Low speed ramp opposite crossing":n==2?"Stop and restart on ramp":"Off-centre low speed ramp crossing",(n==1?motor.Body.position.z<11:motor.Body.position.z>23)&&motor.Speed>1&&maxY>.4f&&!motor.Resetting,"position="+motor.Body.position+", maxY="+maxY.ToString("F2"));
            }
            input.externalInput=default;foreach(var other in others)if(other)other.SetActive(true);if(recovery)recovery.enabled=active;
        }
        IEnumerator DriverFeatureChecks()
        {
            var skills=motor.GetComponent<KartRaceSkills>();skills.slipstreamEnabled=false;skills.enabled=true;
            input.externalInput=default;motor.SetCheckpoint(new Vector3(150,.05f,25),Quaternion.identity);motor.BeginReset();yield return new WaitForSeconds(.65f);
            skills.BeginCountdown();input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(1);
            Check("Countdown holds vehicle at start",motor.Speed<.05f&&skills.CountingDown);
            yield return new WaitForSeconds(2.15f);Check("Early throttle gives no rocket boost",!skills.CountingDown&&skills.RocketStarts==0&&!motor.Boosted);yield return new WaitForSeconds(skills.stallDuration);
            input.externalInput=default;motor.BeginReset();yield return new WaitForSeconds(.65f);
            skills.BeginCountdown();yield return new WaitForSeconds(1.2f);input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(1.95f);
            Check("Timed throttle earns rocket start",skills.RocketStarts==1&&motor.Boosted&&!motor.driveLocked);
            skills.BeginCountdown();skills.enabled=false;Check("Disabled start controller releases drive lock",!motor.driveLocked);skills.enabled=true;
            input.externalInput=default;motor.BeginReset();yield return new WaitForSeconds(.65f);
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(1.2f);
            motor.Body.linearVelocity=motor.transform.forward*12+Vector3.up*4;yield return new WaitForSeconds(.2f);
            input.externalInput=new DriveInput{throttle=1,skill=true};yield return new WaitForSeconds(.1f);Check("Fresh airborne skill press arms trick",skills.TrickArmed);
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(.7f);Check("Trick landing rewards boost",skills.SuccessfulTricks==1&&motor.Boosted);
            input.externalInput=default;motor.BeginReset();yield return new WaitForSeconds(.65f);
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(1.4f);input.externalInput=new DriveInput{throttle=1,steering=.7f,skill=true};yield return new WaitForSeconds(.6f);
            Check("Drift develops bounded lateral slip",Mathf.Abs(motor.SlipSpeed)>.5f&&Mathf.Abs(motor.SlipSpeed)<2.4f,motor.SlipSpeed.ToString("F2"));
            input.externalInput=new DriveInput{throttle=1};yield return new WaitForSeconds(.2f);Check("Release drift restores tyre grip",Mathf.Abs(motor.SlipSpeed)<.2f,motor.SlipSpeed.ToString("F2"));
            input.externalInput=default;motor.BeginReset();yield return new WaitForSeconds(.65f);
            motor.assistedDriving=false;motor.Body.linearVelocity=Vector3.forward*18;input.externalInput=new DriveInput{throttle=1,steering=1};yield return new WaitForSeconds(.6f);
            Check("Standard mode keeps speed and requires braking",motor.Speed>17,motor.Speed.ToString("F2"));motor.assistedDriving=true;
            input.externalInput=default;motor.BeginReset();yield return new WaitForSeconds(.65f);
            var inventory=motor.GetComponent<KartInventory>();if(inventory.Held!=ItemKind.None){inventory.Use();yield return new WaitForSeconds(.3f);}motor.BeginReset();yield return new WaitForSeconds(.65f);
            inventory.Pickup(ItemKind.GreenShell);yield return new WaitForSeconds(.7f);inventory.Use(true);yield return null;
            bool backwards=false;foreach(var shot in FindObjectsByType<KartProjectile>())if(Vector3.Distance(shot.transform.position,motor.transform.position)<4&&Vector3.Dot(shot.transform.forward,motor.transform.forward)<-.9f)backwards=true;
            Check("Backward item launches behind the driver",backwards);
            var cockpit=motor.GetComponent<KartCockpit>();inventory.Pickup(ItemKind.IceFlower);yield return new WaitForSeconds(.7f);
            Check("Held 3D item matches inventory",cockpit.itemModels[(int)ItemKind.IceFlower].activeSelf&&!cockpit.itemModels[(int)ItemKind.GreenShell].activeSelf);
            input.externalInput=default;motor.BeginReset();yield return new WaitForSeconds(.65f);skills.slipstreamEnabled=true;
            var rival=new GameObject("Draft verification rival");rival.layer=8;var rc=rival.AddComponent<BoxCollider>();rc.size=new Vector3(1,.6f,1.4f);rc.center=Vector3.up*.3f;rival.AddComponent<Rigidbody>();var ri=rival.AddComponent<KartInput>();ri.useExternalInput=true;ri.externalInput=new DriveInput{throttle=1};var rm=rival.AddComponent<KartMotor>();rm.input=ri;rm.topSpeed=12;
            motor.Body.position=new Vector3(150,.05f,15);motor.Body.rotation=Quaternion.identity;motor.Body.linearVelocity=Vector3.forward*12;input.externalInput=new DriveInput{throttle=.67f};rm.Body.position=new Vector3(150,.05f,23);rm.Body.rotation=Quaternion.identity;rm.Body.linearVelocity=Vector3.forward*12;
            float end=Time.time+3;bool earned=false;while(Time.time<end){if(motor.LastEvent=="SLIPSTREAM BOOST"){earned=true;break;}yield return null;}
            Check("Following a moving kart earns slipstream",earned);Destroy(rival);skills.enabled=false;
            input.externalInput=default;motor.BeginReset();yield return new WaitForSeconds(.65f);
        }
        IEnumerator ReentryChecks()
        {
            var starts=new[]{new Vector3(16,.03f,-18),new Vector3(-16,.03f,-18),new Vector3(16,.03f,-18),new Vector3(16,.03f,-18),new Vector3(17.98f,.25f,-18),new Vector3(18,.03f,30)};
            float[] headings={90,270,60,270,90,45};
            string[] names={"Right straight grass reentry","Left straight grass reentry","Diagonal grass reentry","Reverse grass reentry","Restart straddling road edge","Curved road grass reentry"};
            for(int n=0;n<starts.Length;n++)
            {
                input.externalInput=default;motor.SetCheckpoint(starts[n],Quaternion.Euler(0,headings[n],0));motor.BeginReset();yield return new WaitForSeconds(.7f);
                input.externalInput=n==3?new DriveInput{brake=1}:new DriveInput{throttle=.35f};
                yield return new WaitForSeconds(n==3?2.6f:2.1f);
                float x=motor.Body.position.x;
                Check(names[n],(n==1?x< -21:x>21)&&motor.Grounded&&motor.Speed>1&&!motor.Resetting&&motor.SpeedFactor()>.99f,"position="+motor.Body.position+", speed="+motor.Speed.ToString("F2")+", grounded="+motor.Grounded);
            }
            input.externalInput=default;
            var floor=new GameObject("Dynamic obstacle test floor").AddComponent<BoxCollider>();floor.transform.position=new Vector3(1000,-.5f,1000);floor.size=new Vector3(10,1,10);
            var obstacle=new GameObject("Low dynamic obstacle").AddComponent<BoxCollider>();obstacle.transform.position=new Vector3(1000,.1f,1002);obstacle.size=new Vector3(6,.2f,1);obstacle.gameObject.AddComponent<Rigidbody>().isKinematic=true;
            motor.SetCheckpoint(new Vector3(1000,.03f,999),Quaternion.identity);motor.BeginReset();yield return new WaitForSeconds(.7f);
            input.externalInput=new DriveInput{throttle=.35f};yield return new WaitForSeconds(1.5f);
            Check("Dynamic bodies are not climbable kerbs",motor.Body.position.z<1001&&motor.Body.position.y<.05f&&motor.Speed<1,motor.Body.position.ToString());
            input.externalInput=default;Destroy(obstacle.gameObject);Destroy(floor.gameObject);
        }
        IEnumerator RecoveryChecks()
        {
            var recovery=motor.GetComponent<KartRecovery>();
            if(!recovery){Check("Recovery component installed",false);yield break;}
            input.externalInput=default;motor.SetCheckpoint(new Vector3(24,.18f,-18),Quaternion.identity);motor.BeginReset();yield return new WaitForSeconds(.7f);
            recovery.route=FindFirstObjectByType<TrackRoute>();recovery.enabled=true;yield return new WaitForSeconds(.4f);
            Pose saved=recovery.LastRoadPose;int count=recovery.RecoveryCount;var inventory=motor.GetComponent<KartInventory>();ItemKind item=inventory.Held;
            motor.Body.position=new Vector3(14,.03f,-18);yield return new WaitForSeconds(.35f);
            Check("Brief excursion has recovery grace",recovery.RecoveryCount==count&&!motor.Resetting);
            motor.Body.position=new Vector3(14,.03f,-8);yield return new WaitForSeconds(1.4f);
            Check("Off-track returns to exit point centre",recovery.RecoveryCount==count+1&&Vector3.Distance(motor.Body.position,saved.position)<.3f,motor.Body.position.ToString());
            Check("Recovery aligns with track and retains item",Quaternion.Angle(motor.Body.rotation,saved.rotation)<1&&inventory.Held==item);
            yield return new WaitForSeconds(.5f);Check("Recovery does not loop",recovery.RecoveryCount==count+1);
            count=recovery.RecoveryCount;motor.Body.position=new Vector3(14,.03f,-18);yield return new WaitForSeconds(.3f);motor.Body.position=saved.position;yield return new WaitForSeconds(.9f);
            Check("Returning before timeout cancels rescue",recovery.RecoveryCount==count);
            motor.Body.position=saved.position+Vector3.up*.4f;motor.Body.linearVelocity=Vector3.up*2;yield return new WaitForSeconds(1.2f);
            Check("Small jumps stay out of rescue",recovery.RecoveryCount==count&&motor.Grounded);
            // A curved exit restores the local tangent, not the kart's sideways heading.
            recovery.enabled=false;Vector3 curve=new Vector3(22.2426f,.18f,34.2426f);motor.SetCheckpoint(curve,Quaternion.identity);motor.BeginReset();yield return new WaitForSeconds(.7f);
            recovery.enabled=true;yield return new WaitForSeconds(.4f);saved=recovery.LastRoadPose;count=recovery.RecoveryCount;
            motor.Body.position=new Vector3(18,.03f,30);yield return new WaitForSeconds(1.75f);
            Check("Curve rescue uses last centre and tangent",recovery.RecoveryCount==count+1&&Vector3.Distance(motor.Body.position,saved.position)<.3f&&Quaternion.Angle(motor.Body.rotation,saved.rotation)<1);
            recovery.enabled=false;
            var wall=new GameObject("Edge obstruction regression").AddComponent<BoxCollider>();wall.transform.position=new Vector3(19,1,-10);wall.size=new Vector3(.2f,2,6);
            motor.SetCheckpoint(new Vector3(20,.18f,-10),Quaternion.Euler(0,270,0));motor.BeginReset();yield return new WaitForSeconds(.7f);
            recovery.enabled=true;yield return new WaitForSeconds(.4f);count=recovery.RecoveryCount;input.externalInput=new DriveInput{throttle=.3f};
            float deadline=Time.time+4;while(recovery.RecoveryCount==count&&Time.time<deadline)yield return null;
            input.externalInput=default;yield return new WaitForSeconds(.65f);
            Check("Stuck at road edge automatically rescues",recovery.RecoveryCount==count+1&&Mathf.Abs(motor.Body.position.x-24)<.3f,motor.Body.position.ToString());
            recovery.enabled=false;Destroy(wall.gameObject);
        }
        IEnumerator HornChecks()
        {
            foreach(var turret in FindObjectsByType<TestTurret>())turret.enabled=false;
            var horn=motor.GetComponent<KartHorn>();
            var inventory=motor.GetComponent<KartInventory>();
            inventory.Pickup(ItemKind.Mushroom);yield return new WaitForSeconds(.8f);
            Check("Sustain horn clip preloaded",horn&&horn.clip&&horn.clip.loadState==AudioDataLoadState.Loaded);
            input.externalInput=new DriveInput{horn=true};yield return new WaitForSeconds(.04f);
            Check("Press starts horn audio",horn.Sounding);
            input.externalInput=default;yield return new WaitForSeconds(.02f);
            Check("Short tap has an audible minimum pulse",horn.Sounding);
            yield return new WaitForSeconds(.2f);
            Check("Tap ends without remaining stuck on",!horn.Sounding);
            bool rapid=true, released=true;
            for(int n=0;n<4;n++)
            {
                input.externalInput=new DriveInput{horn=true};yield return new WaitForSeconds(.05f);
                rapid&=horn.Sounding;
                input.externalInput=default;yield return new WaitForSeconds(.13f);
                released&=!horn.Sounding;
            }
            Check("Four rapid presses all sound without cooldown",rapid);
            Check("Rapid presses remain separate beeps",released);
            input.externalInput=new DriveInput{horn=true};yield return new WaitForSeconds(1.2f);
            Check("Held button sustains past the recording length",horn.Sounding);
            input.externalInput=default;yield return new WaitForSeconds(.1f);
            Check("Release promptly stops sustained horn",!horn.Sounding);
            input.externalInput=new DriveInput{horn=true};yield return new WaitForSeconds(.05f);
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.08f);
            Check("Pause stops active horn",!horn.Sounding);
            Time.timeScale=1;yield return new WaitForSeconds(.1f);
            Check("Held input does not honk on resume",!horn.Sounding);
            input.externalInput=default;yield return new WaitForSeconds(.04f);
            input.externalInput=new DriveInput{horn=true};yield return new WaitForSeconds(.04f);
            Check("New press after pause works",horn.Sounding);
            Check("Horn does not consume held item",inventory.Held==ItemKind.Mushroom);
            input.externalInput=default;
            yield return new WaitForSeconds(.15f);
        }

        static void Capture(string path,Camera camera)
        {
            var rt=new RenderTexture(1440,900,24);camera.targetTexture=rt;camera.Render();
            RenderTexture.active=rt;var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Destroy(texture);rt.Release();Destroy(rt);
        }
    }
}
#endif
