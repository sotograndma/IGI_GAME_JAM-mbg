using MBG.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// Daftar kontrol, dibuka dari menu utama. Muncul di atas MainMenuPanel dan
    /// ditutup dengan tombol Kembali atau Escape.
    /// </summary>
    public class HowToPlayPanel : UIPanel
    {
        [Header("Referensi (diisi Tools > MBG > Build Menu Panels)")]
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text bodyLabel;
        [SerializeField] Button backButton;

        const string Body =
            "KONTROL\n" +
            "W A S D  —  jalan (di dalam dapur cukup A dan D)\n" +
            "Shift  —  lari\n" +
            "F  —  pakai station atau buka pintu\n" +
            "Spasi  —  tekan saat indikator masuk zona hijau\n" +
            "Escape  —  jeda\n\n" +
            "CARA MAIN\n" +
            "1. Terima pesanan catering dari sebuah institusi.\n" +
            "2. Ikuti langkah resep berurutan: potong, masak, lalu kemas.\n" +
            "3. Tiap langkah butuh satu tekanan Spasi yang tepat waktu.\n" +
            "4. Gagal total membuat satu batch harus diulang dari awal.\n" +
            "5. Kalau semua porsi siap, serahkan di meja serah terima.\n" +
            "6. Jangan sampai kehabisan waktu — reputasi ikut jatuh.";

        protected override void OnShow()
        {
            ApplyStyle();

            if (titleLabel != null) titleLabel.text = "CARA BERMAIN";
            if (bodyLabel != null) bodyLabel.text = Body;

            if (backButton == null) return;

            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Close);
        }

        void Update()
        {
            if (!IsVisible) return;

            if (InputService.CancelPressed) Close();
        }

        void Close()
        {
            AudioService.PlaySFX(SfxId.UiBack);
            Hide();
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

            style.ApplyBody(bodyLabel);
        }
    }
}
