using MBG.QTE;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Semua angka gangguan Santet.
    ///
    /// Santet tidak datang lewat pintu: ia menyerang pemain langsung di dapur.
    /// Kegagalannya menyakitkan, tapi TIDAK pernah langsung mengakhiri permainan —
    /// game over hanya lewat reputasi 0.
    /// </summary>
    [CreateAssetMenu(fileName = "SantetConfig", menuName = "MBG/Santet Config")]
    public class SantetConfigSO : ScriptableObject
    {
        [Header("Fase 1 — serangan di dapur")]
        [Tooltip("Pola ritme untuk difficulty rendah.")]
        public RhythmPatternSO patternEasy;

        public RhythmPatternSO patternNormal;
        public RhythmPatternSO patternHard;

        [Tooltip("Akurasi minimum agar serangan berhasil ditahan.")]
        [Range(0f, 1f)]
        public float accuracyThreshold = 0.6f;

        [Header("Jendela penilaian (selisih waktu dalam detik)")]
        [Min(0.01f)] public float perfectWindow = 0.09f;
        [Min(0.02f)] public float goodWindow = 0.2f;

        [Tooltip("Selewat ini tanpa ditekan, not dihitung meleset.")]
        [Min(0.05f)] public float missWindow = 0.28f;

        [Header("Nilai akurasi tiap penilaian")]
        [Range(0f, 1f)] public float perfectValue = 1f;
        [Range(0f, 1f)] public float goodValue = 0.7f;
        [Range(0f, 1f)] public float missValue = 0f;

        [Header("Efek layar")]
        [Tooltip("Warna overlay yang menutupi layar saat santet menyerang.")]
        public Color overlayColor = new Color(0.62f, 0.05f, 0.08f, 0.42f);

        [Tooltip("Warna vignette di tepi layar.")]
        public Color vignetteColor = new Color(0.18f, 0f, 0.02f, 0.9f);

        [Tooltip("Lapisan kelabu untuk meniru saturasi yang turun. Tanpa post-processing, " +
                 "ini pendekatan terdekat yang bisa dilakukan UI Image biasa.")]
        public Color desaturateColor = new Color(0.35f, 0.33f, 0.36f, 0.3f);

        [Min(0.05f)] public float overlayFadeDuration = 0.45f;

        [Tooltip("Kecepatan denyut overlay.")]
        public float overlayPulseSpeed = 3.2f;

        [Tooltip("Seberapa dalam denyutnya, sebagai fraksi alpha.")]
        [Range(0f, 1f)] public float overlayPulseAmount = 0.22f;

        [Header("Audio")]
        [Tooltip("Musik dipelankan ke fraksi ini selama serangan. Stub AudioService " +
                 "belum benar-benar memutar musik, jadi ini baru dicatat di log.")]
        [Range(0f, 1f)]
        public float musicDuckVolume = 0.25f;

        [Header("Kalau ritme gagal")]
        [Min(0)] public int failGoldPenalty = 150;
        [Min(0)] public int failReputationPenalty = 1;
        [Min(0)] public int failBatchesLost = 1;

        [Header("Fase 2 — cari dukun")]
        public string objectiveText = "Cari dukun di luar";
        public string dukunPrompt = "Hentikan santetnya";

        [Tooltip("Pengali waktu selama fase 2 berlangsung; pemain sudah bebas bergerak.")]
        [Range(0.1f, 1f)]
        public float phase2TimeMultiplier = 0.5f;

        public float dukunWidth = 0.9f;
        public float dukunHeight = 1.8f;
        public Color dukunColor = new Color(0.42f, 0.18f, 0.52f, 1f);
        public Vector2 dukunTriggerSize = new Vector2(3.4f, 3f);

        [Tooltip("QTE penutup saat menghadapi dukun. Diikat ke QTE_Hard (3 hit).")]
        public QTEConfigSO confrontationConfig;

        public string confrontationLabel = "Patahkan santetnya!";

        [Header("Hasil")]
        [Min(0)] public int winReputationGain = 1;
        [Min(0)] public int confrontationFailGoldPenalty = 100;

        /// <summary>Pola sesuai tingkat kesulitan jadwal hari.</summary>
        public RhythmPatternSO GetPattern(float difficulty)
        {
            if (difficulty >= 0.7f && patternHard != null) return patternHard;
            if (difficulty >= 0.35f && patternNormal != null) return patternNormal;

            return patternEasy != null ? patternEasy : (patternNormal != null ? patternNormal : patternHard);
        }

        static SantetConfigSO _fallback;

        /// <summary>Dipakai kalau asset-nya belum diikat, supaya sistem tetap jalan.</summary>
        public static SantetConfigSO Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<SantetConfigSO>();
                    _fallback.name = "SantetConfig (bawaan)";
                }
                return _fallback;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _fallback = null;
    }
}
