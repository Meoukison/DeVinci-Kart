using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DeVinciKart
{
    /// <summary>
    /// Compte à rebours (feux), checkpoints dans l'ordre, chrono, arrivée, replacement du kart.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        public KartController kart;
        public Checkpoint[] checkpoints;
        public Renderer[] startLights;
        public Transform startPoint;
        public float countdownStep = 1f;
        public float fallY = -1.5f;

        enum State { Countdown, Racing, Finished }
        State state = State.Countdown;
        int nextCheckpoint;
        Transform respawn;
        float startTime, finishTime;
        string bigText = "";
        float bigTextUntil;
        MaterialPropertyBlock mpb;
        GUIStyle bigStyle, smallStyle;

        void Awake()
        {
            Instance = this;
            mpb = new MaterialPropertyBlock();
        }

        void Start()
        {
            respawn = startPoint;
            if (kart) kart.controlsLocked = true;
            for (int i = 0; i < startLights.Length; i++) SetLight(i, Color.black);
            StartCoroutine(Countdown());
        }

        IEnumerator Countdown()
        {
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < startLights.Length; i++)
            {
                SetLight(i, new Color(1f, 0.05f, 0.05f));
                Show((startLights.Length - i).ToString(), countdownStep);
                yield return new WaitForSeconds(countdownStep);
            }
            for (int i = 0; i < startLights.Length; i++) SetLight(i, new Color(0.1f, 1f, 0.2f));
            Show("GO !", 1.2f);
            state = State.Racing;
            startTime = Time.time;
            if (kart) kart.controlsLocked = false;
            yield return new WaitForSeconds(3f);
            for (int i = 0; i < startLights.Length; i++) SetLight(i, Color.black);
        }

        void SetLight(int i, Color c)
        {
            var r = startLights[i];
            if (!r) return;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c == Color.black ? new Color(0.1f, 0.1f, 0.1f) : c);
            mpb.SetColor("_EmissionColor", c * 4f);
            r.SetPropertyBlock(mpb);
        }

        void Show(string text, float duration)
        {
            bigText = text;
            bigTextUntil = Time.time + duration;
        }

        public void PassCheckpoint(Checkpoint cp, KartController k)
        {
            if (state != State.Racing || k != kart) return;
            if (cp.index != nextCheckpoint) return;
            nextCheckpoint++;
            respawn = cp.transform;
            if (cp.isFinish)
            {
                state = State.Finished;
                finishTime = Time.time - startTime;
                kart.controlsLocked = true;
                Show("ARRIVÉE !", 6f);
            }
        }

        void Update()
        {
            if (!kart) return;
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            bool reset = (kb != null && kb.rKey.wasPressedThisFrame) || (gp != null && gp.selectButton.wasPressedThisFrame);
            if (state == State.Racing && (reset || kart.transform.position.y < fallY)) Respawn();

            bool restart = (kb != null && kb.enterKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
            if (state == State.Finished && restart) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void Respawn()
        {
            if (!respawn) return;
            Vector3 fwd = Vector3.ProjectOnPlane(respawn.forward, Vector3.up).normalized;
            kart.Teleport(respawn.position + Vector3.up * 0.6f, Quaternion.LookRotation(fwd, Vector3.up));
        }

        static string FormatTime(float t)
        {
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return $"{m:00}:{s:00.00}";
        }

        void OnGUI()
        {
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                smallStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            }
            bigStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.12f);
            smallStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.035f);

            float time = state == State.Racing ? Time.time - startTime : state == State.Finished ? finishTime : 0f;
            int total = checkpoints != null ? checkpoints.Length : 0;
            string hud = $"Temps  {FormatTime(time)}\nCheckpoint  {Mathf.Min(nextCheckpoint, total)}/{total}";
            if (kart) hud += $"\nObjet  {kart.heldItem}" + (kart.heldItem != KartItem.Aucun ? "  (E / A)" : "");
            if (kart) hud += $"\n{Mathf.RoundToInt(Mathf.Abs(kart.Speed) * 3.6f)} km/h";
            DrawShadowed(new Rect(20, 20, Screen.width, Screen.height * 0.4f), hud, smallStyle);

            if (Time.time < bigTextUntil || state == State.Finished)
            {
                string txt = state == State.Finished
                    ? $"ARRIVÉE !\n{FormatTime(finishTime)}"
                    : bigText;
                DrawShadowed(new Rect(0, 0, Screen.width, Screen.height * 0.8f), txt, bigStyle);
                if (state == State.Finished)
                    DrawShadowed(new Rect(0, Screen.height * 0.75f, Screen.width, Screen.height * 0.1f),
                        "Entrée / Start pour recommencer", new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter });
            }
        }

        static void DrawShadowed(Rect r, string text, GUIStyle style)
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
