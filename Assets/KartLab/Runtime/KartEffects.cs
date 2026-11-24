using UnityEngine;

namespace KartLab
{
    public class KartEffects : MonoBehaviour
    {
        public KartMotor motor;
        public Material glowMaterial;
        public Transform[] orbitStars;
        ParticleSystem[] exhaust=new ParticleSystem[2], sparks=new ParticleSystem[2];
        ParticleSystem burst;
        public AudioClip idleLoop, loadLoop, highLoop, skidLoop;
        AudioSource engine, loadedEngine, highEngine, tyres, road;
        int lastTier;
        Renderer[] bodyRenderers;
        MaterialPropertyBlock propertyBlock;
        public static Color TierColor(int tier) => tier>=3?new Color(1,.16f,.8f):tier==2?new Color(1,.5f,.03f):new Color(.12f,.75f,1);
        void Start()
        {
            propertyBlock=new MaterialPropertyBlock();bodyRenderers=motor.visual.GetComponentsInChildren<Renderer>();
            for(int n=0;n<2;n++)
            {
                float side=n==0?-.43f:.43f;
                exhaust[n]=Emitter("Exhaust",new Vector3(side,.4f,-.9f),.24f,.28f,4);
                sparks[n]=Emitter("Drift sparks",new Vector3(side,.13f,-.45f),.28f,.1f,3);
            }
            burst=Emitter("Pickup and hit burst",Vector3.up*.7f,.45f,.18f,5);
            var shape=burst.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.7f;
            motor.Feedback+=React;
            engine=Loop(idleLoop,.12f);loadedEngine=Loop(loadLoop,0);highEngine=Loop(highLoop,0);tyres=Loop(skidLoop,0);
            // Low-pass the sourced skid loop for coarse off-road rumble.
            road=Loop(skidLoop,0);road.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency=350;
        }
        AudioSource Loop(AudioClip clip,float volume)
        {var go=new GameObject("Audio / "+(clip?clip.name:"unassigned"));go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.spatialBlend=motor.input.useExternalInput?1:0;source.minDistance=2;source.maxDistance=35;source.volume=volume;if(clip)source.Play();return source;}

        ParticleSystem Emitter(string name,Vector3 p,float life,float size,float speed)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,180,0);
            var ps=go.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=life;main.startSpeed=speed;main.startSize=size;main.maxParticles=160;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.rateOverTime=0;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=20;shape.radius=.035f;
            var color=ps.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=glowMaterial;return ps;
        }
        void React(string text)
        {
            if(!burst)return;
            if(text.Contains("HIT")||text.Contains("BLOCK")||text.Contains("FROZEN")||text.Contains("COLLECTED"))
            {var main=burst.main;main.startColor=text.Contains("HIT")?new Color(1,.2f,.1f):text.Contains("FROZEN")?Color.cyan:new Color(1,.85f,.15f);burst.Emit(20);}
        }
        void Update()
        {
            if(!engine)return;
            float speed=Mathf.Clamp01(motor.Speed/motor.topSpeed);
            float throttle=motor.input.Current.throttle, audible=Time.timeScale==0||motor.Resetting?0:1;
            engine.pitch=Mathf.Lerp(.85f,1.35f,speed);engine.volume=audible*Mathf.Lerp(.12f,.025f,speed);
            loadedEngine.pitch=Mathf.Lerp(.75f,1.4f,speed);loadedEngine.volume=audible*(.03f+.13f*throttle)*Mathf.Sin(speed*Mathf.PI*.75f);
            highEngine.pitch=Mathf.Lerp(.8f,1.25f,speed);highEngine.volume=audible*Mathf.InverseLerp(.45f,1,speed)*(.045f+.13f*throttle);
            tyres.pitch=motor.Drifting?1.05f:.85f;tyres.volume=audible*speed*(motor.Drifting?.12f:motor.input.Current.brake>.1f?.08f:Mathf.Abs(motor.Steering)*.025f);
            road.volume=audible*speed*(motor.SpeedFactor()<.8f?.1f:0);road.pitch=.65f;
            int tier=motor.DriftTier;if(tier>lastTier){motor.Notify("DRIFT CHARGE "+tier);motor.input.Haptic(.1f+tier*.08f,.06f);}lastTier=tier;
            for(int n=0;n<2;n++)
            {
                var emission=exhaust[n].emission;emission.rateOverTime=motor.Boosted&&motor.Speed>2?85:0;
                var main=exhaust[n].main;main.startColor=motor.Invincible?Color.HSVToRGB(Mathf.Repeat(Time.time*.7f,1),.8f,1):new Color(1,.38f,.035f);
                var se=sparks[n].emission;se.rateOverTime=motor.Drifting&&motor.Grounded?45:0;var sm=sparks[n].main;sm.startColor=TierColor(motor.DriftTier);
            }
            if(orbitStars!=null)for(int n=0;n<orbitStars.Length;n++)
            {
                var star=orbitStars[n];if(!star)continue;star.gameObject.SetActive(motor.Invincible);
                float phase=Time.time*3+n*Mathf.PI*2/orbitStars.Length;
                star.localPosition=new Vector3(Mathf.Cos(phase)*1.15f,.8f+Mathf.Sin(phase*2)*.18f,Mathf.Sin(phase)*1.15f);star.Rotate(0,Time.deltaTime*180,0);
            }
            propertyBlock.Clear();
            if(motor.Invincible)propertyBlock.SetColor("_EmissionColor",Color.HSVToRGB(Mathf.Repeat(Time.time*.5f,1),.8f,.5f));
            else if(motor.Frozen)propertyBlock.SetColor("_EmissionColor",new Color(0,.3f,.5f));
            else propertyBlock.SetColor("_EmissionColor",Color.black);
            if(bodyRenderers!=null)foreach(var renderer in bodyRenderers)if(renderer)renderer.SetPropertyBlock(propertyBlock);
        }
        void OnDestroy(){if(motor)motor.Feedback-=React;}
    }
}
