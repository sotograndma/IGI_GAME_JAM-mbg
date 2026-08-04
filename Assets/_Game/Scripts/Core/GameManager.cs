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

        [Header("Jeda")]
        [Tooltip("Escape saat bermain membuka menu jeda, dan menutupnya lagi.")]
        [SerializeField] bool allowPauseToggle = true;

        [Header("Debug")]
        [SerializeField] bool logStateChanges = true;

        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public GameState PreviousState { get; private set; }

        /// <summary>True saat dunia gameplay berjalan (Playing / InQTE / InObstacle).</summary>
        public bool IsGameplayActive => State.IsGameplay();

        bool _initialized;
        PlayerController2D _player;

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

        void OnEnable()
        {
            if (Instance != this) return;
            GameEventBus.OnGameOver += HandleGameOver;
        }

        void OnDisable()
        {
            if (Instance != this) return;
            GameEventBus.OnGameOver -= HandleGameOver;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!allowPauseToggle) return;
            if (!InputService.CancelPressed) return;

            // Escape hanya bekerja di dua arah antara bermain dan jeda; state lain
            // (QTE, ringkasan hari, game over) punya alurnya sendiri.
            if (State == GameState.Playing) ChangeState(GameState.Paused);
            else if (State == GameState.Paused) ChangeState(GameState.Playing);
        }

        /// <summary>Siapa pun yang menyatakan permainan berakhir cukup memancarkan event.</summary>
        void HandleGameOver(GameOverReason reason)
        {
            if (State == GameState.GameOver) return;
            ChangeState(GameState.GameOver);
        }

        /// <summary>Keluar dari permainan; di Editor cukup menghentikan Play mode.</summary>
        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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

            ApplyPlayerFreeze(next);

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

        /// <summary>
        /// Pemain hanya boleh bergerak saat benar-benar bermain. GameClock yang
        /// di-pause tidak cukup: PlayerController2D berjalan di FixedUpdate dengan
        /// waktu fisika sendiri, jadi tanpa ini karakter masih bisa jalan-jalan di
        /// balik menu jeda.
        ///
        /// InQTE dan InObstacle sengaja dilewati — pembekuannya sudah diurus sistem
        /// masing-masing, dan mencampurinya di sini akan membuat dua sumber perintah.
        /// </summary>
        void ApplyPlayerFreeze(GameState state)
        {
            if (state == GameState.InQTE || state == GameState.InObstacle) return;

            if (_player == null)
                _player = FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);

            if (_player != null) _player.SetFrozen(state != GameState.Playing);
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
