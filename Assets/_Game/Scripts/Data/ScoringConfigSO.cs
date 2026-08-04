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

        // Angka reputasi TIDAK ada di sini — semuanya di ReputationConfigSO, supaya
        // tidak tersebar di dua asset.

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
        /// <param name="reputationGoldMultiplier">
        /// Pengali bayaran dari tingkat reputasi. Diteruskan pemanggil supaya fungsi
        /// ini tetap murni dan hasilnya bisa dihitung ulang siapa pun dengan angka
        /// yang sama.
        /// </param>
        public OrderResult CalculateResult(OrderRuntime order, bool success,
                                           float reputationGoldMultiplier = 1f)
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

            // Rasio dihitung dari deadline yang benar-benar dipakai pesanan ini,
            // termasuk pengali hari — bukan dari angka mentah di OrderSO.
            result.timeBonusGold = Mathf.RoundToInt(order.TimeRemaining01 * maxTimeBonus);

            int baseGold = order.source != null ? order.source.baseGoldReward : 0;
            int baseScore = order.source != null ? order.source.baseScoreReward : 0;

            float reputationScale = Mathf.Max(0f, reputationGoldMultiplier);
            result.goldEarned = Mathf.RoundToInt((baseGold * multiplier + result.timeBonusGold) * reputationScale);
            result.scoreEarned = Mathf.RoundToInt(baseScore * multiplier) + result.perfectCount * perfectBonus;

            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _fallback = null;
    }
}
