using System.Text;
using MBG.Core;
using MBG.Data;
using MBG.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// Layar kalah: sebab kekalahan dalam bahasa Indonesia, statistik akhir, lalu
    /// Ulangi atau Keluar.
    ///
    /// Ini layar terminal, jadi ia mengambil snapshot menyeluruh langsung dari
    /// EconomyService dan DayManager. Panel gameplay lain tidak boleh melakukan itu
    /// — di sini justru tidak ada gunanya membuat event khusus yang hanya dipakai
    /// sekali di akhir permainan.
    /// </summary>
    public class GameOverPanel : UIPanel
    {
        const string PauseReason = "GameOverPanel";

        [Header("Referensi (diisi Tools > MBG > Build Menu Panels)")]
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text reasonLabel;
        [SerializeField] TMP_Text statsLabel;
        [SerializeField] Button restartButton;
        [SerializeField] Button quitButton;

        public void ShowGameOver(GameOverReason reason)
        {
            Show();
            Populate(reason);
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

        void Populate(GameOverReason reason)
        {
            UIStyle style = UIManager.Style;

            if (titleLabel != null)
            {
                titleLabel.text = reason.GetTitle();
                if (style != null) titleLabel.color = style.dangerColor;
            }

            if (reasonLabel != null) reasonLabel.text = reason.GetLabel();

            if (statsLabel != null) statsLabel.text = BuildStats();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestart);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(OnQuit);
            }
        }

        string BuildStats()
        {
            EconomyService economy = EconomyService.Instance;
            DayManager days = DayManager.Instance;

            int score = economy != null ? economy.Score : 0;
            bool newRecord = HighScoreStore.TrySubmit(score);
            int highScore = HighScoreStore.Get();

            ReputationService reputation = ReputationService.Instance;

            var sb = new StringBuilder();
            sb.Append($"Hari bertahan: {(days != null ? days.CurrentDayNumber : 1)}\n");
            sb.Append($"Hari terjauh: {HighScoreStore.GetBestDay()}\n");

            if (reputation != null)
                sb.Append($"Reputasi akhir: {reputation.Reputation}/{reputation.MaxReputation} " +
                          $"({reputation.Tier.GetLabel()})\n");

            sb.Append($"Total gold: {(economy != null ? economy.Gold : 0):n0}\n");
            sb.Append($"Total skor: {score:n0}\n");
            sb.Append($"Rekor skor: {highScore:n0}");

            if (newRecord) sb.Append("   (REKOR BARU!)");

            return sb.ToString();
        }

        void OnRestart()
        {
            AudioService.PlaySFX(SfxId.UiClick);

            GameManager manager = GameManager.Instance;
            if (manager != null) manager.RestartGame();
        }

        void OnQuit()
        {
            AudioService.PlaySFX(SfxId.UiBack);
            GameManager.QuitGame();
        }

        void ApplyStyle()
        {
            UIStyle style = UIManager.Style;
            if (style == null) return;

            if (titleLabel != null) titleLabel.fontSize = style.fontSizeHeading * 1.2f;

            if (reasonLabel != null)
            {
                reasonLabel.fontSize = style.fontSizeBody;
                reasonLabel.color = style.textMuted;
            }

            style.ApplyBody(statsLabel);
        }
    }
}
