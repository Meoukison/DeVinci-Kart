using UnityEngine;
using UnityEngine.UI;

namespace KartLab
{
    public class KartHUD : MonoBehaviour
    {
        public KartMotor motor;
        public KartView kartView;
        public Text dashText;
        public AudioClip pickupSound, hitSound, boostSound;
        public AudioSource audioSource;
        public string CourseName = "APEX / KART CLUB";
        float eventUntil;
        string message = "";
        GUIStyle title, small, number, accent, tiny, centered, itemTitle;
        bool hasDriven;
        KartHorn horn;
        void Awake(){horn=GetComponent<KartHorn>();}
        static readonly Color Cream=new(1,.97f,.89f), Dark=new(.025f,.065f,.11f,.94f), Mint=new(.3f,1,.73f);
        void OnEnable() { if (motor) motor.Feedback += OnFeedback; }
        void OnDisable() { if (motor) motor.Feedback -= OnFeedback; }
        void OnFeedback(string text)
        {
            message = text; eventUntil = Time.time + 1.65f;
            if (audioSource && text!="DRIFT")
            {
                audioSource.pitch=text.StartsWith("DRIFT CHARGE")?1+(motor.DriftTier*.22f):1;
                var clip = text.Contains("HIT") || text.Contains("WALL") ? hitSound : text.Contains("TURBO") || text.Contains("MUSHROOM") ? boostSound : pickupSound;
                if (clip) audioSource.PlayOneShot(clip, .35f);
            }
        }
        void Update()
        {
            if(motor.Speed>2)hasDriven=true;
            if (!dashText) return;
            var inv = motor.GetComponent<KartInventory>();
            dashText.text=Time.timeScale==0?(motor.input.XRActive?"PAUSED | LEFT PRIMARY: OPTION / TRIGGERS: ADJUST | "+kartView.VRSettingLabel:"PAUSED / ESC TO RESUME"):motor.input.XRActive&&!motor.input.WheelHeld?"HOLD BOTH GRIPS / TWO-HAND WHEEL":Time.time<eventUntil?message:"APEX MOTORSPORT / DRIVER LAB";
            if(Time.timeScale>0&&horn&&horn.Sounding)dashText.text="BEEP BEEP / HORN";
        }
        void Styles()
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };title.normal.textColor=Cream;
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };small.normal.textColor=new Color(.69f,.8f,.87f);
            tiny=new GUIStyle(small){fontSize=11};
            number = new GUIStyle(title) { fontSize = 64 };
            accent = new GUIStyle(title);accent.normal.textColor=Mint;
            itemTitle=new GUIStyle(title){fontSize=18};
            centered=new GUIStyle(title){alignment=TextAnchor.MiddleCenter};
        }
        void Card(Rect r,Color c)
        {GUI.color=Color.white;GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,0,Mathf.Min(12,r.height*.5f));}

        void Bar(Rect r,float value,Color color)
        {Card(r,new Color(.15f,.23f,.28f));if(value>.001f)Card(new Rect(r.x,r.y,r.width*Mathf.Clamp01(value),r.height),color);}
        void SpeedLines(float height)
        {
            if(!motor.Boosted||motor.Speed<6||Time.timeScale==0)return;
            Vector2 center=new Vector2(720,height*.48f);
            Matrix4x4 original=GUI.matrix;
            for(int n=0;n<22;n++)
            {
                float angle=n*360f/22+8;
                Vector2 direction=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
                float phase=Mathf.Repeat(Time.time*1.8f+n*.137f,1);
                float distance=Mathf.Lerp(390,850,phase);
                Vector2 start=center+direction*distance;
                GUIUtility.RotateAroundPivot(angle,start);GUI.color=new Color(.8f,.95f,1,(1-phase)*.42f);
                GUI.DrawTexture(new Rect(start.x,start.y,35+phase*90,2),Texture2D.whiteTexture);GUI.matrix=original;
            }
            GUI.color=Color.white;
        }
        void Settings(float h)
        {
            float y=h*.22f;Card(new Rect(430,y,580,420),Dark);
            GUI.Label(new Rect(460,y+20,520,35),"DRIVING SETUP",title);
            motor.assistedDriving=GUI.Toggle(new Rect(460,y+70,500,25),motor.assistedDriving," Assisted corner braking (recommended)");
            GUI.Label(new Rect(460,y+108,500,25),"Wheel rotation range  /  "+motor.input.wheelRange.ToString("F0")+"°",small);
            motor.input.wheelRange=GUI.HorizontalSlider(new Rect(460,y+141,490,20),motor.input.wheelRange,35,100);
            GUI.Label(new Rect(460,y+165,500,25),"Seat height",small);kartView.seatHeight=GUI.HorizontalSlider(new Rect(460,y+195,490,20),kartView.seatHeight,-.12f,.18f);
            GUI.Label(new Rect(460,y+220,500,25),"Wheel reach",small);kartView.wheelDistance=GUI.HorizontalSlider(new Rect(460,y+250,490,20),kartView.wheelDistance,-.12f,.15f);
            kartView.comfortVignette=GUI.Toggle(new Rect(460,y+281,500,25),kartView.comfortVignette," VR comfort vignette");
            if(GUI.Button(new Rect(460,y+326,240,40),"RECENTER / CALIBRATE"))kartView.Recenter();
            if(GUI.Button(new Rect(720,y+326,240,40),"SAVE AND DRIVE")){kartView.SavePreferences();Time.timeScale=1;}
            GUI.Label(new Rect(460,y+378,405,30),"Audio: domasx2 / qubodup (CC0)\nTyre skid: Tom Haigh (CC BY 3.0)",tiny);
            if(GUI.Button(new Rect(865,y+379,95,28),"CREDITS"))Application.OpenURL(System.IO.Path.Combine(Application.streamingAssetsPath,"KartLab-Credits.txt"));
        }

        void OnGUI()
        {
            if (!motor || motor.input.XRActive) return;
            if (title == null) Styles();
            float scale = Screen.width / 1440f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale,scale,1));
            float h = Screen.height / scale;
            SpeedLines(h);
            Card(new Rect(28,26,300,76),Dark);
            GUI.Label(new Rect(46,34,265,30),CourseName,title);
            var recovery=motor.GetComponent<KartRecovery>();
            GUI.Label(new Rect(47,70,265,23),recovery&&recovery.enabled&&recovery.route?"SUNSET CIRCUIT   /   AUTO RETURN ON":"SUNSET CIRCUIT   /   PRACTICE",tiny);
            if(kartView.thirdPerson){
            Card(new Rect(28,h-144,285,116),Dark);
            GUI.Label(new Rect(46,h-146,150,78),(motor.Speed*3.6f).ToString("00"),number);
            GUI.Label(new Rect(171,h-119,115,24),"KM/H",small);
            GUI.Label(new Rect(171,h-92,130,28),motor.Boosted?"BOOST!":motor.Frozen?"FROZEN":"CRUISE",accent);
            Bar(new Rect(48,h-51,242,8),motor.Speed/(motor.topSpeed*motor.boostMultiplier),motor.Boosted?new Color(1,.66f,.17f):Mint);
            }
            var inv=motor.GetComponent<KartInventory>();
            Card(new Rect(1094,26,318,130),Dark);
            ItemKind display=inv.Rolling?(ItemKind)(1+(int)(Time.time*14)%5):inv.Held;
            Card(new Rect(1107,39,92,92),new Color(.14f,.23f,.31f,1));
            var icon=inv.Icon(display);if(icon)GUI.DrawTexture(new Rect(1108,40,90,90),icon,ScaleMode.ScaleToFit,true);
            else GUI.Label(new Rect(1128,63,55,45),"?",centered);
            GUI.Label(new Rect(1211,40,184,22),"E FORWARD  /  Q BACK",tiny);
            GUI.Label(new Rect(1211,67,192,33),inv.Rolling?"ROLLING...":KartInventory.Name(inv.Held),itemTitle);
            GUI.Label(new Rect(1211,105,190,30),inv.Held==ItemKind.IceFlower?inv.Charges+" ICE SHOTS":motor.Invincible?"STAR  "+motor.StarRemaining.ToString("F1")+"s":"COLLECT A ? BOX",tiny);
            if(motor.Drifting||motor.Charging)
            {
                Card(new Rect(520,h-105,400,67),Dark);
                GUI.Label(new Rect(541,h-96,350,28),motor.Drifting?"DRIFT  /  "+(motor.DriftTier==3?"ULTRA":motor.DriftTier==2?"SUPER":"MINI TURBO"):"CHARGE JUMP",itemTitle);
                Bar(new Rect(541,h-58,358,8),motor.Charge/(motor.Drifting?2.1f:.6f),KartEffects.TierColor(motor.DriftTier));
            }
            Card(new Rect(1016,h-110,396,82),Dark);
            GUI.Label(new Rect(1034,h-100,365,67),"WASD DRIVE / SPACE DRIFT / H HORN\nE / Q ITEM  /  C VIEW  /  HOLD R RECOVER\nENTER START PRACTICE  /  ESC SETTINGS",tiny);
            if(!hasDriven && Time.timeScale>0)
            {
                Card(new Rect(460,h*.30f,520,132),Dark);
                GUI.Label(new Rect(478,h*.30f+12,484,38),"TAKE THE WHEEL",centered);
                var hint=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};
                GUI.Label(new Rect(478,h*.30f+59,484,55),"Hold W to drive. A / D steer. S brakes.\nENTER: 3–2–1 start. Apply throttle in the final second.",hint);
            }
            if(Time.timeScale==0 || (Time.time<eventUntil && message!="DRIFT"))
            {
                Card(new Rect(470,28,500,50),Dark);
                GUI.Label(new Rect(487,37,466,34),Time.timeScale==0?"PAUSED  /  ESC TO RESUME":message,centered);
            }
            var skills=motor.GetComponent<KartRaceSkills>();
            if(skills&&skills.CountingDown){Card(new Rect(650,h*.28f,140,110),Dark);GUI.Label(new Rect(650,h*.28f,140,110),skills.CountdownLabel,new GUIStyle(number){alignment=TextAnchor.MiddleCenter});}
            if(Time.timeScale==0)Settings(h);
            if (motor.Resetting) {GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,1440,h),Texture2D.whiteTexture);GUI.color=Color.white;}
        }
    }
}
