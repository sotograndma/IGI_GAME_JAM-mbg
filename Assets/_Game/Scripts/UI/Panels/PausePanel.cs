using MBG.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// Menu jeda. Muncul saat <see cref="GameState.Paused"/>.
    ///
    /// Panel ini TIDAK memanggil GameClock.PushPause sendiri: GameManager sudah
    /// melakukannya saat masuk state Paused, dan melepasnya saat keluar. Menambah
    /// pause kedua di sini hanya akan membuat penghitungnya tidak seimbang.
    ///
    /// "Ulangi" dan "Menu Utama" sama-sama memuat ulang scene. Itu disengaja:
    /// tidak ada state gameplay yang boleh bertahan lintas restart, dan PlayerPrefs
    /// hanya boleh dipakai untuk high score.
    /// </summary>
    public class PausePanel : UIPanel
    {
        [Header("Referensi (diisi Tools > MBG > Build Menu Panels)")]
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text hintLabel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button menuButton;

        protected override void OnShow()
        {
            ApplyStyle();

            if (titleLabel != null) titleLabel.text = "JEDA";
            if (hintLabel != null) hintLabel.text = "Tekan Escape untuk melanjutkan";

            Bind(resumeButton, OnResume);
            Bind(restartButton, OnRestart);
            Bind(menuButton, OnMainMenu);
        }

        void OnResume()
        {
            AudioService.PlaySFX(SfxId.UiBack);

            GameManager manager = GameManager.Instance;
            if (manager != null) manager.ChangeState(GameState.Playing);
        }

        void OnRestart()
        {
            AudioService.PlaySFX(SfxId.UiClick);
            Restart();
        }

        void OnMainMenu()
        {
            AudioService.PlaySFX(SfxId.UiBack);
            Restart();
        }

        static void Restart()
        {
            GameManager manager = GameManager.Instance;
            if (manager != null) manager.RestartGame();
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
                titleLabel.fontSize = style.fontSizeHeading;
                titleLabel.color = style.textPrimary;
            }

            style.ApplySmall(hintLabel);
        }
    }
}
