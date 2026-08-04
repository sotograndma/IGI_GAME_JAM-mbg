using System.Collections.Generic;
using MBG.Data;
using MBG.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MBG.Obstacles
{
    /// <summary>
    /// Menyiapkan sistem gangguan: asset ObstacleConfig, ObstacleManager di
    /// __Systems, peringatan di Door_Interior, dan indikator di HUD.
    ///
    /// Door_Interior sendiri TIDAK PERNAH diubah — tool ini hanya MENAMBAHKAN
    /// komponen DoorAlertSystem dan satu child "AlertVisual" di bawahnya. Transform
    /// pintu (posisi 1000.145 / -2.002, scale 0.55806) dan collider-nya tidak
    /// disentuh sama sekali.
    /// </summary>
    public static class ObstacleSetupBuilder
    {
        const string MenuPath = "Tools/MBG/Build Obstacle Setup";

        const string SystemsName = "__Systems";
        const string DoorName = "Door_Interior";
        const string AlertRootName = "AlertVisual";
        const string DataFolder = "Assets/_Game/Data";
        const string ConfigPath = DataFolder + "/ObstacleConfig.asset";
        const string SquareSpritePath = "Assets/_Placeholder/Sprites/square.png";

        // Pintu digambar sebagai bagian dari artwork, jadi peringatan memakai bingkai
        // sendiri seukuran collider pintu (satuan lokal pintu).
        const float FrameWidth = 2.1f;
        const float FrameHeight = 2f;
        const float IconOffsetY = 1.35f;
        const float OnomatopeOffsetY = 2.25f;
        const int FrameSortingOrder = 5;
        const int LabelSortingOrder = 6;

        [MenuItem(MenuPath, false, 108)]
        public static void BuildObstacleSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Obstacle Setup",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("TMP Essentials belum di-import",
                    "Teks onomatope memakai TextMeshPro. Import dulu lewat:\n" +
                    "Window > TextMeshPro > Import TMP Essential Resources", "OK");
                Debug.LogError("[MBG] Build Obstacle Setup dibatalkan: TMP Essential Resources belum di-import.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MBG] Tidak ada scene aktif yang valid.");
                return;
            }

            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (square == null)
            {
                Debug.LogError($"[MBG] Sprite placeholder '{SquareSpritePath}' tidak ditemukan.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Obstacle Setup");

            var log = new List<string>();
            bool changed = false;

            ObstacleConfigSO config = EnsureConfig(log, ref changed);
            DoorAlertSystem alert = EnsureDoorAlert(scene, config, square, log, ref changed);
            changed |= EnsureManager(scene, config, alert, log);
            changed |= BuildHudIndicator(log);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[MBG] Build Obstacle Setup selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).");
            }
            else
            {
                Debug.Log("[MBG] Sistem gangguan sudah lengkap. Tidak ada perubahan.");
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildObstacleSetup() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- Asset ---------------------------------------------------------------

        static ObstacleConfigSO EnsureConfig(List<string> log, ref bool changed)
        {
            var config = AssetDatabase.LoadAssetAtPath<ObstacleConfigSO>(ConfigPath);
            if (config != null) return config;

            EnsureFolder(DataFolder);

            config = ScriptableObject.CreateInstance<ObstacleConfigSO>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();

            changed = true;
            log.Add($"{ConfigPath} dibuat (pengali waktu 0.5, ambang urgent 0.75). Tidak ikut ter-undo.");
            return config;
        }

        // ---- Pintu -----------------------------------------------------------------

        static DoorAlertSystem EnsureDoorAlert(Scene scene, ObstacleConfigSO config, Sprite square,
                                               List<string> log, ref bool changed)
        {
            GameObject door = FindInScene(scene, DoorName);
            if (door == null)
            {
                Debug.LogError($"[MBG] '{DoorName}' tidak ditemukan di scene aktif.");
                return null;
            }

            var alert = door.GetComponent<DoorAlertSystem>();
            if (alert == null)
            {
                alert = Undo.AddComponent<DoorAlertSystem>(door);
                changed = true;
                log.Add($"DoorAlertSystem ditambahkan ke '{DoorName}' (transform pintu tidak disentuh).");
            }

            // Semua efek hidup di child ini; transform pintu tetap seperti aslinya.
            Transform root = door.transform.Find(AlertRootName);
            if (root == null)
            {
                var go = new GameObject(AlertRootName);
                Undo.RegisterCreatedObjectUndo(go, "Create AlertVisual");
                Undo.SetTransformParent(go.transform, door.transform, "Parent AlertVisual");

                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                root = go.transform;
                changed = true;
                log.Add($"'{AlertRootName}' dibuat sebagai child pintu — hanya child ini yang bergetar.");
            }

            SpriteRenderer frame = EnsureSpriteChild(root, "Frame", square, FrameSortingOrder, out bool created);
            if (created)
            {
                Vector3 spriteSize = square.bounds.size;
                if (spriteSize.x <= 0f || spriteSize.y <= 0f) spriteSize = Vector3.one;

                frame.transform.localPosition = Vector3.zero;
                frame.transform.localScale = new Vector3(FrameWidth / spriteSize.x, FrameHeight / spriteSize.y, 1f);
                frame.color = new Color(1f, 1f, 1f, 0.55f);
                frame.enabled = false;
                changed = true;
            }

            TMP_Text icon = EnsureWorldLabel(root, "Icon", "!", 2.4f, IconOffsetY, out created);
            changed |= created;

            TMP_Text onomatope = EnsureWorldLabel(root, "Onomatope", "TOK TOK TOK", 1.4f, OnomatopeOffsetY, out created);
            changed |= created;

            var cameraFollow = Object.FindAnyObjectByType<CameraFollow2D>(FindObjectsInactive.Include);

            var so = new SerializedObject(alert);
            bool bound = false;
            bound |= SetObject(so, "shakeRoot", root);
            bound |= SetObject(so, "frame", frame);
            bound |= SetObject(so, "iconLabel", icon);
            bound |= SetObject(so, "onomatopeLabel", onomatope);
            bound |= SetObject(so, "config", config);
            if (cameraFollow != null) bound |= SetObject(so, "cameraFollow", cameraFollow);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi DoorAlertSystem diikat.");
            }

            return alert;
        }

        // ---- Manager ------------------------------------------------------------------

        static bool EnsureManager(Scene scene, ObstacleConfigSO config, DoorAlertSystem alert, List<string> log)
        {
            GameObject systems = FindInScene(scene, SystemsName);
            if (systems == null)
            {
                Debug.LogError($"[MBG] '{SystemsName}' belum ada. Jalankan Tools > MBG > Setup Systems Object dulu.");
                return false;
            }

            bool changed = false;

            var manager = systems.GetComponent<ObstacleManager>();
            if (manager == null)
            {
                manager = Undo.AddComponent<ObstacleManager>(systems);
                changed = true;
                log.Add($"ObstacleManager ditambahkan ke '{SystemsName}'.");
            }

            var so = new SerializedObject(manager);
            bool bound = false;
            bound |= SetObject(so, "config", config);
            if (alert != null) bound |= SetObject(so, "doorAlert", alert);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("ObstacleManager diikat ke ObstacleConfig dan DoorAlertSystem.");
            }

            return changed;
        }

        // ---- HUD --------------------------------------------------------------------------

        static bool BuildHudIndicator(List<string> log)
        {
            var hud = Object.FindAnyObjectByType<HUDPanel>(FindObjectsInactive.Include);
            if (hud == null)
            {
                Debug.LogError("[MBG] HUDPanel tidak ditemukan. Jalankan Tools > MBG > Build HUD dulu.");
                return false;
            }

            RectTransform slot = hud.ObstacleSlot;
            if (slot == null)
            {
                Debug.LogError("[MBG] HUDPanel.ObstacleSlot kosong. Jalankan Tools > MBG > Build HUD dulu.");
                return false;
            }

            bool changed = false;

            var group = slot.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = Undo.AddComponent<CanvasGroup>(slot.gameObject);
                group.alpha = 0f;
                group.blocksRaycasts = false;
                changed = true;
            }

            RectTransform background = EnsureChild(slot, "Background", out bool created);
            Image backgroundImage = EnsureImage(background);
            if (created)
            {
                Stretch(background);
                backgroundImage.color = new Color(0.08f, 0.07f, 0.10f, 0.80f);
                changed = true;
            }

            TMP_Text icon = EnsureLabel(slot, "IconLabel", "!", TextAlignmentOptions.Center, 40f, out created);
            if (created)
            {
                RectTransform rect = icon.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(56f, -16f);
                rect.anchoredPosition = new Vector2(12f, 0f);
                changed = true;
            }

            TMP_Text nameLabel = EnsureLabel(slot, "NameLabel", "ORMAS", TextAlignmentOptions.Left, 24f, out created);
            if (created)
            {
                RectTransform rect = nameLabel.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(-88f, 40f);
                rect.anchoredPosition = new Vector2(34f, -18f);
                changed = true;
            }

            RectTransform bar = EnsureChild(slot, "UrgencyBar", out created);
            Image barImage = EnsureImage(bar);
            if (created)
            {
                bar.anchorMin = new Vector2(0f, 0f);
                bar.anchorMax = new Vector2(1f, 0f);
                bar.pivot = new Vector2(0.5f, 0f);
                bar.sizeDelta = new Vector2(-88f, 22f);
                bar.anchoredPosition = new Vector2(34f, 22f);
                barImage.color = new Color(1f, 1f, 1f, 0.15f);
                changed = true;
            }

            RectTransform fill = EnsureChild(bar, "Fill", out created);
            EnsureImage(fill);
            if (created)
            {
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(0f, 1f);
                fill.pivot = new Vector2(0.5f, 0.5f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
                changed = true;
            }

            var so = new SerializedObject(hud);
            bool bound = false;
            bound |= SetObject(so, "obstacleGroup", group);
            bound |= SetObject(so, "obstacleIconLabel", icon);
            bound |= SetObject(so, "obstacleNameLabel", nameLabel);
            bound |= SetObject(so, "obstacleUrgencyFill", fill);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Indikator gangguan di HUD kanan bawah diisi dan diikat.");
            }

            return changed;
        }

        // ---- Helper ----------------------------------------------------------------------

        static SpriteRenderer EnsureSpriteChild(Transform parent, string name, Sprite sprite,
                                                int sortingOrder, out bool created)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                created = false;
                return existing.GetComponent<SpriteRenderer>();
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Undo.SetTransformParent(go.transform, parent, $"Parent {name}");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;

            created = true;
            return renderer;
        }

        static TMP_Text EnsureWorldLabel(Transform parent, string name, string text,
                                         float fontSize, float offsetY, out bool created)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                created = false;
                return existing.GetComponent<TMP_Text>();
            }

            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Undo.SetTransformParent(go.transform, parent, $"Parent {name}");

            go.transform.localPosition = new Vector3(0f, offsetY, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var label = go.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = fontSize;
            label.text = text;
            label.enabled = false;

            var rect = label.rectTransform;
            rect.sizeDelta = new Vector2(6f, 1.2f);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = LabelSortingOrder;

            created = true;
            return label;
        }

        static RectTransform EnsureChild(RectTransform parent, string name, out bool created)
        {
            Transform existing = parent.Find(name);
            if (existing is RectTransform rect)
            {
                created = false;
                return rect;
            }

            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Undo.SetTransformParent(go.transform, parent, $"Parent {name}");

            var newRect = (RectTransform)go.transform;
            newRect.localScale = Vector3.one;
            newRect.localRotation = Quaternion.identity;
            newRect.anchoredPosition3D = Vector3.zero;

            created = true;
            return newRect;
        }

        static TMP_Text EnsureLabel(RectTransform parent, string name, string text,
                                    TextAlignmentOptions alignment, float fontSize, out bool created)
        {
            RectTransform rect = EnsureChild(parent, name, out created);

            var label = rect.GetComponent<TMP_Text>();
            if (label != null) return label;

            var tmp = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            tmp.fontSize = fontSize;
            tmp.text = text;
            return tmp;
        }

        static Image EnsureImage(RectTransform rect)
        {
            var image = rect.GetComponent<Image>();
            if (image != null) return image;

            image = Undo.AddComponent<Image>(rect.gameObject);
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

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
