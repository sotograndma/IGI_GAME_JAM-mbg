using MBG.Catering;
using MBG.Obstacles;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>Tingkat reputasi usaha catering.</summary>
    public enum ReputationTier
    {
        Buruk = 0,
        Biasa = 1,
        Baik = 2,
        Dicintai = 3
    }

    /// <summary>
    /// Satu-satunya tempat angka reputasi hidup: dari mana ia naik-turun, di mana
    /// batas tiap tingkat, dan apa untungnya berada di tingkat atas.
    ///
    /// Config gangguan hanya mengurus gold; potongan reputasinya semua ada di sini
    /// supaya tidak tersebar di empat asset berbeda.
    /// </summary>
    [CreateAssetMenu(fileName = "ReputationConfig", menuName = "MBG/Reputation Config")]
    public class ReputationConfigSO : ScriptableObject
    {
        [Header("Rentang")]
        [Min(0)] public int minReputation = 0;
        [Min(1)] public int maxReputation = 100;
        [Min(0)] public int startingReputation = 50;

        [Header("Ambang tingkat (batas atas tiap tingkat)")]
        public int burukMax = 25;
        public int biasaMax = 50;
        public int baikMax = 75;

        [Header("Pengali gold per tingkat")]
        public float burukGoldMultiplier = 0.7f;
        public float biasaGoldMultiplier = 1f;
        public float baikGoldMultiplier = 1.2f;
        public float dicintaiGoldMultiplier = 1.5f;

        [Header("Bonus tingkat Dicintai")]
        [Tooltip("Pesanan tambahan per hari saat reputasi berada di tingkat Dicintai.")]
        [Min(0)]
        public int lovedExtraOrdersPerDay = 1;

        [Header("Perubahan dari hasil pesanan")]
        public int orderPerfect = 8;
        public int orderGood = 4;
        public int orderBad = -3;
        public int orderFailed = -12;

        [Header("Perubahan dari gangguan")]
        [Tooltip("Gangguan berhasil diatasi.")]
        public int obstacleResolved = 3;

        [Header("Gangguan diabaikan sampai konsekuensinya jatuh")]
        public int ormasIgnored = -15;
        public int santetIgnored = -10;
        public int illegalTaxIgnored = -8;

        public ReputationTier GetTier(int reputation)
        {
            if (reputation <= burukMax) return ReputationTier.Buruk;
            if (reputation <= biasaMax) return ReputationTier.Biasa;
            if (reputation <= baikMax) return ReputationTier.Baik;
            return ReputationTier.Dicintai;
        }

        public float GetGoldMultiplier(ReputationTier tier)
        {
            switch (tier)
            {
                case ReputationTier.Buruk: return burukGoldMultiplier;
                case ReputationTier.Biasa: return biasaGoldMultiplier;
                case ReputationTier.Baik: return baikGoldMultiplier;
                default: return dicintaiGoldMultiplier;
            }
        }

        /// <summary>Perubahan reputasi untuk satu hasil pesanan.</summary>
        public int GetOrderDelta(FoodQuality quality)
        {
            switch (quality)
            {
                case FoodQuality.Perfect: return orderPerfect;
                case FoodQuality.Good: return orderGood;
                case FoodQuality.Bad: return orderBad;
                default: return orderFailed;
            }
        }

        /// <summary>Potongan reputasi saat gangguan dibiarkan sampai konsekuensinya jatuh.</summary>
        public int GetIgnoredPenalty(ObstacleType type)
        {
            switch (type)
            {
                case ObstacleType.Ormas: return ormasIgnored;
                case ObstacleType.Santet: return santetIgnored;
                default: return illegalTaxIgnored;
            }
        }

        void OnValidate()
        {
            biasaMax = Mathf.Max(biasaMax, burukMax);
            baikMax = Mathf.Max(baikMax, biasaMax);
            maxReputation = Mathf.Max(maxReputation, baikMax);
            startingReputation = Mathf.Clamp(startingReputation, minReputation, maxReputation);
        }

        static ReputationConfigSO _fallback;

        public static ReputationConfigSO Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<ReputationConfigSO>();
                    _fallback.name = "ReputationConfig (bawaan)";
                }
                return _fallback;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _fallback = null;
    }

    public static class ReputationTierExtensions
    {
        /// <summary>Ikon placeholder ASCII — font TMP bawaan hanya memuat ASCII.</summary>
        public static string GetIcon(this ReputationTier tier)
        {
            switch (tier)
            {
                case ReputationTier.Buruk: return "x";
                case ReputationTier.Biasa: return "-";
                case ReputationTier.Baik: return "+";
                default: return "*";
            }
        }

        public static string GetLabel(this ReputationTier tier) => tier.ToString().ToUpperInvariant();
    }
}
