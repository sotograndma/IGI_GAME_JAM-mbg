using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// Melengkapi UICanvas yang SUDAH ADA dengan panel-panel MBG.UI, UIManager,
    /// dan asset UIStyle — plus menukar Label prompt dari legacy Text ke TMP.
    ///
    /// Additive dan idempoten. Yang TIDAK pernah disentuh:
    /// PromptPanel dan Fade tidak pernah dihapus, di-rename, atau dipindah
    /// parent-nya, dan RectTransform-nya tidak pernah diubah. Panel baru
    /// disisipkan tepat SEBELUM Fade supaya Fade tetap menjadi child terakhir —
    /// kalau tidak, overlay fade berhenti menutupi UI dan transisi antar area
    /// akan bocor.
    /// </summary>
    public static class UIHierarchyBuilder
    {
        const string MenuPath = "Tools/MBG/Build UI Hierarchy";
        const string CanvasName = "UICanvas";
        const string FadeName = "Fade";
        const string DataFolder = "Assets/_Game/Data";
        const string StyleAssetPath = DataFolder + "/UIStyle.asset";

        struct PanelSpec
        {
            public string Name;
            public Type Type;
            public bool VisibleByDefault;
            public bool BlockRaycasts;

            public PanelSpec(string name, Type type, bool visibleByDefault, bool blockRaycasts)
            {
                Name = name;
                Type = type;
                VisibleByDefault = visibleByDefault;
                BlockRaycasts = blockRaycasts;
            }
        }

        static readonly PanelSpec[] Panels =
        {
            // HUD terlihat sejak awal dan tidak boleh memblokir klik ke dunia.
            new PanelSpec("HUDPanel", typeof(HUDPanel), true, false),
            new PanelSpec("QTEPanel", typeof(QTEPanel), false, false),
            new PanelSpec("ResultPanel", typeof(ResultPanel), false, true),
            new PanelSpec("DaySummaryPanel", typeof(DaySummaryPanel), false, true),
            new PanelSpec("PausePanel", typeof(PausePanel), false, true),
            new PanelSpec("GameOverPanel", typeof(GameOverPanel), false, true),
            new PanelSpec("MainMenuPanel", typeof(MainMenuPanel), false, true),
            new PanelSpec("HowToPlayPanel", typeof(HowToPlayPanel), false, true),
        };

        [MenuItem(MenuPath, false, 101)]
        public static void BuildUIHierarchy()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build UI Hierarchy",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            if (!IsTmpReady())
            {
                EditorUtility.DisplayDialog("TMP Essentials belum di-import",
                    "TextMeshPro sudah terpasang lewat package com.unity.ugui, tapi TMP Essential Resources " +
                    "(TMP Settings + font default) belum ada di project.\n\n" +
                    "Import dulu lewat:\nWindow > TextMeshPro > Import TMP Essential Resources\n\n" +
                    "Setelah itu jalankan tool ini lagi.", "OK");
                Debug.LogError("[MBG] Build UI Hierarchy dibatalkan: TMP Essential Resources belum di-import " +
                               "(Window > TextMeshPro > Import TMP Essential Resources).");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MBG] Tidak ada scene aktif yang valid. Buka Assets/Scenes/SampleScene.unity dulu.");
                return;
            }

            GameObject canvas = FindCanvas(scene);
            if (canvas == null)
            {
                Debug.LogError($"[MBG] GameObject '{CanvasName}' tidak ditemukan di scene aktif. " +
                               "Tool ini hanya melengkapi canvas yang sudah ada, tidak membuat yang baru.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build UI Hierarchy");

            var log = new List<string>();
            bool changed = false;

            UIStyle style = EnsureStyleAsset(log, ref changed);
            changed |= MigratePromptLabel(canvas, log);
            changed |= EnsureUIManager(canvas, style, log);
            changed |= EnsurePanels(canvas, log);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = canvas;
                Debug.Log("[MBG] Build UI Hierarchy selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).", canvas);
            }
            else
            {
                Debug.Log("[MBG] Hierarki UI sudah lengkap. Tidak ada perubahan.", canvas);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildUIHierarchy() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- TMP ---------------------------------------------------------

        static bool IsTmpReady()
        {
            // Urutan cek penting: TMP_Settings.defaultFontAsset melempar NRE kalau
            // instance-nya belum ada, jadi instance harus dicek lebih dulu.
            return TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null;
        }

        /// <summary>
        /// Tukar komponen Text legacy pada Label prompt menjadi TextMeshProUGUI,
        /// lalu ikat ulang ke field 'label' milik InteractionPromptUI.
        /// GameObject Label sendiri tidak dihapus, di-rename, atau dipindah — hanya
        /// komponen teksnya yang ditukar, jadi RectTransform-nya (posisi & ukuran)
        /// tetap persis sama.
        /// </summary>
        static bool MigratePromptLabel(GameObject canvas, List<string> log)
        {
            var prompt = canvas.GetComponentInChildren<InteractionPromptUI>(true);
            if (prompt == null)
            {
                log.Add("InteractionPromptUI tidak ditemukan — migrasi label dilewati.");
                return false;
            }

            var so = new SerializedObject(prompt);
            SerializedProperty panelProp = so.FindProperty("panel");
            SerializedProperty labelProp = so.FindProperty("label");

            var panelGo = panelProp != null ? panelProp.objectReferenceValue as GameObject : null;
            Transform searchRoot = panelGo != null ? panelGo.transform : canvas.transform;

            bool changed = false;
            var tmp = searchRoot.GetComponentInChildren<TMP_Text>(true);

            if (tmp == null)
            {
                var legacy = searchRoot.GetComponentInChildren<Text>(true);
                if (legacy == null)
                {
                    log.Add("Tidak ada komponen teks di bawah PromptPanel — migrasi label dilewati.");
                    return false;
                }

                tmp = ConvertToTMP(legacy);
                changed = true;
                log.Add($"'{tmp.gameObject.name}': legacy Text -> TextMeshProUGUI (teks, ukuran font, warna, " +
                        "dan alignment disalin apa adanya).");
            }

            if (labelProp != null && labelProp.objectReferenceValue != tmp)
            {
                labelProp.objectReferenceValue = tmp;
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("InteractionPromptUI.label diikat ulang ke TMP_Text.");
            }

            return changed;
        }

        static TMP_Text ConvertToTMP(Text legacy)
        {
            GameObject host = legacy.gameObject;

            string text = legacy.text;
            float fontSize = legacy.fontSize;
            Color color = legacy.color;
            TextAlignmentOptions alignment = ConvertAlignment(legacy.alignment);
            bool raycastTarget = legacy.raycastTarget;
            bool richText = legacy.supportRichText;
            bool bestFit = legacy.resizeTextForBestFit;
            float minSize = legacy.resizeTextMinSize;
            float maxSize = legacy.resizeTextMaxSize;

            // Dua komponen Graphic tidak boleh hidup bersama di satu GameObject,
            // jadi yang lama dibuang dulu. CanvasRenderer-nya dipakai ulang TMP.
            Undo.DestroyObjectImmediate(legacy);

            var tmp = Undo.AddComponent<TextMeshProUGUI>(host);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = raycastTarget;
            tmp.richText = richText;
            tmp.enableAutoSizing = bestFit;
            if (bestFit)
            {
                tmp.fontSizeMin = minSize;
                tmp.fontSizeMax = maxSize;
            }

            return tmp;
        }

        static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        // ---- UIStyle & UIManager ----------------------------------------

        static UIStyle EnsureStyleAsset(List<string> log, ref bool changed)
        {
            var style = AssetDatabase.LoadAssetAtPath<UIStyle>(StyleAssetPath);
            if (style != null) return style;

            EnsureFolder(DataFolder);

            style = ScriptableObject.CreateInstance<UIStyle>();
            AssetDatabase.CreateAsset(style, StyleAssetPath);
            AssetDatabase.SaveAssets();

            changed = true;
            // Pembuatan asset di luar jangkauan Undo — sengaja disebut supaya tidak
            // bingung kalau Ctrl+Z tidak menghapusnya.
            log.Add($"{StyleAssetPath} dibuat (tidak ikut ter-undo; hapus manual bila perlu).");
            return style;
        }

        static bool EnsureUIManager(GameObject canvas, UIStyle style, List<string> log)
        {
            bool changed = false;

            var manager = canvas.GetComponent<UIManager>();
            if (manager == null)
            {
                manager = Undo.AddComponent<UIManager>(canvas);
                changed = true;
                log.Add($"UIManager ditambahkan ke '{canvas.name}'.");
            }

            var so = new SerializedObject(manager);
            SerializedProperty styleProp = so.FindProperty("style");
            if (styleProp != null && styleProp.objectReferenceValue != style)
            {
                styleProp.objectReferenceValue = style;
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("UIManager.style diikat ke UIStyle.asset.");
            }

            return changed;
        }

        // ---- Panel -------------------------------------------------------

        static bool EnsurePanels(GameObject canvas, List<string> log)
        {
            bool changed = false;
            Transform canvasTransform = canvas.transform;
            Transform fade = canvasTransform.Find(FadeName);

            foreach (PanelSpec spec in Panels)
            {
                Transform existing = canvasTransform.Find(spec.Name);
                if (existing != null)
                {
                    // Sudah ada: lengkapi komponen yang hilang saja. Urutan sibling,
                    // RectTransform, dan status aktifnya dibiarkan seperti yang
                    // diatur user.
                    changed |= EnsurePanelComponents(existing.gameObject, spec, log, isNew: false);
                    continue;
                }

                var go = new GameObject(spec.Name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, $"Create {spec.Name}");
                Undo.SetTransformParent(go.transform, canvasTransform, $"Parent {spec.Name}");

                StretchFull((RectTransform)go.transform);
                EnsurePanelComponents(go, spec, log, isNew: true);

                // Fade harus tetap child terakhir supaya overlay-nya menutupi semua UI.
                if (fade != null) go.transform.SetSiblingIndex(fade.GetSiblingIndex());

                go.SetActive(spec.VisibleByDefault);

                changed = true;
                log.Add($"{spec.Name} dibuat ({(spec.VisibleByDefault ? "aktif" : "nonaktif")}).");
            }

            return changed;
        }

        static bool EnsurePanelComponents(GameObject go, PanelSpec spec, List<string> log, bool isNew)
        {
            bool changed = false;

            if (go.GetComponent<CanvasGroup>() == null)
            {
                Undo.AddComponent<CanvasGroup>(go);
                changed = true;
                if (!isNew) log.Add($"{spec.Name}: CanvasGroup yang hilang ditambahkan.");
            }

            var panel = go.GetComponent(spec.Type) as UIPanel;
            if (panel == null)
            {
                panel = Undo.AddComponent(go, spec.Type) as UIPanel;
                changed = true;
                if (!isNew) log.Add($"{spec.Name}: komponen {spec.Type.Name} yang hilang ditambahkan.");
            }

            // Nilai default hanya ditulis untuk panel yang baru dibuat — kalau tidak,
            // tool ini akan menimpa tuning user setiap kali dijalankan ulang.
            if (isNew && panel != null)
            {
                var so = new SerializedObject(panel);
                SetBool(so, "visibleOnAwake", spec.VisibleByDefault);
                SetBool(so, "blockRaycastsWhenVisible", spec.BlockRaycasts);
                so.ApplyModifiedProperties();
            }

            return changed;
        }

        static void SetBool(SerializedObject so, string propertyName, bool value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null) prop.boolValue = value;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition3D = Vector3.zero;
        }

        // ---- Util --------------------------------------------------------

        static GameObject FindCanvas(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (c.gameObject.name == CanvasName) return c.gameObject;
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
