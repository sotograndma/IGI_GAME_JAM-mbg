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
    /// Mengisi ResultPanel: latar gelap, kartu tengah, judul, kualitas, rincian
    /// bayaran, reaksi pemesan, dan petunjuk lanjut.
    ///
    /// Additive dan idempoten — tata letak hanya ditulis untuk elemen baru,
    /// referensi selalu diikat ulang.
    /// </summary>
    public static class ResultPanelBuilder
    {
        const string MenuPath = "Tools/MBG/Build Result Panel";

        // Tata letak dalam piksel referensi canvas (1920x1080).
        const float CardWidth = 760f;
        const float CardHeight = 520f;
        const float Padding = 36f;

        [MenuItem(MenuPath, false, 106)]
        public static void BuildResultPanel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Result Panel",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("TMP Essentials belum di-import",
                    "Panel hasil memakai TextMeshPro. Import dulu lewat:\n" +
                    "Window > TextMeshPro > Import TMP Essential Resources", "OK");
                Debug.LogError("[MBG] Build Result Panel dibatalkan: TMP Essential Resources belum di-import.");
                return;
            }

            var panel = Object.FindAnyObjectByType<ResultPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogError("[MBG] ResultPanel tidak ditemukan di scene. " +
                               "Jalankan Tools > MBG > Build UI Hierarchy dulu.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Result Panel");

            var log = new List<string>();
            bool changed = false;

            var root = (RectTransform)panel.transform;

            // Latar gelap menutupi seluruh layar.
            RectTransform backdrop = EnsureChild(root, "Backdrop", out bool created);
            Image backdropImage = EnsureImage(backdrop);
            if (created)
            {
                Stretch(backdrop);
                backdropImage.color = new Color(0.03f, 0.03f, 0.05f, 0.82f);
                changed = true;
                log.Add("Backdrop dibuat.");
            }

            RectTransform card = EnsureChild(root, "Card", out created);
            Image cardImage = EnsureImage(card);
            if (created)
            {
                card.anchorMin = new Vector2(0.5f, 0.5f);
                card.anchorMax = new Vector2(0.5f, 0.5f);
                card.pivot = new Vector2(0.5f, 0.5f);
                card.sizeDelta = new Vector2(CardWidth, CardHeight);
                card.anchoredPosition = Vector2.zero;
                cardImage.color = new Color(0.10f, 0.09f, 0.13f, 0.96f);
                changed = true;
                log.Add("Card dibuat di tengah layar.");
            }

            TMP_Text title = EnsureLabel(card, "TitleLabel", "CATERING DISERAHKAN",
                                         TextAlignmentOptions.Center, 46f, out created);
            if (created) { PlaceTop(title.rectTransform, -Padding, 60f); changed = true; }

            TMP_Text quality = EnsureLabel(card, "QualityLabel", "Kualitas: PERFECT",
                                           TextAlignmentOptions.Center, 30f, out created);
            if (created) { PlaceTop(quality.rectTransform, -(Padding + 68f), 44f); changed = true; }

            TMP_Text detail = EnsureLabel(card, "DetailLabel",
                                          "Porsi diserahkan: 0\nGold: +0\nSkor: +0",
                                          TextAlignmentOptions.TopLeft, 26f, out created);
            if (created)
            {
                PlaceTop(detail.rectTransform, -(Padding + 126f), 180f);
                detail.textWrappingMode = TextWrappingModes.Normal;
                changed = true;
            }

            TMP_Text reaction = EnsureLabel(card, "ReactionLabel", "\"Anak-anak makan sampai habis!\"",
                                            TextAlignmentOptions.Center, 26f, out created);
            if (created)
            {
                PlaceTop(reaction.rectTransform, -(Padding + 318f), 90f);
                reaction.textWrappingMode = TextWrappingModes.Normal;
                reaction.fontStyle = FontStyles.Italic;
                changed = true;
            }

            TMP_Text continueLabel = EnsureLabel(card, "ContinueLabel", "Tekan SPASI untuk lanjut",
                                                 TextAlignmentOptions.Center, 22f, out created);
            if (created)
            {
                RectTransform rect = continueLabel.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(-Padding * 2f, 40f);
                rect.anchoredPosition = new Vector2(0f, Padding * 0.6f);
                changed = true;
            }

            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "titleLabel", title);
            bound |= SetObject(so, "qualityLabel", quality);
            bound |= SetObject(so, "detailLabel", detail);
            bound |= SetObject(so, "reactionLabel", reaction);
            bound |= SetObject(so, "continueLabel", continueLabel);

            var scoring = AssetDatabase.LoadAssetAtPath<MBG.Data.ScoringConfigSO>(
                "Assets/_Game/Data/Catering/ScoringConfig.asset");
            if (scoring != null) bound |= SetObject(so, "scoring", scoring);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi ResultPanel diikat.");
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = panel.gameObject;
                Debug.Log("[MBG] Build Result Panel selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).", panel);
            }
            else
            {
                Debug.Log("[MBG] ResultPanel sudah lengkap. Tidak ada perubahan.", panel);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildResultPanel() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- Helper ---------------------------------------------------------

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void PlaceTop(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-Padding * 2f, height);
            rect.anchoredPosition = new Vector2(0f, y);
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

        static bool SetObject(SerializedObject so, string path, Object value)
        {
            SerializedProperty prop = so.FindProperty(path);
            if (prop == null || prop.objectReferenceValue == value) return false;

            prop.objectReferenceValue = value;
            return true;
        }
    }
}
