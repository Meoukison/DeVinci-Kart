using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DeVinciKart
{
    /// <summary>Menu de choix du circuit. Souris, touches 1 / 2, flèches + Entrée, ou manette (croix + A).</summary>
    public class TrackMenu : MonoBehaviour
    {
        public string[] scenes;
        public string[] titles;
        public string[] descriptions;
        int selected;
        GUIStyle title, button, desc;

        void Update()
        {
            if (scenes == null || scenes.Length == 0) return;
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Load(0);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Load(1);
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame) selected = Mathf.Max(0, selected - 1);
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) selected = Mathf.Min(scenes.Length - 1, selected + 1);
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Load(selected);
            }
            if (gp != null)
            {
                if (gp.dpad.up.wasPressedThisFrame) selected = Mathf.Max(0, selected - 1);
                if (gp.dpad.down.wasPressedThisFrame) selected = Mathf.Min(scenes.Length - 1, selected + 1);
                if (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame) Load(selected);
            }
        }

        void Load(int i)
        {
            if (scenes == null || i < 0 || i >= scenes.Length) return;
            if (!Application.CanStreamedLevelBeLoaded(scenes[i]))
            {
                Debug.LogWarning($"[DeVinci Kart] La scène « {scenes[i]} » n'est pas dans le build : générez la piste puis relancez le menu « Créer le menu de choix des circuits ».");
                return;
            }
            SceneManager.LoadScene(scenes[i]);
        }

        void OnGUI()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                desc = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
            }
            float h = Screen.height, w = Screen.width;
            title.fontSize = Mathf.RoundToInt(h * 0.09f);
            button.fontSize = Mathf.RoundToInt(h * 0.045f);
            desc.fontSize = Mathf.RoundToInt(h * 0.026f);
            GUI.Label(new Rect(0, h * 0.06f, w, h * 0.14f), "DEVINCI KART", title);
            GUI.Label(new Rect(0, h * 0.2f, w, h * 0.06f), "Choisis ton circuit", desc);
            if (scenes == null) return;
            for (int i = 0; i < scenes.Length; i++)
            {
                float y = h * (0.3f + i * 0.27f);
                var r = new Rect(w * 0.2f, y, w * 0.6f, h * 0.12f);
                var old = GUI.color;
                if (i == selected) GUI.color = new Color(1f, 0.85f, 0.3f);
                if (GUI.Button(r, titles != null && i < titles.Length ? titles[i] : scenes[i], button)) Load(i);
                GUI.color = old;
                if (descriptions != null && i < descriptions.Length)
                    GUI.Label(new Rect(w * 0.15f, y + h * 0.13f, w * 0.7f, h * 0.1f), descriptions[i], desc);
            }
            GUI.Label(new Rect(0, h * 0.9f, w, h * 0.06f), "1 / 2, flèches + Entrée, ou manette  ·  en course : M pour revenir ici", desc);
        }
    }
}
