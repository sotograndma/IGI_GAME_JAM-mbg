using UnityEngine;
using UnityEngine.InputSystem;

namespace MBG.Core
{
    /// <summary>
    /// Titik masuk lapisan core. Dipasang di GameObject root bernama "__Systems"
    /// lewat menu Tools > MBG > Setup Systems Object.
    ///
    /// Alasan komponen ini ada: urutan Awake antar komponen pada GameObject yang
    /// sama tidak dijamin Unity. GameBootstrap berjalan lebih dulu
    /// (DefaultExecutionOrder) lalu memanggil Initialize() tiap service secara
    /// eksplisit dengan urutan yang benar. Semua Initialize() itu idempoten, jadi
    /// service yang kebetulan sudah menginisialisasi dirinya sendiri di Awake
    /// tidak akan ter-reset.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Alur awal")]
        [Tooltip("Pindah otomatis dari Boot ke Playing saat scene mulai. Matikan kalau " +
                 "nanti sudah ada main menu yang mengatur perpindahan state sendiri.")]
        [SerializeField] bool enterPlayingOnStart = true;

        [Header("Debug")]
        [Tooltip("F1 mencetak ringkasan state ke console.")]
        [SerializeField] bool enableDebugKeys = true;

        public static GameBootstrap Instance { get; private set; }

        GameManager _gameManager;
        GameClock _gameClock;
        AudioService _audioService;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameBootstrap] Sudah ada '__Systems' lain di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }
            Instance = this;

            // Urutan penting: input tidak bergantung pada apa pun, clock dipakai
            // GameManager saat state Paused, audio dipanggil siapa saja setelahnya.
            InputService.Initialize();

            _gameClock = Resolve<GameClock>();
            _gameClock.Initialize();

            _audioService = Resolve<AudioService>();
            _audioService.Initialize();

            _gameManager = Resolve<GameManager>();
            _gameManager.Initialize();
        }

        void Start()
        {
            if (enterPlayingOnStart && _gameManager != null && _gameManager.State == GameState.Boot)
                _gameManager.ChangeState(GameState.Playing);
        }

        void Update()
        {
            // Menjaga langganan onTextInput menempel pada keyboard yang aktif.
            InputService.Tick();

            if (enableDebugKeys && InputService.WasKeyPressedThisFrame(Key.F1))
                LogDebugState();
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            InputService.Shutdown();
            Instance = null;
        }

        /// <summary>
        /// Ambil service dari GameObject ini; tambahkan kalau hilang, supaya
        /// __Systems yang komponennya terhapus manual tetap bisa jalan.
        /// </summary>
        T Resolve<T>() where T : Component
        {
            var component = GetComponent<T>();
            if (component != null) return component;

            Debug.LogWarning($"[GameBootstrap] Komponen {typeof(T).Name} tidak ada di '{name}', ditambahkan saat runtime. " +
                             "Jalankan Tools > MBG > Setup Systems Object agar tersimpan di scene.", this);
            return gameObject.AddComponent<T>();
        }

        /// <summary>Isi debug key F1.</summary>
        public void LogDebugState()
        {
            string state = _gameManager != null ? _gameManager.Describe() : "GameManager: TIDAK ADA";

            Debug.Log(
                "=== MBG DEBUG (F1) ===\n" +
                $"{state}\n" +
                $"Clock: multiplier={GameClock.TimeMultiplier:0.##}, paused={GameClock.IsPaused} " +
                $"(count={GameClock.PauseCount}, alasan: {GameClock.DescribePauses()}), " +
                $"elapsed={GameClock.ElapsedTime:0.00}s, delta={GameClock.DeltaTime:0.0000}\n" +
                $"Input: TextInputMode={InputService.TextInputMode}, MoveAxis={InputService.MoveAxis}, " +
                $"SprintHeld={InputService.SprintHeld}\n" +
                $"Audio: musik={AudioService.CurrentMusic}",
                this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
