using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DeVinciKart
{
    /// <summary>Touche M (dans n'importe quelle piste) : retour au menu de choix des circuits, s'il est dans le build.</summary>
    public class MenuReturn : MonoBehaviour
    {
        public const string MenuScene = "Menu_Choix_Circuit";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("DeVinciKart_MenuReturn") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<MenuReturn>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.mKey.wasPressedThisFrame) return;
            if (SceneManager.GetActiveScene().name == MenuScene) return;
            if (!Application.CanStreamedLevelBeLoaded(MenuScene)) return;
            Time.timeScale = 1f;
            SceneManager.LoadScene(MenuScene);
        }
    }
}
