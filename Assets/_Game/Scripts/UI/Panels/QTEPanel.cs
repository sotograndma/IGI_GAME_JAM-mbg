using System.Collections.Generic;
using MBG.Core;
using MBG.QTE;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// Tampilan mekanik timing bar: zona Good, zona Perfect di dalamnya, indikator
    /// berjalan, instruksi, dan teks hasil besar (PERFECT / BAGUS / MELESET / GAGAL).
    ///
    /// Panel ini murni presentasi — ia membaca state dari
    /// <see cref="QTEController"/> dan tidak pernah menilai input sendiri. Warna
    /// dan ukuran font selalu diambil dari <see cref="UIStyle"/> saat panel muncul,
    /// jadi mengganti tema tidak perlu menyentuh scene.
    ///
    /// Ditempatkan di tengah bawah layar supaya tidak menutupi karakter.
    /// </summary>
    public class QTEPanel : UIPanel
    {
        [Header("Referensi (diisi Tools > MBG > Build QTE Setup)")]
        [SerializeField] TMP_Text instructionLabel;
        [SerializeField] TMP_Text feedbackLabel;
        [SerializeField] Image barBackground;
        [SerializeField] RectTransform goodZone;
        [SerializeField] RectTransform perfectZone;
        [SerializeField] RectTransform cursor;

        [Header("Ritme (diisi Tools > MBG > Build Santet Setup)")]
        [Tooltip("Batang bar timing/mash — disembunyikan selama mekanik ritme. " +
                 "Label instruksi sengaja TIDAK ikut disembunyikan.")]
        [SerializeField] RectTransform barFrame;

        [SerializeField] RectTransform rhythmArea;
        [SerializeField] RectTransform noteTemplate;

        [Header("Mengetik (diisi Tools > MBG > Build Tax Setup)")]
        [SerializeField] RectTransform typingArea;
        [SerializeField] TMP_Text typingTextLabel;
        [SerializeField] TMP_Text typingStatsLabel;

        [Tooltip("Seberapa besar ring luar saat baru muncul, relatif ring dalam.")]
        [SerializeField] float noteMaxScale = 3.4f;

        const float FeedbackFadeTail = 0.25f;

        float _feedbackTimer;

        readonly List<RectTransform> _notePool = new();

        protected override void OnShow()
        {
            GameEventBus.OnQTEHit += HandleHit;

            ApplyStyle();
            ClearFeedback();
            RefreshInstruction();
        }

        protected override void OnHide()
        {
            GameEventBus.OnQTEHit -= HandleHit;
            ClearFeedback();
        }

        void Update()
        {
            QTEController controller = QTEController.Instance;
            IQTEModule module = controller != null ? controller.ActiveModule : null;

            bool rhythm = module is IRhythmReadout;
            bool typing = module is ITypingReadout;

            SetActive(barFrame, !rhythm && !typing);
            SetActive(rhythmArea, rhythm);
            SetActive(typingArea, typing);

            if (module is ITimingBarReadout bar)
            {
                UpdateBar(bar);
                RefreshInstruction();
            }
            else if (module is ITugOfWarReadout tug)
            {
                UpdateTugOfWar(tug);
            }
            else if (module is IRhythmReadout notes)
            {
                UpdateRhythm(notes);
            }
            else if (module is ITypingReadout text)
            {
                UpdateTyping(text);
            }

            TickFeedback();
        }

        // ---- Bar ----------------------------------------------------------

        void UpdateBar(ITimingBarReadout bar)
        {
            StretchHorizontal(goodZone, bar.GoodMin, bar.GoodMax);
            StretchHorizontal(perfectZone, bar.PerfectMin, bar.PerfectMax);
            PlaceCursor(cursor, bar.Cursor01);
        }

        /// <summary>
        /// Zona digambar lewat anchor, bukan lebar piksel, jadi bar ikut menyesuaikan
        /// diri di resolusi mana pun tanpa perhitungan tambahan.
        /// </summary>
        static void StretchHorizontal(RectTransform rect, float min, float max)
        {
            if (rect == null) return;

            rect.anchorMin = new Vector2(min, 0f);
            rect.anchorMax = new Vector2(max, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Mekanik tarik-menarik memakai elemen bar yang sama: zona Good menjadi
        /// sisi pemain yang terisi, zona Perfect disembunyikan, dan cursor menjadi
        /// batas tarik-menarik.
        /// </summary>
        void UpdateTugOfWar(ITugOfWarReadout tug)
        {
            StretchHorizontal(goodZone, 0f, tug.Position01);
            StretchHorizontal(perfectZone, tug.Position01, tug.Position01);
            PlaceCursor(cursor, tug.Position01);

            if (instructionLabel == null) return;

            string taunt = string.IsNullOrWhiteSpace(tug.CurrentTaunt) ? "" : $"\"{tug.CurrentTaunt}\"  ";
            string text = $"{taunt}TEKAN SPASI TERUS!";

            if (instructionLabel.text != text) instructionLabel.text = text;
        }

        /// <summary>
        /// Gambar not ritme: ring dalam diam, ring luar mengecil menuju ring dalam.
        /// Not dipakai ulang dari pool supaya tidak ada alokasi tiap frame.
        /// </summary>
        void UpdateRhythm(IRhythmReadout readout)
        {
            if (rhythmArea == null || noteTemplate == null) return;

            IReadOnlyList<RhythmNoteView> views = readout.ActiveNotes;
            EnsureNotePool(views.Count);

            for (int i = 0; i < _notePool.Count; i++)
            {
                RectTransform note = _notePool[i];

                if (i >= views.Count)
                {
                    if (note.gameObject.activeSelf) note.gameObject.SetActive(false);
                    continue;
                }

                RhythmNoteView view = views[i];

                if (!note.gameObject.activeSelf) note.gameObject.SetActive(true);

                note.anchorMin = view.position01;
                note.anchorMax = view.position01;
                note.anchoredPosition = Vector2.zero;

                Transform outer = note.childCount > 0 ? note.GetChild(0) : null;
                if (outer == null) continue;

                // approach01: 1 = baru muncul, 0 = saatnya ditekan. Nilai negatif
                // berarti sudah lewat sedikit, ring menyusut di bawah ring dalam.
                float scale = Mathf.Lerp(1f, noteMaxScale, Mathf.Max(0f, view.approach01));
                outer.localScale = new Vector3(scale, scale, 1f);
            }

            if (instructionLabel == null) return;

            string text = $"Tahan santetnya! ({readout.NotesJudged}/{readout.TotalNotes})";
            if (instructionLabel.text != text) instructionLabel.text = text;
        }

        /// <summary>
        /// Gambar kutipan yang harus diketik: karakter yang benar hijau, yang salah
        /// merah, sisanya redup, dengan kursor di posisi ketikan berikutnya.
        /// Warnanya diambil dari UIStyle, bukan ditulis di sini.
        /// </summary>
        void UpdateTyping(ITypingReadout readout)
        {
            if (typingTextLabel == null) return;

            UIStyle style = UIManager.Style;
            string ok = ToHex(style != null ? style.successColor : Color.green);
            string bad = ToHex(style != null ? style.missColor : Color.red);
            string idle = ToHex(style != null ? style.textMuted : Color.gray);
            string cursorColor = ToHex(style != null ? style.textPrimary : Color.white);

            string target = readout.TargetText;
            string typed = readout.TypedText;

            var sb = new System.Text.StringBuilder(target.Length * 24);

            for (int i = 0; i < target.Length; i++)
            {
                if (i == typed.Length) sb.Append($"<color=#{cursorColor}>|</color>");

                char c = target[i];

                if (i < typed.Length)
                    sb.Append($"<color=#{(typed[i] == c ? ok : bad)}>{c}</color>");
                else
                    sb.Append($"<color=#{idle}>{c}</color>");
            }

            if (typed.Length >= target.Length) sb.Append($"<color=#{cursorColor}>|</color>");

            typingTextLabel.text = sb.ToString();

            if (typingStatsLabel == null) return;

            typingStatsLabel.text = $"Sisa {readout.TimeRemaining:0}s     " +
                                    $"Akurasi {readout.Accuracy01 * 100f:0}%     " +
                                    $"{readout.WordsPerMinute:0} WPM";

            if (style != null)
                typingStatsLabel.color = readout.TimeRemaining01 <= style.timerWarningThreshold
                    ? style.dangerColor
                    : style.textMuted;
        }

        static string ToHex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        void EnsureNotePool(int count)
        {
            while (_notePool.Count < count)
            {
                RectTransform note = Instantiate(noteTemplate, rhythmArea);
                note.gameObject.SetActive(false);
                note.name = $"Note_{_notePool.Count}";
                _notePool.Add(note);
            }
        }

        static void SetActive(RectTransform rect, bool active)
        {
            if (rect != null && rect.gameObject.activeSelf != active) rect.gameObject.SetActive(active);
        }

        static void PlaceCursor(RectTransform rect, float position01)
        {
            if (rect == null) return;

            // Lebar cursor tetap (sizeDelta.x), jadi anchor min dan max sama-sama
            // ditempel di posisi yang sama.
            rect.anchorMin = new Vector2(position01, 0f);
            rect.anchorMax = new Vector2(position01, 1f);
            rect.anchoredPosition = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 0f);
        }

        // ---- Teks ---------------------------------------------------------

        void RefreshInstruction()
        {
            if (instructionLabel == null) return;

            QTEController controller = QTEController.Instance;
            if (controller == null) return;

            string text = controller.ActiveLabel;
            if (controller.ActiveModule is ITimingBarReadout bar && bar.TotalHits > 1)
                text += $"  ({bar.CurrentHit}/{bar.TotalHits})";

            if (instructionLabel.text != text) instructionLabel.text = text;
        }

        void HandleHit(QTEGrade grade)
        {
            if (feedbackLabel == null) return;

            UIStyle style = UIManager.Style;

            feedbackLabel.text = grade.GetLabel();
            feedbackLabel.color = style != null ? style.GetQTEColor(grade) : Color.white;
            _feedbackTimer = style != null ? style.qteFeedbackDuration : 0.5f;
        }

        void TickFeedback()
        {
            if (feedbackLabel == null || _feedbackTimer <= 0f) return;

            // Waktu unscaled: teks hasil harus tetap memudar walau clock gameplay
            // sedang di-pause.
            _feedbackTimer -= Time.unscaledDeltaTime;

            if (_feedbackTimer <= 0f)
            {
                ClearFeedback();
                return;
            }

            Color c = feedbackLabel.color;
            c.a = _feedbackTimer >= FeedbackFadeTail ? 1f : _feedbackTimer / FeedbackFadeTail;
            feedbackLabel.color = c;
        }

        void ClearFeedback()
        {
            _feedbackTimer = 0f;
            if (feedbackLabel != null) feedbackLabel.text = "";
        }

        // ---- Tema ---------------------------------------------------------

        void ApplyStyle()
        {
            UIStyle style = UIManager.Style;
            if (style == null) return;

            if (barBackground != null) barBackground.color = style.panelBackground;

            SetZoneColor(goodZone, style.goodColor, 0.55f);
            SetZoneColor(perfectZone, style.perfectColor, 0.9f);
            SetZoneColor(cursor, style.textPrimary, 1f);

            if (instructionLabel != null) style.ApplyBody(instructionLabel);

            if (feedbackLabel != null)
            {
                feedbackLabel.fontSize = style.fontSizeHeading;
                feedbackLabel.color = style.textPrimary;
            }
        }

        static void SetZoneColor(RectTransform rect, Color color, float alpha)
        {
            if (rect == null) return;

            var image = rect.GetComponent<Image>();
            if (image == null) return;

            color.a = alpha;
            image.color = color;
        }
    }
}
