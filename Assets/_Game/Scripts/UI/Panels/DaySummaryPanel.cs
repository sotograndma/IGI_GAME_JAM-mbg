using System.Text;
using MBG.Core;
using MBG.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// Rekap akhir hari. Setelah hari terakhir, panel yang sama berubah menjadi
    /// layar kemenangan sederhana.
    ///
    /// Data datang lewat <c>GameEventBus.OnDaySummary</c> yang diteruskan UIManager,
    /// jadi panel tidak perlu tahu DayManager.
    /// </summary>
    public class DaySummaryPanel : UIPanel
    {
        const string PauseReason = "DaySummaryPanel";

        [Header("Referensi (diisi Tools > MBG > Build Menu Panels)")]
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text detailLabel;
        [SerializeField] TMP_Text footerLabel;
        [SerializeField] Button continueButton;
        [SerializeField] TMP_Text continueButtonLabel;

        DayStats _stats;
        bool _victory;

        public void ShowSummary(DayStats stats)
        {
            _stats = stats;
            _victory = stats.isFinalDay;

            Show();
            Populate();
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

        void Populate()
        {
            UIStyle style = UIManager.Style;

            if (titleLabel != null)
            {
                titleLabel.text = _victory ? "SELAMAT!" : $"HARI {_stats.day} SELESAI";
                if (style != null)
                    titleLabel.color = _victory ? style.goldColor : style.textPrimary;
            }

            if (detailLabel != null) detailLabel.text = BuildDetail();

            if (footerLabel != null)
            {
                bool endless = DayManager.Instance != null && DayManager.Instance.IsEndlessDay;

                footerLabel.text = _victory
                    ? "Semua hari terlewati. Dapurmu bertahan sampai akhir!"
                    : endless
                        ? $"Mode bertahan — hari terjauh: {HighScoreStore.GetBestDay()}."
                        : "Istirahat sebentar, besok pesanan datang lagi.";
            }

            if (continueButtonLabel != null)
                continueButtonLabel.text = _victory ? "Selesai" : "Lanjut ke Hari Berikutnya";

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(OnContinue);
            }

            // Rekor hanya dicatat kalau permainan benar-benar tamat.
            if (!_victory) return;

            EconomyService economy = EconomyService.Instance;
            if (economy != null && HighScoreStore.TrySubmit(economy.Score))
                Debug.Log($"[UI] Rekor skor baru: {economy.Score}", this);
        }

        string BuildDetail()
        {
            var sb = new StringBuilder();

            sb.Append($"Pesanan berhasil: {_stats.ordersSucceeded} dari {_stats.totalOrders}\n");
            sb.Append($"Pesanan gagal: {_stats.ordersFailed}\n");
            sb.Append($"Gold hari ini: {_stats.goldEarned:+#;-#;0}\n");
            sb.Append($"Skor hari ini: +{_stats.scoreEarned}\n");
            sb.Append($"Reputasi: {_stats.reputation}/{_stats.maxReputation}");

            EconomyService economy = EconomyService.Instance;
            if (economy != null)
                sb.Append($"\n\nTotal: {economy.Gold:n0} gold, {economy.Score:n0} skor");

            return sb.ToString();
        }

        void OnContinue()
        {
            AudioService.PlaySFX(SfxId.UiClick);
            Hide();

            if (_victory)
            {
                // Kemenangan menutup permainan: muat ulang scene supaya semuanya
                // benar-benar bersih, lalu pemain kembali di menu utama.
                GameManager manager = GameManager.Instance;
                if (manager != null) manager.RestartGame();
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null) gameManager.ChangeState(GameState.Playing);

            DayManager days = DayManager.Instance;
            if (days != null) days.StartNextDay();
            else Debug.LogWarning("[UI] DayManager tidak ada — hari berikutnya tidak bisa dimulai.", this);
        }

        void ApplyStyle()
        {
            UIStyle style = UIManager.Style;
            if (style == null) return;

            if (titleLabel != null) titleLabel.fontSize = style.fontSizeHeading;

            style.ApplyBody(detailLabel);
            style.ApplySmall(footerLabel);
        }
    }
}
