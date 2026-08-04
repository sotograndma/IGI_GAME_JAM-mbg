using System;

namespace MBG.QTE
{
    /// <summary>
    /// Permintaan menjalankan satu sesi QTE. Pemanggil (station dapur, gangguan,
    /// dsb.) mengisi ini lalu menyerahkannya ke <see cref="QTEController.Begin"/>.
    /// </summary>
    public struct QTERequest
    {
        /// <summary>Mekanik yang dipakai — menentukan module mana yang dipilih controller.</summary>
        public QTEType type;

        /// <summary>Semua angka kesulitan. Wajib diisi; sesi ditolak kalau null.</summary>
        public QTEConfigSO config;

        /// <summary>Instruksi yang tampil di panel, misal "Potong bawang!".</summary>
        public string label;

        /// <summary>Dipanggil sekali saat sesi selesai, termasuk saat gagal atau dibatalkan.</summary>
        public Action<QTEResult> onComplete;

        public QTERequest(QTEType type, QTEConfigSO config, string label, Action<QTEResult> onComplete = null)
        {
            this.type = type;
            this.config = config;
            this.label = label;
            this.onComplete = onComplete;
        }

        /// <summary>Pintasan untuk mekanik timing bar.</summary>
        public static QTERequest TimingBar(QTEConfigSO config, string label, Action<QTEResult> onComplete = null)
            => new QTERequest(QTEType.TimingBar, config, label, onComplete);
    }
}
