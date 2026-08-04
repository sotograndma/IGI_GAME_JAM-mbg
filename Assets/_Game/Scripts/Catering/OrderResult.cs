using MBG.Data;

namespace MBG.Catering
{
    /// <summary>
    /// Hasil akhir satu pesanan. Dihitung oleh <c>ScoringConfigSO.CalculateResult</c>
    /// — rumusnya hanya ada di sana, supaya angka bisa ditune tanpa menyentuh kode.
    ///
    /// Struct ini murni data hasil hitungan: siapa pun yang memanggil
    /// CalculateResult dengan OrderRuntime yang sama akan mendapat angka yang sama.
    /// </summary>
    public struct OrderResult
    {
        public OrderSO source;

        /// <summary>False kalau pesanan gagal (deadline habis).</summary>
        public bool success;

        public FoodQuality quality;

        /// <summary>Rata-rata nilai QTE, 0..1.</summary>
        public float averageQuality;

        public int portionsDelivered;

        /// <summary>Bisa negatif: pesanan gagal dikenai denda.</summary>
        public int goldEarned;

        public int scoreEarned;

        /// <summary>Bagian dari <see cref="goldEarned"/> yang berasal dari sisa waktu.</summary>
        public int timeBonusGold;

        public float timeRemaining;

        /// <summary>Berapa kali batch diulang karena CriticalMiss.</summary>
        public int batchRestarts;

        /// <summary>Berapa QTE yang mendapat Perfect.</summary>
        public int perfectCount;

        public override string ToString()
            => $"{(success ? "berhasil" : "GAGAL")} — {quality} (rata-rata {averageQuality:0.00}), " +
               $"{portionsDelivered} porsi, {goldEarned:+#;-#;0} gold (bonus waktu {timeBonusGold}), " +
               $"+{scoreEarned} skor, sisa {timeRemaining:0.0}s";
    }
}
