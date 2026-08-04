using MBG.Catering;
using MBG.QTE;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Angka yang menerjemahkan permainan menjadi nilai: berapa "bagus" tiap grade
    /// QTE, ambang batas tiap tingkat kualitas makanan, dan pengali bayaran.
    ///
    /// Dipisah dari OrderSO karena ini aturan global, bukan properti satu pesanan.
    /// </summary>
    [CreateAssetMenu(fileName = "CateringBalance", menuName = "MBG/Catering Balance")]
    public class CateringBalanceSO : ScriptableObject
    {
        [Header("Nilai tiap grade QTE (0..1)")]
        [Range(0f, 1f)] public float perfectValue = 1f;
        [Range(0f, 1f)] public float goodValue = 0.7f;
        [Range(0f, 1f)] public float missValue = 0.35f;
        [Range(0f, 1f)] public float criticalMissValue = 0f;

        [Header("Ambang tingkat kualitas (rata-rata nilai grade)")]
        [Range(0f, 1f)] public float perfectThreshold = 0.9f;
        [Range(0f, 1f)] public float goodThreshold = 0.7f;
        [Tooltip("Di bawah ambang ini makanan dianggap gagal.")]
        [Range(0f, 1f)] public float badThreshold = 0.4f;

        // Pengali bayaran TIDAK ada di sini — semua rumus gold & skor tinggal di
        // ScoringConfigSO, supaya tidak ada dua sumber kebenaran.

        static CateringBalanceSO _fallback;

        /// <summary>
        /// Dipakai kalau CateringController belum diberi asset balance, supaya
        /// permainan tetap jalan dengan angka bawaan alih-alih membagi nol.
        /// </summary>
        public static CateringBalanceSO Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<CateringBalanceSO>();
                    _fallback.name = "CateringBalance (bawaan)";
                }
                return _fallback;
            }
        }

        public float GetGradeValue(QTEGrade grade)
        {
            switch (grade)
            {
                case QTEGrade.Perfect: return perfectValue;
                case QTEGrade.Good: return goodValue;
                case QTEGrade.Miss: return missValue;
                default: return criticalMissValue;
            }
        }

        public FoodQuality GetQualityTier(float averageQuality)
        {
            if (averageQuality >= perfectThreshold) return FoodQuality.Perfect;
            if (averageQuality >= goodThreshold) return FoodQuality.Good;
            if (averageQuality >= badThreshold) return FoodQuality.Bad;
            return FoodQuality.Failed;
        }

        void OnValidate()
        {
            // Ambang harus tetap berurutan, kalau tidak GetQualityTier jadi omong kosong.
            goodThreshold = Mathf.Min(goodThreshold, perfectThreshold);
            badThreshold = Mathf.Min(badThreshold, goodThreshold);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _fallback = null;
    }
}
