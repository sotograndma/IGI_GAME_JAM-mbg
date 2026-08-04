using System.Collections.Generic;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Semua angka gangguan Ormas: seberapa cepat mereka menerobos, seberapa berat
    /// kerugiannya, dan seberapa alot mereka didorong saat dihadapi.
    ///
    /// Perubahan dari GDD yang sudah disepakati: penerobosan BUKAN game over instan,
    /// melainkan Intrusion Meter yang menghancurkan satu batch masakan.
    /// </summary>
    [CreateAssetMenu(fileName = "OrmasConfig", menuName = "MBG/Ormas Config")]
    public class OrmasConfigSO : ScriptableObject
    {
        [Header("Intrusion Meter")]
        [Tooltip("Detik dari gedoran pertama sampai ormas menerobos, pada difficulty 0.5.")]
        [Min(1f)]
        public float intrusionDuration = 22f;

        [Tooltip("Difficulty 0 mengali durasi dengan angka ini (lebih longgar).")]
        public float difficultyEasyScale = 1.35f;

        [Tooltip("Difficulty 1 mengali durasi dengan angka ini (lebih mendesak).")]
        public float difficultyHardScale = 0.65f;

        [Header("Konsekuensi kalau mereka menerobos")]
        [Tooltip("Berapa batch masakan yang hancur.")]
        [Min(0)]
        public int batchesDestroyed = 1;

        [Min(0)] public int intrusionGoldPenalty = 250;

        [Tooltip("Berapa lama sprite ormas terlihat di dalam dapur sebelum pergi.")]
        [Min(0.1f)]
        public float intruderVisitDuration = 2.5f;

        [Header("Guncangan layar saat menerobos")]
        public float intrusionShakeAmplitude = 0.28f;
        public float intrusionShakeDuration = 0.9f;

        [Header("Encounter di luar rumah")]
        [Min(1)] public int minMemberCount = 3;
        [Min(1)] public int maxMemberCount = 5;

        [Tooltip("Jarak antar anggota ormas, dalam unit dunia.")]
        public float memberSpacing = 0.9f;

        public float memberWidth = 0.8f;
        public float memberHeight = 1.7f;
        public Color memberColor = new Color(0.35f, 0.32f, 0.38f, 1f);

        [Tooltip("Ukuran zona interaksi grup, dalam unit dunia.")]
        public Vector2 encounterTriggerSize = new Vector2(5f, 3f);

        public string encounterPrompt = "Hadapi mereka";

        [Header("Mini-game tarik-menarik")]
        [Tooltip("Seberapa jauh bar terdorong ke kanan tiap tekan Spasi (0..1).")]
        [Range(0.005f, 0.2f)]
        public float pushPerPress = 0.045f;

        [Tooltip("Kecepatan ormas mendorong ke kiri per detik, pada difficulty 0.5.")]
        [Min(0.01f)]
        public float opponentPushSpeed = 0.16f;

        [Tooltip("Difficulty 0 mengali kecepatan dorong dengan angka ini.")]
        public float opponentEasyScale = 0.75f;

        [Tooltip("Difficulty 1 mengali kecepatan dorong dengan angka ini.")]
        public float opponentHardScale = 1.35f;

        [Min(1f)] public float encounterTimeLimit = 15f;

        [Range(0f, 1f)] public float barStartPosition = 0.5f;

        [Tooltip("Jeda ganti kalimat provokasi, dalam detik.")]
        [Min(0.5f)]
        public float tauntInterval = 2.2f;

        [Header("Hasil encounter")]
        [Min(0)] public int loseGoldPenalty = 200;

        [Header("Provokasi ormas (bahasa Indonesia)")]
        public List<string> taunts = new();

        public float GetIntrusionDuration(float difficulty)
        {
            float scale = Mathf.Lerp(difficultyEasyScale, difficultyHardScale, Mathf.Clamp01(difficulty));
            return Mathf.Max(1f, intrusionDuration * scale);
        }

        public float GetOpponentSpeed(float difficulty)
        {
            float scale = Mathf.Lerp(opponentEasyScale, opponentHardScale, Mathf.Clamp01(difficulty));
            return Mathf.Max(0.01f, opponentPushSpeed * scale);
        }

        public int GetMemberCount()
            => Random.Range(Mathf.Min(minMemberCount, maxMemberCount), Mathf.Max(minMemberCount, maxMemberCount) + 1);

        static OrmasConfigSO _fallback;

        /// <summary>Dipakai kalau asset-nya belum diikat, supaya sistem tetap jalan.</summary>
        public static OrmasConfigSO Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<OrmasConfigSO>();
                    _fallback.name = "OrmasConfig (bawaan)";
                    _fallback.ApplyDefaultTaunts();
                }
                return _fallback;
            }
        }

        /// <summary>Kalimat provokasi bawaan — satir dan absurd, sesuai GDD.</summary>
        public void ApplyDefaultTaunts()
        {
            taunts = new List<string>
            {
                "Ini wilayah kami!",
                "Mana uang keamanannya?",
                "Catering kok enak, mencurigakan!",
                "Sudah izin RT belum, Bu?",
                "Dapur ramai begini, pasti untungnya besar!",
                "Kami cuma minta jatah, bukan maling!",
                "Bau bumbunya sampai pos ronda, itu pelanggaran!",
                "Kami ini pengaman, bukan pengganggu!"
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _fallback = null;
    }
}
