using System.IO;
using System.Text;
using KartLab;
using UnityEngine;

namespace DeVinciKart
{
    /// <summary>
    /// Test automatique : pilote le kart KartLab le long de la TrackRoute du départ à l'arrivée
    /// et écrit un rapport (progression, sauts, retours sur piste, blocages) dans Logs/DeVinciKart_TestPiste.txt.
    /// Ajouté temporairement par le menu « DeVinci Kart > Tester la piste (pilote auto) ». Désactivé par défaut.
    /// </summary>
    public class TrackAutopilotTest : MonoBehaviour
    {
        public const string SessionKey = "DeVinciKart.Autopilot", RouteKey = "DeVinciKart.AutopilotRoute";
        public KartMotor motor;
        public TrackRoute route;
        public float lookAhead = 14f, maxSeconds = 420f, timeScale = 3f;

#if UNITY_EDITOR
        // Installé uniquement quand le menu « Tester la piste (pilote auto) » vient d'être utilisé : la scène n'est pas modifiée.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
            UnityEditor.SessionState.SetBool(SessionKey, false);
            var m = FindAnyObjectByType<KartMotor>();
            if (m != null) m.gameObject.AddComponent<TrackAutopilotTest>();
            else Debug.LogWarning("[TestPiste] Aucun KartMotor dans la scène.");
        }
#endif

        KartInput input;
        KartRecovery recovery;
        int bestIndex;
        float t0, airStart = -1f, maxAir, maxHeight, stuckSince = -1f;
        int jumps, recoveriesAtStart;
        readonly StringBuilder log = new StringBuilder();
        bool done;

        void Start()
        {
            if (!motor) motor = FindAnyObjectByType<KartMotor>();
            #if UNITY_EDITOR
            string wanted = UnityEditor.SessionState.GetString(RouteKey, "");
            UnityEditor.SessionState.SetString(RouteKey, "");
            if (!route && wanted.Length > 0)
                foreach (var r in Resources.FindObjectsOfTypeAll<TrackRoute>())
                    if (r.gameObject.scene.IsValid() && r.gameObject.name == wanted) { route = r; break; }
#endif
            if (!route) foreach (var r in TrackRoute.All) if (!r.alternative) { route = r; break; }
            if (!route) route = FindAnyObjectByType<TrackRoute>();
            // le pilote auto ne sait pas esquiver : on neutralise les obstacles mobiles pour tester le tracé lui-même
            int movers = 0;
            foreach (var m in FindObjectsByType<PingPongMover>(FindObjectsSortMode.None))
            {
                foreach (var c in m.GetComponentsInChildren<Collider>()) c.enabled = false;
                movers++;
            }
            input = motor.input;
            recovery = motor.GetComponent<KartRecovery>();
            input.useExternalInput = true;
            recoveriesAtStart = recovery ? recovery.RecoveryCount : 0;
            Time.timeScale = timeScale;
            t0 = Time.time;
            Log($"Départ : {motor.transform.position}  route « {route.name} »  points : {route.points.Length}  (obstacles mobiles neutralisés : {movers})");
        }

        void Log(string s) { log.AppendLine($"[{Time.time - t0,6:0.0}s] {s}"); Debug.Log("[TestPiste] " + s); }

        int lastIdx, wraps, lastLapsLogged;

        int NearestIndex(Vector3 p)
        {
            int n = route.points.Length;
            int best = lastIdx; float bd = float.MaxValue;
            for (int k = -20; k < 60; k++)
            {
                int i = lastIdx + k;
                if (route.closed) i = ((i % n) + n) % n;
                else if (i < 0 || i >= n) continue;
                float d = (route.transform.TransformPoint(route.points[i]) - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        void FixedUpdate()
        {
            if (done || !motor || !route) return;
            Vector3 pos = motor.transform.position;
            int n = route.points.Length;
            int idx = NearestIndex(pos);
            if (route.closed)
            {
                if (idx < lastIdx - n / 2) wraps++;
                else if (idx > lastIdx + n / 2) wraps--;
            }
            lastIdx = idx;
            int progress = wraps * n + idx;
            if (progress > bestIndex) bestIndex = progress;
            var race = LapRaceManager.Instance;
            if (race != null && race.LapTimes.Count > lastLapsLogged)
            {
                lastLapsLogged = race.LapTimes.Count;
                Log($"TOUR {lastLapsLogged} bouclé en {LapRaceManager.Format(race.LapTimes[lastLapsLogged - 1])}");
            }

            // point visé un peu plus loin sur la ligne centrale
            int ahead = idx + Mathf.CeilToInt(lookAhead / 2f);
            ahead = route.closed ? ahead % n : Mathf.Min(n - 1, ahead);
            Vector3 target = route.transform.TransformPoint(route.points[ahead]);
            Vector3 to = Vector3.ProjectOnPlane(target - pos, Vector3.up);
            Vector3 fwd = Vector3.ProjectOnPlane(motor.transform.forward, Vector3.up);
            float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
            float steer = Mathf.Clamp(angle / 35f, -1f, 1f);
            float throttle = Mathf.Abs(angle) > 50f ? 0.4f : 1f;
            bool waiting = race != null && !race.Racing && !race.Finished;   // compte à rebours
            input.externalInput = waiting ? default(DriveInput) : new DriveInput { throttle = throttle, steering = steer };

            // sauts / temps en l'air
            if (!motor.Grounded) { if (airStart < 0) airStart = Time.time; maxHeight = Mathf.Max(maxHeight, pos.y); }
            else if (airStart >= 0)
            {
                float air = Time.time - airStart;
                if (air > 0.35f) { jumps++; Log($"Saut : {air:0.00}s en l'air, point {idx}, hauteur max {maxHeight:0.0} m"); }
                maxAir = Mathf.Max(maxAir, air); airStart = -1f;
            }

            // blocage
            if (motor.Speed < 1f && !motor.Resetting && !waiting) { if (stuckSince < 0) stuckSince = Time.time; }
            else stuckSince = -1f;

            bool finished = race != null ? race.Finished : route.closed ? wraps >= 3 : bestIndex >= n - 3;
            bool stuck = stuckSince > 0 && Time.time - stuckSince > 8f;
            bool timeout = Time.time - t0 > maxSeconds;
            if (finished || stuck || timeout)
            {
                done = true;
                input.externalInput = default;
                int rec = recovery ? recovery.RecoveryCount - recoveriesAtStart : -1;
                Log(finished ? "RÉSULTAT : ARRIVÉE ATTEINTE" : stuck ? $"RÉSULTAT : BLOQUÉ à {pos}" : "RÉSULTAT : TEMPS ÉCOULÉ");
                if (race != null && race.Finished) Log($"Course en {race.LapTimes.Count} tours : {LapRaceManager.Format(race.RaceTime)}");
                Log($"Progression {bestIndex}/{route.points.Length - 1}  durée {Time.time - t0:0.0}s  sauts {jumps}  plus long saut {maxAir:0.00}s  retours sur piste automatiques {rec}");
                File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/DeVinciKart_TestPiste.txt"), log.ToString());
                Time.timeScale = 1f;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
            else if (Time.frameCount % 250 == 0)
                Log($"point {bestIndex}/{route.points.Length - 1}  vitesse {motor.Speed * 3.6f:0} km/h  pos {pos}");
        }
    }
}
