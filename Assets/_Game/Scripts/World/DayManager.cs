using System.Collections.Generic;
using MBG.Catering;
using MBG.Core;
using MBG.Data;
using MBG.Obstacles;
using MBG.QTE;
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

        [Header("Mode bertahan")]
        [Tooltip("Setelah hari terjadwal habis, pesanan terus datang dengan kesulitan naik.")]
        [SerializeField] bool endlessEnabled = true;

        [Tooltip("Kolam pesanan yang dipakai mode bertahan. Diisi Tools > MBG > Build Catering Data.")]
        [SerializeField] List<OrderSO> endlessOrderPool = new();

        [Tooltip("Preset QTE untuk mode bertahan. Diikat ke QTE_Hard.")]
        [SerializeField] QTEConfigSO endlessQteConfig;

        [Tooltip("Berapa pesanan di hari bertahan pertama.")]
        [Min(1)]
        [SerializeField] int endlessBaseOrderCount = 4;

        [Tooltip("Tambahan pesanan tiap berapa hari bertahan.")]
        [Min(1)]
        [SerializeField] int endlessOrdersEveryDays = 2;

        [Tooltip("Deadline dipotong sekian tiap hari bertahan, sampai batas bawah.")]
        [SerializeField] float endlessDeadlineStep = 0.03f;

        [SerializeField] float endlessMinDeadlineMultiplier = 0.6f;

        [Tooltip("Jumlah gangguan di hari bertahan pertama.")]
        [Min(0)]
        [SerializeField] int endlessBaseObstacles = 2;

        [Header("Debug")]
        [Tooltip("F3 memulai hari pertama (atau mengulang hari yang sedang berjalan).")]
        [SerializeField] bool enableDebugKeys = true;

        public static DayManager Instance { get; private set; }

        public DayConfigSO CurrentDayConfig { get; private set; }

        /// <summary>Nomor hari yang sedang atau terakhir berjalan.</summary>
        public int CurrentDayNumber => CurrentDayConfig != null ? CurrentDayConfig.dayNumber : _dayIndex + 1;

        public bool IsDayRunning { get; private set; }

        /// <summary>
        /// True hanya kalau tidak ada lagi hari setelah ini — di mode bertahan, hari
        /// tidak pernah habis, jadi tidak pernah ada "hari terakhir".
        /// </summary>
        public bool IsFinalDay => !endlessEnabled && _dayIndex >= days.Count - 1;

        /// <summary>True kalau hari yang sedang berjalan sudah di luar jadwal.</summary>
        public bool IsEndlessDay => _dayIndex >= days.Count;

        /// <summary>Rekap hari terakhir yang selesai.</summary>
        public DayStats LastStats { get; private set; }

        public int TotalDays => days != null ? days.Count : 0;

        int _dayIndex = -1;
        int _orderIndex = -1;

        /// <summary>Pesanan tambahan dari bonus reputasi tingkat Dicintai.</summary>
        int _bonusOrders;

        DayConfigSO _generatedDay;

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

            if (index < 0)
            {
                Debug.LogWarning($"[Day] Indeks hari tidak valid: {index}.", this);
                return;
            }

            DayConfigSO config;

            if (index < days.Count)
            {
                config = days[index];
            }
            else if (endlessEnabled)
            {
                // Jadwal habis: susun hari baru yang makin berat.
                config = BuildEndlessDay(index);
            }
            else
            {
                Debug.LogWarning($"[Day] Tidak ada hari ke-{index + 1}. Total hari: {days.Count}.", this);
                return;
            }

            if (config == null || config.OrderCount == 0)
            {
                Debug.LogWarning($"[Day] Hari ke-{index + 1} tidak punya pesanan.", this);
                return;
            }

            _dayIndex = index;
            CurrentDayConfig = config;
            IsDayRunning = true;

            // Reputasi tingkat Dicintai menarik pelanggan tambahan.
            _bonusOrders = ReputationService.Instance != null
                ? ReputationService.Instance.ExtraOrdersPerDay
                : 0;

            if (_bonusOrders > 0)
                Debug.Log($"[Day] Reputasi Dicintai — {_bonusOrders} pesanan tambahan hari ini.", this);

            _orderIndex = -1;
            _succeeded = 0;
            _failed = 0;
            _waitingForNextOrder = false;
            _nextOrderTimer = 0f;

            EconomyService economy = EconomyService.Instance;
            _goldAtDayStart = economy != null ? economy.Gold : 0;
            _scoreAtDayStart = economy != null ? economy.Score : 0;

            Debug.Log($"[Day] Hari {config.dayNumber} dimulai — {config.OrderCount + _bonusOrders} pesanan, " +
                      $"pengali deadline {config.orderDeadlineMultiplier:0.##}" +
                      $"{(IsEndlessDay ? ", mode bertahan" : "")}.", this);

            // Bertahan sampai hari ini sudah sebuah pencapaian; catat rekornya.
            if (HighScoreStore.TrySubmitBestDay(config.dayNumber))
                Debug.Log($"[Day] Rekor baru: hari {config.dayNumber}.", this);

            GameEventBus.RaiseDayStarted(config.dayNumber);

            // Pesanan pertama langsung datang; sisanya menunggu jeda.
            AdvanceOrder();
        }

        // ---- Antrian pesanan --------------------------------------------------

        int TotalOrdersToday => CurrentDayConfig != null ? CurrentDayConfig.OrderCount + _bonusOrders : 0;

        void AdvanceOrder()
        {
            if (!IsDayRunning || CurrentDayConfig == null) return;

            _orderIndex++;

            while (_orderIndex < TotalOrdersToday && ResolveOrder(_orderIndex) == null)
                _orderIndex++;

            if (_orderIndex >= TotalOrdersToday)
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

            OrderSO order = ResolveOrder(_orderIndex);
            Debug.Log($"[Day] Pesanan {_orderIndex + 1}/{TotalOrdersToday} hari " +
                      $"{CurrentDayConfig.dayNumber}.", this);

            catering.StartOrder(order, CurrentDayConfig.orderDeadlineMultiplier,
                                CurrentDayConfig.qteConfigOverride);
        }

        /// <summary>
        /// Pesanan pada indeks tertentu. Indeks di luar daftar hari berarti pesanan
        /// bonus dari reputasi — diambil berputar dari daftar yang sama.
        /// </summary>
        OrderSO ResolveOrder(int index)
        {
            if (CurrentDayConfig == null || CurrentDayConfig.OrderCount == 0) return null;

            if (index < CurrentDayConfig.OrderCount) return CurrentDayConfig.GetOrder(index);

            return CurrentDayConfig.GetOrder(index % CurrentDayConfig.OrderCount);
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
                totalOrders = TotalOrdersToday,
                reputation = ReputationService.Instance != null ? ReputationService.Instance.Reputation : 0,
                maxReputation = ReputationService.Instance != null ? ReputationService.Instance.MaxReputation : 0,
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

        // ---- Mode bertahan --------------------------------------------------------

        /// <summary>
        /// Susun hari di luar jadwal: makin banyak pesanan, deadline makin ketat,
        /// gangguan makin sering, dan QTE memakai preset tersulit.
        ///
        /// Config-nya dibuat runtime (bukan asset) supaya tidak ada file baru yang
        /// menumpuk di project setiap kali pemain bertahan lama.
        /// </summary>
        DayConfigSO BuildEndlessDay(int index)
        {
            List<OrderSO> pool = endlessOrderPool != null && endlessOrderPool.Count > 0
                ? endlessOrderPool
                : CollectOrdersFromSchedule();

            if (pool.Count == 0)
            {
                Debug.LogWarning("[Day] Mode bertahan tidak punya kolam pesanan.", this);
                return null;
            }

            int endlessIndex = index - days.Count;   // 0 untuk hari bertahan pertama
            int dayNumber = index + 1;

            if (_generatedDay == null)
            {
                _generatedDay = ScriptableObject.CreateInstance<DayConfigSO>();
                _generatedDay.name = "Day_Endless (runtime)";
            }

            _generatedDay.dayNumber = dayNumber;
            _generatedDay.timeBetweenOrders = 2.5f;

            _generatedDay.orderDeadlineMultiplier = Mathf.Max(
                endlessMinDeadlineMultiplier,
                0.85f - endlessDeadlineStep * endlessIndex);

            _generatedDay.qteConfigOverride = endlessQteConfig;

            int orderCount = endlessBaseOrderCount + endlessIndex / Mathf.Max(1, endlessOrdersEveryDays);
            _generatedDay.orders = new List<OrderSO>(orderCount);
            for (int i = 0; i < orderCount; i++)
                _generatedDay.orders.Add(pool[i % pool.Count]);

            // Gangguan bertambah satu tiap hari bertahan, disebar rata sepanjang hari.
            int obstacleCount = endlessBaseObstacles + endlessIndex;
            float difficulty = Mathf.Clamp01(0.5f + endlessIndex * 0.1f);

            _generatedDay.obstacleSchedule = new List<ObstacleScheduleEntry>(obstacleCount);
            for (int i = 0; i < obstacleCount; i++)
            {
                _generatedDay.obstacleSchedule.Add(new ObstacleScheduleEntry
                {
                    type = (ObstacleType)(i % 3),
                    triggerAtSeconds = 25f + i * 30f,
                    difficulty = difficulty
                });
            }

            Debug.Log($"[Day] Hari bertahan {dayNumber}: {orderCount} pesanan, {obstacleCount} gangguan, " +
                      $"deadline x{_generatedDay.orderDeadlineMultiplier:0.##}, difficulty {difficulty:0.00}.", this);

            return _generatedDay;
        }

        List<OrderSO> CollectOrdersFromSchedule()
        {
            var pool = new List<OrderSO>();

            foreach (DayConfigSO day in days)
            {
                if (day == null) continue;

                foreach (OrderSO order in day.orders)
                {
                    if (order != null && !pool.Contains(order)) pool.Add(order);
                }
            }

            return pool;
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
            bool hasMoreOrders = _orderIndex + 1 < TotalOrdersToday;

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
