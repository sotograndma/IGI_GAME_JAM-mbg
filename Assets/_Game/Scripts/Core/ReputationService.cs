using MBG.Catering;
using MBG.Data;
using MBG.Obstacles;
using UnityEngine;

namespace MBG.Core
{
    /// <summary>
    /// Pemegang tunggal reputasi usaha, 0..100 dan mulai dari 50.
    ///
    /// Reputasi naik-turun sendiri dari event bus — hasil pesanan dan gangguan yang
    /// berhasil diatasi. Potongan untuk gangguan yang DIABAIKAN sampai konsekuensinya
    /// jatuh dilaporkan gangguan itu sendiri lewat <see cref="ApplyIgnoredPenalty"/>,
    /// karena event bus tidak bisa membedakan "kalah bertarung" dari "tidak ditengok
    /// sama sekali".
    ///
    /// Mencapai 0 berarti permainan berakhir — satu-satunya jalan menuju game over.
    /// </summary>
    [DisallowMultipleComponent]
    public class ReputationService : MonoBehaviour
    {
        [Tooltip("Semua angka reputasi. Diikat oleh Tools > MBG > Build Catering Data.")]
        [SerializeField] ReputationConfigSO config;

        public static ReputationService Instance { get; private set; }

        public int Reputation { get; private set; }

        public ReputationConfigSO Config => config != null ? config : ReputationConfigSO.Fallback;

        public int MaxReputation => Config.maxReputation;

        public ReputationTier Tier => Config.GetTier(Reputation);

        /// <summary>Pengali bayaran dari tingkat reputasi saat ini.</summary>
        public float GoldMultiplier => Config.GetGoldMultiplier(Tier);

        /// <summary>Pesanan tambahan per hari saat berada di tingkat Dicintai.</summary>
        public int ExtraOrdersPerDay
            => Tier == ReputationTier.Dicintai ? Config.lovedExtraOrdersPerDay : 0;

        /// <summary>Pengali bayaran saat ini, aman dipanggil walau service belum ada.</summary>
        public static float CurrentGoldMultiplier => Instance != null ? Instance.GoldMultiplier : 1f;

        bool _initialized;
        bool _gameOverRaised;

        void Awake() => Initialize();

        public void Initialize()
        {
            if (_initialized && Instance == this) return;

            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Reputation] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;

            ReputationConfigSO cfg = Config;
            Reputation = Mathf.Clamp(cfg.startingReputation, cfg.minReputation, cfg.maxReputation);
            _gameOverRaised = false;
            _initialized = true;
        }

        void OnEnable()
        {
            if (Instance != this) return;

            GameEventBus.OnOrderCompleted += HandleOrderCompleted;
            GameEventBus.OnOrderFailed += HandleOrderFailed;
            GameEventBus.OnObstacleResolved += HandleObstacleResolved;
        }

        void OnDisable()
        {
            if (Instance != this) return;

            GameEventBus.OnOrderCompleted -= HandleOrderCompleted;
            GameEventBus.OnOrderFailed -= HandleOrderFailed;
            GameEventBus.OnObstacleResolved -= HandleObstacleResolved;
        }

        void Start()
        {
            // Siarkan nilai awal setelah semua subscriber sempat mendaftar.
            GameEventBus.RaiseReputationChanged(Reputation);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- API ---------------------------------------------------------------

        public void AddReputation(int amount)
        {
            if (amount == 0) return;

            ReputationConfigSO cfg = Config;
            int next = Mathf.Clamp(Reputation + amount, cfg.minReputation, cfg.maxReputation);
            if (next == Reputation) return;

            ReputationTier before = Tier;
            Reputation = next;
            ReputationTier after = Tier;

            GameEventBus.RaiseReputationChanged(Reputation);

            if (before != after)
                Debug.Log($"[Reputation] {Reputation}/{cfg.maxReputation} — tingkat {before} -> {after}.", this);

            if (Reputation > cfg.minReputation || _gameOverRaised) return;

            _gameOverRaised = true;
            Debug.Log("[Reputation] Habis — permainan berakhir.", this);
            GameEventBus.RaiseGameOver(GameOverReason.ReputationZero);
        }

        /// <summary>
        /// Dipanggil gangguan yang dibiarkan sampai konsekuensinya jatuh — bukan
        /// yang sekadar kalah di mini-game.
        /// </summary>
        public void ApplyIgnoredPenalty(ObstacleType type)
        {
            int delta = Config.GetIgnoredPenalty(type);

            Debug.Log($"[Reputation] {type} diabaikan — {delta} reputasi.", this);
            AddReputation(delta);
        }

        // ---- Event -------------------------------------------------------------

        void HandleOrderCompleted(OrderResult result)
            => AddReputation(Config.GetOrderDelta(result.quality));

        void HandleOrderFailed(OrderRuntime order)
            => AddReputation(Config.orderFailed);

        void HandleObstacleResolved(ObstacleType type, bool success)
        {
            // Hanya keberhasilan yang diberi nilai di sini; kegagalannya sudah
            // ditangani masing-masing gangguan lewat ApplyIgnoredPenalty.
            if (!success) return;

            AddReputation(Config.obstacleResolved);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
