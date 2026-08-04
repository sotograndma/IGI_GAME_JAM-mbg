using MBG.Catering;
using MBG.Data;
using UnityEngine;

namespace MBG.Core
{
    /// <summary>
    /// Pemegang tunggal uang dan skor pemain. Setiap perubahan disiarkan lewat
    /// <see cref="GameEventBus"/>, jadi HUD dan sistem lain tidak perlu memegang
    /// referensi ke service ini.
    ///
    /// Bayaran pesanan tidak dihitung di sini: service ini hanya menerapkan angka
    /// yang sudah dihitung <see cref="ScoringConfigSO.CalculateResult"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class EconomyService : MonoBehaviour
    {
        [Tooltip("Sumber gold awal dan denda kegagalan. Diikat oleh Tools > MBG > Build Catering Data.")]
        [SerializeField] ScoringConfigSO scoring;

        public static EconomyService Instance { get; private set; }

        public int Gold { get; private set; }
        public int Score { get; private set; }

        /// <summary>Reputasi usaha catering. Mencapai 0 = permainan berakhir.</summary>
        public int Reputation { get; private set; }

        public int MaxReputation => scoring != null ? scoring.maxReputation : 5;

        bool _initialized;
        bool _gameOverRaised;

        void Awake() => Initialize();

        /// <summary>Idempoten: aman dipanggil dari Awake sendiri maupun dari luar.</summary>
        public void Initialize()
        {
            if (_initialized && Instance == this) return;

            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[EconomyService] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
            Gold = scoring != null ? scoring.startingGold : 0;
            Score = 0;
            Reputation = scoring != null ? scoring.startingReputation : 3;
            _gameOverRaised = false;
            _initialized = true;
        }

        void OnEnable()
        {
            if (Instance != this) return;

            GameEventBus.OnOrderCompleted += HandleOrderCompleted;
            GameEventBus.OnOrderFailed += HandleOrderFailed;
        }

        void OnDisable()
        {
            if (Instance != this) return;

            GameEventBus.OnOrderCompleted -= HandleOrderCompleted;
            GameEventBus.OnOrderFailed -= HandleOrderFailed;
        }

        void Start()
        {
            // Siarkan nilai awal setelah semua subscriber sempat mendaftar, supaya
            // HUD tidak menampilkan 0 di frame pertama.
            GameEventBus.RaiseGoldChanged(Gold);
            GameEventBus.RaiseScoreChanged(Score);
            GameEventBus.RaiseReputationChanged(Reputation);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- API ------------------------------------------------------------

        public void AddGold(int amount)
        {
            if (amount == 0) return;

            Gold = Mathf.Max(0, Gold + amount);
            GameEventBus.RaiseGoldChanged(Gold);
        }

        public void AddScore(int amount)
        {
            if (amount == 0) return;

            Score = Mathf.Max(0, Score + amount);
            GameEventBus.RaiseScoreChanged(Score);
        }

        /// <summary>Kurangi gold. False (dan tidak ada yang berubah) kalau tidak cukup.</summary>
        public bool SpendGold(int amount)
        {
            if (amount <= 0) return true;

            if (Gold < amount)
            {
                Debug.Log($"[Economy] Gold tidak cukup: butuh {amount}, punya {Gold}.", this);
                return false;
            }

            Gold -= amount;
            GameEventBus.RaiseGoldChanged(Gold);
            return true;
        }

        /// <summary>
        /// Ubah reputasi. Mencapai 0 memancarkan
        /// <see cref="GameEventBus.OnGameOver"/> sekali saja.
        /// </summary>
        public void AddReputation(int amount)
        {
            if (amount == 0) return;

            int max = MaxReputation;
            int next = Mathf.Clamp(Reputation + amount, 0, max);
            if (next == Reputation) return;

            Reputation = next;
            GameEventBus.RaiseReputationChanged(Reputation);

            if (Reputation > 0 || _gameOverRaised) return;

            _gameOverRaised = true;
            Debug.Log("[Economy] Reputasi habis — permainan berakhir.", this);
            GameEventBus.RaiseGameOver(GameOverReason.ReputationZero);
        }

        // ---- Event ----------------------------------------------------------

        void HandleOrderCompleted(OrderResult result)
        {
            AddGold(result.goldEarned);
            AddScore(result.scoreEarned);

            if (scoring != null) AddReputation(scoring.GetReputationDelta(result.quality));

            Debug.Log($"[Economy] Pesanan selesai: {result.goldEarned:+#;-#;0} gold, " +
                      $"+{result.scoreEarned} skor. Total: {Gold} gold, {Score} skor, " +
                      $"reputasi {Reputation}/{MaxReputation}.", this);
        }

        void HandleOrderFailed(OrderRuntime order)
        {
            // Denda dihitung dari rumus yang sama dengan bayaran, supaya tidak ada
            // angka kedua yang bisa ketinggalan saat di-tune.
            if (scoring == null) return;

            OrderResult result = scoring.CalculateResult(order, success: false);
            AddGold(result.goldEarned);
            AddReputation(scoring.reputationOnFailed);

            Debug.Log($"[Economy] Pesanan gagal: {result.goldEarned} gold. Total: {Gold} gold, " +
                      $"reputasi {Reputation}/{MaxReputation}.", this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
