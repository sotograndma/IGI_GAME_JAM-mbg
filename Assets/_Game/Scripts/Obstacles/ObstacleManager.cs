using System.Collections.Generic;
using MBG.Core;
using MBG.Data;
using MBG.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MBG.Obstacles
{
    /// <summary>
    /// Memicu gangguan sesuai jadwal hari, dan menjaga agar hanya satu gangguan
    /// berjalan dalam satu waktu.
    ///
    /// Selama gangguan aktif:
    ///   - GameClock dikali <c>timeMultiplierDuringObstacle</c> (default 0.5), jadi
    ///     timer pesanan melambat tapi tidak berhenti;
    ///   - GameState menjadi InObstacle, sehingga CateringController menolak memicu
    ///     QTE masak (station menjawab "Belum saatnya");
    ///   - pintu menyalakan peringatannya lewat <see cref="DoorAlertSystem"/>.
    ///
    /// Jadwal dan gangguan aktif di-tick dengan <c>GameClock.RawDeltaTime</c>, bukan
    /// DeltaTime: sistem inilah yang memperlambat clock, jadi kalau ia ikut membaca
    /// waktu yang sudah dikali, durasinya sendiri akan molor dua kali lipat.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObstacleManager : MonoBehaviour
    {
        struct ScheduledObstacle
        {
            public ObstacleType type;
            public float triggerAt;
            public float difficulty;
        }

        [Header("Data")]
        [Tooltip("Angka gangguan umum. Diikat oleh Tools > MBG > Build Obstacle Setup.")]
        [SerializeField] ObstacleConfigSO config;

        [Tooltip("Angka khusus Ormas. Diikat oleh Tools > MBG > Build Exterior Encounters.")]
        [SerializeField] OrmasConfigSO ormasConfig;

        [Header("Referensi")]
        [Tooltip("Peringatan di Door_Interior. Diikat oleh Tools > MBG > Build Obstacle Setup.")]
        [SerializeField] DoorAlertSystem doorAlert;

        [Header("Debug")]
        [Tooltip("F5 Ormas, F6 Santet, F7 Pajak Ilegal, F8 menyelesaikan gangguan aktif.")]
        [SerializeField] bool enableDebugKeys = true;

        [Range(0f, 1f)]
        [SerializeField] float debugDifficulty = 0.5f;

        public static ObstacleManager Instance { get; private set; }

        /// <summary>Gangguan yang sedang berjalan, atau null.</summary>
        public IObstacle Active { get; private set; }

        public static bool HasActiveObstacle => Instance != null && Instance.Active != null;

        /// <summary>0..1 — dibaca HUD untuk mengisi bar urgency.</summary>
        public static float ActiveUrgency => HasActiveObstacle ? Instance.Active.UrgencyNormalized : 0f;

        public ObstacleConfigSO Config => config != null ? config : ObstacleConfigSO.Fallback;

        readonly Dictionary<ObstacleType, IObstacle> _modules = new();
        readonly List<ScheduledObstacle> _pending = new();
        readonly Queue<ScheduledObstacle> _deferred = new();

        float _dayTimer;
        GameState _stateBeforeObstacle = GameState.Playing;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[ObstacleManager] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;

            RegisterModule(new OrmasObstacle(ormasConfig));
            RegisterModule(new SantetObstacle(Config));
            RegisterModule(new IllegalTaxObstacle(Config));
        }

        void OnEnable()
        {
            if (Instance != this) return;

            GameEventBus.OnDayStarted += HandleDayStarted;
            GameEventBus.OnDayCompleted += HandleDayCompleted;
        }

        void OnDisable()
        {
            if (Instance != this) return;

            GameEventBus.OnDayStarted -= HandleDayStarted;
            GameEventBus.OnDayCompleted -= HandleDayCompleted;
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            // Jangan tinggalkan clock melambat kalau scene dibongkar saat gangguan aktif.
            if (Active != null) GameClock.SetMultiplier(1f);
            Instance = null;
        }

        void Update()
        {
            if (enableDebugKeys) HandleDebugKeys();

            float delta = GameClock.RawDeltaTime;

            _dayTimer += delta;
            CheckSchedule();

            if (Active == null) return;

            // Tick bisa membuat urgency penuh, yang langsung menyelesaikan gangguan
            // dan mengosongkan Active di tengah pemanggilan ini.
            IObstacle current = Active;
            current.Tick(delta);

            if (Active == current && doorAlert != null) doorAlert.SetUrgency(current.UrgencyNormalized);
        }

        void RegisterModule(IObstacle module)
        {
            _modules[module.Type] = module;
            module.OnResolved += HandleResolved;
        }

        // ---- Jadwal ------------------------------------------------------------

        void HandleDayStarted(int day)
        {
            _dayTimer = 0f;
            _pending.Clear();
            _deferred.Clear();

            DayManager dayManager = DayManager.Instance;
            DayConfigSO dayConfig = dayManager != null ? dayManager.CurrentDayConfig : null;
            if (dayConfig == null || dayConfig.obstacleSchedule == null) return;

            foreach (ObstacleScheduleEntry entry in dayConfig.obstacleSchedule)
            {
                if (entry == null) continue;

                _pending.Add(new ScheduledObstacle
                {
                    type = entry.type,
                    triggerAt = entry.triggerAtSeconds,
                    difficulty = entry.difficulty
                });
            }

            _pending.Sort((a, b) => a.triggerAt.CompareTo(b.triggerAt));

            if (_pending.Count > 0)
                Debug.Log($"[Obstacle] Hari {day}: {_pending.Count} gangguan dijadwalkan.", this);
        }

        void HandleDayCompleted(int day)
        {
            _pending.Clear();
            _deferred.Clear();

            // Hari sudah tutup; gangguan yang masih berjalan dianggap selesai begitu saja.
            if (Active != null) Active.Resolve(false);
        }

        void CheckSchedule()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].triggerAt > _dayTimer) continue;

                ScheduledObstacle scheduled = _pending[i];
                _pending.RemoveAt(i);

                if (Active == null)
                {
                    Begin(scheduled);
                }
                else
                {
                    // Hanya satu gangguan dalam satu waktu: yang bertabrakan ditunda
                    // sampai yang sedang berjalan selesai.
                    _deferred.Enqueue(scheduled);
                    Debug.Log($"[Obstacle] {scheduled.type.GetLabel()} ditunda — " +
                              $"{Active.Type.GetLabel()} masih berjalan.", this);
                }
            }
        }

        // ---- API ---------------------------------------------------------------

        /// <summary>Picu gangguan sekarang juga. False kalau ada yang masih berjalan.</summary>
        public bool TriggerNow(ObstacleType type, float difficulty)
        {
            if (Active != null)
            {
                Debug.Log($"[Obstacle] {Active.Type.GetLabel()} masih berjalan — permintaan diabaikan.", this);
                return false;
            }

            return Begin(new ScheduledObstacle { type = type, difficulty = difficulty, triggerAt = _dayTimer });
        }

        /// <summary>Selesaikan gangguan yang sedang berjalan.</summary>
        public void ResolveActive(bool success)
        {
            if (Active == null) return;
            Active.Resolve(success);
        }

        // ---- Siklus gangguan ----------------------------------------------------

        bool Begin(ScheduledObstacle scheduled)
        {
            if (!_modules.TryGetValue(scheduled.type, out IObstacle obstacle))
            {
                Debug.LogError($"[Obstacle] Belum ada module untuk {scheduled.type}.", this);
                return false;
            }

            Active = obstacle;

            GameClock.SetMultiplier(Config.timeMultiplierDuringObstacle);

            GameManager manager = GameManager.Instance;
            if (manager != null)
            {
                _stateBeforeObstacle = manager.State;
                manager.ChangeState(GameState.InObstacle);
            }

            if (doorAlert != null)
            {
                doorAlert.Show(scheduled.type, scheduled.type.GetDoorAlert());
                doorAlert.SetUrgency(0f);
            }

            GameEventBus.RaiseObstacleStarted(scheduled.type);
            obstacle.Begin(scheduled.difficulty);

            return true;
        }

        void HandleResolved(bool success)
        {
            if (Active == null) return;

            ObstacleType type = Active.Type;
            Active = null;

            GameClock.SetMultiplier(1f);

            if (doorAlert != null) doorAlert.Clear();

            Debug.Log($"[Obstacle] {type.GetLabel()} selesai — {(success ? "berhasil diatasi" : "gagal")}.", this);
            AudioService.PlaySFX(success ? SfxId.UiClick : SfxId.SantetHit);

            GameEventBus.RaiseObstacleResolved(type, success);

            GameManager manager = GameManager.Instance;
            if (manager != null && manager.State == GameState.InObstacle)
                manager.ChangeState(_stateBeforeObstacle);

            // Gangguan yang tadi ditunda baru boleh jalan sekarang.
            if (_deferred.Count > 0) Begin(_deferred.Dequeue());
        }

        // ---- Debug ---------------------------------------------------------------

        void HandleDebugKeys()
        {
            if (InputService.WasKeyPressedThisFrame(Key.F5)) TriggerNow(ObstacleType.Ormas, debugDifficulty);
            if (InputService.WasKeyPressedThisFrame(Key.F6)) TriggerNow(ObstacleType.Santet, debugDifficulty);
            if (InputService.WasKeyPressedThisFrame(Key.F7)) TriggerNow(ObstacleType.IllegalTax, debugDifficulty);
            if (InputService.WasKeyPressedThisFrame(Key.F8)) ResolveActive(true);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
