using KartLab;
using UnityEngine;

namespace DeVinciKart
{
    /// <summary>
    /// Lakitu du départ, façon Mario Kart :
    ///  • arrive en volant du ciel et se place face au joueur (suit le kart tant que la course n'est pas lancée) ;
    ///  • feu synchronisé avec le compte à rebours KartLab : éteint → 1 rouge → 2 → 3 → tout vert ;
    ///  • animation procédurale du squelette (bras qui pointe/agite, tête, buste, canne, feu qui se balance) ;
    ///  • au vert : il agite le bras puis s'envole et disparaît. Revient si un nouveau départ est lancé.
    /// Os du modèle Tripo (Modele3dlakitu.glb) : bone_9→11 bras levé, bone_6→8 bras + canne, bone_5 tête,
    /// bone_3/4 buste, bone_26 boîtier du feu. Les lampes Lampe_1..3 viennent de Backup_Modeles/lakitu_feu.py.
    /// </summary>
    public class LakituStartLights : MonoBehaviour
    {
        public Renderer[] lamps = new Renderer[3];
        public Color offColor = new Color(0.05f, 0.05f, 0.06f), redColor = new Color(1f, 0.07f, 0.04f), greenColor = new Color(0.15f, 1f, 0.3f);
        [Tooltip("Petite lumière colorée devant chaque lampe allumée.")]
        public float glowRange = 1.4f, glowIntensity = 0.9f;

        [Header("Placement face au joueur (repère du kart)")]
        public float distance = 7f;
        public float sideOffset = 2.9f;
        [HideInInspector] public float heightOffset = -0.7f;   // ancienne valeur, plus utilisée
        [Tooltip("Hauteur du bas de Lakitu par rapport aux yeux du pilote (positif = au-dessus de l'horizon).")]
        public float heightAboveEyes = 0.35f;
        public float followSharpness = 4f;

        [Header("Vol")]
        public float arriveDuration = 1.8f, leaveDelay = 1.1f, leaveDuration = 2.2f;

        enum Phase { Arriving, Hovering, Leaving, Gone }

        KartRaceSkills skills;
        KartMotor motor;
        Transform cam, kart;
        Material[] mats;
        Light[] glows;
        Renderer[] allRenderers;
        bool wasCounting, green, waitIntro;
        int shown = -1, lastReds;

        Phase phase = Phase.Arriving;
        float phaseStart, greenAt = -1f;
        Vector3 flyFrom, flyTo;
        Quaternion flyFromRot;

        // squelette
        Transform shoulder, elbow, hand, rodArm, rodArm2, head, spine, chest, box;
        Transform[] animBones;
        Quaternion[] restLocal;
        Vector3 faceLocal;          // normale des lampes dans le repère de Lakitu
        float faceYawOffset;
        float armKick, headKick, boxAngle, boxVel;
        Vector3 lastPos, lastVel;

        void Start()
        {
            skills = FindAnyObjectByType<KartRaceSkills>();
            motor = skills ? skills.GetComponent<KartMotor>() : FindAnyObjectByType<KartMotor>();
            kart = motor ? motor.transform : skills ? skills.transform : null;

            // le flottement du générateur (Spinner sur le parent) est remplacé par le nôtre
            foreach (var mb in GetComponentsInParent<MonoBehaviour>())
                if (mb != this && mb.GetType().Name == "Spinner") mb.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

            // orientation : de quel côté regardent les lampes ?
            Transform mid = lamps.Length > 1 && lamps[1] ? lamps[1].transform : null;
            faceLocal = mid ? transform.InverseTransformDirection(mid.up) : Vector3.forward;
            Vector3 fl = new Vector3(faceLocal.x, 0f, faceLocal.z);
            faceYawOffset = fl.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(Vector3.forward, fl, Vector3.up) : 0f;

            // os
            shoulder = Bone("bone_9"); elbow = Bone("bone_10"); hand = Bone("bone_11");
            rodArm = Bone("bone_6"); rodArm2 = Bone("bone_7");
            head = Bone("bone_5"); spine = Bone("bone_3"); chest = Bone("bone_4");
            box = Bone("bone_26");
            animBones = new[] { spine, chest, head, rodArm, rodArm2, shoulder, elbow, hand, box };   // parents d'abord
            restLocal = new Quaternion[animBones.Length];
            for (int i = 0; i < animBones.Length; i++) if (animBones[i]) restLocal[i] = animBones[i].localRotation;

            // les lampes suivent le boîtier quand il se balance
            if (box) foreach (var l in lamps) if (l) l.transform.SetParent(box, true);

            // matériaux + lueurs
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            mats = new Material[lamps.Length];
            glows = new Light[lamps.Length];
            for (int i = 0; i < lamps.Length; i++)
            {
                if (!lamps[i]) continue;
                mats[i] = new Material(shader);
                lamps[i].sharedMaterial = mats[i];
                var g = new GameObject("Lueur").AddComponent<Light>();
                g.transform.SetParent(lamps[i].transform, false);
                g.transform.localPosition = Vector3.up * 0.6f;
                g.type = LightType.Point; g.range = glowRange; g.intensity = glowIntensity; g.shadows = LightShadows.None;
                glows[i] = g;
            }
            allRenderers = GetComponentsInChildren<Renderer>(true);
            Apply(0, false);

            ResolveCamera();
            if (KartRaceSkills.CinematicActive) { phase = Phase.Gone; SetVisible(false); waitIntro = true; }   // il arrive après la cinématique
            else BeginArrive();
            lastPos = transform.position;
        }

