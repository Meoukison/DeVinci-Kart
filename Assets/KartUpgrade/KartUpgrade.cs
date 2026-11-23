using System.Collections.Generic;
using KartLab;
using UnityEngine;

namespace DeVinciKart.Upgrade
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KartMotor))]
    [DefaultExecutionOrder(-20)]
    public sealed class KartUpgrade : MonoBehaviour
    {
        [Header("Conduite")]
        [SerializeField, Min(1f)] private float maxSpeed = 20f;
        [SerializeField, Min(0.1f)] private float responseSeconds = 0.24f;
        [SerializeField] private AnimationCurve torqueCurve = new(
            new Keyframe(0f, 0.88f), new Keyframe(0.32f, 1.36f),
            new Keyframe(0.68f, 1f), new Keyframe(1f, 0.48f));

        [Header("Son moteur")]
        [SerializeField] private AudioClip idleRecording;
        [SerializeField] private AudioClip drivingRecording;
        [SerializeField, Range(0f, 1f)] private float engineVolume = 0.24f;

        [Header("Carrosserie")]
        [SerializeField] private Material originalPaint;
        [SerializeField] private Material newPaint;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color ReturnTint = new(1f, 0.66f, 0.27f);

        private readonly Dictionary<Renderer, Material[]> originalMaterials = new();
        private readonly List<AudioSource> mutedEngineSources = new();
        private KartMotor motor;
        private KartBoundaryAssist boundaryAssist;
        private Material livePaint;
        private Color paintColor;
        private bool wasResetting;
        private float returnFlashStart = float.NegativeInfinity;
        private AudioSource idle, driving;
        private float originalMaxSpeed, originalAcceleration;
        private float torqueEngagement, rpm, load;
        private bool started, applied, engineSourcesChecked;

        private void Start()
        {
            started = true;
            Apply();
        }

        private void OnEnable()
        {
            if (started) Apply();
        }

        private void Apply()
        {
            motor = GetComponent<KartMotor>();
            if (!motor.input)
            {
                Debug.LogWarning("Kart Upgrade : référence KartInput manquante.", this);
                return;
            }

            originalMaxSpeed = motor.topSpeed;
            originalAcceleration = motor.acceleration;
            motor.topSpeed = maxSpeed;
            torqueEngagement = rpm = load = 0f;
            applied = true;
            wasResetting = motor.Resetting;
            returnFlashStart = float.NegativeInfinity;
            boundaryAssist = gameObject.AddComponent<KartBoundaryAssist>();
            boundaryAssist.Initialize(motor);

            if (originalPaint && newPaint && newPaint.HasProperty(BaseColor))
            {
                livePaint = new Material(newPaint) { name = newPaint.name };
                paintColor = livePaint.GetColor(BaseColor);
                foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    if (System.Array.IndexOf(materials, originalPaint) < 0) continue;
                    originalMaterials.Add(renderer, (Material[])materials.Clone());
                    for (int index = 0; index < materials.Length; index++)
                    {
                        if (materials[index] == originalPaint) materials[index] = livePaint;
                    }
                    renderer.sharedMaterials = materials;
                }
            }

            if (idleRecording && drivingRecording)
            {
                idle = CreateLoop("Kart Upgrade / Ralenti", idleRecording);
                driving = CreateLoop("Kart Upgrade / En charge", drivingRecording);
                engineSourcesChecked = false;
            }
        }

        private void FixedUpdate()
        {
            if (!applied) return;

            // Shape the response before KartMotor consumes its acceleration field.
            // Its brake, drift, boost, collision and surface rules still run as usual.
            var input = motor.input.Current;
            if (motor.driveLocked || motor.Resetting || motor.Boosted ||
                input.throttle <= 0.01f || input.brake > 0.1f)
            {
                torqueEngagement = 0f;
                motor.acceleration = originalAcceleration;
                return;
            }

            torqueEngagement = Mathf.MoveTowards(torqueEngagement, 1f, Time.fixedDeltaTime / responseSeconds);
            float normalizedSpeed = Mathf.Clamp01(motor.Speed / motor.EffectiveTopSpeed);
            float traction = Mathf.Clamp(torqueCurve.Evaluate(normalizedSpeed), 0.2f, 2f);
            motor.acceleration = originalAcceleration * traction * Mathf.Lerp(0.5f, 1f, torqueEngagement);
        }

        private AudioSource CreateLoop(string emitterName, AudioClip recording)
        {
            var emitter = new GameObject(emitterName);
            emitter.transform.SetParent(transform, false);
            emitter.transform.localPosition = new Vector3(0.34f, 0.38f, -0.58f);
            var source = emitter.AddComponent<AudioSource>();
            source.clip = recording;
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            source.dopplerLevel = 0f;
            source.spatialBlend = 0.45f;
            source.minDistance = 2f;
            source.maxDistance = 25f;
            source.Play();
            return source;
        }

        private void LateUpdate()
        {
            if (!applied) return;
            UpdateReturnFlash();
            if (!idle || !driving) return;
            if (!engineSourcesChecked) MuteOriginalEngine();

            bool silent = Time.timeScale <= 0f || motor.Resetting;
            var input = motor.input.Current;
            float throttle = motor.driveLocked || silent || input.brake > 0.1f
                ? 0f : Mathf.Clamp01(input.throttle);
            float speed = Mathf.Clamp01(motor.Speed / motor.EffectiveTopSpeed);
            float targetRpm = Mathf.Clamp01(speed * 0.80f + throttle * 0.22f + (motor.Boosted ? 0.15f : 0f));
            float dt = Time.unscaledDeltaTime;
            rpm = Mathf.Lerp(rpm, targetRpm, 1f - Mathf.Exp(-7f * dt));
            load = Mathf.Lerp(load, throttle, 1f - Mathf.Exp(-10f * dt));

            float mix = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.65f, rpm));
            idle.pitch = Mathf.Lerp(0.9f, 1.65f, rpm);
            driving.pitch = Mathf.Lerp(0.73f, 1.35f, rpm);
            idle.volume = silent ? 0f : engineVolume * (1f - mix) * Mathf.Lerp(0.42f, 0.72f, load);
            driving.volume = silent ? 0f : engineVolume * mix * Mathf.Lerp(0.39f, 1f, load);
        }

        private void UpdateReturnFlash()
        {
            bool resetting = motor.Resetting;
            if (wasResetting && !resetting)
            {
                returnFlashStart = Time.time;
                motor.Notify("BACK ON TRACK");
            }
            wasResetting = resetting;
            if (!livePaint) return;

            float elapsed = Time.time - returnFlashStart;
            const float duration = 0.95f;
            if (resetting || elapsed >= duration)
            {
                if (livePaint.GetColor(BaseColor) != paintColor) livePaint.SetColor(BaseColor, paintColor);
                return;
            }

            float phase = elapsed / duration;
            float pulses = motor.input.XRActive ? 1f : 2f;
            float blend = (0.5f - 0.5f * Mathf.Cos(phase * pulses * 2f * Mathf.PI)) * 0.65f;
            livePaint.SetColor(BaseColor, Color.Lerp(paintColor, ReturnTint, blend));
        }

        private void MuteOriginalEngine()
        {
            engineSourcesChecked = true;
            if (!TryGetComponent<KartEffects>(out var effects)) return;

            // Only the three engine emitters from KartEffects. The horn, tyres and
            // item sounds keep their original sources and mix.
            foreach (var source in effects.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.mute || !source.clip || source.transform.parent != effects.transform ||
                    source.name != "Audio / " + source.clip.name) continue;

                AudioClip clip = source.clip;
                if (clip != effects.idleLoop && clip != effects.loadLoop && clip != effects.highLoop) continue;
                source.mute = true;
                mutedEngineSources.Add(source);
            }
        }

        private void OnDisable()
        {
            if (!applied) return;
            applied = false;

            if (motor)
            {
                motor.topSpeed = originalMaxSpeed;
                motor.acceleration = originalAcceleration;
            }

            foreach (var entry in originalMaterials)
            {
                if (entry.Key) entry.Key.sharedMaterials = entry.Value;
            }
            originalMaterials.Clear();
            if (livePaint) Destroy(livePaint);
            livePaint = null;

            if (boundaryAssist)
            {
                boundaryAssist.enabled = false;
                Destroy(boundaryAssist);
                boundaryAssist = null;
            }

            foreach (var source in mutedEngineSources)
            {
                if (source) source.mute = false;
            }
            mutedEngineSources.Clear();

            StopLoop(idle);
            StopLoop(driving);
            idle = driving = null;
        }

        private static void StopLoop(AudioSource source)
        {
            if (!source) return;
            source.Stop();
            Destroy(source.gameObject);
        }
    }
}
