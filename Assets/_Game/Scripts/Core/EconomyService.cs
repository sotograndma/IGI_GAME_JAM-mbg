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

        // Reputasi TIDAK di sini — itu urusan ReputationService.

        bool _initialized;

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

        // ---- Event ----------------------------------------------------------

        void HandleOrderCompleted(OrderResult result)
        {
            AddGold(result.goldEarned);
            AddScore(result.scoreEarned);

            Debug.Log($"[Economy] Pesanan selesai: {result.goldEarned:+#;-#;0} gold, " +
                      $"+{result.scoreEarned} skor. Total: {Gold} gold, {Score} skor.", this);
        }

        void HandleOrderFailed(OrderRuntime order)
        {
            // Denda dihitung dari rumus yang sama dengan bayaran, supaya tidak ada
            // angka kedua yang bisa ketinggalan saat di-tune.
            if (scoring == null) return;

            OrderResult result = scoring.CalculateResult(order, success: false,
                                                         ReputationService.CurrentGoldMultiplier);
            AddGold(result.goldEarned);

            Debug.Log($"[Economy] Pesanan gagal: {result.goldEarned} gold. Total: {Gold} gold.", this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
