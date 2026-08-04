using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MBG.Core
{
    /// <summary>
    /// Membuat / melengkapi GameObject root "__Systems" tempat semua service core
    /// menempel.
    ///
    /// Additive dan idempoten: dijalankan berkali-kali tidak pernah menduplikasi
    /// GameObject maupun komponen, dan tidak menyentuh apa pun di luar "__Systems"
    /// — termasuk seluruh hierarki __Placeholder yang sudah di-tuning tangan.
    /// </summary>
    public static class SystemsObjectSetup
    {
        const string SystemsName = "__Systems";
        const string MenuPath = "Tools/MBG/Setup Systems Object";

        [MenuItem(MenuPath, false, 100)]
        public static void SetupSystemsObject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Setup Systems Object",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.",
                    "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MBG] Tidak ada scene aktif yang valid. Buka Assets/Scenes/SampleScene.unity dulu.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Setup {SystemsName}");

            bool changed = false;
            GameObject systems = FindRootObject(scene, SystemsName);

            if (systems == null)
            {
                systems = new GameObject(SystemsName);
                Undo.RegisterCreatedObjectUndo(systems, $"Create {SystemsName}");
                systems.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                systems.transform.localScale = Vector3.one;
                changed = true;
            }

            // Urutan penambahan mengikuti urutan inisialisasi di GameBootstrap.
            changed |= EnsureComponent<GameBootstrap>(systems);
            changed |= EnsureComponent<GameClock>(systems);
            changed |= EnsureComponent<AudioService>(systems);
            changed |= EnsureComponent<EconomyService>(systems);
            changed |= EnsureComponent<GameManager>(systems);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = systems;
                Debug.Log($"[MBG] '{SystemsName}' siap: GameBootstrap + GameClock + AudioService + " +
                          "EconomyService + GameManager. Jangan lupa simpan scene (Ctrl+S).", systems);
            }
            else
            {
                Debug.Log($"[MBG] '{SystemsName}' sudah lengkap. Tidak ada perubahan.", systems);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateSetupSystemsObject() => !EditorApplication.isPlayingOrWillChangePlaymode;

        static bool EnsureComponent<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() != null) return false;

            Undo.AddComponent<T>(target);
            return true;
        }

        static GameObject FindRootObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }
    }
}
