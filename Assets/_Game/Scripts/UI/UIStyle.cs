using MBG.QTE;
using TMPro;
using UnityEngine;

namespace MBG.UI
{
    /// <summary>
    /// Palet warna dan ukuran font terpusat untuk seluruh UI. Semua panel WAJIB
    /// membaca nilai dari sini, tidak boleh menulis warna atau font size sendiri —
    /// dengan begitu ganti tema cukup mengubah satu asset.
    ///
    /// Asset-nya: Assets/_Game/Data/UIStyle.asset (dibuat otomatis oleh
    /// Tools > MBG > Build UI Hierarchy).
    /// </summary>
    [CreateAssetMenu(fileName = "UIStyle", menuName = "MBG/UI Style")]
    public class UIStyle : ScriptableObject
    {
        [Header("Warna hasil QTE")]
        public Color perfectColor = new Color(0.34f, 0.88f, 0.56f, 1f);
        public Color goodColor = new Color(0.95f, 0.78f, 0.30f, 1f);
        public Color missColor = new Color(0.92f, 0.34f, 0.34f, 1f);

        [Header("Warna status")]
        [Tooltip("Timer hampir habis, reputasi kritis, gangguan aktif.")]
        public Color dangerColor = new Color(0.89f, 0.34f, 0.18f, 1f);
        [Tooltip("Uang, tip, bonus.")]
        public Color goldColor = new Color(1f, 0.82f, 0.40f, 1f);
        public Color successColor = new Color(0.34f, 0.88f, 0.56f, 1f);

        [Header("Warna teks & panel")]
        public Color textPrimary = new Color(1f, 1f, 1f, 1f);
        public Color textMuted = new Color(0.72f, 0.72f, 0.78f, 1f);
        public Color panelBackground = new Color(0.08f, 0.07f, 0.10f, 0.85f);
        public Color overlayBackground = new Color(0.03f, 0.03f, 0.05f, 0.92f);

        [Header("Ukuran font")]
        public float fontSizeHeading = 48f;
        public float fontSizeBody = 28f;
        public float fontSizeSmall = 20f;

        [Header("Animasi panel")]
        [Tooltip("Durasi fade default UIPanel, dalam detik unscaled.")]
        public float panelFadeDuration = 0.15f;

        [Tooltip("Berapa lama teks hasil QTE (PERFECT / BAGUS / ...) bertahan sebelum memudar.")]
        public float qteFeedbackDuration = 0.5f;

        /// <summary>
        /// Warna untuk sebuah nilai QTE. Teksnya sendiri diambil dari
        /// <c>QTEGradeExtensions.GetLabel()</c>, bukan dari sini.
        /// </summary>
        public Color GetQTEColor(QTEGrade grade)
        {
            switch (grade)
            {
                case QTEGrade.Perfect: return perfectColor;
                case QTEGrade.Good: return goodColor;
                case QTEGrade.Miss: return missColor;
                default: return dangerColor;
            }
        }

        // ---- Helper penerapan ke TMP -------------------------------------
        // Dipakai panel supaya tidak ada satu pun angka font yang ditulis ulang.

        public void ApplyHeading(TMP_Text target)
        {
            if (target == null) return;
            target.fontSize = fontSizeHeading;
            target.color = textPrimary;
        }

        public void ApplyBody(TMP_Text target)
        {
            if (target == null) return;
            target.fontSize = fontSizeBody;
            target.color = textPrimary;
        }

        public void ApplySmall(TMP_Text target)
        {
            if (target == null) return;
            target.fontSize = fontSizeSmall;
            target.color = textMuted;
        }
    }
}
