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
    /// Mengisi HUDPanel dengan kartu pesanan, uang &amp; skor, timer, petunjuk
    /// langkah berikutnya, slot gangguan, dan teks melayang.
    ///
    /// Additive dan idempoten: tata letak hanya ditulis untuk elemen yang baru
    /// dibuat, jadi hasil tuning manual tidak tertimpa. Referensi komponen selalu
    /// diikat ulang.
    /// </summary>
    public static class HUDPanelBuilder
    {
        const string MenuPath = "Tools/MBG/Build HUD";

        // Tata letak dalam piksel referensi canvas (1920x1080).
        const float Margin = 24f;
        const float CardWidth = 430f;
        const float CardHeight = 186f;
        const float CardPadding = 14f;
        const float CurrencyWidth = 340f;
        const float CurrencyHeight = 130f;
        const float TimerWidth = 260f;
        const float TimerHeight = 84f;
        const float NextStepWidth = 900f;
        const float NextStepHeight = 52f;
        const float NextStepBottom = 56f;
        const float ArrowWidth = 56f;
        const float ObstacleWidth = 260f;
        const float ObstacleHeight = 120f;
        const float FloatingWidth = 240f;
        const float FloatingHeight = 40f;

        [MenuItem(MenuPath, false, 105)]
        public static void BuildHUD()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build HUD",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("TMP Essentials belum di-import",
                    "HUD memakai TextMeshPro. Import dulu lewat:\n" +
                    "Window > TextMeshPro > Import TMP Essential Resources", "OK");
                Debug.LogError("[MBG] Build HUD dibatalkan: TMP Essential Resources belum di-import.");
                return;
            }

            var panel = Object.FindAnyObjectByType<HUDPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogError("[MBG] HUDPanel tidak ditemukan di scene. " +
                               "Jalankan Tools > MBG > Build UI Hierarchy dulu.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build HUD");

            var log = new List<string>();
            bool changed = false;

            var root = (RectTransform)panel.transform;

            // ---- Kartu pesanan (kiri atas) ----
            RectTransform card = EnsureChild(root, "OrderCard", out bool created);
            var cardGroup = EnsureComponent<CanvasGroup>(card);
            var cardImage = EnsureImage(card);
            if (created)
            {
                Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f));
                card.sizeDelta = new Vector2(CardWidth, CardHeight);
                card.anchoredPosition = new Vector2(Margin, -Margin);
                cardImage.color = new Color(0.08f, 0.07f, 0.10f, 0.78f);
                changed = true;
                log.Add("OrderCard dibuat di kiri atas.");
            }

            TMP_Text recipient = EnsureLabel(card, "RecipientLabel", "SD Negeri 03 Sukamaju",
                                             TextAlignmentOptions.Left, 20f, out created);
            if (created) { PlaceInCard(recipient.rectTransform, 0f, 26f); changed = true; }

            TMP_Text recipe = EnsureLabel(card, "RecipeLabel", "Nasi Kotak",
                                          TextAlignmentOptions.Left, 28f, out created);
            if (created) { PlaceInCard(recipe.rectTransform, -26f, 36f); changed = true; }

            RectTransform progressBar = EnsureChild(card, "ProgressBar", out created);
            EnsureImage(progressBar);
            if (created)
            {
                PlaceInCard(progressBar, -66f, 26f);
                EnsureImage(progressBar).color = new Color(1f, 1f, 1f, 0.12f);
                changed = true;
            }

            RectTransform progressFill = EnsureChild(progressBar, "Fill", out created);
            EnsureImage(progressFill);
            if (created) { FillLeft(progressFill, 0.4f); changed = true; }

            TMP_Text progressLabel = EnsureLabel(card, "ProgressLabel", "0 / 0 porsi",
                                                 TextAlignmentOptions.Left, 20f, out created);
            if (created) { PlaceInCard(progressLabel.rectTransform, -94f, 24f); changed = true; }

            RectTransform qualityBar = EnsureChild(card, "QualityBar", out created);
            EnsureImage(qualityBar);
            if (created)
            {
                PlaceInCard(qualityBar, -120f, 12f);
                EnsureImage(qualityBar).color = new Color(1f, 1f, 1f, 0.12f);
                changed = true;
            }

            RectTransform qualityFill = EnsureChild(qualityBar, "QualityFill", out created);
            EnsureImage(qualityFill);
            if (created) { FillLeft(qualityFill, 1f); changed = true; }

            TMP_Text qualityLabel = EnsureLabel(card, "QualityLabel", "Kualitas: SEMPURNA",
                                                TextAlignmentOptions.Left, 20f, out created);
            if (created) { PlaceInCard(qualityLabel.rectTransform, -136f, 24f); changed = true; }

            // ---- Uang & skor (kanan atas) ----
            RectTransform currency = EnsureChild(root, "Currency", out created);
            if (created)
            {
                Anchor(currency, new Vector2(1f, 1f), new Vector2(1f, 1f));
                currency.sizeDelta = new Vector2(CurrencyWidth, CurrencyHeight);
                currency.anchoredPosition = new Vector2(-Margin, -Margin);
                changed = true;
                log.Add("Currency (gold + skor) dibuat di kanan atas.");
            }

            TMP_Text gold = EnsureLabel(currency, "GoldLabel", "Rp 0",
                                        TextAlignmentOptions.Right, 28f, out created);
            if (created) { PlaceTop(gold.rectTransform, 0f, 44f, 0f); changed = true; }

            TMP_Text score = EnsureLabel(currency, "ScoreLabel", "Skor 0",
                                         TextAlignmentOptions.Right, 24f, out created);
            if (created) { PlaceTop(score.rectTransform, -46f, 38f, 0f); changed = true; }

            TMP_Text reputation = EnsureLabel(currency, "ReputationLabel", "- BIASA  50/100",
                                              TextAlignmentOptions.Right, 22f, out created);
            if (created) { PlaceTop(reputation.rectTransform, -86f, 34f, 0f); changed = true; }

            // ---- Timer (tengah atas) ----
            RectTransform timer = EnsureChild(root, "Timer", out created);
            if (created)
            {
                Anchor(timer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                timer.sizeDelta = new Vector2(TimerWidth, TimerHeight);
                timer.anchoredPosition = new Vector2(0f, -Margin);
                changed = true;
                log.Add("Timer dibuat di tengah atas.");
            }

            TMP_Text timerLabel = EnsureLabel(timer, "TimerLabel", "--:--",
                                              TextAlignmentOptions.Center, 48f, out created);
            if (created) { Stretch(timerLabel.rectTransform); changed = true; }

            // ---- Langkah berikutnya (bawah tengah) ----
            RectTransform nextStep = EnsureChild(root, "NextStep", out created);
            if (created)
            {
                Anchor(nextStep, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                nextStep.sizeDelta = new Vector2(NextStepWidth, NextStepHeight);
                nextStep.anchoredPosition = new Vector2(0f, NextStepBottom);
                changed = true;
                log.Add("NextStep (petunjuk + panah) dibuat di bawah tengah.");
            }

            TMP_Text nextStepLabel = EnsureLabel(nextStep, "NextStepLabel", "Belum ada pesanan.",
                                                 TextAlignmentOptions.Center, 26f, out created);
            if (created) { Stretch(nextStepLabel.rectTransform); changed = true; }

            // Panah memakai karakter ASCII supaya pasti ada di font TMP bawaan.
            TMP_Text arrowLeft = EnsureLabel(nextStep, "ArrowLeft", "<",
                                             TextAlignmentOptions.Center, 40f, out created);
            if (created)
            {
                RectTransform rect = arrowLeft.rectTransform;
                Anchor(rect, new Vector2(0f, 0f), new Vector2(0f, 1f));
                rect.sizeDelta = new Vector2(ArrowWidth, 0f);
                rect.anchoredPosition = new Vector2(ArrowWidth * 0.5f, 0f);
                changed = true;
            }

            TMP_Text arrowRight = EnsureLabel(nextStep, "ArrowRight", ">",
                                              TextAlignmentOptions.Center, 40f, out created);
            if (created)
            {
                RectTransform rect = arrowRight.rectTransform;
                Anchor(rect, new Vector2(1f, 0f), new Vector2(1f, 1f));
                rect.sizeDelta = new Vector2(ArrowWidth, 0f);
                rect.anchoredPosition = new Vector2(-ArrowWidth * 0.5f, 0f);
                changed = true;
            }

            // ---- Slot gangguan (kanan bawah, sengaja kosong) ----
            RectTransform obstacleSlot = EnsureChild(root, "ObstacleSlot", out created);
            if (created)
            {
                Anchor(obstacleSlot, new Vector2(1f, 0f), new Vector2(1f, 0f));
                obstacleSlot.sizeDelta = new Vector2(ObstacleWidth, ObstacleHeight);
                obstacleSlot.anchoredPosition = new Vector2(-Margin, Margin);
                changed = true;
                log.Add("ObstacleSlot dibuat di kanan bawah (masih kosong).");
            }

            // ---- Teks melayang ----
            RectTransform floatingRoot = EnsureChild(root, "FloatingText", out created);
            TMP_Text floating = EnsureLabelOn(floatingRoot, "+0 porsi", TextAlignmentOptions.Left, 26f);
            if (created)
            {
                Anchor(floatingRoot, new Vector2(0f, 1f), new Vector2(0f, 1f));
                floatingRoot.sizeDelta = new Vector2(FloatingWidth, FloatingHeight);
                floatingRoot.anchoredPosition = new Vector2(Margin + CardPadding, -(Margin + CardHeight - 40f));
                floatingRoot.gameObject.SetActive(false);
                changed = true;
                log.Add("FloatingText dibuat di bawah kartu pesanan.");
            }

            // ---- Ikat referensi ----
            var so = new SerializedObject(panel);
            bool bound = false;
            bound |= SetObject(so, "orderCard", cardGroup);
            bound |= SetObject(so, "recipientLabel", recipient);
            bound |= SetObject(so, "recipeLabel", recipe);
            bound |= SetObject(so, "progressFill", progressFill);
            bound |= SetObject(so, "progressLabel", progressLabel);
            bound |= SetObject(so, "qualityFill", qualityFill);
            bound |= SetObject(so, "qualityLabel", qualityLabel);
            bound |= SetObject(so, "goldLabel", gold);
            bound |= SetObject(so, "scoreLabel", score);
            bound |= SetObject(so, "reputationLabel", reputation);
            bound |= SetObject(so, "timerRoot", timer);
            bound |= SetObject(so, "timerLabel", timerLabel);
            bound |= SetObject(so, "nextStepLabel", nextStepLabel);
            bound |= SetObject(so, "arrowLeft", arrowLeft.gameObject);
            bound |= SetObject(so, "arrowRight", arrowRight.gameObject);
            bound |= SetObject(so, "obstacleSlot", obstacleSlot);
            bound |= SetObject(so, "floatingTextRoot", floatingRoot);
            bound |= SetObject(so, "floatingText", floating);

            var player = Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);
            if (player != null) bound |= SetObject(so, "playerTransform", player.transform);
            else Debug.LogWarning("[MBG] Player tidak ditemukan — panah arah HUD tidak akan muncul.");

            if (bound)
            {
                so.ApplyModifiedProperties();
                changed = true;
                log.Add("Referensi HUDPanel diikat.");
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = panel.gameObject;
                Debug.Log("[MBG] Build HUD selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).", panel);
            }
            else
            {
                Debug.Log("[MBG] HUD sudah lengkap. Tidak ada perubahan.", panel);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildHUD() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- Layout helper ---------------------------------------------------

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(min.x, max.y);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Tempel di bagian atas parent, melebar penuh, dengan padding kiri-kanan.</summary>
        static void PlaceTop(RectTransform rect, float y, float height, float padding)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-padding * 2f, height);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        static void PlaceInCard(RectTransform rect, float y, float height)
            => PlaceTop(rect, y - CardPadding, height, CardPadding);

        static void FillLeft(RectTransform rect, float amount01)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(Mathf.Clamp01(amount01), 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ---- Pembuatan objek --------------------------------------------------

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

        static T EnsureComponent<T>(RectTransform rect) where T : Component
        {
            var component = rect.GetComponent<T>();
            if (component != null) return component;

            return Undo.AddComponent<T>(rect.gameObject);
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
