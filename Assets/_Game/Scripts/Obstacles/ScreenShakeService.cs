using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Satu-satunya penulis offset guncangan kamera.
    ///
    /// Dipisah jadi service karena ada dua sumber guncangan yang bisa hidup
    /// bersamaan: getaran berkelanjutan dari gedoran pintu, dan hentakan sesaat
    /// saat ormas menerobos. Kalau keduanya menulis langsung ke CameraFollow2D,
    /// yang belakangan menimpa yang duluan dan guncangan bisa nyangkut di offset
    /// bukan nol.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenShakeService : MonoBehaviour
    {
        [Tooltip("Kamera yang digoyang. Diikat oleh Tools > MBG > Build Obstacle Setup.")]
        [SerializeField] CameraFollow2D cameraFollow;

        [SerializeField] float shakeSpeed = 26f;

        public static ScreenShakeService Instance { get; private set; }

        float _continuous;
        float _pulseAmplitude;
        float _pulseTimer;
        float _pulseDuration;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[ScreenShake] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            Apply(Vector2.zero);
            Instance = null;
        }

        /// <summary>Getaran yang menyala terus sampai dimatikan. 0 untuk mematikan.</summary>
        public static void SetContinuous(float amplitude)
        {
            if (Instance == null) return;
            Instance._continuous = Mathf.Max(0f, amplitude);
        }

        /// <summary>Hentakan sesaat yang memudar sendiri.</summary>
        public static void Pulse(float amplitude, float duration)
        {
            if (Instance == null || amplitude <= 0f || duration <= 0f) return;

            Instance._pulseAmplitude = amplitude;
            Instance._pulseDuration = duration;
            Instance._pulseTimer = duration;
        }

        public static void ClearAll()
        {
            if (Instance == null) return;

            Instance._continuous = 0f;
            Instance._pulseTimer = 0f;
            Instance.Apply(Vector2.zero);
        }

        void Update()
        {
            float pulse = 0f;

            if (_pulseTimer > 0f)
            {
                _pulseTimer -= Time.unscaledDeltaTime;
                float t = _pulseDuration > 0f ? Mathf.Clamp01(_pulseTimer / _pulseDuration) : 0f;
                pulse = _pulseAmplitude * t;
            }

            float amplitude = Mathf.Max(_continuous, pulse);

            if (amplitude <= 0.0001f)
            {
                Apply(Vector2.zero);
                return;
            }

            // Waktu unscaled: guncangan harus tetap terasa walau clock gameplay
            // sedang melambat karena gangguan itu sendiri.
            float time = Time.unscaledTime * shakeSpeed;
            Apply(new Vector2(Mathf.Sin(time) * amplitude, Mathf.Cos(time * 1.3f) * amplitude * 0.55f));
        }

        void Apply(Vector2 offset)
        {
            if (cameraFollow != null) cameraFollow.SetShakeOffset(offset);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
