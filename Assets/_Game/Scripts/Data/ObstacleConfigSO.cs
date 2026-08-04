using MBG.Obstacles;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Semua angka gangguan: seberapa lambat waktu berjalan selama gangguan,
    /// berapa lama tiap gangguan bertahan sebelum konsekuensinya jatuh, dan
    /// seberapa keras pintu bergetar.
    /// </summary>
    [CreateAssetMenu(fileName = "ObstacleConfig", menuName = "MBG/Obstacle Config")]
    public class ObstacleConfigSO : ScriptableObject
    {
        [Header("Waktu")]
        [Tooltip("Pengali GameClock selama gangguan aktif. 0.5 = timer pesanan berjalan setengah kecepatan.")]
        [Range(0.1f, 1f)]
        public float timeMultiplierDuringObstacle = 0.5f;

        [Header("Durasi sampai konsekuensi (detik, pada difficulty 0.5)")]
        [Min(1f)] public float ormasDuration = 20f;
        [Min(1f)] public float santetDuration = 15f;
        [Min(1f)] public float illegalTaxDuration = 25f;

        [Tooltip("Difficulty 0 mengali durasi dengan angka ini (lebih longgar).")]
        public float difficultyEasyScale = 1.3f;

        [Tooltip("Difficulty 1 mengali durasi dengan angka ini (lebih mendesak).")]
        public float difficultyHardScale = 0.7f;

        [Header("Urgency")]
        [Tooltip("Di atas ambang ini, peringatan pintu masuk mode Urgent.")]
        [Range(0f, 1f)]
        public float urgentThreshold = 0.75f;

        [Header("Getaran pintu (satuan lokal)")]
        public float knockShakeAmplitude = 0.03f;
        public float knockShakeSpeed = 16f;
        public float bangShakeAmplitude = 0.09f;
        public float bangShakeSpeed = 30f;

        [Tooltip("Pengali kecepatan getar saat urgency melewati ambang Urgent.")]
        public float urgentSpeedMultiplier = 1.8f;

        [Header("Jeda bunyi & kedip onomatope (detik)")]
        [Min(0.1f)] public float knockInterval = 1.5f;
        [Min(0.1f)] public float bangInterval = 0.8f;

        [Header("Guncangan layar (hanya untuk gedoran)")]
        public float screenShakeAmplitude = 0.06f;
        public float screenShakeSpeed = 26f;

        [Header("Warna peringatan")]
        public Color knockColor = new Color(0.95f, 0.82f, 0.35f, 1f);
        public Color bangColor = new Color(0.92f, 0.34f, 0.28f, 1f);
        public Color urgentColor = new Color(1f, 0.15f, 0.12f, 1f);

        /// <summary>Durasi dasar satu gangguan sebelum memperhitungkan difficulty.</summary>
        public float GetBaseDuration(ObstacleType type)
        {
            switch (type)
            {
                case ObstacleType.Ormas: return ormasDuration;
                case ObstacleType.Santet: return santetDuration;
                case ObstacleType.IllegalTax: return illegalTaxDuration;
                default: return 20f;
            }
        }

        /// <summary>Durasi setelah difficulty diperhitungkan.</summary>
        public float GetDuration(ObstacleType type, float difficulty)
        {
            float scale = Mathf.Lerp(difficultyEasyScale, difficultyHardScale, Mathf.Clamp01(difficulty));
            return Mathf.Max(1f, GetBaseDuration(type) * scale);
        }

        public float GetShakeAmplitude(DoorAlertState state)
            => state == DoorAlertState.Bang ? bangShakeAmplitude : knockShakeAmplitude;

        public float GetShakeSpeed(DoorAlertState state)
            => state == DoorAlertState.Bang ? bangShakeSpeed : knockShakeSpeed;

        public float GetInterval(DoorAlertState state)
            => state == DoorAlertState.Bang ? bangInterval : knockInterval;

        public Color GetAlertColor(DoorAlertState state)
            => state == DoorAlertState.Bang ? bangColor : knockColor;

        static ObstacleConfigSO _fallback;

        /// <summary>Dipakai kalau asset-nya belum diikat, supaya sistem tetap jalan.</summary>
        public static ObstacleConfigSO Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<ObstacleConfigSO>();
                    _fallback.name = "ObstacleConfig (bawaan)";
                }
                return _fallback;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _fallback = null;
    }
}
