using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Semua angka gangguan Pajak Ilegal.
    ///
    /// Berbeda dari Ormas, gangguan ini sabar: ketukannya sopan dan urgency-nya
    /// mengisi jauh lebih lambat. Yang mahal bukan kecepatannya, melainkan
    /// ongkosnya kalau diabaikan.
    /// </summary>
    [CreateAssetMenu(fileName = "IllegalTaxConfig", menuName = "MBG/Illegal Tax Config")]
    public class IllegalTaxConfigSO : ScriptableObject
    {
        [Header("Naskah & teks")]
        public TaxDialogueSO dialogue;
        public TypingChallengeSO challenges;

        [Header("Urgency (lebih lambat daripada Ormas)")]
        [Tooltip("Detik sampai usaha disegel, pada difficulty 0.5.")]
        [Min(1f)]
        public float sealDuration = 40f;

        public float difficultyEasyScale = 1.3f;
        public float difficultyHardScale = 0.75f;

        [Header("Kalau diabaikan sampai disegel")]
        [Min(0)] public int sealGoldPenalty = 400;
        [Min(0)] public int sealReputationPenalty = 1;

        [Header("Tantangan mengetik")]
        [Min(5f)] public float typingTimeLimit = 45f;

        [Range(0.5f, 1f)] public float perfectAccuracy = 0.95f;
        [Range(0.3f, 1f)] public float goodAccuracy = 0.8f;

        [Header("Hasil")]
        [Min(0)] public int winReputationGain = 1;

        [Tooltip("Dibayar saat ketikan selesai tapi berantakan.")]
        [Min(0)] public int partialGoldPenalty = 200;

        [Tooltip("Dibayar penuh saat tantangan tidak selesai.")]
        [Min(0)] public int fullGoldPenalty = 450;

        [Min(0)] public int failReputationPenalty = 1;

        [Header("NPC petugas")]
        public string npcPrompt = "Layani petugas";
        public float npcWidth = 0.85f;
        public float npcHeight = 1.75f;
        public Color npcColor = new Color(0.30f, 0.45f, 0.62f, 1f);
        public Vector2 npcTriggerSize = new Vector2(3.4f, 3f);

        [Header("Waktu")]
        [Tooltip("Pengali waktu selama pemain berurusan dengan petugas di luar.")]
        [Range(0.1f, 1f)]
        public float outsideTimeMultiplier = 0.5f;

        public float GetSealDuration(float difficulty)
        {
            float scale = Mathf.Lerp(difficultyEasyScale, difficultyHardScale, Mathf.Clamp01(difficulty));
            return Mathf.Max(1f, sealDuration * scale);
        }

        static IllegalTaxConfigSO _fallback;

        /// <summary>Dipakai kalau asset-nya belum diikat, supaya sistem tetap jalan.</summary>
        public static IllegalTaxConfigSO Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<IllegalTaxConfigSO>();
                    _fallback.name = "IllegalTaxConfig (bawaan)";
                }
                return _fallback;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _fallback = null;
    }
}
