using System.Collections;
using System.Collections.Generic;
using KartLab;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DeVinciKart
{
    /// <summary>
    /// Cinématique de présentation du circuit façon Mario Kart, puis départ automatique :
    ///  1. survol aérien du circuit, puis plans qui tournent autour des lieux importants (musique CircuitIntro) ;
    ///  2. retour derrière le kart, Lakitu arrive avec son feu ;
    ///  3. compte à rebours lancé tout seul, avec le son du décompte synchronisé sur les lampes.
    /// Gère aussi le son de la roulette des objets. S'installe tout seul dans chaque scène « Piste_* »
    /// (rien à câbler dans les scènes). Entrée / Start / A : passer la cinématique.
    /// Sons : Assets/DeVinciKart/Resources/Audio/ (CircuitIntro, Countdown, ItemRoulette).
    /// </summary>
    [DefaultExecutionOrder(1000)]   // après KartView, pour secouer la caméra
    public class RaceIntro : MonoBehaviour
    {
        /// <summary>Vrai tant que la cinématique tourne ou que le départ n'a pas encore été donné par elle.</summary>
        public static bool Active { get; private set; }
        public static bool Enabled = true;

        [Header("Lieux montrés (début du nom des objets, dans n'importe quel ordre : ils sont remis dans l'ordre du tour)")]
        public string[] featured = { "Le Pouce", "Campus du Parc" };
        [Tooltip("Nombre de plans automatiques retirés à la fin avant d'ajouter les lieux imposés.")]
        public int dropLastAuto = 2;
        [Tooltip("Recul de la caméra sur le Pouce (1 = normal).")]
        public float pouceDistance = 1.9f;
        [Header("Plans fixes (caméra → cible, léger travelling vers l'avant)")]
        public Vector3[] fixedCamPositions = { new Vector3(217.7f, 14f, -45.2f) };   // route surélevée, affiche du mur vert → Grande Arche
        public Vector3[] fixedCamTargets = { new Vector3(0f, 30f, 0f) };
        public float fixedDolly = 7f;

        [Header("Durées")]
        public float aerialDuration = 3f;
        public float landmarkDuration = 2.4f;
        public int maxLandmarks = 4;
        public float returnDuration = 2.2f;
        [Tooltip("Temps laissé à Lakitu pour arriver avant le 3.")]
        public float lakituArriveTime = 1.9f;

        [Header("Objets")]
        [Tooltip("Durée de la roulette (calée sur le son de la boîte : le « ding » tombe vers 3,5 s).")]
        public float rouletteDuration = 3.5f;
        [Tooltip("Moment du premier bip (« 3 ») dans Countdown.mp3.")]
        public float countdownClipOffset = 1.1f;

        static readonly string[] Keywords =
        {
            "arche", "pole", "pilier", "ange", "pouce", "campus", "esilv", "pizz", "kebab",
            "viaduc", "tunnel", "monument", "square", "puzzle", "statue", "defense", "rondpoint"
        };
        static readonly string[] Excluded =
        {
            "texte", "boite", "lakitu", "checkpoint", "porte_", "shadow", "light", "lampe", "feux", "ligne", "grille", "trottoir"
        };

        struct Shot
        {
            public Vector3 center;
            public float radius, startAngle, sweep, heightA, heightB, distA, distB, duration;
            public bool aerial, fixedCam;
            public Vector3 camA, camB, look;
            public int order;
        }

        KartRaceSkills skills;
        KartMotor motor;
        KartInventory inventory;
        Camera mainCam, introCam;
        AudioSource music, sfx, roulette;
        AudioClip introClip, countdownClip, rouletteClip;
        readonly List<Shot> shots = new List<Shot>();
        bool skip, cinematic, wasCounting, wasRolling;
        float fade, bars;
        string circuitName = "";
        GUIStyle nameStyle, subStyle, hintStyle;

        // ------------------------------------------------------------------ installation automatique

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Le projet a « Enter Play Mode Options » sans rechargement de scène ni de domaine :
        // sceneLoaded n'est alors pas émis en entrant en Play, et les statiques gardent leur valeur.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false;
            KartRaceSkills.CustomCountdownUI = false;
            KartRaceSkills.CinematicActive = false;
            KartRaceSkills.StartBlocked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FirstScene() => OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (FindAnyObjectByType<RaceIntro>()) return;   // déjà installée pour cette scène
            Active = false;
            KartRaceSkills.CustomCountdownUI = false;
            KartRaceSkills.CinematicActive = false;
            KartRaceSkills.StartBlocked = false;
            if (!Enabled || !scene.name.StartsWith("Piste_")) return;
            var s = FindAnyObjectByType<KartRaceSkills>();
            if (!s) return;
            // positionné AVANT les Start() de la scène, pour que Lakitu et le LapRaceManager attendent
            Active = true;
            KartRaceSkills.CustomCountdownUI = true;
            KartRaceSkills.CinematicActive = true;
            KartRaceSkills.StartBlocked = true;
            var go = new GameObject("RaceIntro (cinématique + sons)");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<RaceIntro>();
        }

        // ------------------------------------------------------------------

        void Start()
        {
            skills = FindAnyObjectByType<KartRaceSkills>();
            motor = skills ? (skills.motor ? skills.motor : skills.GetComponent<KartMotor>()) : null;
            inventory = motor ? motor.GetComponent<KartInventory>() : null;
            if (inventory) inventory.rollDuration = rouletteDuration;
            foreach (var hud in FindObjectsByType<KartHUD>(FindObjectsSortMode.None)) hud.itemBoxSounds = false;

            introClip = Resources.Load<AudioClip>("Audio/CircuitIntro");
            countdownClip = Resources.Load<AudioClip>("Audio/Countdown");
            rouletteClip = Resources.Load<AudioClip>("Audio/ItemRoulette");
            music = NewSource(0.8f);
            sfx = NewSource(0.9f);
            roulette = NewSource(0.75f);

            circuitName = PrettyName(SceneManager.GetActiveScene().name);
            StartCoroutine(Run());
        }

        AudioSource NewSource(float volume)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 0f; a.volume = volume;
            return a;
        }

        static string PrettyName(string scene)
        {
            string s = scene.StartsWith("Piste_") ? scene.Substring(6) : scene;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_') { sb.Append("  ·  "); continue; }
                if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().ToUpperInvariant();
        }

        // ------------------------------------------------------------------ déroulé

        IEnumerator Run()
        {
            if (motor) motor.driveLocked = true;
            yield return null;                       // laisse KartView placer la caméra du joueur une première fois
            mainCam = Camera.main;
            BuildShots();

            if (shots.Count > 0 && mainCam)
            {
                cinematic = true;
                var go = new GameObject("Camera_Cinematique");
                go.transform.SetParent(transform, false);
                introCam = go.AddComponent<Camera>();
                introCam.CopyFrom(mainCam);
                introCam.depth = mainCam.depth + 10;
                introCam.fieldOfView = 52f;
                introCam.farClipPlane = Mathf.Max(mainCam.farClipPlane, 2500f);
                mainCam.enabled = false;
                if (introClip) { music.clip = introClip; music.Play(); }

                foreach (var shot in shots)
                {
                    float t0 = Time.time;
                    while (!skip && Time.time - t0 < shot.duration)
                    {
                        float u = (Time.time - t0) / shot.duration;
                        Pose(shot, u, out var pos, out var look);
                        introCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
                        float edge = Mathf.Min(u * shot.duration, (1f - u) * shot.duration);
                        fade = 1f - Mathf.Clamp01(edge / 0.22f);     // petit fondu au noir à chaque coupe
                        bars = Mathf.MoveTowards(bars, 1f, Time.deltaTime * 2f);
                        yield return null;
                    }
                    if (skip) break;
                }

                // retour vers la caméra du joueur
                if (!skip)
                {
                    fade = 0f;
                    Vector3 fromP = introCam.transform.position;
                    Quaternion fromR = introCam.transform.rotation;
                    Vector3 kartP = motor ? motor.transform.position : mainCam.transform.position;
                    Vector3 kartF = motor ? Vector3.ProjectOnPlane(motor.transform.forward, Vector3.up).normalized : mainCam.transform.forward;
                    fromP = kartP + kartF * 14f + Vector3.up * 9f + Vector3.Cross(Vector3.up, kartF) * 6f;
                    fromR = Quaternion.LookRotation(kartP + Vector3.up - fromP, Vector3.up);
                    float t0 = Time.time;
                    while (!skip && Time.time - t0 < returnDuration)
                    {
                        float u = Mathf.Clamp01((Time.time - t0) / returnDuration);
                        float e = u * u * (3f - 2f * u);
                        var target = mainCam.transform;
                        // arc par-dessus le kart pour finir pile dans la vue de jeu
                        Vector3 p = Vector3.Lerp(fromP, target.position, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 3f;
                        introCam.transform.SetPositionAndRotation(p, Quaternion.Slerp(fromR, target.rotation, e));
                        introCam.fieldOfView = Mathf.Lerp(52f, mainCam.fieldOfView, e);
                        fade = u < 0.12f ? 1f - u / 0.12f : 0f;
                        bars = 1f - e;
                        if (music.isPlaying && u > 0.5f) music.volume = Mathf.Lerp(0.8f, 0f, (u - 0.5f) * 2f);
                        yield return null;
                    }
                }

                if (skip) { fade = 1f; yield return null; }
                mainCam.enabled = true;
                Destroy(introCam.gameObject);
                StartCoroutine(FadeOutMusic(skip ? 0.35f : 0.15f));
                cinematic = false;
                fade = skip ? 1f : 0f;
                bars = 0f;
            }

            // Lakitu arrive (il attend la fin de la cinématique), puis le compte à rebours part tout seul
            KartRaceSkills.CinematicActive = false;
            float wait = Time.time + lakituArriveTime;
            while (Time.time < wait) { fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime * 3f); yield return null; }
            fade = 0f;
            KartRaceSkills.StartBlocked = false;
            if (skills && !skills.CountingDown) skills.BeginCountdown();
            Active = false;
        }

        IEnumerator FadeOutMusic(float seconds)
        {
            float v = music.volume;
            for (float t = 0; t < seconds; t += Time.deltaTime) { music.volume = Mathf.Lerp(v, 0f, t / seconds); yield return null; }
            music.Stop();
        }

        void Update()
        {
            if (cinematic)
            {
                var kb = Keyboard.current; var gp = Gamepad.current;
                if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) ||
                    (gp != null && (gp.startButton.wasPressedThisFrame || gp.buttonSouth.wasPressedThisFrame)))
                    skip = true;
                if (motor) motor.driveLocked = true;
            }

            // --- son du compte à rebours, calé sur les lampes de Lakitu (3 · 2 · 1 · GO)
            if (skills)
            {
                bool counting = skills.CountingDown;
                if (counting && !wasCounting && countdownClip)
                {
                    sfx.Stop(); sfx.clip = countdownClip;
                    sfx.time = Mathf.Min(countdownClipOffset, countdownClip.length - 0.05f);
                    sfx.Play();
                }
                else if (!counting && wasCounting && motor && motor.Resetting) sfx.Stop();   // départ annulé
                wasCounting = counting;
            }

            // --- son de la roulette des objets
            if (inventory)
            {
                bool rolling = inventory.Rolling;
                if (rolling && !wasRolling && rouletteClip) { roulette.Stop(); roulette.clip = rouletteClip; roulette.time = 0f; roulette.Play(); }
                wasRolling = rolling;
            }
        }

        // mauvais départ : le kart et la caméra vibrent pendant que le moteur cale
        void LateUpdate()
        {
            if (!skills || !skills.Stalled) return;
            float t = Time.time, k = Mathf.Clamp01((skills.LastStartTime + skills.stallDuration - t) / skills.stallDuration);
            float amp = 0.5f + 0.5f * k;
            var cam = Camera.main;
            if (cam)
            {
                cam.transform.position += cam.transform.right * Mathf.Sin(t * 61f) * 0.035f * amp + cam.transform.up * Mathf.Sin(t * 47f) * 0.03f * amp;
                cam.transform.rotation *= Quaternion.Euler(Mathf.Sin(t * 53f) * 0.8f * amp, 0f, Mathf.Sin(t * 67f) * 1.2f * amp);
            }
            if (motor && motor.visual)
                motor.visual.localRotation *= Quaternion.Euler(Mathf.Sin(t * 58f) * 2.5f * amp, Mathf.Sin(t * 41f) * 2f * amp, Mathf.Sin(t * 73f) * 3f * amp);
            if (motor && motor.input && Time.frameCount % 6 == 0) motor.input.Haptic(0.35f * amp, 0.1f);
        }

        // ------------------------------------------------------------------ plans

        void BuildShots()
        {
            shots.Clear();
            var route = RoutePoints();
            Vector3 start = motor ? motor.transform.position : mainCam ? mainCam.transform.position : Vector3.zero;

            // 1. survol aérien de tout le circuit
            if (route.Count > 1)
            {
                var b = new Bounds(route[0], Vector3.zero);
                foreach (var p in route) b.Encapsulate(p);
                float r = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 40f, 450f);
                Vector3 fromStart = Vector3.ProjectOnPlane(start - b.center, Vector3.up);
                float a0 = fromStart.sqrMagnitude > 1f ? Mathf.Atan2(fromStart.z, fromStart.x) * Mathf.Rad2Deg + 150f : 200f;
                shots.Add(new Shot
                {
                    aerial = true, center = b.center, radius = r, startAngle = a0, sweep = 28f,
                    heightA = r * 0.95f, heightB = r * 0.7f, distA = r * 1.25f, distB = r * 1.05f, duration = aerialDuration
                });
            }

            // 2. lieux importants, dans l'ordre du parcours
            var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var found = new List<(Bounds b, int order)>();
            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant();
                if (!Match(n)) continue;
                if (t.GetComponentInParent<KartMotor>() || t.GetComponentInParent<TrackZone>()) continue;
                if (t.parent && Match(t.parent.name.ToLowerInvariant())) continue;   // on garde le parent le plus haut
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                foreach (var r in rs) if (r.enabled) b.Encapsulate(r.bounds);
                float size = b.size.magnitude;
                if (size < 6f || size > 600f) continue;
                if (Vector3.Distance(b.center, start) < 18f) continue;                // la ligne de départ, on la voit déjà à la fin
                bool dup = false;
                foreach (var f in found) if (Vector3.Distance(f.b.center, b.center) < 30f) { dup = true; break; }
                if (dup) continue;
                found.Add((b, NearestIndex(route, b.center)));
            }
            found.Sort((x, y) => x.order.CompareTo(y.order));
            // choix automatique : des lieux répartis sur tout le tour
            var picked = new List<(Bounds b, int order)>();
            if (found.Count <= maxLandmarks) picked.AddRange(found);
            else for (int i = 0; i < maxLandmarks; i++) picked.Add(found[Mathf.RoundToInt(i * (found.Count - 1) / (float)(maxLandmarks - 1))]);
            // on enlève les derniers plans automatiques…
            picked.RemoveRange(Mathf.Max(0, picked.Count - dropLastAuto), Mathf.Min(dropLastAuto, picked.Count));
            // …et on ajoute les lieux imposés (le Pouce, le Campus du Parc)
            if (featured != null)
                foreach (var name in featured)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    foreach (var t in all)
                    {
                        if (!t.name.StartsWith(name)) continue;
                        var rs = t.GetComponentsInChildren<Renderer>();
                        if (rs.Length == 0) continue;
                        var b = rs[0].bounds;
                        foreach (var r in rs) if (r.enabled) b.Encapsulate(r.bounds);
                        picked.RemoveAll(f => Vector3.Distance(f.b.center, b.center) < 30f);
                        picked.Add((b, NearestIndex(route, b.center)));
                        break;
                    }
                }
            picked.Sort((x, y) => x.order.CompareTo(y.order));
            var chosen = new List<Bounds>();
            var orders = new List<int>();
            foreach (var f in picked) { chosen.Add(f.b); orders.Add(f.order); }
            Bounds pouce = default; bool hasPouce = false;
            foreach (var t in all) if (t.name.StartsWith("Le Pouce")) { var rr = t.GetComponentsInChildren<Renderer>(); if (rr.Length > 0) { pouce = rr[0].bounds; foreach (var r2 in rr) pouce.Encapsulate(r2.bounds); hasPouce = true; } break; }

            // complète avec des points de la piste s'il manque des lieux
            for (int i = chosen.Count; i < 3 && route.Count > 4; i++)
            {
                var p = route[Mathf.Clamp(Mathf.RoundToInt(route.Count * (0.25f + 0.25f * i)), 0, route.Count - 1)];
                chosen.Add(new Bounds(p + Vector3.up * 2f, new Vector3(16f, 4f, 16f)));
                orders.Add(NearestIndex(route, p));
            }

            int k = 0;
            var landmarkShots = new List<Shot>();
            for (int ci = 0; ci < chosen.Count; ci++)
            {
                var b = chosen[ci];
                float far = hasPouce && Vector3.Distance(b.center, pouce.center) < 5f ? pouceDistance : 1f;
                float r = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.y, b.extents.z), 8f, 70f);
                // on part du côté de la piste pour que la route soit dans le champ
                int idx = NearestIndex(route, b.center);
                Vector3 side = route.Count > 0 ? Vector3.ProjectOnPlane(route[Mathf.Clamp(idx, 0, route.Count - 1)] - b.center, Vector3.up) : Vector3.forward;
                float a0 = side.sqrMagnitude > 1f ? Mathf.Atan2(side.z, side.x) * Mathf.Rad2Deg : k * 90f;
                bool left = (k++ & 1) == 0;
                landmarkShots.Add(new Shot
                {
                    center = b.center + Vector3.up * b.extents.y * 0.15f, radius = r,
                    startAngle = a0 + (left ? -25f : 25f), sweep = left ? 40f : -40f,
                    heightA = Mathf.Max(b.extents.y * 0.4f, 4f) + r * 0.25f * far, heightB = Mathf.Max(b.extents.y * 0.6f, 6f) + r * 0.4f * far,
                    distA = (r * 1.9f + 6f) * far, distB = (r * 1.5f + 5f) * far, duration = landmarkDuration,
                    order = ci < orders.Count ? orders[ci] : 0
                });
            }
            // plans fixes, rangés eux aussi dans l'ordre du tour
            if (fixedCamPositions != null)
                for (int i = 0; i < fixedCamPositions.Length; i++)
                {
                    Vector3 cp = fixedCamPositions[i];
                    Vector3 tg = fixedCamTargets != null && i < fixedCamTargets.Length ? fixedCamTargets[i] : cp + Vector3.forward;
                    Vector3 dir = Vector3.ProjectOnPlane(tg - cp, Vector3.up).normalized;
                    landmarkShots.Add(new Shot
                    {
                        fixedCam = true, camA = cp, camB = cp + dir * fixedDolly, look = tg,
                        duration = landmarkDuration, order = NearestIndex(route, cp)
                    });
                }
            landmarkShots.Sort((x, y) => x.order.CompareTo(y.order));
            shots.AddRange(landmarkShots);
        }

        static bool Match(string n)
        {
            foreach (var e in Excluded) if (n.Contains(e)) return false;
            foreach (var k in Keywords) if (n.Contains(k)) return true;
            return false;
        }

        List<Vector3> RoutePoints()
        {
            var list = new List<Vector3>();
            var rec = motor ? motor.GetComponent<KartRecovery>() : null;
            TrackRoute route = rec && rec.route ? rec.route : null;
            if (!route)
            {
                var tp = FindAnyObjectByType<TrackPath>();
                if (tp && tp.points != null && tp.points.Length > 1) { list.AddRange(tp.points); return list; }
                foreach (var r in TrackRoute.All)
                    if (r && !r.alternative && r.points != null && (!route || r.points.Length > route.points.Length)) route = r;
            }
            if (route && route.points != null) foreach (var p in route.points) list.Add(route.transform.TransformPoint(p));
            return list;
        }

        static int NearestIndex(List<Vector3> pts, Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < pts.Count; i++) { float d = (pts[i] - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            return best;
        }

        static void Pose(Shot s, float u, out Vector3 pos, out Vector3 look)
        {
            float e = u * u * (3f - 2f * u) * 0.6f + u * 0.4f;     // mouvement continu, légèrement adouci
            if (s.fixedCam) { pos = Vector3.Lerp(s.camA, s.camB, e); look = s.look + (s.camB - s.camA) * e; return; }
            float a = (s.startAngle + s.sweep * e) * Mathf.Deg2Rad;
            float d = Mathf.Lerp(s.distA, s.distB, e);
            pos = s.center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d + Vector3.up * Mathf.Lerp(s.heightA, s.heightB, e);
            look = s.aerial ? s.center : s.center + Vector3.up * s.radius * 0.1f;
        }

        // ------------------------------------------------------------------ habillage écran

        // ------------------------------------------------------------------ compte à rebours stylisé

        GUIStyle bigStyle, resultStyle, tipStyle;
        string shownLabel = "";
        float labelAt;

        void DrawCountdown()
        {
            if (!skills || KartRaceSkills.CinematicActive) return;
            float w = Screen.width, h = Screen.height, t = Time.time;
            if (bigStyle == null)
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                bigStyle = new GUIStyle(GUI.skin.label) { font = font, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
                resultStyle = new GUIStyle(bigStyle);
                tipStyle = new GUIStyle(bigStyle) { fontStyle = FontStyle.Bold };
            }

            string label = null; Color fill = Color.white, edge = new Color(0.05f, 0.08f, 0.25f);
            float age;
            if (skills.CountingDown)
            {
                label = skills.CountdownLabel;
                if (label != shownLabel) { shownLabel = label; labelAt = t; }
                fill = label == "3" ? new Color(1f, 0.3f, 0.2f) : label == "2" ? new Color(1f, 0.62f, 0.1f) : new Color(1f, 0.9f, 0.15f);
            }
            else if (t - skills.LastStartTime < 1.1f)
            {
                label = "GO!";
                if (shownLabel != "GO!") { shownLabel = "GO!"; labelAt = skills.LastStartTime; }
                fill = new Color(0.35f, 1f, 0.3f);
                edge = new Color(0.02f, 0.2f, 0.05f);
            }
            else shownLabel = "";

            if (label != null)
            {
                age = t - labelAt;
                // pop : arrive grand, rebondit, se pose
                float pop = age < 0.22f ? Mathf.Lerp(1.9f, 0.9f, age / 0.22f) : age < 0.34f ? Mathf.Lerp(0.9f, 1f, (age - 0.22f) / 0.12f) : 1f;
                float alpha = label == "GO!" ? Mathf.Clamp01((1.1f - age) / 0.3f) : 1f;
                if (label == "GO!") pop *= 1f + Mathf.Max(0f, age - 0.6f) * 0.6f;
                bigStyle.fontSize = Mathf.RoundToInt(h * (label == "GO!" ? 0.26f : 0.3f) * pop);
                var r = new Rect(0, h * 0.12f, w, h * 0.42f);
                Outlined(r, label, bigStyle, fill, edge, alpha, Mathf.Max(3f, h * 0.012f));
            }

        }

        static void Outlined(Rect r, string text, GUIStyle s, Color fill, Color edge, float alpha, float px)
        {
            var old = s.normal.textColor;
            s.normal.textColor = new Color(0, 0, 0, 0.45f * alpha);
            GUI.Label(new Rect(r.x + px * 1.6f, r.y + px * 2.2f, r.width, r.height), text, s);      // ombre portée
            s.normal.textColor = new Color(edge.r, edge.g, edge.b, edge.a * alpha);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * px, r.y + Mathf.Sin(a) * px, r.width, r.height), text, s);
            }
            s.normal.textColor = new Color(Mathf.Min(1, fill.r + 0.35f), Mathf.Min(1, fill.g + 0.35f), Mathf.Min(1, fill.b + 0.35f), alpha);
            GUI.Label(new Rect(r.x, r.y - px * 0.35f, r.width, r.height), text, s);                // reflet
            s.normal.textColor = new Color(fill.r, fill.g, fill.b, alpha);
            GUI.Label(new Rect(r.x, r.y + px * 0.25f, r.width, r.height), text, s);
            s.normal.textColor = old;
        }

        void OnGUI()
        {
            DrawCountdown();
            if (!cinematic && fade <= 0.001f) return;
            float w = Screen.width, h = Screen.height;
            GUI.depth = -100;
            if (bars > 0.001f)
            {
                float bh = h * 0.1f * bars;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, w, bh), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, h - bh, w, bh), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (cinematic && bars > 0.5f)
            {
                if (nameStyle == null)
                {
                    nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.LowerLeft };
                    subStyle = new GUIStyle(nameStyle) { fontStyle = FontStyle.Bold };
                    hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
                }
                nameStyle.fontSize = Mathf.RoundToInt(h * 0.055f);
                subStyle.fontSize = Mathf.RoundToInt(h * 0.026f);
                hintStyle.fontSize = Mathf.RoundToInt(h * 0.022f);
                float y = h * 0.9f - h * 0.02f;
                Label(new Rect(w * 0.05f, y - h * 0.12f, w * 0.9f, h * 0.05f), "DEVINCI KART", subStyle, new Color(1f, 0.85f, 0.2f));
                Label(new Rect(w * 0.05f, y - h * 0.08f, w * 0.9f, h * 0.08f), circuitName, nameStyle, Color.white);
                hintStyle.normal.textColor = new Color(1, 1, 1, 0.75f);
                GUI.Label(new Rect(0, h * 0.95f - h * 0.02f, w * 0.97f, h * 0.04f), "Entrée / A : passer", hintStyle);
            }
            if (fade > 0.001f)
            {
                GUI.color = new Color(0, 0, 0, fade);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        static void Label(Rect r, string text, GUIStyle s, Color c)
        {
            s.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, s);
            s.normal.textColor = c;
            GUI.Label(r, text, s);
        }

        void OnDestroy()
        {
            if (mainCam) mainCam.enabled = true;
            Active = false;
            KartRaceSkills.CinematicActive = false;
            KartRaceSkills.StartBlocked = false;
        }
    }
}
