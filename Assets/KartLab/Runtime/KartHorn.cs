using UnityEngine;

namespace KartLab
{
    /// <summary>Tap for a beep, hold for a continuous horn. Never stacks audio voices.</summary>
    [RequireComponent(typeof(KartInput))]
    public class KartHorn : MonoBehaviour
    {
        public AudioClip clip;
        [Range(0,1)] public float volume=.32f;
        public bool Sounding => source && source.isPlaying;
        AudioSource source;
        KartInput input;
        KartMotor motor;
        bool wasPressed, active;
        float minimumUntil;

        void Awake()
        {
            input=GetComponent<KartInput>();motor=GetComponent<KartMotor>();
            var emitter=new GameObject("Horn audio");emitter.transform.SetParent(transform,false);
            emitter.transform.localPosition=new Vector3(0,.6f,.7f);
            source=emitter.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;
            source.clip=clip;source.volume=0;source.pitch=1.08f;source.spatialBlend=1;
            source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;
            source.minDistance=2;source.maxDistance=28;
        }

        // Read after both input and pause state have updated this frame.
        void LateUpdate()
        {
            bool pressed=input.Current.horn;
            bool blocked=Time.timeScale<=0||(motor&&motor.Resetting);
            if(blocked)
            {
                source.Stop();active=false;wasPressed=pressed;return;
            }
            if(pressed&&!wasPressed&&clip)
            {
                active=true;minimumUntil=Time.unscaledTime+.075f;
                // Repress during release raises the same voice; no cooldown or layering.
                if(!source.isPlaying){source.volume=0;source.Play();}
                input.Haptic(.1f,.04f);
            }
            if(active)
            {
                bool sustain=pressed||Time.unscaledTime<minimumUntil;
                float target=sustain?volume:0;
                source.volume=Mathf.MoveTowards(source.volume,target,
                    Mathf.Max(volume,.001f)*Time.unscaledDeltaTime/(sustain?.012f:.03f));
                if(!sustain&&source.volume<=0){source.Stop();active=false;}
            }
            // Holding through pause/recovery must not queue a honk on resume.
            wasPressed=pressed;
        }

        void OnDisable(){if(source)source.Stop();active=false;wasPressed=true;}
    }
}
