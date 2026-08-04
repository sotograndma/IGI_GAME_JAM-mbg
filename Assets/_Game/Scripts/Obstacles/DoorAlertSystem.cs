using MBG.Core;
using MBG.Data;
using TMPro;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Pintu sebagai sistem peringatan. Dipasang di Door_Interior.
    ///
    /// ATURAN KERAS: transform Door_Interior TIDAK PERNAH disentuh. Semua getaran
    /// terjadi pada child <see cref="shakeRoot"/>, posisi awalnya dicatat di Awake
    /// dan dikembalikan setiap kali peringatan berhenti — termasuk saat komponen
    /// dinonaktifkan di tengah gedoran.
    ///
    /// State: Idle (diam), Knock (ketukan sopan), Bang (gedoran keras + guncangan
    /// layar). Di atas ambang Urgent, getaran mempercepat dan warnanya memerah.
    /// </summary>
    [DisallowMultipleComponent]
    public class DoorAlertSystem : MonoBehaviour, IPromptOverride
    {
        [Header("Referensi (diisi Tools > MBG > Build Obstacle Setup)")]
        [Tooltip("Child yang digetarkan. JANGAN isi dengan transform pintu itu sendiri.")]
        [SerializeField] Transform shakeRoot;

        [SerializeField] SpriteRenderer frame;
        [SerializeField] TMP_Text iconLabel;
        [SerializeField] TMP_Text onomatopeLabel;

        [Header("Data")]
        [SerializeField] ObstacleConfigSO config;

        [Header("Kamera")]
        [Tooltip("Untuk guncangan layar saat gedoran. Boleh kosong.")]
        [SerializeField] CameraFollow2D cameraFollow;

        [Header("Prompt saat ada gangguan")]
        [SerializeField] string obstaclePrompt = "Keluar dan hadapi mereka";

        public DoorAlertState State { get; private set; } = DoorAlertState.Idle;

        public bool IsAlerting => State != DoorAlertState.Idle;

        ObstacleConfigSO Config => config != null ? config : ObstacleConfigSO.Fallback;

        ObstacleType _type;
        float _urgency;
        float _pulseTimer;
        bool _restCaptured;
        Vector3 _restPosition;

        void Awake()
        {
            CaptureRestPosition();
            ApplyIdle();
        }

        void OnDisable()
        {
            // Apa pun yang terjadi, child harus kembali ke tempatnya semula.
            RestoreRestPosition();
            SetCameraShake(Vector2.zero);
        }

        void CaptureRestPosition()
        {
            if (_restCaptured || shakeRoot == null) return;

            _restPosition = shakeRoot.localPosition;
            _restCaptured = true;
        }

        // ---- API dipanggil ObstacleManager --------------------------------------

        public void Show(ObstacleType type, DoorAlertState state)
        {
            CaptureRestPosition();

            _type = type;
            State = state;
            _urgency = 0f;
            _pulseTimer = 0f;

            if (state == DoorAlertState.Idle)
            {
                ApplyIdle();
                return;
            }

            SetActiveVisual(true);

            if (iconLabel != null) iconLabel.text = type.GetIcon();
            if (onomatopeLabel != null) onomatopeLabel.text = state.GetOnomatope();

            ApplyColor();
            PlayAlertSfx();
        }

        public void SetUrgency(float urgency) => _urgency = Mathf.Clamp01(urgency);

        public void Clear()
        {
            State = DoorAlertState.Idle;
            _urgency = 0f;
            ApplyIdle();
        }

        // ---- Frame ---------------------------------------------------------------

        void Update()
        {
            if (State == DoorAlertState.Idle) return;

            ObstacleConfigSO cfg = Config;
            bool urgent = _urgency >= cfg.urgentThreshold;

            float amplitude = cfg.GetShakeAmplitude(State);
            float speed = cfg.GetShakeSpeed(State) * (urgent ? cfg.urgentSpeedMultiplier : 1f);

            // Getaran memakai waktu unscaled: peringatan harus tetap terasa hidup
            // walau clock gameplay sedang melambat karena gangguan ini sendiri.
            float t = Time.unscaledTime * speed;
            var offset = new Vector3(Mathf.Sin(t) * amplitude, Mathf.Cos(t * 1.7f) * amplitude * 0.6f, 0f);

            if (shakeRoot != null) shakeRoot.localPosition = _restPosition + offset;

            ApplyColor();
            TickPulse(cfg);

            if (State == DoorAlertState.Bang)
            {
                float shake = cfg.screenShakeAmplitude * (urgent ? 1.6f : 1f);
                float st = Time.unscaledTime * cfg.screenShakeSpeed;
                SetCameraShake(new Vector2(Mathf.Sin(st) * shake, Mathf.Cos(st * 1.3f) * shake * 0.5f));
            }
        }

        /// <summary>Bunyi dan kedip onomatope pada interval tetap.</summary>
        void TickPulse(ObstacleConfigSO cfg)
        {
            float interval = cfg.GetInterval(State);
            bool urgent = _urgency >= cfg.urgentThreshold;
            if (urgent) interval *= 0.6f;

            _pulseTimer += Time.unscaledDeltaTime;
            if (_pulseTimer < interval) return;

            _pulseTimer = 0f;
            PlayAlertSfx();
        }

        void PlayAlertSfx()
            => AudioService.PlaySFX(State == DoorAlertState.Bang ? SfxId.DoorBang : SfxId.DoorKnock);

        void ApplyColor()
        {
            ObstacleConfigSO cfg = Config;
            bool urgent = _urgency >= cfg.urgentThreshold;

            // Makin dekat konsekuensi, makin merah.
            Color baseColor = cfg.GetAlertColor(State);
            Color color = Color.Lerp(baseColor, cfg.urgentColor, urgent ? 1f : _urgency / Mathf.Max(0.01f, cfg.urgentThreshold));

            if (frame != null) frame.color = new Color(color.r, color.g, color.b, 0.55f + _urgency * 0.35f);
            if (iconLabel != null) iconLabel.color = color;
            if (onomatopeLabel != null) onomatopeLabel.color = color;
        }

        void ApplyIdle()
        {
            RestoreRestPosition();
            SetCameraShake(Vector2.zero);
            SetActiveVisual(false);
        }

        void RestoreRestPosition()
        {
            if (shakeRoot != null && _restCaptured) shakeRoot.localPosition = _restPosition;
        }

        void SetActiveVisual(bool visible)
        {
            if (frame != null) frame.enabled = visible;
            if (iconLabel != null) iconLabel.enabled = visible;
            if (onomatopeLabel != null) onomatopeLabel.enabled = visible;
        }

        void SetCameraShake(Vector2 offset)
        {
            if (cameraFollow == null) return;
            cameraFollow.SetShakeOffset(offset);
        }

        // ---- IPromptOverride -------------------------------------------------------

        /// <summary>Selama ada gedoran, pintu menawarkan hal lain daripada sekadar keluar.</summary>
        public bool TryGetPromptOverride(out string text, out Color tint)
        {
            if (!IsAlerting)
            {
                text = null;
                tint = Color.white;
                return false;
            }

            text = obstaclePrompt;
            tint = Config.urgentColor;
            return true;
        }
    }
}
