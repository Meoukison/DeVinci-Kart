using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KartLab
{
    /// <summary>Presentation only: never applies forces or moves the XR head.</summary>
    public class KartCockpit : MonoBehaviour
    {
        public KartMotor motor;
        public KartView view;
        public Transform wheel, dashboard, heldMount;
        public Text speedText, statusText, itemText;
        public Image[] revLights;
        public GameObject[] itemModels;
        public Transform[] wheelPivots, wheelMeshes;
        public Transform column, exteriorColumn, exteriorWheel;
        public float columnLength;
        public Vector3 wheelPosition=new(0,.771f,.321f);
        float steeringAngle, roll;
        void LateUpdate()
        {
            if(!motor || !view)return;
            float dt=Time.deltaTime;
            steeringAngle=Mathf.Lerp(steeringAngle,-motor.Steering*85,1-Mathf.Exp(-12*dt));
            if(wheel) {wheel.localPosition=wheelPosition+Vector3.forward*view.wheelDistance;wheel.localRotation=Quaternion.Euler(45,0,0)*Quaternion.Euler(0,0,steeringAngle);}
            if(wheel&&exteriorWheel){exteriorWheel.localPosition=wheel.localPosition;exteriorWheel.localRotation=wheel.localRotation;}
            UpdateColumn(column);UpdateColumn(exteriorColumn);
            // Instruments stay bolted to the cowl, including in VR.
            roll=Mathf.Repeat(roll+Vector3.Dot(motor.Body.linearVelocity,motor.transform.forward)/.275f*Mathf.Rad2Deg*dt,360);
            for(int n=0;n<wheelMeshes.Length;n++)
            {if(wheelPivots[n])wheelPivots[n].localRotation=Quaternion.Euler(0,n%4<2?motor.Steering*28:0,0);if(wheelMeshes[n])wheelMeshes[n].localRotation=Quaternion.Euler(roll,0,0);}
            var inv=motor.GetComponent<KartInventory>();var skills=motor.GetComponent<KartRaceSkills>();
            if(speedText)speedText.text=$"<size=60>{motor.Speed*3.6f:00}</size> <size=16>KM/H</size>";
            if(statusText)statusText.text=Time.timeScale==0?"PAUSED":skills&&skills.CountingDown?skills.CountdownLabel:motor.Resetting?"RECOVERING":motor.Drifting?"DRIFT  "+motor.DriftTier:motor.Boosted?"BOOST  "+motor.BoostRemaining.ToString("F1")+"s":motor.Frozen?"ICE / LOW GRIP":motor.assistedDriving?"DRIVE / ASSIST":"DRIVE / STANDARD";
            if(itemText)itemText.text=inv.Rolling?"ACQUIRING...":inv.Held==ItemKind.None?"APEX  /  07":KartInventory.Name(inv.Held)+(inv.Charges>1?" ×"+inv.Charges:"");
            for(int n=0;n<revLights.Length;n++)if(revLights[n])
            {float value=motor.Drifting?motor.Charge/2.1f:motor.Speed/motor.topSpeed;revLights[n].color=value>(n+.5f)/revLights.Length?(motor.Drifting?KartEffects.TierColor(motor.DriftTier):n>6?new Color(1,.25f,.09f):new Color(.3f,1,.75f)):new Color(.08f,.13f,.16f);}
            for(int n=1;n<itemModels.Length;n++)if(itemModels[n])itemModels[n].SetActive(!inv.Rolling&&(int)inv.Held==n);
            if(heldMount)heldMount.localRotation=Quaternion.Euler(0,Time.time*35,0);
        }
        void UpdateColumn(Transform shaft)
        {
            if(!wheel||!shaft||columnLength<=.01f)return;
            var axis=wheel.localPosition-shaft.localPosition;
            shaft.localRotation=Quaternion.LookRotation(axis);
            shaft.localScale=new Vector3(1,1,axis.magnitude/columnLength);
        }
    }
}