        Transform Bone(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        void ResolveCamera()
        {
            if (cam && cam.gameObject.activeInHierarchy) return;
            var c = Camera.main;
            cam = c ? c.transform : null;
        }

        // ------------------------------------------------------------------ cible devant le joueur

        void Target(out Vector3 pos, out Quaternion rot)
        {
            ResolveCamera();
            Vector3 origin = cam ? cam.position : kart ? kart.position + Vector3.up * 1.2f : transform.position;
            Vector3 fwd = kart ? kart.forward : cam ? cam.forward : transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            pos = origin + fwd * distance + right * sideOffset + Vector3.up * heightAboveEyes;
            Vector3 toCam = origin - pos; toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-4f) toCam = -fwd;
            rot = Quaternion.LookRotation(toCam.normalized, Vector3.up) * Quaternion.Euler(0f, -faceYawOffset, 0f);
        }

        void BeginArrive()
        {
            Target(out var p, out var r);
            Vector3 fwd = kart ? Vector3.ProjectOnPlane(kart.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            flyFrom = p + Vector3.up * 9f + right * 7f + fwd * 4f;
            flyFromRot = r * Quaternion.Euler(0f, 60f, 0f);
            transform.SetPositionAndRotation(flyFrom, flyFromRot);
            SetVisible(true);
            phase = Phase.Arriving; phaseStart = Time.time;
            armKick = 1f;   // il salue en arrivant
        }

        void BeginLeave()
        {
            phase = Phase.Leaving; phaseStart = Time.time;
            flyFrom = transform.position; flyFromRot = transform.rotation;
            Vector3 fwd = kart ? Vector3.ProjectOnPlane(kart.forward, Vector3.up).normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            flyTo = flyFrom + Vector3.up * 16f + right * 10f + fwd * 6f;
        }

        void SetVisible(bool v)
        {
            if (allRenderers == null) return;
            foreach (var r in allRenderers) if (r) r.enabled = v;
            if (glows != null) foreach (var g in glows) if (g) g.gameObject.SetActive(v);
        }

        // ------------------------------------------------------------------ boucle

        void Update()
        {
            if (waitIntro)
            {
                if (KartRaceSkills.CinematicActive) return;
                waitIntro = false; ResolveCamera(); BeginArrive(); lastPos = transform.position;
            }
            // --- feu
            int reds = 0;
            if (skills)
            {
                bool counting = skills.CountingDown;
                if (counting)
                {
                    if (green || phase == Phase.Leaving || phase == Phase.Gone) { green = false; BeginArrive(); }
                    int.TryParse(skills.CountdownLabel, out int left);
                    reds = Mathf.Clamp(4 - Mathf.Max(1, left), 1, 3);
                }
                else if (wasCounting)
                {
                    green = !(motor && motor.Resetting);       // départ donné (pas un retour sur piste)
                    if (green) { greenAt = Time.time; armKick = 1f; boxVel += 140f; }
                }
                wasCounting = counting;
            }
            if (reds != lastReds && reds > 0) { armKick = 1f; headKick = 1f; boxVel += 90f; }
            lastReds = reds;
            int state = green ? 4 : reds;
            if (state != shown) Apply(reds, green);

            // --- vol
            float t = Time.time;
            Target(out var tp, out var tr);
            float bob = Mathf.Sin(t * 1.7f) * 0.18f;
            switch (phase)
            {
                case Phase.Arriving:
                {
                    float k = Mathf.Clamp01((t - phaseStart) / arriveDuration);
                    float e = 1f - Mathf.Pow(1f - k, 3f);                            // ease-out
                    Vector3 arc = Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.5f;        // petite courbe
                    transform.position = Vector3.LerpUnclamped(flyFrom, tp, e) + arc + Vector3.up * bob * e;
                    transform.rotation = Quaternion.Slerp(flyFromRot, tr, e) * Quaternion.Euler(0f, 0f, (1f - e) * -25f);
                    if (k >= 1f) phase = Phase.Hovering;
                    break;
                }
                case Phase.Hovering:
                {
                    float a = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
                    Vector3 want = tp + Vector3.up * bob;
                    transform.position = Vector3.Lerp(transform.position, want, a);
                    float roll = Mathf.Sin(t * 1.1f) * 4f;
                    transform.rotation = Quaternion.Slerp(transform.rotation, tr * Quaternion.Euler(0f, Mathf.Sin(t * 0.6f) * 6f, roll), a);
                    if (green && t - greenAt > leaveDelay) BeginLeave();
                    break;
                }
                case Phase.Leaving:
                {
                    float k = Mathf.Clamp01((t - phaseStart) / leaveDuration);
                    float e = k * k * (3f - 2f * k);
                    transform.position = Vector3.Lerp(flyFrom, flyTo, e * e);
                    transform.rotation = flyFromRot * Quaternion.Euler(0f, e * -70f, e * 20f);
                    if (k >= 1f) { phase = Phase.Gone; SetVisible(false); }
                    break;
                }
            }

            // vitesse/accélération pour le balancier du feu
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 vel = (transform.position - lastPos) / dt;
            Vector3 acc = (vel - lastVel) / dt;
            lastPos = transform.position; lastVel = vel;
            Vector3 sideW = transform.TransformDirection(Vector3.Cross(Vector3.up, faceLocal).normalized);
            boxVel += Vector3.Dot(acc, sideW) * 1.2f * dt * 10f;
        }

        void LateUpdate()
        {
            if (animBones == null || phase == Phase.Gone) return;
            float t = Time.time, dt = Time.deltaTime;

            for (int i = 0; i < animBones.Length; i++) if (animBones[i]) animBones[i].localRotation = restLocal[i];

            Vector3 faceW = transform.TransformDirection(faceLocal).normalized;   // vers le joueur
            Vector3 upW = transform.up;
            Vector3 sideW = Vector3.Cross(upW, faceW).normalized;

            armKick = Mathf.MoveTowards(armKick, 0f, dt * 1.4f);
            headKick = Mathf.MoveTowards(headKick, 0f, dt * 2f);
            bool waving = green && t - greenAt < leaveDelay + leaveDuration;

            // buste : respiration + léger penché
            Rot(spine, sideW, Mathf.Sin(t * 1.3f) * 4f);
            Rot(chest, upW, Mathf.Sin(t * 0.8f) * 6f);

            // tête : regarde autour, hoche quand une lampe s'allume
            Rot(head, upW, Mathf.Sin(t * 0.9f) * 10f);
            Rot(head, sideW, Mathf.Sin(t * 14f) * 12f * headKick);

            // bras levé : petit geste au repos, coup de bras à chaque lampe, grand salut au vert
            float idle = Mathf.Sin(t * 2.2f) * 8f;
            float kick = Mathf.Sin(t * 13f) * 28f * armKick;
            float wave = waving ? Mathf.Sin(t * 10f) * 35f : 0f;
            Rot(shoulder, faceW, idle + kick + wave);
            Rot(shoulder, sideW, -18f * armKick + (waving ? -15f : 0f));
            Rot(elbow, faceW, Mathf.Sin(t * 2.2f + 0.8f) * 10f + (waving ? Mathf.Sin(t * 10f + 1f) * 25f : kick * 0.6f));
            Rot(hand, faceW, Mathf.Sin(t * 3.1f) * 12f);

            // bras + canne à pêche
            Rot(rodArm, sideW, Mathf.Sin(t * 1.6f) * 7f);
            Rot(rodArm2, faceW, Mathf.Sin(t * 1.6f + 1.2f) * 6f);

            // feu : pendule amorti
            boxVel += (-60f * boxAngle - 2.2f * boxVel) * dt;
            boxAngle += boxVel * dt;
            boxAngle = Mathf.Clamp(boxAngle, -22f, 22f);
            Rot(box, faceW, boxAngle);
        }

        static void Rot(Transform b, Vector3 worldAxis, float deg)
        {
            if (b && Mathf.Abs(deg) > 0.01f) b.rotation = Quaternion.AngleAxis(deg, worldAxis) * b.rotation;
        }

        void Apply(int reds, bool allGreen)
        {
            shown = allGreen ? 4 : reds;
            for (int i = 0; i < lamps.Length; i++)
            {
                if (!mats[i]) continue;
                bool on = allGreen || i < reds;
                Color c = allGreen ? greenColor : on ? redColor : offColor;
                mats[i].color = c;
                if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", c);
                glows[i].enabled = on;
                glows[i].color = c;
            }
        }
    }
}
