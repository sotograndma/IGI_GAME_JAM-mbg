namespace MBG.QTE
{
    /// <summary>
    /// Nilai satu input QTE — dan juga nilai akhir satu sesi.
    ///
    /// Urutan nilainya sengaja dari terbaik ke terburuk: nilai akhir sesi dihitung
    /// sebagai yang TERBURUK di antara semua input, jadi cukup
    /// <c>(QTEGrade)Mathf.Max((int)a, (int)b)</c> tanpa tabel perbandingan.
    /// </summary>
    public enum QTEGrade
    {
        Perfect = 0,
        Good = 1,
        Miss = 2,

        /// <summary>Indikator sampai ujung tanpa input sama sekali — gagal total.</summary>
        CriticalMiss = 3
    }

    public static class QTEGradeExtensions
    {
        /// <summary>Perfect dan Good dianggap berhasil; Miss dan CriticalMiss tidak.</summary>
        public static bool IsSuccess(this QTEGrade grade)
            => grade == QTEGrade.Perfect || grade == QTEGrade.Good;

        /// <summary>Teks besar yang tampil di QTEPanel (bahasa Indonesia).</summary>
        public static string GetLabel(this QTEGrade grade)
        {
            switch (grade)
            {
                case QTEGrade.Perfect: return "PERFECT";
                case QTEGrade.Good: return "BAGUS";
                case QTEGrade.Miss: return "MELESET";
                default: return "GAGAL";
            }
        }
    }
}
