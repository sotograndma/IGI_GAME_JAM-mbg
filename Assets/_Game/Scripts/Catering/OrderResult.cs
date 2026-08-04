using MBG.Data;

namespace MBG.Catering
{
    /// <summary>
    /// Hasil akhir satu pesanan, dikirim lewat <c>GameEventBus.OnOrderCompleted</c>.
    /// Sistem ekonomi dan skor membaca ini — bukan OrderRuntime, yang state-nya
    /// masih bisa berubah.
    /// </summary>
    public struct OrderResult
    {
        public OrderSO source;
        public FoodQuality quality;

        /// <summary>Rata-rata nilai QTE, 0..1.</summary>
        public float averageQuality;

        public int portionsDelivered;
        public int goldEarned;
        public int scoreEarned;

        /// <summary>Sisa waktu saat diserahkan — bahan untuk bonus kecepatan nanti.</summary>
        public float timeRemaining;

        /// <summary>Berapa kali batch harus diulang karena CriticalMiss.</summary>
        public int batchRestarts;

        public OrderResult(OrderRuntime order, int goldEarned, int scoreEarned)
        {
            source = order != null ? order.source : null;
            quality = order != null ? order.QualityTier : FoodQuality.Failed;
            averageQuality = order != null ? order.AverageQuality : 0f;
            portionsDelivered = order != null ? order.portionsCompleted : 0;
            timeRemaining = order != null ? order.timeRemaining : 0f;
            batchRestarts = order != null ? order.batchRestarts : 0;

            this.goldEarned = goldEarned;
            this.scoreEarned = scoreEarned;
        }

        public override string ToString()
            => $"{quality} (rata-rata {averageQuality:0.00}), {portionsDelivered} porsi, " +
               $"+{goldEarned} gold, +{scoreEarned} skor, sisa {timeRemaining:0.0}s";
    }
}
