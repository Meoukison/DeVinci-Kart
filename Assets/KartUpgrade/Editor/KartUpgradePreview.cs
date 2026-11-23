using KartLab;
using UnityEditor;
using UnityEngine;

namespace DeVinciKart.Upgrade.Editor
{
    public static class KartUpgradePreview
    {
        internal const string PrefabPath = "Assets/KartUpgrade/PlayerKart_DeVinci_Upgrade.prefab";
        private const string MenuPath = "DeVinci Kart/Kart Upgrade/Tester sur le kart sélectionné (Play)";

        [MenuItem(MenuPath, true)]
        private static bool CanTry()
        {
            return EditorApplication.isPlaying && SelectedKart() != null;
        }

        [MenuItem(MenuPath)]
        private static void TrySelected()
        {
            var kart = SelectedKart();
            if (!EditorApplication.isPlaying || !kart) return;
            if (kart.TryGetComponent<KartUpgrade>(out var current))
            {
                current.enabled = !current.enabled;
                Debug.Log(current.enabled ? "Kart Upgrade : activé" : "Kart Upgrade : désactivé", kart);
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var preset = prefab ? prefab.GetComponent<KartUpgrade>() : null;
            if (!preset)
            {
                Debug.LogError("Kart Upgrade : prefab de configuration introuvable.", kart);
                return;
            }

            var upgrade = kart.gameObject.AddComponent<KartUpgrade>();
            EditorUtility.CopySerializedManagedFieldsOnly(preset, upgrade);
            Debug.Log("Kart Upgrade : activé pour cette session Play.", kart);
        }

        private static KartMotor SelectedKart()
        {
            var selected = Selection.activeGameObject;
            if (!selected || !selected.scene.IsValid()) return null;
            return selected.GetComponentInParent<KartMotor>();
        }
    }
}
