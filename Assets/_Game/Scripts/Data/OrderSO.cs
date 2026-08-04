using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Satu pesanan catering seperti tertulis di papan pesanan: resep apa, untuk
    /// siapa, berapa porsi, berapa lama, dan bayarannya.
    /// </summary>
    [CreateAssetMenu(fileName = "Order_", menuName = "MBG/Order")]
    public class OrderSO : ScriptableObject
    {
        public RecipeSO recipe;
        public RecipientSO recipient;

        [Min(1)]
        public int totalPortions = 100;

        [Tooltip("Batas waktu dasar, sebelum dikali patienceMultiplier pemesan.")]
        [Min(1f)]
        public float deadlineSeconds = 300f;

        [Tooltip("Bayaran sebelum dikali pengali kualitas.")]
        [Min(0)]
        public int baseGoldReward = 500;

        [Min(0)]
        public int baseScoreReward = 1000;

        /// <summary>Deadline sesungguhnya setelah memperhitungkan kesabaran pemesan.</summary>
        public float EffectiveDeadline
            => deadlineSeconds * (recipient != null ? Mathf.Max(0.1f, recipient.patienceMultiplier) : 1f);

        /// <summary>Berapa batch masak yang dibutuhkan pesanan ini.</summary>
        public int BatchCount => recipe != null ? recipe.GetBatchCount(totalPortions) : 0;

        public string RecipientName
            => recipient != null && !string.IsNullOrWhiteSpace(recipient.institutionName)
                ? recipient.institutionName
                : "Pemesan tanpa nama";

        public string RecipeName
            => recipe != null && !string.IsNullOrWhiteSpace(recipe.displayName)
                ? recipe.displayName
                : "Menu tanpa nama";

        /// <summary>Judul siap tampil, misal "120 porsi Nasi Kotak — SD Negeri 03 Sukamaju".</summary>
        public string DisplayTitle => $"{totalPortions} porsi {RecipeName} — {RecipientName}";

        public bool IsValid => recipe != null && recipe.StepCount > 0;
    }
}
