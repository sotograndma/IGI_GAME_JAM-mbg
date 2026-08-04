using System.Collections.Generic;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Naskah dialog petugas pungli. Sengaja BUKAN dialogue tree: hanya deretan
    /// baris yang dilewati satu per satu, lalu satu pilihan di akhir.
    ///
    /// Semua nama lembaga dan peraturan fiktif.
    /// </summary>
    [CreateAssetMenu(fileName = "TaxDialogue", menuName = "MBG/Tax Dialogue")]
    public class TaxDialogueSO : ScriptableObject
    {
        [Tooltip("Nama yang tampil di panel dialog. Tanda tanya-nya disengaja.")]
        public string speakerName = "Petugas (?)";

        [Header("Tuntutan (3-4 baris)")]
        public List<string> demandLines = new();

        [Tooltip("Teks pilihan di baris terakhir.")]
        public string challengePrompt = "Tunjukkan dokumen resmi";

        [Header("Penutup")]
        [Tooltip("Dipakai saat pemain menang telak.")]
        public List<string> closingPerfect = new();

        [Tooltip("Dipakai saat pemain menang tipis.")]
        public List<string> closingGood = new();

        [Tooltip("Dipakai saat pemain mengetik berantakan.")]
        public List<string> closingPartial = new();

        [Tooltip("Dipakai saat pemain menyerah atau kehabisan waktu.")]
        public List<string> closingFail = new();

        public int LineCount => demandLines != null ? demandLines.Count : 0;

        public string GetLine(int index)
        {
            if (demandLines == null || index < 0 || index >= demandLines.Count) return "";
            return demandLines[index];
        }

        public string GetClosing(List<string> pool)
        {
            if (pool == null || pool.Count == 0) return "";
            return pool[Random.Range(0, pool.Count)];
        }

        public string GetClosingPerfect() => GetClosing(closingPerfect);
        public string GetClosingGood() => GetClosing(closingGood);
        public string GetClosingPartial() => GetClosing(closingPartial);
        public string GetClosingFail() => GetClosing(closingFail);

        public bool HasContent => LineCount > 0;

        /// <summary>Naskah bawaan — satir, dan seluruh rujukannya karangan.</summary>
        public void ApplyDefaultScript()
        {
            speakerName = "Petugas (?)";
            challengePrompt = "Tunjukkan dokumen resmi";

            demandLines = new List<string>
            {
                "Selamat siang, Bu. Ada retribusi baru, Pak Lurah sudah tahu kok.",
                "Nomor peraturannya? Ada, tapi tidak perlu Bapak-Ibu tahu.",
                "Ini demi ketertiban. Dapur ramai begini kan perlu diawasi.",
                "Kalau tidak bayar, izin catering bisa bermasalah, lho."
            };

            closingPerfect = new List<string>
            {
                "Oh... itu... saya salah alamat. Permisi!",
                "Wah, Ibu hafal peraturannya? Saya... ada rapat mendadak."
            };

            closingGood = new List<string>
            {
                "Hmm, berkasnya lengkap juga. Ya sudah, lain kali saja.",
                "Baik, baik. Saya catat sebagai kunjungan pembinaan saja."
            };

            closingPartial = new List<string>
            {
                "Berkas Ibu agak berantakan. Setengahnya saja, ya, untuk administrasi.",
                "Ada beberapa yang keliru. Kita anggap lunas sebagian saja."
            };

            closingFail = new List<string>
            {
                "Nah, kan. Bayar dulu, urusan belakangan.",
                "Tidak ada dokumen, ya tarifnya penuh, Bu."
            };
        }
    }
}
