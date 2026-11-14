using UnityEngine;
using UnityEngine.XR;

namespace KartLab
{
    public class KartView : MonoBehaviour
    {
        public KartMotor motor;
        public Camera view;
        public Transform trackingSpace, wheel;
        public GameObject cockpit;
        public GameObject exterior;
        public Vector3 seat = new(0, 1.22f, -.30f);
        public bool thirdPerson = false;
        public float wheelDistance, seatHeight;
        public void Recenter(){calibrated=false;motor.input.Recalibrate();}
        public void SavePreferences(){PlayerPrefs.SetInt("KartAssist",motor.assistedDriving?1:0);PlayerPrefs.SetFloat("KartSeat",seatHeight);PlayerPrefs.SetFloat("KartWheelDistance",wheelDistance);PlayerPrefs.SetFloat("KartWheelRange",motor.input.wheelRange);PlayerPrefs.SetInt("KartComfort",comfortVignette?1:0);PlayerPrefs.Save();}
        public bool comfortVignette = true;
        public Shader fadeShader, vignetteShader;
        bool oldCamera, oldPause, calibrated, oldMenuUse, oldMenuSkill;
        int vrSetting;
        public string VRSettingLabel => vrSetting switch{0=>"ASSIST "+motor.assistedDriving,1=>"STEERING "+motor.input.wheelRange.ToString("F0"),2=>"SEAT "+seatHeight.ToString("F2"),3=>"REACH "+wheelDistance.ToString("F2"),4=>"VIGNETTE "+comfortVignette,_=>"RIGHT PRIMARY: RECENTER"};
        Vector3 headOrigin;
        Quaternion headYaw;
        Material fadeMaterial;
        GameObject fadeQuad;
        GameObject vignetteQuad;
        Material vignetteMaterial;
        Texture2D vignetteTexture;
        float speedBlend, boostBlend;
        Quaternion cameraHeading;
        bool cameraInitialized;
        void Start()
        {
            view.nearClipPlane = .04f;
            if(!motor.input.useExternalInput){motor.assistedDriving=PlayerPrefs.GetInt("KartAssist",1)==1;seatHeight=PlayerPrefs.GetFloat("KartSeat",0);wheelDistance=PlayerPrefs.GetFloat("KartWheelDistance",0);motor.input.wheelRange=PlayerPrefs.GetFloat("KartWheelRange",65);comfortVignette=PlayerPrefs.GetInt("KartComfort",1)==1;}
            fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fadeQuad.name = "Reset fade";
            Destroy(fadeQuad.GetComponent<Collider>());
            fadeQuad.transform.SetParent(view.transform, false);
            fadeQuad.transform.localPosition = new Vector3(0, 0, .08f);
            fadeQuad.transform.localScale = Vector3.one;
            fadeMaterial = new Material(fadeShader ? fadeShader : Shader.Find("Unlit/Color"));
            fadeMaterial.color = Color.black;
            fadeMaterial.renderQueue = 4000;
            fadeQuad.GetComponent<MeshRenderer>().sharedMaterial = fadeMaterial;
            fadeQuad.SetActive(false);
            vignetteQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vignetteQuad.name = "Comfort peripheral dimming";
            Destroy(vignetteQuad.GetComponent<Collider>());
            vignetteQuad.transform.SetParent(view.transform,false);
            vignetteQuad.transform.localPosition = new Vector3(0,0,.09f);
            vignetteQuad.transform.localScale = new Vector3(.28f,.28f,1);
            vignetteTexture = new Texture2D(128,128,TextureFormat.RGBA32,false);
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float distance = Vector2.Distance(new Vector2(x/127f,y/127f),Vector2.one*.5f)*2;
                vignetteTexture.SetPixel(x,y,new Color(0,0,0,Mathf.SmoothStep(0,.6f,Mathf.InverseLerp(.48f,.95f,distance))));
            }
            vignetteTexture.Apply();
            vignetteMaterial = new Material(vignetteShader ? vignetteShader : Shader.Find("Unlit/Transparent"));
            vignetteMaterial.mainTexture = vignetteTexture;vignetteMaterial.renderQueue=3900;
            vignetteQuad.GetComponent<MeshRenderer>().sharedMaterial=vignetteMaterial;
            vignetteQuad.SetActive(false);
        }
        void Update()
        {
            var i = motor.input.Current;
            if (i.camera && !oldCamera && !motor.input.XRActive) thirdPerson = !thirdPerson;
            if (i.pause && !oldPause){Time.timeScale = Time.timeScale == 0 ? 1 : 0;if(Time.timeScale>0)SavePreferences();}
            if(motor.input.XRActive&&Time.timeScale==0)
            {
                if(i.use&&!oldMenuUse)vrSetting=(vrSetting+1)%6;
                float change=(i.throttle-i.brake)*Time.unscaledDeltaTime;
                if(vrSetting==0&&i.skill&&!oldMenuSkill)motor.assistedDriving=!motor.assistedDriving;
                if(vrSetting==1)motor.input.wheelRange=Mathf.Clamp(motor.input.wheelRange+change*25,35,100);
                if(vrSetting==2)seatHeight=Mathf.Clamp(seatHeight+change*.08f,-.12f,.18f);
                if(vrSetting==3)wheelDistance=Mathf.Clamp(wheelDistance+change*.08f,-.12f,.15f);
                if(vrSetting==4&&i.skill&&!oldMenuSkill)comfortVignette=!comfortVignette;
                if(vrSetting==5&&i.skill&&!oldMenuSkill)Recenter();
            }
            oldMenuUse=i.use;oldMenuSkill=i.skill;
            oldCamera = i.camera; oldPause = i.pause;
        }
        void LateUpdate()
        {
            bool xr = motor.input.XRActive;
            if (xr)
            {
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                bool tracked = head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p)
                    && head.TryGetFeatureValue(CommonUsages.isTracked, out bool valid) && valid;
                head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion q);
                if (tracked)
                {
                    if (!calibrated)
                    {
                        headOrigin = p;
                        headYaw = Quaternion.Euler(0, q.eulerAngles.y, 0);
                        calibrated = true;
                    }
                    trackingSpace.SetPositionAndRotation(motor.transform.TransformPoint(seat+Vector3.up*seatHeight) - motor.transform.rotation * Quaternion.Inverse(headYaw) * headOrigin,
                        motor.transform.rotation * Quaternion.Inverse(headYaw));
                    view.transform.SetParent(trackingSpace, false);
                    view.transform.SetLocalPositionAndRotation(p, q);
                }
            }
            else
            {
                calibrated = false;
                view.transform.SetParent(null);
                float dt=Time.deltaTime;
                speedBlend=Mathf.Lerp(speedBlend,Mathf.Clamp01(motor.Speed/motor.topSpeed),1-Mathf.Exp(-5*dt));
                boostBlend=Mathf.Lerp(boostBlend,motor.Boosted?1:0,1-Mathf.Exp(-7*dt));
                view.fieldOfView=Mathf.Lerp(65,76,speedBlend)+boostBlend*10;
                if(!cameraInitialized || motor.Resetting){cameraHeading=motor.transform.rotation;cameraInitialized=true;}
                cameraHeading=Quaternion.Slerp(cameraHeading,motor.transform.rotation,1-Mathf.Exp(-14*dt));
                if(thirdPerson)
                {
                    float distance=Mathf.Lerp(4.8f,5.6f,speedBlend)+boostBlend*.8f;
                    Vector3 offset=new Vector3(-motor.Steering*.12f,2.65f,-distance);
                    view.transform.position=motor.transform.position+cameraHeading*offset;
                    Vector3 aim=motor.transform.position+Vector3.up*1.05f+motor.transform.forward*(1.7f+speedBlend);
                    view.transform.rotation=Quaternion.LookRotation(aim-view.transform.position,Vector3.up);
                }
                else
                {
                    view.transform.position=motor.transform.TransformPoint(seat+Vector3.up*seatHeight);
                    view.transform.rotation=motor.transform.rotation*Quaternion.Euler(8,0,0);
                }
            }
            if (cockpit) cockpit.SetActive(xr || !thirdPerson);
            if (exterior) exterior.SetActive(!xr && thirdPerson);

            if (fadeQuad) fadeQuad.SetActive(motor.Resetting);
            if (vignetteQuad) vignetteQuad.SetActive(xr && comfortVignette && motor.Speed > 6 && (Mathf.Abs(motor.input.Current.steering) > .35f || motor.Boosted));
        }
        void OnDestroy()
        { Time.timeScale = 1; if (fadeMaterial) Destroy(fadeMaterial); if(vignetteMaterial)Destroy(vignetteMaterial);if(vignetteTexture)Destroy(vignetteTexture); }
    }
}
