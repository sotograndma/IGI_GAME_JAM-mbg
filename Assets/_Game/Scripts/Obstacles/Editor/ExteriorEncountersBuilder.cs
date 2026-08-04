using System.Collections.Generic;
using MBG.Data;
using MBG.Kitchen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MBG.Obstacles
{
    /// <summary>
    /// Menyiapkan titik-titik encounter di luar rumah dan sisi runtime gangguan
    /// Ormas.
    ///
    /// Additive dan idempoten. Yang TIDAK pernah disentuh di ExteriorRoot: Grid,
    /// Ground_Tilemap, Border_*, House, Door_Exterior, ExteriorSpawn. Door_Exterior
    /// hanya DIBACA untuk menempatkan titik kumpul di depan rumah.
    /// </summary>
    public static class ExteriorEncountersBuilder
    {
        const string MenuPath = "Tools/MBG/Build Exterior Encounters";

        const string SystemsName = "__Systems";
        const string ExteriorRootName = "ExteriorRoot";
        const string InteriorRootName = "InteriorRoot";
        const string DoorExteriorName = "Door_Exterior";
        const string EncounterPointsName = "EncounterPoints";
        const string IntrusionPointsName = "IntrusionPoints";

        const string OrmasPointName = "OrmasSpawnPoint";
        const string SantetPointName = "SantetCasterPoint";
        const string TaxPointName = "TaxCollectorPoint";
        const string IntruderPointName = "OrmasIntruderPoint";

        const string DataFolder = "Assets/_Game/Data";
        const string OrmasConfigPath = DataFolder + "/OrmasConfig.asset";
        const string KitchenLayoutPath = DataFolder + "/KitchenLayout.asset";
        const string SquareSpritePath = "Assets/_Placeholder/Sprites/square.png";

        // Offset dari Door_Exterior, dalam unit dunia.
        static readonly Vector2 OrmasOffset = new Vector2(2.6f, 0f);
        static readonly Vector2 SantetOffset = new Vector2(-3.4f, 1.2f);
        static readonly Vector2 TaxOffset = new Vector2(4.6f, 0f);

        [MenuItem(MenuPath, false, 109)]
        public static void BuildExteriorEncounters()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Exterior Encounters",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MBG] Tidak ada scene aktif yang valid.");
                return;
            }

            GameObject exteriorRoot = FindInScene(scene, ExteriorRootName);
            if (exteriorRoot == null)
            {
                Debug.LogError($"[MBG] '{ExteriorRootName}' tidak ditemukan di scene aktif.");
                return;
            }

            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (square == null)
            {
                Debug.LogError($"[MBG] Sprite placeholder '{SquareSpritePath}' tidak ditemukan.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Exterior Encounters");

            var log = new List<string>();
            bool changed = false;

            OrmasConfigSO ormasConfig = EnsureOrmasConfig(log, ref changed);

            Transform points = EnsureChild(exteriorRoot.transform, EncounterPointsName, log, ref changed);
            Vector3 doorPosition = ResolveDoorPosition(exteriorRoot);

            Transform ormasPoint = EnsurePoint(points, OrmasPointName, doorPosition + (Vector3)OrmasOffset, log, ref changed);
            EnsurePoint(points, SantetPointName, doorPosition + (Vector3)SantetOffset, log, ref changed);
            EnsurePoint(points, TaxPointName, doorPosition + (Vector3)TaxOffset, log, ref changed);

            Transform intruderPoint = EnsureIntruderPoint(scene, log, ref changed);

            changed |= EnsureEncounterController(scene, ormasConfig, ormasPoint, intruderPoint, square, log);
            changed |= EnsureScreenShake(scene, log);
            changed |= BindObstacleManager(scene, ormasConfig, log);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[MBG] Build Exterior Encounters selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).");
            }
            else
            {
                Debug.Log("[MBG] Titik encounter sudah lengkap. Tidak ada perubahan.");
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildExteriorEncounters() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- Asset ----------------------------------------------------------------

        static OrmasConfigSO EnsureOrmasConfig(List<string> log, ref bool changed)
        {
            var config = AssetDatabase.LoadAssetAtPath<OrmasConfigSO>(OrmasConfigPath);
            if (config != null)
            {
                if (config.taunts != null && config.taunts.Count > 0) return config;

                // Asset ada tapi daftar provokasinya kosong — lengkapi tanpa menimpa
                // angka yang mungkin sudah di-tune.
                config.ApplyDefaultTaunts();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();

                changed = true;
                log.Add("Daftar provokasi OrmasConfig diisi.");
                return config;
            }

            EnsureFolder(DataFolder);

            config = ScriptableObject.CreateInstance<OrmasConfigSO>();
            config.ApplyDefaultTaunts();
            AssetDatabase.CreateAsset(config, OrmasConfigPath);
            AssetDatabase.SaveAssets();

            changed = true;
            log.Add($"{OrmasConfigPath} dibuat (meter 22s, dorong 0.045/tekan, batas 15s). Tidak ikut ter-undo.");
            return config;
        }

        // ---- Titik ------------------------------------------------------------------

        /// <summary>Door_Exterior hanya dibaca, tidak pernah diubah.</summary>
        static Vector3 ResolveDoorPosition(GameObject exteriorRoot)
        {
            foreach (Transform t in exteriorRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == DoorExteriorName) return t.position;
            }

            Debug.LogWarning($"[MBG] '{DoorExteriorName}' tidak ditemukan — titik encounter dipasang di sekitar origin.");
            return Vector3.zero;
        }

        static Transform EnsureChild(Transform parent, string name, List<string> log, ref bool changed)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Undo.SetTransformParent(go.transform, parent, $"Parent {name}");

            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            changed = true;
            log.Add($"'{name}' dibuat di bawah '{parent.name}'.");
            return go.transform;
        }

        static Transform EnsurePoint(Transform parent, string name, Vector3 worldPosition,
                                     List<string> log, ref bool changed)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Undo.SetTransformParent(go.transform, parent, $"Parent {name}");

            go.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            changed = true;
            log.Add($"{name} dibuat di ({worldPosition.x:0.##}, {worldPosition.y:0.##}).");
            return go.transform;
        }

        /// <summary>Titik munculnya penerobos di dalam dapur, tepat di depan pintu interior.</summary>
        static Transform EnsureIntruderPoint(Scene scene, List<string> log, ref bool changed)
        {
            GameObject interiorRoot = FindInScene(scene, InteriorRootName);
            if (interiorRoot == null) return null;

            Transform parent = EnsureChild(interiorRoot.transform, IntrusionPointsName, log, ref changed);

            var layout = AssetDatabase.LoadAssetAtPath<KitchenLayoutSO>(KitchenLayoutPath);
            float x = layout != null ? layout.roomCenterX + layout.doorOffsetX : 1000.145f;
            float y = layout != null ? layout.floorSurfaceY : -2.81f;

            return EnsurePoint(parent, IntruderPointName, new Vector3(x, y, 0f), log, ref changed);
        }

        // ---- Komponen -------------------------------------------------------------------

        static bool EnsureEncounterController(Scene scene, OrmasConfigSO config, Transform spawnPoint,
                                              Transform intruderPoint, Sprite square, List<string> log)
        {
            GameObject systems = FindInScene(scene, SystemsName);
            if (systems == null)
            {
                Debug.LogError($"[MBG] '{SystemsName}' belum ada. Jalankan Tools > MBG > Setup Systems Object dulu.");
                return false;
            }

            bool changed = false;

            var controller = systems.GetComponent<OrmasEncounterController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<OrmasEncounterController>(systems);
                changed = true;
                log.Add($"OrmasEncounterController ditambahkan ke '{SystemsName}'.");
            }

            var so = new SerializedObject(controller);
            bool bound = false;
            bound |= SetObject(so, "config", config);
            bound |= SetObject(so, "spawnPoint", spawnPoint);
            bound |= SetObject(so, "intruderPoint", intruderPoint);
            bound |= SetObject(so, "placeholderSprite", square);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi OrmasEncounterController diikat.");
            }

            return changed;
        }

        static bool EnsureScreenShake(Scene scene, List<string> log)
        {
            GameObject systems = FindInScene(scene, SystemsName);
            if (systems == null) return false;

            bool changed = false;

            var shaker = systems.GetComponent<ScreenShakeService>();
            if (shaker == null)
            {
                shaker = Undo.AddComponent<ScreenShakeService>(systems);
                changed = true;
                log.Add($"ScreenShakeService ditambahkan ke '{SystemsName}'.");
            }

            var cameraFollow = Object.FindAnyObjectByType<CameraFollow2D>(FindObjectsInactive.Include);
            if (cameraFollow == null) return changed;

            var so = new SerializedObject(shaker);
            if (SetObject(so, "cameraFollow", cameraFollow))
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("ScreenShakeService diikat ke kamera.");
            }

            return changed;
        }

        static bool BindObstacleManager(Scene scene, OrmasConfigSO config, List<string> log)
        {
            GameObject systems = FindInScene(scene, SystemsName);
            if (systems == null) return false;

            var manager = systems.GetComponent<ObstacleManager>();
            if (manager == null)
            {
                Debug.LogWarning("[MBG] ObstacleManager belum ada. Jalankan Tools > MBG > Build Obstacle Setup dulu.");
                return false;
            }

            var so = new SerializedObject(manager);
            if (!SetObject(so, "ormasConfig", config)) return false;

            so.ApplyModifiedProperties();
            log.Add("ObstacleManager.ormasConfig diikat ke OrmasConfig.asset.");
            return true;
        }

        // ---- Util ----------------------------------------------------------------------------

        static bool SetObject(SerializedObject so, string path, Object value)
        {
            SerializedProperty prop = so.FindProperty(path);
            if (prop == null || prop.objectReferenceValue == value) return false;

            prop.objectReferenceValue = value;
            return true;
        }

        static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;

                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name) return t.gameObject;
                }
            }
            return null;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
