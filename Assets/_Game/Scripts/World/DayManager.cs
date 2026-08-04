using System.Collections.Generic;
using MBG.Catering;
using MBG.Core;
using MBG.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MBG.World
{
    /// <summary>
    /// Mengurus alur hari: pesanan mana yang datang, kapan pesanan berikutnya
    /// menyusul, dan kapan hari dinyatakan selesai.
    ///
    /// Pembagian tugas dengan <see cref="CateringController"/>: controller itu
    /// hanya tahu SATU pesanan yang sedang dimasak; DayManager yang tahu antrian
    /// dan urutan hari. Keduanya berbicara lewat event bus, bukan saling menyimpan
    /// state.
    ///
    /// Alur: StartDay -> pesanan 1 -> layar hasil -> jeda timeBetweenOrders ->
    /// pesanan 2 -> ... -> antrian habis -> OnDaySummary + OnDayCompleted ->
    /// GameState jadi DaySummary.
    /// </summary>
    [DisallowMultipleComponent]
    public class DayManager : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Hari-hari permainan, berurutan. Diisi Tools > MBG > Build Catering Data.")]
        [SerializeField] List<DayConfigSO> days = new();

        [Header("Debug")]
        [Tooltip("F3 memulai hari pertama (atau mengulang hari yang sedang berjalan).")]
        [SerializeField] bool enableDebugKeys = true;

        public static DayManager Instance { get; private set; }

        public DayConfigSO CurrentDayConfig { get; private set; }

        /// <summary>Nomor hari yang sedang atau terakhir berjalan.</summary>
        public int CurrentDayNumber => CurrentDayConfig != null ? CurrentDayConfig.dayNumber : _dayIndex + 1;

        public bool IsDayRunning { get; private set; }

        /// <summary>True kalau hari yang sedang berjalan adalah yang terakhir dijadwalkan.</summary>
        public bool IsFinalDay => _dayIndex >= days.Count - 1;

        /// <summary>Rekap hari terakhir yang selesai.</summary>
        public DayStats LastStats { get; private set; }

        public int TotalDays => days != null ? days.Count : 0;

        int _dayIndex = -1;
        int _orderIndex = -1;

        int _goldAtDayStart;
        int _scoreAtDayStart;
        int _succeeded;
        int _failed;

        float _nextOrderTimer;
        bool _waitingForNextOrder;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[DayManager] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
        }

        void OnEnable()
        {
            if (Instance != this) return;

            GameEventBus.OnOrderCompleted += HandleOrderCompleted;
            GameEventBus.OnOrderFailed += HandleOrderFailed;
            GameEventBus.OnResultAcknowledged += HandleResultAcknowledged;
        }

        void OnDisable()
        {
            if (Instance != this) return;

            GameEventBus.OnOrderCompleted -= HandleOrderCompleted;
            GameEventBus.OnOrderFailed -= HandleOrderFailed;
            GameEventBus.OnResultAcknowledged -= HandleResultAcknowledged;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (enableDebugKeys && InputService.WasKeyPressedThisFrame(Key.F3)) DebugStartDay();

            TickNextOrderDelay();
        }

        // ---- API -------------------------------------------------------------

        /// <summary>Mulai hari pertama. Dipanggil tombol "Mulai" di menu utama.</summary>
        public void StartFirstDay() => StartDayAt(0);

        /// <summary>Lanjut ke hari berikutnya. Dipanggil dari layar ringkasan hari.</summary>
        public void StartNextDay() => StartDayAt(_dayIndex + 1);

        /// <summary>Mulai hari pada indeks tertentu (0-based).</summary>
        public void StartDayAt(int index)
        {
            if (days == null || days.Count == 0)
            {
                Debug.LogWarning("[Day] Daftar hari kosong. Jalankan Tools > MBG > Build Catering Data.", this);
                return;
            }

            if (index < 0 || index >= days.Count)
            {
                Debug.LogWarning($"[Day] Tidak ada hari ke-{index + 1}. Total hari: {days.Count}.", this);
                return;
            }

            DayConfigSO config = days[index];
            if (config == null || config.OrderCount == 0)
            {
                Debug.LogWarning($"[Day] Hari ke-{index + 1} tidak punya pesanan.", this);
                return;
            }

            _dayIndex = index;
            CurrentDayConfig = config;
            IsDayRunning = true;

            _orderIndex = -1;
            _succeeded = 0;
            _failed = 0;
            _waitingForNextOrder = false;
            _nextOrderTimer = 0f;

            EconomyService economy = EconomyService.Instance;
            _goldAtDayStart = economy != null ? economy.Gold : 0;
            _scoreAtDayStart = economy != null ? economy.Score : 0;

            Debug.Log($"[Day] Hari {config.dayNumber} dimulai — {config.OrderCount} pesanan, " +
                      $"pengali deadline {config.orderDeadlineMultiplier:0.##}.", this);

            GameEventBus.RaiseDayStarted(config.dayNumber);

            // Pesanan pertama langsung datang; sisanya menunggu jeda.
            AdvanceOrder();
        }

        // ---- Antrian pesanan --------------------------------------------------

        void AdvanceOrder()
        {
            if (!IsDayRunning || CurrentDayConfig == null) return;

            _orderIndex++;

            while (_orderIndex < CurrentDayConfig.OrderCount && CurrentDayConfig.GetOrder(_orderIndex) == null)
                _orderIndex++;

            if (_orderIndex >= CurrentDayConfig.OrderCount)
            {
                CompleteDay();
                return;
            }

            CateringController catering = CateringController.Instance;
            if (catering == null)
            {
                Debug.LogError("[Day] CateringController tidak ada di scene. " +
                               "Jalankan Tools > MBG > Build Catering Data.", this);
                return;
            }

            OrderSO order = CurrentDayConfig.GetOrder(_orderIndex);
            Debug.Log($"[Day] Pesanan {_orderIndex + 1}/{CurrentDayConfig.OrderCount} hari " +
                      $"{CurrentDayConfig.dayNumber}.", this);

            catering.StartOrder(order, CurrentDayConfig.orderDeadlineMultiplier);
        }

        /// <summary>
        /// Jeda antar pesanan memakai <c>GameClock.DeltaTime</c>, jadi ikut berhenti
        /// kalau permainan sedang di-pause.
        /// </summary>
        void TickNextOrderDelay()
        {
            if (!_waitingForNextOrder) return;

            _nextOrderTimer -= GameClock.DeltaTime;
            if (_nextOrderTimer > 0f) return;

            _waitingForNextOrder = false;
            AdvanceOrder();
        }

        void CompleteDay()
        {
            IsDayRunning = false;
            _waitingForNextOrder = false;

            EconomyService economy = EconomyService.Instance;

            LastStats = new DayStats
            {
                day = CurrentDayConfig != null ? CurrentDayConfig.dayNumber : _dayIndex + 1,
                goldEarned = economy != null ? economy.Gold - _goldAtDayStart : 0,
                scoreEarned = economy != null ? economy.Score - _scoreAtDayStart : 0,
                ordersSucceeded = _succeeded,
                ordersFailed = _failed,
                totalOrders = CurrentDayConfig != null ? CurrentDayConfig.OrderCount : 0,
                reputation = economy != null ? economy.Reputation : 0,
                maxReputation = economy != null ? economy.MaxReputation : 0,
                isFinalDay = IsFinalDay
            };

            Debug.Log($"[Day] {LastStats}", this);

            // Rekap dulu supaya panel sudah punya isinya saat state berubah.
            GameEventBus.RaiseDaySummary(LastStats);
            GameEventBus.RaiseDayCompleted(LastStats.day);

            GameManager manager = GameManager.Instance;
            if (manager != null && manager.State != GameState.GameOver)
                manager.ChangeState(GameState.DaySummary);
        }

        // ---- Event -------------------------------------------------------------

        void HandleOrderCompleted(OrderResult result)
        {
            if (IsDayRunning) _succeeded++;
        }

        void HandleOrderFailed(OrderRuntime order)
        {
            if (IsDayRunning) _failed++;
        }

        /// <summary>Pemain menutup layar hasil: tunggu jeda lalu ambil pesanan berikutnya.</summary>
        void HandleResultAcknowledged()
        {
            if (!IsDayRunning) return;

            float delay = CurrentDayConfig != null ? CurrentDayConfig.timeBetweenOrders : 0f;

            // Pesanan terakhir tidak perlu menunggu apa-apa — langsung tutup hari,
            // supaya tidak ada jeda kosong sebelum layar ringkasan.
            bool hasMoreOrders = CurrentDayConfig != null && _orderIndex + 1 < CurrentDayConfig.OrderCount;

            if (delay <= 0f || !hasMoreOrders)
            {
                AdvanceOrder();
                return;
            }

            _nextOrderTimer = delay;
            _waitingForNextOrder = true;
        }

        // ---- Debug --------------------------------------------------------------

        void DebugStartDay()
        {
            if (IsDayRunning)
            {
                Debug.Log($"[Day] F3: hari {CurrentDayNumber} masih berjalan.", this);
                return;
            }

            StartDayAt(Mathf.Max(0, _dayIndex));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
