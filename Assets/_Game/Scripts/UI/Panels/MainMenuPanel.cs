using MBG.Core;
using MBG.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// Menu utama: judul, Mulai, Cara Bermain, Keluar.
    /// Muncul saat <see cref="GameState.MainMenu"/> — permainan tidak langsung
    /// masuk gameplay begitu scene dibuka.
    ///
    /// Listener tombol dipasang di <see cref="OnShow"/> (bukan Awake) karena panel
    /// disimpan nonaktif di scene; RemoveAllListeners lebih dulu supaya tidak
    /// bertumpuk kalau panel dibuka berkali-kali.
    /// </summary>
    public class MainMenuPanel : UIPanel
    {
        [Header("Referensi (diisi Tools > MBG > Build Menu Panels)")]
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text subtitleLabel;
        [SerializeField] TMP_Text highScoreLabel;
        [SerializeField] Button startButton;
        [SerializeField] Button howToPlayButton;
        [SerializeField] Button quitButton;

        protected override void OnShow()
        {
            ApplyStyle();

            if (highScoreLabel != null)
                highScoreLabel.text = $"Rekor skor: {HighScoreStore.Get():n0}";

            Bind(startButton, OnStart);
            Bind(howToPlayButton, OnHowToPlay);
            Bind(quitButton, OnQuit);
        }

        void OnStart()
        {
            AudioService.PlaySFX(SfxId.UiClick);

            GameManager manager = GameManager.Instance;
            if (manager != null) manager.ChangeState(GameState.Playing);

            DayManager days = DayManager.Instance;
            if (days != null) days.StartFirstDay();
            else Debug.LogWarning("[UI] DayManager tidak ada — tidak ada hari yang bisa dimulai.", this);
        }

        void OnHowToPlay()
        {
            AudioService.PlaySFX(SfxId.UiClick);

            if (UIManager.Instance != null) UIManager.Instance.ShowPanel<HowToPlayPanel>();
        }

        void OnQuit()
        {
            AudioService.PlaySFX(SfxId.UiBack);
            GameManager.QuitGame();
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        void ApplyStyle()
        {
            UIStyle style = UIManager.Style;
            if (style == null) return;

            if (titleLabel != null)
            {
                titleLabel.fontSize = style.fontSizeHeading * 1.4f;
                titleLabel.color = style.goldColor;
            }

            if (subtitleLabel != null)
            {
                subtitleLabel.fontSize = style.fontSizeBody;
                subtitleLabel.color = style.textMuted;
            }

            style.ApplySmall(highScoreLabel);
        }
    }
}
