using MBG.Catering;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Rumus bayaran dan skor. Satu-satunya tempat angka ini boleh hidup —
    /// EconomyService, CateringController, dan ResultPanel semuanya memanggil
    /// <see cref="CalculateResult"/>, tidak ada yang menghitung sendiri.
    ///
    /// gold  = baseGoldReward  * qualityMultiplier + timeBonus
    /// score = baseScoreReward * qualityMultiplier + (jumlah Perfect * perfectBonus)
    /// timeBonus = (sisa waktu / total waktu) * maxTimeBonus
    ///
    /// Pesanan gagal: gold = -failPenaltyGold, score = 0.
    /// </summary>
    [CreateAssetMenu(fileName = "ScoringConfig", menuName = "MBG/Scoring Config")]
    public class ScoringConfigSO : ScriptableObject
    {
        [Header("Pengali kualitas")]
        public float perfectMultiplier = 1.5f;
        public float goodMultiplier = 1f;
        public float badMultiplier = 0.6f;
        public float failedMultiplier = 0.2f;

        [Header("Bonus")]
        [Tooltip("Gold maksimum dari sisa waktu, didapat kalau pesanan selesai tanpa waktu berkurang.")]
        [Min(0)]
        public int maxTimeBonus = 200;

        [Tooltip("Skor tambahan untuk tiap QTE bernilai Perfect.")]
        [Min(0)]
        public int perfectBonus = 50;

        [Header("Hukuman")]
        [Tooltip("Gold yang hilang saat pesanan gagal karena deadline habis.")]
        [Min(0)]
        public int failPenaltyGold = 150;

        [Header("Ekonomi")]
        [Tooltip("Gold awal pemain saat permainan dimulai.")]
        [Min(0)]
        public int startingGold = 500;

        static ScoringConfigSO _fallback;

        /// <summary>Dipakai kalau asset-nya belum diikat, supaya permainan tetap jalan.</summary>
        public static ScoringConfigSO Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<ScoringConfigSO>();
                    _fallback.name = "ScoringConfig (bawaan)";
                }
                return _fallback;
            }
        }

        public float GetQualityMultiplier(FoodQuality quality)
        {
            switch (quality)
            {
                case FoodQuality.Perfect: return perfectMultiplier;
                case FoodQuality.Good: return goodMultiplier;
                case FoodQuality.Bad: return badMultiplier;
                default: return failedMultiplier;
            }
        }

        /// <summary>
        /// Hitung hasil satu pesanan. Fungsi murni — tidak mengubah apa pun, jadi
        /// aman dipanggil beberapa sistem sekaligus dengan hasil identik.
        /// </summary>
        public OrderResult CalculateResult(OrderRuntime order, bool success)
        {
            var result = new OrderResult();
            if (order == null) return result;

            result.source = order.source;
            result.success = success;
            result.averageQuality = order.AverageQuality;
            result.portionsDelivered = order.portionsCompleted;
            result.timeRemaining = order.timeRemaining;
            result.batchRestarts = order.batchRestarts;
            result.perfectCount = order.PerfectCount;

            // Pesanan yang tidak sempat diserahkan selalu dihitung gagal, berapa pun
            // rata-rata QTE-nya.
            result.quality = success ? order.QualityTier : FoodQuality.Failed;

            if (!success)
            {
                result.goldEarned = -Mathf.Abs(failPenaltyGold);
                result.scoreEarned = 0;
                result.timeBonusGold = 0;
                return result;
            }

            float multiplier = GetQualityMultiplier(result.quality);

            float totalTime = order.source != null ? order.source.EffectiveDeadline : 0f;
            float ratio = totalTime > 0f ? Mathf.Clamp01(order.timeRemaining / totalTime) : 0f;
            result.timeBonusGold = Mathf.RoundToInt(ratio * maxTimeBonus);

            int baseGold = order.source != null ? order.source.baseGoldReward : 0;
            int baseScore = order.source != null ? order.source.baseScoreReward : 0;

            result.goldEarned = Mathf.RoundToInt(baseGold * multiplier) + result.timeBonusGold;
            result.scoreEarned = Mathf.RoundToInt(baseScore * multiplier) + result.perfectCount * perfectBonus;

            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _fallback = null;
    }
}
