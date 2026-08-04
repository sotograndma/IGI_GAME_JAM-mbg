using System.Text;
using MBG.Catering;
using MBG.Core;
using MBG.Data;
using TMPro;
using UnityEngine;

namespace MBG.UI
{
    /// <summary>
    /// Layar hasil satu pesanan: berhasil atau gagal, tingkat kualitas, rincian
    /// gold &amp; skor, dan satu kalimat reaksi dari pemesan.
    ///
    /// Selama panel tampil <see cref="GameClock"/> di-pause, jadi deadline pesanan
    /// berikutnya tidak berjalan sementara pemain membaca. Menekan SPASI menutup
    /// panel dan menyiarkan <see cref="GameEventBus.OnResultAcknowledged"/>, yang
    /// menjadi aba-aba bagi sistem catering untuk melanjutkan antrian.
    ///
    /// Panel ini tidak tahu apa-apa tentang CateringController; datanya datang dari
    /// UIManager yang meneruskan event bus.
    /// </summary>
    public class ResultPanel : UIPanel
    {
        const string PauseReason = "ResultPanel";

        [Header("Referensi (diisi Tools > MBG > Build Result Panel)")]
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text qualityLabel;
        [SerializeField] TMP_Text detailLabel;
        [SerializeField] TMP_Text reactionLabel;
        [SerializeField] TMP_Text continueLabel;

        [Header("Data")]
        [Tooltip("Dipakai menghitung denda saat pesanan gagal. Diikat oleh Tools > MBG > Build Catering Data.")]
        [SerializeField] ScoringConfigSO scoring;

        ScoringConfigSO Scoring => scoring != null ? scoring : ScoringConfigSO.Fallback;

        /// <summary>Tampilkan hasil pesanan yang berhasil diserahkan.</summary>
        public void ShowSuccess(OrderResult result)
        {
            Show();
            Populate(result);
        }

        /// <summary>
        /// Tampilkan hasil pesanan yang gagal. Angkanya dihitung dengan rumus yang
        /// sama seperti EconomyService, jadi denda yang tampil pasti cocok dengan
        /// gold yang benar-benar terpotong.
        /// </summary>
        public void ShowFailure(OrderRuntime order)
        {
            Show();
            Populate(Scoring.CalculateResult(order, success: false));
        }

        protected override void OnShow()
        {
            ApplyStyle();
            GameClock.PushPause(PauseReason);
        }

        protected override void OnHide()
        {
            GameClock.PopPause(PauseReason);
        }

        void Update()
        {
            if (!IsVisible) return;

            if (InputService.ConfirmPressed) Dismiss();
        }

        void Dismiss()
        {
            Hide();
            AudioService.PlaySFX(SfxId.UiClick);
            GameEventBus.RaiseResultAcknowledged();
        }

        // ---- Isi ------------------------------------------------------------

        void Populate(OrderResult result)
        {
            UIStyle style = UIManager.Style;

            if (titleLabel != null)
            {
                titleLabel.text = result.success ? "CATERING DISERAHKAN" : "CATERING GAGAL";
                if (style != null)
                    titleLabel.color = result.success ? style.successColor : style.dangerColor;
            }

            if (qualityLabel != null)
            {
                qualityLabel.text = $"Kualitas: {result.quality.GetLabel()}";
                if (style != null) qualityLabel.color = style.GetQualityColor(result.quality);
            }

            if (detailLabel != null) detailLabel.text = BuildDetail(result);

            if (reactionLabel != null) reactionLabel.text = BuildReaction(result);

            if (continueLabel != null) continueLabel.text = "Tekan SPASI untuk lanjut";
        }

        string BuildDetail(OrderResult result)
        {
            var sb = new StringBuilder();

            sb.Append($"Porsi diserahkan: {result.portionsDelivered}\n");

            if (result.success)
            {
                sb.Append($"Gold: +{result.goldEarned}\n");
                sb.Append($"   termasuk bonus waktu: +{result.timeBonusGold}\n");
                sb.Append($"Skor: +{result.scoreEarned}");

                if (result.perfectCount > 0)
                    sb.Append($"   ({result.perfectCount}x PERFECT)");
            }
            else
            {
                sb.Append($"Gold: {result.goldEarned} (denda keterlambatan)\n");
                sb.Append("Bonus waktu: -\n");
                sb.Append("Skor: 0");
            }

            if (result.batchRestarts > 0)
                sb.Append($"\nBatch diulang: {result.batchRestarts}x");

            return sb.ToString();
        }

        string BuildReaction(OrderResult result)
        {
            RecipientSO recipient = result.source != null ? result.source.recipient : null;
            if (recipient == null) return "";

            string line = recipient.GetReaction(result.quality);
            if (string.IsNullOrWhiteSpace(line)) return "";

            return $"\"{line}\"";
        }

        // ---- Tema -----------------------------------------------------------

        void ApplyStyle()
        {
            UIStyle style = UIManager.Style;
            if (style == null) return;

            if (titleLabel != null) titleLabel.fontSize = style.fontSizeHeading;

            if (qualityLabel != null) qualityLabel.fontSize = style.fontSizeBody;

            style.ApplyBody(detailLabel);

            if (reactionLabel != null)
            {
                reactionLabel.fontSize = style.fontSizeBody;
                reactionLabel.color = style.textMuted;
            }

            style.ApplySmall(continueLabel);
        }
    }
}
