using UnityEngine;

namespace MBG.Core
{
    /// <summary>Semua efek suara pendek. Nilai enum dulu, klip menyusul.</summary>
    public enum SfxId
    {
        None = 0,

        // QTE
        QtePerfect,
        QteGood,
        QteMiss,
        QteStart,

        // Gangguan
        DoorKnock,
        DoorBang,
        SantetHit,
        SantetWarning,

        // Pesanan & dapur
        OrderIncoming,
        OrderComplete,
        OrderFail,
        Chop,
        Fry,
        Plate,
        Pickup,

        // UI & alur
        UiClick,
        UiBack,
        CoinGain,
        DayStart,
        DayEnd,
        GameOver
    }

    /// <summary>Semua musik latar.</summary>
    public enum MusicId
    {
        None = 0,
        MainMenu,
        DayCalm,
        DayBusy,
        Tense,
        DaySummary,
        GameOver
    }

    /// <summary>
    /// STUB. Sengaja belum memutar apa-apa — isinya cuma Debug.Log supaya sistem
    /// gameplay sudah bisa memanggil audio dengan API final sejak sekarang, dan
    /// penggantian ke AudioSource/mixer nanti tidak menyentuh satu pun pemanggil.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioService : MonoBehaviour
    {
        [Header("Stub")]
        [Tooltip("Matikan kalau log audio mulai memenuhi console.")]
        [SerializeField] bool verboseLogging = true;

        public static AudioService Instance { get; private set; }

        public static MusicId CurrentMusic { get; private set; }

        bool _initialized;

        void Awake() => Initialize();

        /// <summary>Idempoten: aman dipanggil dari Awake sendiri maupun dari GameBootstrap.</summary>
        public void Initialize()
        {
            if (_initialized && Instance == this) return;

            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[AudioService] Sudah ada instance di '{Instance.name}'. Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
            CurrentMusic = MusicId.None;
            _initialized = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void PlaySFX(SfxId id)
        {
            if (id == SfxId.None) return;
            if (Instance == null || Instance.verboseLogging)
                Debug.Log($"[AudioService] SFX: {id}");
            // TODO: putar klip lewat AudioSource pool + mixer group SFX.
        }

        public static void PlayMusic(MusicId id)
        {
            if (CurrentMusic == id) return;
            CurrentMusic = id;

            if (Instance == null || Instance.verboseLogging)
                Debug.Log($"[AudioService] Musik: {id}");
            // TODO: crossfade ke klip baru lewat mixer group Music.
        }

        public static void StopMusic()
        {
            if (CurrentMusic == MusicId.None) return;
            CurrentMusic = MusicId.None;

            if (Instance == null || Instance.verboseLogging)
                Debug.Log("[AudioService] Musik: berhenti");
            // TODO: fade out lalu stop.
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Instance = null;
            CurrentMusic = MusicId.None;
        }
    }
}
