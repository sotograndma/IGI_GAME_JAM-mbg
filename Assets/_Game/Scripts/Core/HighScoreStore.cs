using UnityEngine;

namespace MBG.Core
{
    /// <summary>
    /// Satu-satunya pemakaian PlayerPrefs di project ini: menyimpan high score
    /// antar sesi. State gameplay lain TIDAK boleh masuk PlayerPrefs.
    /// </summary>
    public static class HighScoreStore
    {
        public const string Key = "MBG_HighScore";

        /// <summary>Hari tertinggi yang pernah dicapai di mode bertahan — juga sebuah rekor.</summary>
        public const string BestDayKey = "MBG_BestDay";

        public static int Get() => PlayerPrefs.GetInt(Key, 0);

        public static int GetBestDay() => PlayerPrefs.GetInt(BestDayKey, 0);

        /// <summary>Simpan hari terjauh kalau memecahkan rekor. True kalau rekor baru.</summary>
        public static bool TrySubmitBestDay(int day)
        {
            if (day <= GetBestDay()) return false;

            PlayerPrefs.SetInt(BestDayKey, day);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Simpan skor kalau memecahkan rekor. Mengembalikan true kalau rekor baru.
        /// </summary>
        public static bool TrySubmit(int score)
        {
            if (score <= Get()) return false;

            PlayerPrefs.SetInt(Key, score);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Hapus semua rekor. Hanya untuk debugging.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BestDayKey);
            PlayerPrefs.Save();
        }
    }
}
