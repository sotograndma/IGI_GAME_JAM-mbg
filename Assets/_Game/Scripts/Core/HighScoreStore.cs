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

        public static int Get() => PlayerPrefs.GetInt(Key, 0);

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

        /// <summary>Hapus rekor. Hanya untuk debugging.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
