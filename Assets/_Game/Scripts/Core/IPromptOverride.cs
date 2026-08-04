using UnityEngine;

namespace MBG.Core
{
    /// <summary>
    /// Ditempel di GameObject yang sama dengan sebuah <c>IInteractable</c> untuk
    /// mengganti teks dan warna prompt sementara waktu.
    ///
    /// Dipakai pintu: saat ada gangguan, "Keluar rumah" berubah menjadi
    /// "Keluar dan hadapi mereka" berwarna merah. Interface terpisah supaya
    /// interactable biasa tidak perlu tahu apa-apa soal ini.
    /// </summary>
    public interface IPromptOverride
    {
        /// <summary>
        /// True kalau prompt sedang perlu diganti. <paramref name="tint"/> boleh
        /// diabaikan pemanggil kalau warnanya tidak didukung.
        /// </summary>
        bool TryGetPromptOverride(out string text, out Color tint);
    }
}
