using System.Collections;
using System.Collections.Generic;
using KartLab;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DeVinciKart
{
    /// <summary>
    /// Course en plusieurs tours : compte à rebours KartLab, portes à franchir dans l'ordre (la porte 0 est la ligne),
    /// temps par tour, meilleur tour, écran d'arrivée. Entrée = recommencer après l'arrivée.
    /// </summary>
    public class LapRaceManager : MonoBehaviour
    {
        public static LapRaceManager Instance { get; private set; }

        public int totalLaps = 3;
        public LapGate[] gates;
        public KartMotor kart;
        public bool useCountdown = true;

        public int Lap { get; private set; } = 1;
        public bool Racing { get; private set; }
        public bool Finished { get; private set; }
        public float RaceTime => Finished ? finishTime : Racing ? Time.time - raceStart : 0f;
        public IReadOnlyList<float> LapTimes => lapTimes;

        readonly List<float> lapTimes = new List<float>();
        KartRaceSkills skills;
        int nextGate = 1;
        float raceStart, lapStart, finishTime, messageUntil;
        bool waitingCountdown;
        string message = "";
        TextMesh dashText;
        GUIStyle big, small;

        void Awake() => Instance = this;

        void Start()
        {
            if (!kart) kart = FindAnyObjectByType<KartMotor>();
            if (gates != null) for (int i = 0; i < gates.Length; i++) if (gates[i]) { gates[i].index = i; gates[i].manager = this; }
            if (gates == null || gates.Length < 2) nextGate = 0;
            skills = kart ? kart.GetComponent<KartRaceSkills>() : null;
            CreateDashText();
            if (useCountdown && skills != null) StartCoroutine(Countdown());
            else StartRace();
        }

        IEnumerator Countdown()
        {
            while (RaceIntro.Active) yield return null;           // la cinématique lance elle-même le compte à rebours
            if (!skills.CountingDown)
            {
                yield return new WaitForSeconds(1f);
                skills.BeginCountdown();
            }
            waitingCountdown = true;
        }

        void StartRace()
        {
            Racing = true;
            raceStart = lapStart = Time.time;
            if (!KartRaceSkills.CustomCountdownUI) Show("GO !", 1.2f);
        }

        void Show(string text, float seconds) { message = text; messageUntil = Time.time + seconds; }

        /// <summary>Appelé par LapGate quand le kart franchit une porte dans le bon sens.</summary>
        public void OnGate(int index, KartMotor who)
        {
            if (!Racing || Finished || who != kart || gates == null || gates.Length == 0) return;
            if (index != nextGate) return;
            if (index != 0) { nextGate = (index + 1) % gates.Length; return; }

            lapTimes.Add(Time.time - lapStart);
            lapStart = Time.time;
            if (Lap >= totalLaps)
            {
                Finished = true;
                Racing = false;
                finishTime = Time.time - raceStart;
                Show("ARRIVÉE !", 999f);
                kart.Notify("FINISH");
                return;
            }
            Lap++;
            nextGate = gates.Length > 1 ? 1 : 0;
            Show(Lap == totalLaps ? "DERNIER TOUR !" : $"TOUR {Lap}/{totalLaps}", 2f);
            kart.Notify("LAP " + Lap);
        }

        void Update()
        {
            if (waitingCountdown && skills != null && !skills.CountingDown)
            {
                waitingCountdown = false;
                StartRace();
            }
            if (Finished && kart && kart.input)
            {
                // le kart freine tout seul après la ligne
                kart.input.useExternalInput = true;
                kart.input.externalInput = new DriveInput { brake = 1f };
            }
            var kb = Keyboard.current;
            if (Finished && kb != null && kb.enterKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            if (dashText) dashText.text = Finished ? $"ARRIVÉE  {Format(finishTime)}" : $"TOUR {Lap}/{totalLaps}   {Format(RaceTime)}";
        }

        /// <summary>Petit affichage dans le cockpit, lisible aussi dans le casque VR.</summary>
        void CreateDashText()
        {
            if (!kart) return;
            var go = new GameObject("Affichage_Tours");
            go.transform.SetParent(kart.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.32f, 1.25f);
            go.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            dashText = go.AddComponent<TextMesh>();
            dashText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dashText.GetComponent<MeshRenderer>().sharedMaterial = dashText.font.material;
            dashText.fontSize = 64;
            dashText.characterSize = 0.006f;
            dashText.anchor = TextAnchor.MiddleCenter;
            dashText.alignment = TextAlignment.Center;
            dashText.color = new Color(1f, 0.9f, 0.3f);
        }

        public static string Format(float t)
        {
            int m = (int)(t / 60f);
            return $"{m:00}:{t - m * 60f:00.00}";
        }

        void OnGUI()
        {
            if (KartRaceSkills.CinematicActive) return;
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                small = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            }
            big.fontSize = Mathf.RoundToInt(Screen.height * 0.1f);
            small.fontSize = Mathf.RoundToInt(Screen.height * 0.032f);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"TOUR {Mathf.Min(Lap, totalLaps)}/{totalLaps}");
            sb.AppendLine(Format(RaceTime));
            float best = float.MaxValue;
            for (int i = 0; i < lapTimes.Count; i++) { sb.AppendLine($"T{i + 1}  {Format(lapTimes[i])}"); best = Mathf.Min(best, lapTimes[i]); }
            if (lapTimes.Count > 0) sb.AppendLine($"Meilleur  {Format(best)}");
            Shadow(new Rect(0, 20, Screen.width - 24, Screen.height * 0.5f), sb.ToString(), small);

            if (Time.time < messageUntil)
            {
                string txt = Finished ? $"ARRIVÉE !\n{Format(finishTime)}" : message;
                Shadow(new Rect(0, 0, Screen.width, Screen.height * 0.6f), txt, big);
                if (Finished)
                    Shadow(new Rect(0, Screen.height * 0.6f, Screen.width, Screen.height * 0.1f), "Entrée : recommencer   ·   M : menu des circuits",
                        new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            }
        }

        static void Shadow(Rect r, string text, GUIStyle style)
        {
            var c = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.75f);
            GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, style);
            style.normal.textColor = Color.white;
            GUI.Label(r, text, style);
            style.normal.textColor = c;
        }
    }
}
