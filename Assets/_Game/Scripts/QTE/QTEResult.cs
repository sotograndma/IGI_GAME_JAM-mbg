namespace MBG.QTE
{
    /// <summary>Hasil satu sesi QTE, dikirim ke pemanggil lewat QTERequest.onComplete.</summary>
    public struct QTEResult
    {
        /// <summary>Nilai akhir sesi = nilai terburuk di antara semua input.</summary>
        public QTEGrade grade;

        /// <summary>Rata-rata ketepatan seluruh input, 0 (jauh) sampai 1 (tepat di tengah zona Perfect).</summary>
        public float accuracy;

        /// <summary>Berapa input yang mendapat Perfect.</summary>
        public int perfectCount;

        /// <summary>Berapa kali pemain benar-benar menekan tombol (timeout tidak dihitung).</summary>
        public int totalInputs;

        public QTEResult(QTEGrade grade, float accuracy, int perfectCount, int totalInputs)
        {
            this.grade = grade;
            this.accuracy = accuracy;
            this.perfectCount = perfectCount;
            this.totalInputs = totalInputs;
        }

        public bool IsSuccess => grade.IsSuccess();

        /// <summary>Hasil untuk sesi yang dibatalkan atau gagal sebelum sempat berjalan.</summary>
        public static QTEResult Failed() => new QTEResult(QTEGrade.CriticalMiss, 0f, 0, 0);

        public override string ToString()
            => $"{grade} (akurasi {accuracy:0.00}, perfect {perfectCount}/{totalInputs})";
    }
}
