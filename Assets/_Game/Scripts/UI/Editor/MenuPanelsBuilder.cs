using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// Mengisi panel menu utama, cara bermain, jeda, ringkasan hari, dan layar
    /// kalah — plus memastikan scene punya EventSystem supaya tombolnya benar-benar
    /// bisa diklik.
    ///
    /// Additive dan idempoten: tata letak hanya ditulis untuk elemen baru, referensi
    /// selalu diikat ulang, dan EventSystem hanya dibuat kalau belum ada.
    /// </summary>
    public static class MenuPanelsBuilder
    {
        const string MenuPath = "Tools/MBG/Build Menu Panels";
        const string EventSystemName = "EventSystem";

        const float CardWidth = 720f;
        const float Padding = 40f;
        const float ButtonWidth = 380f;
        const float ButtonHeight = 62f;
        const float ButtonGap = 14f;

        [MenuItem(MenuPath, false, 107)]
        public static void BuildMenuPanels()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Menu Panels",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("TMP Essentials belum di-import",
                    "Panel menu memakai TextMeshPro. Import dulu lewat:\n" +
                    "Window > TextMeshPro > Import TMP Essential Resources", "OK");
                Debug.LogError("[MBG] Build Menu Panels dibatalkan: TMP Essential Resources belum di-import.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MBG] Tidak ada scene aktif yang valid.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Menu Panels");

            var log = new List<string>();
            bool changed = false;

            changed |= EnsureEventSystem(scene, log);
            changed |= BuildMainMenu(log);
            changed |= BuildHowToPlay(log);
            changed |= BuildPause(log);
            changed |= BuildDaySummary(log);
            changed |= BuildGameOver(log);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[MBG] Build Menu Panels selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).");
            }
            else
            {
                Debug.Log("[MBG] Panel menu sudah lengkap. Tidak ada perubahan.");
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildMenuPanels() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- EventSystem -----------------------------------------------------

        /// <summary>
        /// Tanpa EventSystem, Button UGUI tidak menerima klik sama sekali. Modul
        /// input yang dipakai adalah InputSystemUIInputModule karena project ini
        /// memakai New Input System.
        /// </summary>
        static bool EnsureEventSystem(Scene scene, List<string> log)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<EventSystem>(true) != null) return false;
            }

            var go = new GameObject(EventSystemName, typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");

            log.Add("EventSystem + InputSystemUIInputModule dibuat (tombol UI butuh ini).");
            return true;
        }

        // ---- Panel -----------------------------------------------------------

        static bool BuildMainMenu(List<string> log)
        {
            var panel = Object.FindAnyObjectByType<MainMenuPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogError("[MBG] MainMenuPanel tidak ditemukan. Jalankan Tools > MBG > Build UI Hierarchy dulu.");
                return false;
            }

            bool changed = false;
            var root = (RectTransform)panel.transform;

            changed |= EnsureBackdrop(root, new Color(0.05f, 0.04f, 0.07f, 0.97f), log, "MainMenuPanel");
            RectTransform card = EnsureCard(root, 560f, out bool cardCreated);
            changed |= cardCreated;

            TMP_Text title = EnsureLabel(card, "TitleLabel", "MAKAN BERGIZI GRATIS",
                                         TextAlignmentOptions.Center, 64f, out bool created);
            if (created) { PlaceTop(title.rectTransform, -Padding, 80f); changed = true; }

            TMP_Text subtitle = EnsureLabel(card, "SubtitleLabel", "Simulasi dapur catering",
                                            TextAlignmentOptions.Center, 28f, out created);
            if (created) { PlaceTop(subtitle.rectTransform, -(Padding + 84f), 44f); changed = true; }

            TMP_Text highScore = EnsureLabel(card, "HighScoreLabel", "Rekor skor: 0",
                                             TextAlignmentOptions.Center, 22f, out created);
            if (created) { PlaceTop(highScore.rectTransform, -(Padding + 130f), 36f); changed = true; }

            Button start = EnsureButton(card, "StartButton", "Mulai", 0, out created);
            changed |= created;
            Button howTo = EnsureButton(card, "HowToPlayButton", "Cara Bermain", 1, out created);
            changed |= created;
            Button quit = EnsureButton(card, "QuitButton", "Keluar", 2, out created);
            changed |= created;

            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "titleLabel", title);
            bound |= SetObject(so, "subtitleLabel", subtitle);
            bound |= SetObject(so, "highScoreLabel", highScore);
            bound |= SetObject(so, "startButton", start);
            bound |= SetObject(so, "howToPlayButton", howTo);
            bound |= SetObject(so, "quitButton", quit);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("MainMenuPanel diisi dan diikat.");
            }

            return changed;
        }

        static bool BuildHowToPlay(List<string> log)
        {
            var panel = Object.FindAnyObjectByType<HowToPlayPanel>(FindObjectsInactive.Include);
            if (panel == null) return false;

            bool changed = false;
            var root = (RectTransform)panel.transform;

            changed |= EnsureBackdrop(root, new Color(0.03f, 0.03f, 0.05f, 0.95f), log, "HowToPlayPanel");
            RectTransform card = EnsureCard(root, 700f, out bool cardCreated);
            changed |= cardCreated;

            TMP_Text title = EnsureLabel(card, "TitleLabel", "CARA BERMAIN",
                                         TextAlignmentOptions.Center, 46f, out bool created);
            if (created) { PlaceTop(title.rectTransform, -Padding, 60f); changed = true; }

            TMP_Text body = EnsureLabel(card, "BodyLabel", "",
                                        TextAlignmentOptions.TopLeft, 24f, out created);
            if (created)
            {
                PlaceTop(body.rectTransform, -(Padding + 70f), 460f);
                body.textWrappingMode = TextWrappingModes.Normal;
                changed = true;
            }

            Button back = EnsureButton(card, "BackButton", "Kembali", 0, out created, fromBottom: true);
            changed |= created;

            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "titleLabel", title);
            bound |= SetObject(so, "bodyLabel", body);
            bound |= SetObject(so, "backButton", back);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("HowToPlayPanel diisi dan diikat.");
            }

            return changed;
        }

        static bool BuildPause(List<string> log)
        {
            var panel = Object.FindAnyObjectByType<PausePanel>(FindObjectsInactive.Include);
            if (panel == null) return false;

            bool changed = false;
            var root = (RectTransform)panel.transform;

            changed |= EnsureBackdrop(root, new Color(0.03f, 0.03f, 0.05f, 0.80f), log, "PausePanel");
            RectTransform card = EnsureCard(root, 420f, out bool cardCreated);
            changed |= cardCreated;

            TMP_Text title = EnsureLabel(card, "TitleLabel", "JEDA",
                                         TextAlignmentOptions.Center, 48f, out bool created);
            if (created) { PlaceTop(title.rectTransform, -Padding, 64f); changed = true; }

            TMP_Text hint = EnsureLabel(card, "HintLabel", "Tekan Escape untuk melanjutkan",
                                        TextAlignmentOptions.Center, 22f, out created);
            if (created) { PlaceTop(hint.rectTransform, -(Padding + 66f), 36f); changed = true; }

            Button resume = EnsureButton(card, "ResumeButton", "Lanjut", 0, out created);
            changed |= created;
            Button restart = EnsureButton(card, "RestartButton", "Ulangi", 1, out created);
            changed |= created;
            Button menu = EnsureButton(card, "MenuButton", "Menu Utama", 2, out created);
            changed |= created;

            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "titleLabel", title);
            bound |= SetObject(so, "hintLabel", hint);
            bound |= SetObject(so, "resumeButton", resume);
            bound |= SetObject(so, "restartButton", restart);
            bound |= SetObject(so, "menuButton", menu);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("PausePanel diisi dan diikat.");
            }

            return changed;
        }

        static bool BuildDaySummary(List<string> log)
        {
            var panel = Object.FindAnyObjectByType<DaySummaryPanel>(FindObjectsInactive.Include);
            if (panel == null) return false;

            bool changed = false;
            var root = (RectTransform)panel.transform;

            changed |= EnsureBackdrop(root, new Color(0.04f, 0.04f, 0.06f, 0.92f), log, "DaySummaryPanel");
            RectTransform card = EnsureCard(root, 560f, out bool cardCreated);
            changed |= cardCreated;

            TMP_Text title = EnsureLabel(card, "TitleLabel", "HARI 1 SELESAI",
                                         TextAlignmentOptions.Center, 48f, out bool created);
            if (created) { PlaceTop(title.rectTransform, -Padding, 64f); changed = true; }

            TMP_Text detail = EnsureLabel(card, "DetailLabel", "",
                                          TextAlignmentOptions.TopLeft, 26f, out created);
            if (created)
            {
                PlaceTop(detail.rectTransform, -(Padding + 76f), 240f);
                detail.textWrappingMode = TextWrappingModes.Normal;
                changed = true;
            }

            TMP_Text footer = EnsureLabel(card, "FooterLabel", "",
                                          TextAlignmentOptions.Center, 22f, out created);
            if (created)
            {
                PlaceTop(footer.rectTransform, -(Padding + 324f), 60f);
                footer.textWrappingMode = TextWrappingModes.Normal;
                changed = true;
            }

            Button continueButton = EnsureButton(card, "ContinueButton", "Lanjut ke Hari Berikutnya",
                                                 0, out created, fromBottom: true, width: 460f);
            changed |= created;

            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "titleLabel", title);
            bound |= SetObject(so, "detailLabel", detail);
            bound |= SetObject(so, "footerLabel", footer);
            bound |= SetObject(so, "continueButton", continueButton);
            bound |= SetObject(so, "continueButtonLabel", continueButton.GetComponentInChildren<TMP_Text>(true));

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("DaySummaryPanel diisi dan diikat.");
            }

            return changed;
        }

        static bool BuildGameOver(List<string> log)
        {
            var panel = Object.FindAnyObjectByType<GameOverPanel>(FindObjectsInactive.Include);
            if (panel == null) return false;

            bool changed = false;
            var root = (RectTransform)panel.transform;

            changed |= EnsureBackdrop(root, new Color(0.06f, 0.02f, 0.03f, 0.95f), log, "GameOverPanel");
            RectTransform card = EnsureCard(root, 560f, out bool cardCreated);
            changed |= cardCreated;

            TMP_Text title = EnsureLabel(card, "TitleLabel", "REPUTASI HANCUR",
                                         TextAlignmentOptions.Center, 56f, out bool created);
            if (created) { PlaceTop(title.rectTransform, -Padding, 72f); changed = true; }

            TMP_Text reason = EnsureLabel(card, "ReasonLabel", "",
                                          TextAlignmentOptions.Center, 26f, out created);
            if (created)
            {
                PlaceTop(reason.rectTransform, -(Padding + 80f), 80f);
                reason.textWrappingMode = TextWrappingModes.Normal;
                changed = true;
            }

            TMP_Text stats = EnsureLabel(card, "StatsLabel", "",
                                         TextAlignmentOptions.TopLeft, 26f, out created);
            if (created)
            {
                PlaceTop(stats.rectTransform, -(Padding + 168f), 170f);
                stats.textWrappingMode = TextWrappingModes.Normal;
                changed = true;
            }

            Button restart = EnsureButton(card, "RestartButton", "Ulangi", 0, out created, fromBottom: true);
            changed |= created;
            Button quit = EnsureButton(card, "QuitButton", "Keluar", 1, out created, fromBottom: true);
            changed |= created;

            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "titleLabel", title);
            bound |= SetObject(so, "reasonLabel", reason);
            bound |= SetObject(so, "statsLabel", stats);
            bound |= SetObject(so, "restartButton", restart);
            bound |= SetObject(so, "quitButton", quit);

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("GameOverPanel diisi dan diikat.");
            }

            return changed;
        }

        // ---- Blok bangunan ----------------------------------------------------

        static bool EnsureBackdrop(RectTransform root, Color color, List<string> log, string owner)
        {
            RectTransform backdrop = EnsureChild(root, "Backdrop", out bool created);
            Image image = EnsureImage(backdrop);
            if (!created) return false;

            Stretch(backdrop);
            image.color = color;

            // Backdrop menangkap klik supaya tidak tembus ke panel di belakangnya.
            image.raycastTarget = true;
            return true;
        }

        static RectTransform EnsureCard(RectTransform root, float height, out bool created)
        {
            RectTransform card = EnsureChild(root, "Card", out created);
            Image image = EnsureImage(card);

            if (created)
            {
                card.anchorMin = new Vector2(0.5f, 0.5f);
                card.anchorMax = new Vector2(0.5f, 0.5f);
                card.pivot = new Vector2(0.5f, 0.5f);
                card.sizeDelta = new Vector2(CardWidth, height);
                card.anchoredPosition = Vector2.zero;
                image.color = new Color(0.10f, 0.09f, 0.13f, 0.96f);
            }

            return card;
        }

        /// <summary>
        /// Tombol bertumpuk dari atas (index 0 paling atas) atau dari bawah kartu.
        /// </summary>
        static Button EnsureButton(RectTransform card, string name, string label, int index,
                                   out bool created, bool fromBottom = false, float width = ButtonWidth)
        {
            RectTransform rect = EnsureChild(card, name, out created);
            Image image = EnsureImage(rect);

            var button = rect.GetComponent<Button>();
            if (button == null)
            {
                button = Undo.AddComponent<Button>(rect.gameObject);
                button.targetGraphic = image;
            }

            RectTransform labelRect = EnsureChild(rect, "Label", out bool labelCreated);
            TMP_Text text = EnsureLabelOn(labelRect, label, TextAlignmentOptions.Center, 26f);

            if (!created && !labelCreated) return button;

            if (created)
            {
                image.color = new Color(0.22f, 0.20f, 0.28f, 1f);
                image.raycastTarget = true;

                float offset = (ButtonHeight + ButtonGap) * index;

                if (fromBottom)
                {
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(0f, Padding * 0.6f + offset);
                }
                else
                {
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(0f, Padding + (2 - index) * (ButtonHeight + ButtonGap));
                }

                rect.sizeDelta = new Vector2(width, ButtonHeight);
            }

            if (labelCreated)
            {
                Stretch(labelRect);
                text.raycastTarget = false;
            }

            return button;
        }

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
