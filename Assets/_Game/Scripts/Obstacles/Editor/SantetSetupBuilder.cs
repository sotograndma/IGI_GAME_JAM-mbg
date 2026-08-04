using System.Collections.Generic;
using System.IO;
using MBG.Data;
using MBG.QTE;
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
    /// Menyiapkan gangguan Santet: sprite lingkaran &amp; vignette, tiga pola ritme,
    /// SantetConfig, overlay layar, penunjuk objektif, area not di QTEPanel, dan
    /// SantetPresenter di __Systems.
    ///
    /// Additive dan idempoten. Sprite dibuat prosedural sekali lalu dipakai ulang —
    /// project ini belum punya aset gambar lingkaran, dan menggambarnya dari kode
    /// lebih ringan daripada menambah dependency.
    /// </summary>
    public static class SantetSetupBuilder
    {
        const string MenuPath = "Tools/MBG/Build Santet Setup";

        const string SystemsName = "__Systems";
        const string CanvasName = "UICanvas";
        const string FadeName = "Fade";

        const string DataFolder = "Assets/_Game/Data";
        const string SantetFolder = DataFolder + "/Santet";
        const string SpriteFolder = "Assets/_Game/Sprites";
        const string ConfigPath = SantetFolder + "/SantetConfig.asset";
        const string QteHardPath = DataFolder + "/QTE/QTE_Hard.asset";

        const string RingSpritePath = SpriteFolder + "/circle_ring.png";
        const string DiscSpritePath = SpriteFolder + "/circle_disc.png";
        const string VignetteSpritePath = SpriteFolder + "/vignette.png";
        const string SquareSpritePath = "Assets/_Placeholder/Sprites/square.png";

        const int SpriteSize = 256;
        const float NoteInnerSize = 96f;

        [MenuItem(MenuPath, false, 110)]
        public static void BuildSantetSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Santet Setup",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("TMP Essentials belum di-import",
                    "Penunjuk objektif memakai TextMeshPro. Import dulu lewat:\n" +
                    "Window > TextMeshPro > Import TMP Essential Resources", "OK");
                Debug.LogError("[MBG] Build Santet Setup dibatalkan: TMP Essential Resources belum di-import.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MBG] Tidak ada scene aktif yang valid.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Santet Setup");

            var log = new List<string>();
            bool changed = false;

            Sprite ring = EnsureGeneratedSprite(RingSpritePath, SpriteKind.Ring, log, ref changed);
            Sprite disc = EnsureGeneratedSprite(DiscSpritePath, SpriteKind.Disc, log, ref changed);
            Sprite vignette = EnsureGeneratedSprite(VignetteSpritePath, SpriteKind.Vignette, log, ref changed);

            SantetConfigSO config = EnsureConfig(log, ref changed);

            GameObject canvas = FindInScene(scene, CanvasName);
            if (canvas == null)
            {
                Debug.LogError($"[MBG] '{CanvasName}' tidak ditemukan.");
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            SantetOverlay overlay = EnsureOverlay(canvas, vignette, log, ref changed);
            ObjectiveMarker marker = EnsureObjectiveMarker(canvas, log, ref changed);
            changed |= EnsureRhythmArea(ring, disc, log);
            changed |= EnsurePresenter(scene, config, overlay, marker, log);
            changed |= BindObstacleManager(scene, config, log);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[MBG] Build Santet Setup selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).");
            }
            else
            {
                Debug.Log("[MBG] Sistem santet sudah lengkap. Tidak ada perubahan.");
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildSantetSetup() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- Sprite prosedural ------------------------------------------------------

        enum SpriteKind { Ring, Disc, Vignette }

        static Sprite EnsureGeneratedSprite(string path, SpriteKind kind, List<string> log, ref bool changed)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[SpriteSize * SpriteSize];

            float center = (SpriteSize - 1) * 0.5f;
            float maxRadius = center;

            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / maxRadius;

                    float alpha = kind switch
                    {
                        // Cincin tipis dengan tepi dihaluskan.
                        SpriteKind.Ring => Mathf.Clamp01(1f - Mathf.Abs(distance - 0.82f) / 0.14f),
                        SpriteKind.Disc => Mathf.Clamp01((0.86f - distance) / 0.06f),
                        _ => Mathf.Clamp01((distance - 0.45f) / 0.55f)
                    };

                    pixels[y * SpriteSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            changed = true;
            log.Add($"{path} dibuat ({kind}). Tidak ikut ter-undo.");
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---- Data ----------------------------------------------------------------------

        static SantetConfigSO EnsureConfig(List<string> log, ref bool changed)
        {
            EnsureFolder(SantetFolder);

            RhythmPatternSO easy = EnsurePattern("Rhythm_Easy", 6, 1.25f, 1.0f, log, ref changed);
            RhythmPatternSO normal = EnsurePattern("Rhythm_Normal", 10, 1.0f, 0.85f, log, ref changed);
            RhythmPatternSO hard = EnsurePattern("Rhythm_Hard", 16, 0.78f, 0.7f, log, ref changed);

            var config = AssetDatabase.LoadAssetAtPath<SantetConfigSO>(ConfigPath);
            bool created = config == null;

            if (created)
            {
                config = ScriptableObject.CreateInstance<SantetConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            bool dirty = false;

            if (config.patternEasy == null) { config.patternEasy = easy; dirty = true; }
            if (config.patternNormal == null) { config.patternNormal = normal; dirty = true; }
            if (config.patternHard == null) { config.patternHard = hard; dirty = true; }

            if (config.confrontationConfig == null)
            {
                config.confrontationConfig = AssetDatabase.LoadAssetAtPath<QTEConfigSO>(QteHardPath);
                if (config.confrontationConfig == null)
                    Debug.LogWarning("[MBG] QTE_Hard belum ada — jalankan Tools > MBG > Build QTE Setup dulu.");
                else dirty = true;
            }

            if (dirty || created)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                changed = true;
                log.Add($"{ConfigPath} {(created ? "dibuat" : "dilengkapi")} (ambang akurasi {config.accuracyThreshold:0.00}).");
            }

            return config;
        }

        /// <summary>
        /// Pola dibuat deterministik dari indeks not, bukan acak — supaya asset yang
        /// dihasilkan sama persis di komputer siapa pun.
        /// </summary>
        static RhythmPatternSO EnsurePattern(string assetName, int noteCount, float spacing,
                                             float approach, List<string> log, ref bool changed)
        {
            string path = $"{SantetFolder}/{assetName}.asset";

            var pattern = AssetDatabase.LoadAssetAtPath<RhythmPatternSO>(path);
            if (pattern != null && pattern.NoteCount > 0) return pattern;

            bool created = pattern == null;
            if (created)
            {
                pattern = ScriptableObject.CreateInstance<RhythmPatternSO>();
                AssetDatabase.CreateAsset(pattern, path);
            }

            pattern.displayName = assetName;
            pattern.notes = new List<RhythmNote>();

            float time = approach + 0.4f;
            for (int i = 0; i < noteCount; i++)
            {
                // Sebaran mengikuti dua deret irasional supaya titik tidak pernah
                // menumpuk dan tetap sama tiap kali di-generate.
                float x = Mathf.Repeat(0.18f + i * 0.382f, 0.72f) + 0.14f;
                float y = Mathf.Repeat(0.24f + i * 0.618f, 0.62f) + 0.19f;

                pattern.notes.Add(new RhythmNote(time, new Vector2(x, y), approach));
                time += spacing;
            }

            EditorUtility.SetDirty(pattern);
            AssetDatabase.SaveAssets();

            changed = true;
            log.Add($"{assetName} {(created ? "dibuat" : "diisi")} — {noteCount} not, jarak {spacing:0.00}s.");
            return pattern;
        }

        // ---- UI ----------------------------------------------------------------------------

        static SantetOverlay EnsureOverlay(GameObject canvas, Sprite vignette, List<string> log, ref bool changed)
        {
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform root = EnsureChild(canvasRect, "SantetOverlay", out bool created);
            if (created)
            {
                Stretch(root);
                InsertBeforeFade(root, canvasRect);
                changed = true;
                log.Add("SantetOverlay dibuat di UICanvas (sebelum Fade).");
            }

            var overlay = root.GetComponent<SantetOverlay>();
            if (overlay == null) overlay = Undo.AddComponent<SantetOverlay>(root.gameObject);

            // CanvasGroup ada di child supaya mematikannya tidak ikut mematikan
            // komponen SantetOverlay yang perlu terus meng-update fade.
            RectTransform layers = EnsureChild(root, "Layers", out bool layersCreated);
            var group = layers.GetComponent<CanvasGroup>();
            if (group == null) group = Undo.AddComponent<CanvasGroup>(layers.gameObject);
            if (layersCreated) { Stretch(layers); changed = true; }

            Image desaturate = EnsureImage(layers, "Desaturate", null, out bool madeDesaturate);
            Image tint = EnsureImage(layers, "Tint", null, out bool madeTint);
            Image vignetteImage = EnsureImage(layers, "Vignette", vignette, out bool madeVignette);
            changed |= madeDesaturate | madeTint | madeVignette;

            var so = new SerializedObject(overlay);
            bool bound = false;
            bound |= SetObject(so, "group", group);
            bound |= SetObject(so, "tintImage", tint);
            bound |= SetObject(so, "vignetteImage", vignetteImage);
            bound |= SetObject(so, "desaturateImage", desaturate);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi SantetOverlay diikat.");
            }

            return overlay;
        }

        static ObjectiveMarker EnsureObjectiveMarker(GameObject canvas, List<string> log, ref bool changed)
        {
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform root = EnsureChild(canvasRect, "ObjectiveMarker", out bool created);
            if (created)
            {
                Stretch(root);
                InsertBeforeFade(root, canvasRect);
                changed = true;
                log.Add("ObjectiveMarker dibuat di UICanvas.");
            }

            var marker = root.GetComponent<ObjectiveMarker>();
            if (marker == null) marker = Undo.AddComponent<ObjectiveMarker>(root.gameObject);

            var group = root.GetComponent<CanvasGroup>();
            if (group == null) group = Undo.AddComponent<CanvasGroup>(root.gameObject);

            RectTransform arrow = EnsureChild(root, "Arrow", out bool arrowCreated);
            TMP_Text arrowLabel = EnsureLabelOn(arrow, "^", TextAlignmentOptions.Center, 64f);
            if (arrowCreated)
            {
                arrow.anchorMin = new Vector2(0.5f, 0.5f);
                arrow.anchorMax = new Vector2(0.5f, 0.5f);
                arrow.pivot = new Vector2(0.5f, 0.5f);
                arrow.sizeDelta = new Vector2(90f, 90f);
                arrowLabel.color = new Color(1f, 0.85f, 0.3f, 1f);
                changed = true;
            }

            TMP_Text label = EnsureLabel(root, "ObjectiveLabel", "Cari dukun di luar",
                                         TextAlignmentOptions.Center, 28f, out bool labelCreated);
            if (labelCreated)
            {
                RectTransform rect = label.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(620f, 46f);
                rect.anchoredPosition = new Vector2(0f, -130f);
                label.color = new Color(1f, 0.85f, 0.3f, 1f);
                changed = true;
            }

            var so = new SerializedObject(marker);
            bool bound = false;
            bound |= SetObject(so, "group", group);
            bound |= SetObject(so, "canvasRect", canvasRect);
            bound |= SetObject(so, "arrow", arrow);
            bound |= SetObject(so, "label", label);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi ObjectiveMarker diikat.");
            }

            return marker;
        }

        static bool EnsureRhythmArea(Sprite ring, Sprite disc, List<string> log)
        {
            var panel = Object.FindAnyObjectByType<QTEPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogError("[MBG] QTEPanel tidak ditemukan. Jalankan Tools > MBG > Build UI Hierarchy dulu.");
                return false;
            }

            bool changed = false;
            var panelRect = (RectTransform)panel.transform;

            RectTransform area = EnsureChild(panelRect, "RhythmArea", out bool created);
            if (created)
            {
                // Area not menempati bagian tengah layar, di atas bar QTE.
                area.anchorMin = new Vector2(0.12f, 0.28f);
                area.anchorMax = new Vector2(0.88f, 0.88f);
                area.offsetMin = Vector2.zero;
                area.offsetMax = Vector2.zero;
                changed = true;
                log.Add("QTEPanel/RhythmArea dibuat.");
            }

            RectTransform template = EnsureChild(area, "NoteTemplate", out bool templateCreated);
            if (templateCreated)
            {
                template.anchorMin = new Vector2(0.5f, 0.5f);
                template.anchorMax = new Vector2(0.5f, 0.5f);
                template.pivot = new Vector2(0.5f, 0.5f);
                template.sizeDelta = new Vector2(NoteInnerSize, NoteInnerSize);
                template.anchoredPosition = Vector2.zero;

                // Anak pertama WAJIB ring luar: QTEPanel menskalakannya lewat GetChild(0).
                RectTransform outer = EnsureChild(template, "Outer", out _);
                Image outerImage = EnsureImageOn(outer, ring);
                Stretch(outer);
                outerImage.color = new Color(1f, 1f, 1f, 0.85f);

                RectTransform inner = EnsureChild(template, "Inner", out _);
                Image innerImage = EnsureImageOn(inner, disc);
                Stretch(inner);
                innerImage.color = new Color(0.95f, 0.35f, 0.35f, 0.95f);

                template.gameObject.SetActive(false);
                changed = true;
                log.Add("NoteTemplate dibuat (ring luar + inti).");
            }

            // Yang disembunyikan hanya batangnya, bukan seluruh Frame — label
            // instruksi dan teks hasil tinggal di Frame dan tetap dibutuhkan
            // selama mekanik ritme.
            RectTransform barFrame = panelRect.Find("Frame/Bar") as RectTransform;

            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "rhythmArea", area);
            bound |= SetObject(so, "noteTemplate", template);
            if (barFrame != null) bound |= SetObject(so, "barFrame", barFrame);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi ritme di QTEPanel diikat.");
            }

            return changed;
        }

        // ---- Scene --------------------------------------------------------------------------

        static bool EnsurePresenter(Scene scene, SantetConfigSO config, SantetOverlay overlay,
                                    ObjectiveMarker marker, List<string> log)
        {
            GameObject systems = FindInScene(scene, SystemsName);
            if (systems == null)
            {
                Debug.LogError($"[MBG] '{SystemsName}' belum ada. Jalankan Tools > MBG > Setup Systems Object dulu.");
                return false;
            }

            bool changed = false;

            var presenter = systems.GetComponent<SantetPresenter>();
            if (presenter == null)
            {
                presenter = Undo.AddComponent<SantetPresenter>(systems);
                changed = true;
                log.Add($"SantetPresenter ditambahkan ke '{SystemsName}'.");
            }

            GameObject casterPoint = FindInScene(scene, "SantetCasterPoint");
            if (casterPoint == null)
                Debug.LogWarning("[MBG] SantetCasterPoint belum ada. Jalankan Tools > MBG > Build Exterior Encounters.");

            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);

            var so = new SerializedObject(presenter);
            bool bound = false;
            bound |= SetObject(so, "config", config);
            bound |= SetObject(so, "overlay", overlay);
            bound |= SetObject(so, "objectiveMarker", marker);
            if (casterPoint != null) bound |= SetObject(so, "casterPoint", casterPoint.transform);
            if (square != null) bound |= SetObject(so, "placeholderSprite", square);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi SantetPresenter diikat.");
            }

            return changed;
        }

        static bool BindObstacleManager(Scene scene, SantetConfigSO config, List<string> log)
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
            if (!SetObject(so, "santetConfig", config)) return false;

            so.ApplyModifiedProperties();
            log.Add("ObstacleManager.santetConfig diikat.");
            return true;
        }

        // ---- Helper ------------------------------------------------------------------------------

        /// <summary>Fade harus tetap jadi child terakhir supaya menutupi seluruh UI.</summary>
        static void InsertBeforeFade(RectTransform target, RectTransform canvasRect)
        {
            Transform fade = canvasRect.Find(FadeName);
            if (fade != null) target.SetSiblingIndex(fade.GetSiblingIndex());
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

        static Image EnsureImage(RectTransform parent, string name, Sprite sprite, out bool created)
        {
            RectTransform rect = EnsureChild(parent, name, out created);
            if (created) Stretch(rect);

            return EnsureImageOn(rect, sprite);
        }

        static Image EnsureImageOn(RectTransform rect, Sprite sprite)
        {
            var image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = Undo.AddComponent<Image>(rect.gameObject);
                image.raycastTarget = false;
            }

            if (sprite != null && image.sprite == null) image.sprite = sprite;
            return image;
        }

        static TMP_Text EnsureLabel(RectTransform parent, string name, string text,
                                    TextAlignmentOptions alignment, float fontSize, out bool created)
        {
            RectTransform rect = EnsureChild(parent, name, out created);
            return EnsureLabelOn(rect, created ? text : null, alignment, fontSize);
        }

        static TMP_Text EnsureLabelOn(RectTransform rect, string text,
                                      TextAlignmentOptions alignment, float fontSize)
        {
            var label = rect.GetComponent<TMP_Text>();
            if (label != null) return label;

            var tmp = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            tmp.fontSize = fontSize;
            if (text != null) tmp.text = text;

            return tmp;
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
