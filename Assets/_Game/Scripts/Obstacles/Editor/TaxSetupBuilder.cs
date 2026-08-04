using System.Collections.Generic;
using System.IO;
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
    /// Menyiapkan gangguan Pajak Ilegal: naskah dialog, kutipan peraturan fiktif,
    /// panel dialog, area mengetik di QTEPanel, dan TaxPresenter di __Systems.
    ///
    /// Additive dan idempoten.
    /// </summary>
    public static class TaxSetupBuilder
    {
        const string MenuPath = "Tools/MBG/Build Tax Setup";

        const string SystemsName = "__Systems";
        const string CanvasName = "UICanvas";
        const string FadeName = "Fade";
        const string CollectorPointName = "TaxCollectorPoint";

        const string DataFolder = "Assets/_Game/Data";
        const string TaxFolder = DataFolder + "/Tax";
        const string ConfigPath = TaxFolder + "/IllegalTaxConfig.asset";
        const string DialoguePath = TaxFolder + "/TaxDialogue.asset";
        const string ChallengesPath = TaxFolder + "/TypingChallenges.asset";
        const string SquareSpritePath = "Assets/_Placeholder/Sprites/square.png";

        [MenuItem(MenuPath, false, 111)]
        public static void BuildTaxSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Tax Setup",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("TMP Essentials belum di-import",
                    "Dialog dan tantangan mengetik memakai TextMeshPro. Import dulu lewat:\n" +
                    "Window > TextMeshPro > Import TMP Essential Resources", "OK");
                Debug.LogError("[MBG] Build Tax Setup dibatalkan: TMP Essential Resources belum di-import.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MBG] Tidak ada scene aktif yang valid.");
                return;
            }

            GameObject canvas = FindInScene(scene, CanvasName);
            if (canvas == null)
            {
                Debug.LogError($"[MBG] '{CanvasName}' tidak ditemukan.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Tax Setup");

            var log = new List<string>();
            bool changed = false;

            IllegalTaxConfigSO config = EnsureConfig(log, ref changed);
            DialoguePanel dialogue = EnsureDialoguePanel(canvas, log, ref changed);
            changed |= EnsureTypingArea(log);
            changed |= EnsurePresenter(scene, config, dialogue, log);
            changed |= BindObstacleManager(scene, config, log);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[MBG] Build Tax Setup selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).");
            }
            else
            {
                Debug.Log("[MBG] Sistem pungli sudah lengkap. Tidak ada perubahan.");
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildTaxSetup() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- Data ---------------------------------------------------------------------

        static IllegalTaxConfigSO EnsureConfig(List<string> log, ref bool changed)
        {
            EnsureFolder(TaxFolder);

            TaxDialogueSO dialogue = EnsureDialogue(log, ref changed);
            TypingChallengeSO challenges = EnsureChallenges(log, ref changed);

            var config = AssetDatabase.LoadAssetAtPath<IllegalTaxConfigSO>(ConfigPath);
            bool created = config == null;

            if (created)
            {
                config = ScriptableObject.CreateInstance<IllegalTaxConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            bool dirty = false;
            if (config.dialogue == null) { config.dialogue = dialogue; dirty = true; }
            if (config.challenges == null) { config.challenges = challenges; dirty = true; }

            if (created || dirty)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                changed = true;
                log.Add($"{ConfigPath} {(created ? "dibuat" : "dilengkapi")} " +
                        $"(segel {config.sealDuration:0}s, batas ketik {config.typingTimeLimit:0}s).");
            }

            return config;
        }

        static TaxDialogueSO EnsureDialogue(List<string> log, ref bool changed)
        {
            var dialogue = AssetDatabase.LoadAssetAtPath<TaxDialogueSO>(DialoguePath);
            if (dialogue != null && dialogue.HasContent) return dialogue;

            bool created = dialogue == null;
            if (created)
            {
                dialogue = ScriptableObject.CreateInstance<TaxDialogueSO>();
                AssetDatabase.CreateAsset(dialogue, DialoguePath);
            }

            dialogue.ApplyDefaultScript();
            EditorUtility.SetDirty(dialogue);
            AssetDatabase.SaveAssets();

            changed = true;
            log.Add($"{DialoguePath} {(created ? "dibuat" : "diisi")} — 4 baris tuntutan + penutup.");
            return dialogue;
        }

        static TypingChallengeSO EnsureChallenges(List<string> log, ref bool changed)
        {
            var challenges = AssetDatabase.LoadAssetAtPath<TypingChallengeSO>(ChallengesPath);
            if (challenges != null && challenges.HasAnyText) return challenges;

            bool created = challenges == null;
            if (created)
            {
                challenges = ScriptableObject.CreateInstance<TypingChallengeSO>();
                AssetDatabase.CreateAsset(challenges, ChallengesPath);
            }

            challenges.ApplyDefaultTexts();
            EditorUtility.SetDirty(challenges);
            AssetDatabase.SaveAssets();

            changed = true;
            log.Add($"{ChallengesPath} {(created ? "dibuat" : "diisi")} — 6 kutipan fiktif " +
                    "(2 pendek, 2 sedang, 2 panjang).");
            return challenges;
        }

        // ---- UI ------------------------------------------------------------------------------

        static DialoguePanel EnsureDialoguePanel(GameObject canvas, List<string> log, ref bool changed)
        {
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform root = EnsureChild(canvasRect, "DialoguePanel", out bool created);
            if (created)
            {
                Stretch(root);

                // Fade tetap child terakhir supaya transisi area menutupi semuanya.
                Transform fade = canvasRect.Find(FadeName);
                if (fade != null) root.SetSiblingIndex(fade.GetSiblingIndex());

                changed = true;
                log.Add("DialoguePanel dibuat di UICanvas.");
            }

            var panel = root.GetComponent<DialoguePanel>();
            if (panel == null) panel = Undo.AddComponent<DialoguePanel>(root.gameObject);

            // CanvasGroup di child supaya Update panel tetap jalan saat disembunyikan.
            RectTransform frame = EnsureChild(root, "Frame", out bool frameCreated);
            var group = frame.GetComponent<CanvasGroup>();
            if (group == null) group = Undo.AddComponent<CanvasGroup>(frame.gameObject);

            Image frameImage = EnsureImageOn(frame);
            if (frameCreated)
            {
                frame.anchorMin = new Vector2(0.5f, 0f);
                frame.anchorMax = new Vector2(0.5f, 0f);
                frame.pivot = new Vector2(0.5f, 0f);
                frame.sizeDelta = new Vector2(1120f, 240f);
                frame.anchoredPosition = new Vector2(0f, 60f);
                frameImage.color = new Color(0.08f, 0.07f, 0.11f, 0.95f);
                changed = true;
            }

            RectTransform portraitRect = EnsureChild(frame, "Portrait", out bool portraitCreated);
            Image portrait = EnsureImageOn(portraitRect);
            if (portraitCreated)
            {
                portraitRect.anchorMin = new Vector2(0f, 0.5f);
                portraitRect.anchorMax = new Vector2(0f, 0.5f);
                portraitRect.pivot = new Vector2(0f, 0.5f);
                portraitRect.sizeDelta = new Vector2(150f, 180f);
                portraitRect.anchoredPosition = new Vector2(24f, 0f);
                portrait.color = new Color(0.30f, 0.45f, 0.62f, 1f);
                changed = true;
            }

            TMP_Text nameLabel = EnsureLabel(frame, "NameLabel", "Petugas (?)",
                                             TextAlignmentOptions.Left, 26f, out bool nameCreated);
            if (nameCreated)
            {
                RectTransform rect = nameLabel.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(-220f, 40f);
                rect.anchoredPosition = new Vector2(90f, -18f);
                nameLabel.color = new Color(1f, 0.82f, 0.40f, 1f);
                changed = true;
            }

            TMP_Text bodyLabel = EnsureLabel(frame, "BodyLabel", "",
                                             TextAlignmentOptions.TopLeft, 28f, out bool bodyCreated);
            if (bodyCreated)
            {
                RectTransform rect = bodyLabel.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(190f, 56f);
                rect.offsetMax = new Vector2(-30f, -62f);
                bodyLabel.textWrappingMode = TextWrappingModes.Normal;
                changed = true;
            }

            TMP_Text hintLabel = EnsureLabel(frame, "HintLabel", "[Spasi] lanjut",
                                             TextAlignmentOptions.Right, 22f, out bool hintCreated);
            if (hintCreated)
            {
                RectTransform rect = hintLabel.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(-60f, 34f);
                rect.anchoredPosition = new Vector2(0f, 16f);
                hintLabel.color = new Color(0.72f, 0.72f, 0.78f, 1f);
                changed = true;
            }

            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "group", group);
            bound |= SetObject(so, "portrait", portrait);
            bound |= SetObject(so, "nameLabel", nameLabel);
            bound |= SetObject(so, "bodyLabel", bodyLabel);
            bound |= SetObject(so, "hintLabel", hintLabel);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi DialoguePanel diikat.");
            }

            return panel;
        }

        static bool EnsureTypingArea(List<string> log)
        {
            var panel = Object.FindAnyObjectByType<QTEPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogError("[MBG] QTEPanel tidak ditemukan. Jalankan Tools > MBG > Build UI Hierarchy dulu.");
                return false;
            }

            bool changed = false;
            var panelRect = (RectTransform)panel.transform;

            RectTransform area = EnsureChild(panelRect, "TypingArea", out bool created);
            Image background = EnsureImageOn(area);
            if (created)
            {
                area.anchorMin = new Vector2(0.5f, 0.5f);
                area.anchorMax = new Vector2(0.5f, 0.5f);
                area.pivot = new Vector2(0.5f, 0.5f);
                area.sizeDelta = new Vector2(1240f, 380f);
                area.anchoredPosition = new Vector2(0f, 40f);
                background.color = new Color(0.07f, 0.06f, 0.09f, 0.94f);
                changed = true;
                log.Add("QTEPanel/TypingArea dibuat.");
            }

            TMP_Text text = EnsureLabel(area, "TypingText", "",
                                        TextAlignmentOptions.TopLeft, 30f, out bool textCreated);
            if (textCreated)
            {
                RectTransform rect = text.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(40f, 76f);
                rect.offsetMax = new Vector2(-40f, -40f);
                text.textWrappingMode = TextWrappingModes.Normal;
                changed = true;
            }

            TMP_Text stats = EnsureLabel(area, "TypingStats", "",
                                         TextAlignmentOptions.Center, 24f, out bool statsCreated);
            if (statsCreated)
            {
                RectTransform rect = stats.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(-80f, 40f);
                rect.anchoredPosition = new Vector2(0f, 24f);
                changed = true;
            }

            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "typingArea", area);
            bound |= SetObject(so, "typingTextLabel", text);
            bound |= SetObject(so, "typingStatsLabel", stats);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi area mengetik di QTEPanel diikat.");
            }

            return changed;
        }

        // ---- Scene ---------------------------------------------------------------------------------

        static bool EnsurePresenter(Scene scene, IllegalTaxConfigSO config, DialoguePanel dialogue,
                                    List<string> log)
        {
            GameObject systems = FindInScene(scene, SystemsName);
            if (systems == null)
            {
                Debug.LogError($"[MBG] '{SystemsName}' belum ada. Jalankan Tools > MBG > Setup Systems Object dulu.");
                return false;
            }

            bool changed = false;

            var presenter = systems.GetComponent<TaxPresenter>();
            if (presenter == null)
            {
                presenter = Undo.AddComponent<TaxPresenter>(systems);
                changed = true;
                log.Add($"TaxPresenter ditambahkan ke '{SystemsName}'.");
            }

            GameObject point = FindInScene(scene, CollectorPointName);
            if (point == null)
                Debug.LogWarning($"[MBG] {CollectorPointName} belum ada. " +
                                 "Jalankan Tools > MBG > Build Exterior Encounters.");

            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);

            var so = new SerializedObject(presenter);
            bool bound = false;
            bound |= SetObject(so, "config", config);
            bound |= SetObject(so, "dialoguePanel", dialogue);
            if (point != null) bound |= SetObject(so, "collectorPoint", point.transform);
            if (square != null) bound |= SetObject(so, "placeholderSprite", square);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi TaxPresenter diikat.");
            }

            return changed;
        }

        static bool BindObstacleManager(Scene scene, IllegalTaxConfigSO config, List<string> log)
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
            if (!SetObject(so, "illegalTaxConfig", config)) return false;

            so.ApplyModifiedProperties();
            log.Add("ObstacleManager.illegalTaxConfig diikat.");
            return true;
        }

        // ---- Helper ------------------------------------------------------------------------------------

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

        static Image EnsureImageOn(RectTransform rect)
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

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
