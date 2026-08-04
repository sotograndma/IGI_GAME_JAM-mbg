using System.Collections.Generic;
using MBG.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MBG.QTE
{
    /// <summary>
    /// Menyiapkan seluruh framework QTE dalam satu langkah:
    /// tiga preset kesulitan, komponen QTEController di __Systems, dan isi visual
    /// QTEPanel.
    ///
    /// Additive dan idempoten. Tata letak hanya ditulis untuk elemen yang baru
    /// dibuat, jadi menggeser bar secara manual tidak akan tertimpa saat tool
    /// dijalankan ulang. Referensi komponen selalu diikat ulang supaya tidak ada
    /// field yang menggantung.
    /// </summary>
    public static class QTESetupBuilder
    {
        const string MenuPath = "Tools/MBG/Build QTE Setup";

        const string SystemsName = "__Systems";
        const string DataFolder = "Assets/_Game/Data";
        const string QteFolder = DataFolder + "/QTE";

        // Tata letak panel dalam piksel referensi canvas (1920x1080).
        const float FrameWidth = 760f;
        const float FrameHeight = 200f;
        const float FrameBottomMargin = 140f;
        const float FeedbackHeight = 74f;
        const float InstructionHeight = 40f;
        const float BarHeight = 46f;
        const float BarSideMargin = 20f;
        const float BarBottomMargin = 12f;
        const float CursorWidth = 8f;

        [MenuItem(MenuPath, false, 103)]
        public static void BuildQTESetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build QTE Setup",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("TMP Essentials belum di-import",
                    "Panel QTE memakai TextMeshPro. Import dulu lewat:\n" +
                    "Window > TextMeshPro > Import TMP Essential Resources", "OK");
                Debug.LogError("[MBG] Build QTE Setup dibatalkan: TMP Essential Resources belum di-import.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MBG] Tidak ada scene aktif yang valid.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build QTE Setup");

            var log = new List<string>();
            bool changed = false;

            QTEConfigSO normal = EnsurePresets(log, ref changed);
            changed |= EnsureController(scene, normal, log);
            changed |= BuildPanel(log);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[MBG] Build QTE Setup selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).");
            }
            else
            {
                Debug.Log("[MBG] Setup QTE sudah lengkap. Tidak ada perubahan.");
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildQTESetup() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- Preset --------------------------------------------------------

        static QTEConfigSO EnsurePresets(List<string> log, ref bool changed)
        {
            EnsureFolder(QteFolder);

            EnsurePreset("QTE_Easy", 1.6f, 0.85f, 0.42f, 0.16f, 1, 0.50f, log, ref changed);
            QTEConfigSO normal = EnsurePreset("QTE_Normal", 1.2f, 1.00f, 0.30f, 0.10f, 2, 0.40f, log, ref changed);
            EnsurePreset("QTE_Hard", 0.9f, 1.25f, 0.20f, 0.06f, 3, 0.35f, log, ref changed);

            return normal;
        }

        static QTEConfigSO EnsurePreset(string name, float travel, float speed, float good, float perfect,
                                        int hits, float hold, List<string> log, ref bool changed)
        {
            string path = $"{QteFolder}/{name}.asset";

            var config = AssetDatabase.LoadAssetAtPath<QTEConfigSO>(path);
            if (config != null) return config;

            config = ScriptableObject.CreateInstance<QTEConfigSO>();
            config.travelDuration = travel;
            config.speedMultiplier = speed;
            config.goodZoneWidth = good;
            config.perfectZoneWidth = perfect;
            config.hitCount = hits;
            config.randomizeZonePosition = true;
            config.resultHoldSeconds = hold;

            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            changed = true;
            log.Add($"{path} dibuat (durasi {travel}s, good {good}, perfect {perfect}, {hits} hit). Tidak ikut ter-undo.");
            return config;
        }

        // ---- Controller ----------------------------------------------------

        static bool EnsureController(Scene scene, QTEConfigSO normal, List<string> log)
        {
            GameObject systems = FindRoot(scene, SystemsName);
            if (systems == null)
            {
                Debug.LogError($"[MBG] '{SystemsName}' belum ada. Jalankan Tools > MBG > Setup Systems Object dulu.");
                return false;
            }

            bool changed = false;

            var controller = systems.GetComponent<QTEController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<QTEController>(systems);
                changed = true;
                log.Add($"QTEController ditambahkan ke '{SystemsName}'.");
            }

            var so = new SerializedObject(controller);
            SerializedProperty prop = so.FindProperty("defaultConfig");
            if (prop != null && prop.objectReferenceValue != normal)
            {
                prop.objectReferenceValue = normal;
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("QTEController.defaultConfig diikat ke QTE_Normal (dipakai debug key F2).");
            }

            return changed;
        }

        // ---- Panel ---------------------------------------------------------

        static bool BuildPanel(List<string> log)
        {
            var panel = Object.FindAnyObjectByType<QTEPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogError("[MBG] QTEPanel tidak ditemukan di scene. " +
                               "Jalankan Tools > MBG > Build UI Hierarchy dulu.");
                return false;
            }

            bool changed = false;
            var panelRect = (RectTransform)panel.transform;

            RectTransform frame = EnsureChild(panelRect, "Frame", out bool created);
            if (created)
            {
                frame.anchorMin = new Vector2(0.5f, 0f);
                frame.anchorMax = new Vector2(0.5f, 0f);
                frame.pivot = new Vector2(0.5f, 0f);
                frame.sizeDelta = new Vector2(FrameWidth, FrameHeight);
                frame.anchoredPosition = new Vector2(0f, FrameBottomMargin);
                changed = true;
                log.Add("QTEPanel/Frame dibuat di tengah bawah layar.");
            }

            // Teks hasil besar, paling atas.
            TMP_Text feedback = EnsureLabel(frame, "Feedback", out created);
            if (created)
            {
                RectTransform rect = feedback.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, FeedbackHeight);
                rect.anchoredPosition = Vector2.zero;
                feedback.fontSize = 48f;
                feedback.text = "";
                changed = true;
            }

            // Instruksi, tepat di bawah teks hasil.
            TMP_Text instruction = EnsureLabel(frame, "Instruction", out created);
            if (created)
            {
                RectTransform rect = instruction.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, InstructionHeight);
                rect.anchoredPosition = new Vector2(0f, -(FeedbackHeight + 4f));
                instruction.fontSize = 28f;
                instruction.text = "Tekan SPASI!";
                changed = true;
            }

            // Bar, menempel di bawah frame.
            RectTransform bar = EnsureChild(frame, "Bar", out created);
            var barImage = EnsureImage(bar);
            if (created)
            {
                bar.anchorMin = new Vector2(0f, 0f);
                bar.anchorMax = new Vector2(1f, 0f);
                bar.pivot = new Vector2(0.5f, 0f);
                bar.sizeDelta = new Vector2(-BarSideMargin * 2f, BarHeight);
                bar.anchoredPosition = new Vector2(0f, BarBottomMargin);
                barImage.color = new Color(0.08f, 0.07f, 0.10f, 0.85f);
                changed = true;
                log.Add("QTEPanel/Frame/Bar + zona + cursor dibuat.");
            }

            RectTransform goodZone = EnsureChild(bar, "GoodZone", out created);
            EnsureImage(goodZone);
            if (created)
            {
                StretchInside(goodZone, 0.35f, 0.65f);
                changed = true;
            }

            RectTransform perfectZone = EnsureChild(bar, "PerfectZone", out created);
            EnsureImage(perfectZone);
            if (created)
            {
                StretchInside(perfectZone, 0.45f, 0.55f);
                changed = true;
            }

            RectTransform cursor = EnsureChild(bar, "Cursor", out created);
            EnsureImage(cursor);
            if (created)
            {
                cursor.anchorMin = new Vector2(0f, 0f);
                cursor.anchorMax = new Vector2(0f, 1f);
                cursor.pivot = new Vector2(0.5f, 0.5f);
                cursor.sizeDelta = new Vector2(CursorWidth, 0f);
                cursor.anchoredPosition = Vector2.zero;
                changed = true;
            }

            // Referensi selalu diikat ulang.
            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "instructionLabel", instruction);
            bound |= SetObject(so, "feedbackLabel", feedback);
            bound |= SetObject(so, "barBackground", barImage);
            bound |= SetObject(so, "goodZone", goodZone);
            bound |= SetObject(so, "perfectZone", perfectZone);
            bound |= SetObject(so, "cursor", cursor);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi QTEPanel (instruksi, feedback, bar, zona, cursor) diikat.");
            }

            return changed;
        }

        static void StretchInside(RectTransform rect, float min, float max)
        {
            rect.anchorMin = new Vector2(min, 0f);
            rect.anchorMax = new Vector2(max, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ---- Helper --------------------------------------------------------

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

        static TMP_Text EnsureLabel(RectTransform parent, string name, out bool created)
        {
            RectTransform rect = EnsureChild(parent, name, out created);

            var label = rect.GetComponent<TMP_Text>();
            if (label == null)
            {
                var tmp = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.raycastTarget = false;
                label = tmp;
            }

            return label;
        }

        static Image EnsureImage(RectTransform rect)
        {
            var image = rect.GetComponent<Image>();
            if (image != null) return image;

            image = Undo.AddComponent<Image>(rect.gameObject);
            image.raycastTarget = false;
            return image;
        }

        static bool SetObject(SerializedObject so, string path, Object value)
        {
            SerializedProperty prop = so.FindProperty(path);
            if (prop == null || prop.objectReferenceValue == value) return false;

            prop.objectReferenceValue = value;
            return true;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
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
