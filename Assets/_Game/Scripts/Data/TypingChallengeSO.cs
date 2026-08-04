using System.Collections.Generic;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Kumpulan kutipan peraturan yang harus diketik ulang pemain.
    ///
    /// SEMUA nama lembaga, program, dan nomor peraturan di sini FIKTIF. Jangan
    /// pernah memasukkan nama instansi, pejabat, atau produk hukum yang benar-benar
    /// ada — satirnya jalan justru karena semuanya karangan.
    /// </summary>
    [CreateAssetMenu(fileName = "TypingChallenges", menuName = "MBG/Typing Challenges")]
    public class TypingChallengeSO : ScriptableObject
    {
        [Header("Pendek")]
        public List<string> shortTexts = new();

        [Header("Sedang")]
        public List<string> mediumTexts = new();

        [Header("Panjang")]
        public List<string> longTexts = new();

        /// <summary>Ambil satu teks acak sesuai tingkat kesulitan jadwal hari.</summary>
        public string GetText(float difficulty)
        {
            List<string> pool = difficulty >= 0.7f ? longTexts
                : difficulty >= 0.35f ? mediumTexts
                : shortTexts;

            if (pool == null || pool.Count == 0) pool = FirstNonEmpty();
            if (pool == null || pool.Count == 0) return "Peraturan tidak terbaca.";

            return pool[Random.Range(0, pool.Count)];
        }

        List<string> FirstNonEmpty()
        {
            if (shortTexts != null && shortTexts.Count > 0) return shortTexts;
            if (mediumTexts != null && mediumTexts.Count > 0) return mediumTexts;
            return longTexts;
        }

        public bool HasAnyText
            => (shortTexts != null && shortTexts.Count > 0)
               || (mediumTexts != null && mediumTexts.Count > 0)
               || (longTexts != null && longTexts.Count > 0);

        /// <summary>Enam teks bawaan: dua pendek, dua sedang, dua panjang. Semuanya fiktif.</summary>
        public void ApplyDefaultTexts()
        {
            shortTexts = new List<string>
            {
                "Retribusi wajib menurut Perda Nomor 12 Tahun 2023.",
                "Setiap dapur rakyat dikenai iuran ketertiban."
            };

            mediumTexts = new List<string>
            {
                "Peraturan Dinas Pangan Wilayah Nomor 17 Tahun 2024 tentang " +
                "Penyelenggaraan Program Gizi Rakyat.",

                "Badan Koordinasi Pangan Kecamatan menetapkan biaya pembinaan " +
                "bagi setiap penyedia katering bergizi."
            };

            longTexts = new List<string>
            {
                "Berdasarkan Peraturan Dinas Pangan Wilayah Nomor 17 Tahun 2024 tentang " +
                "Penyelenggaraan Program Gizi Rakyat, setiap penyedia wajib menyetor " +
                "kontribusi pembinaan sebesar dua persen dari nilai pesanan.",

                "Sesuai Keputusan Badan Koordinasi Pangan Kecamatan Nomor 4 Tahun 2025, " +
                "penyelenggara dapur rakyat menanggung biaya pengawasan mutu, biaya " +
                "administrasi berkas, serta iuran ketertiban lingkungan setiap bulan."
            };
        }
    }
}
