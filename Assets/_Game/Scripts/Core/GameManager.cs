using UnityEngine;
using UnityEngine.SceneManagement;

namespace MBG.Core
{
    /// <summary>
    /// Pemegang state global permainan. Singleton per-scene — TANPA
    /// DontDestroyOnLoad: project ini hanya punya satu scene, dan restart memang
    /// dimaksudkan untuk membuang seluruh state lama.
    ///
    /// Perpindahan state disiarkan lewat <see cref="GameEventBus.OnGameStateChanged"/>;
    /// tidak ada sistem lain yang boleh menyimpan salinan state-nya sendiri.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        const string PauseReason = "GameState.Paused";

        [Header("State awal")]
        [SerializeField] GameState initialState = GameState.Boot;

        [Header("Debug")]
        [SerializeField] bool logStateChanges = true;

        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public GameState PreviousState { get; private set; }

        /// <summary>True saat dunia gameplay berjalan (Playing / InQTE / InObstacle).</summary>
        public bool IsGameplayActive => State.IsGameplay();

        bool _initialized;

        void Awake() => Initialize();

        /// <summary>Idempoten: dipanggil dari Awake sendiri maupun dari GameBootstrap.</summary>
        public void Initialize()
        {
            if (_initialized && Instance == this) return;

            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameManager] Sudah ada instance di '{Instance.name}'. Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
            State = initialState;
            PreviousState = initialState;
            _initialized = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Pindah state dan siarkan perubahannya. Masuk <see cref="GameState.Paused"/>
        /// otomatis mem-pause <see cref="GameClock"/>, dan keluar darinya melepasnya
        /// lagi — sistem lain tidak perlu ikut mengurus itu.
        /// </summary>
        public void ChangeState(GameState next)
        {
            if (!_initialized) Initialize();
            if (next == State) return;

            GameState prev = State;
            PreviousState = prev;
            State = next;

            if (prev == GameState.Paused) GameClock.PopPause(PauseReason);
            if (next == GameState.Paused) GameClock.PushPause(PauseReason);

            if (logStateChanges)
                Debug.Log($"[GameManager] State: {prev} -> {next}", this);

            GameEventBus.RaiseGameStateChanged(prev, next);
        }

        /// <summary>
        /// Mulai ulang permainan dengan memuat ulang scene yang sedang aktif.
        /// Bus dibersihkan lebih dulu supaya subscriber dari sesi sebelumnya tidak
        /// ikut terbawa (relevan saat domain reload dimatikan).
        /// </summary>
        public void RestartGame()
        {
            Scene active = SceneManager.GetActiveScene();

            if (active.buildIndex < 0)
            {
                Debug.LogError($"[GameManager] Scene '{active.name}' belum terdaftar di Build Settings, " +
                               "jadi tidak bisa di-reload. Tambahkan lewat File > Build Profiles.", this);
                return;
            }

            GameClock.ClearPauses();
            GameEventBus.ClearAll();
            InputService.Shutdown();

            SceneManager.LoadScene(active.buildIndex);
        }

        /// <summary>Ringkasan state untuk debug key F1.</summary>
        public string Describe()
        {
            return $"State={State} (sebelumnya {PreviousState}), IsGameplayActive={IsGameplayActive}";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
