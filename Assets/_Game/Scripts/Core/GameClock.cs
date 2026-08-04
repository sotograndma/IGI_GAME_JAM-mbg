using System.Collections.Generic;
using UnityEngine;

namespace MBG.Core
{
    /// <summary>
    /// Sumber waktu tunggal untuk semua sistem gameplay. Semua sistem baru WAJIB
    /// memakai <see cref="DeltaTime"/>, bukan Time.deltaTime — hanya dengan begitu
    /// pause (dialog, QTE, cutscene) dan slow-motion bisa bekerja tanpa menyentuh
    /// Time.timeScale, yang akan ikut membekukan animasi UI dan fade area.
    ///
    /// Pause memakai penghitung ber-alasan: dua sistem boleh mem-pause bersamaan,
    /// dan clock baru jalan lagi setelah keduanya melepas pause-nya masing-masing.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameClock : MonoBehaviour
    {
        [Header("Kecepatan waktu")]
        [Tooltip("Pengali delta time untuk sistem gameplay. 1 = normal, 0.5 = slow-mo, 2 = dipercepat.")]
        [SerializeField] float timeMultiplier = 1f;

        [Header("Debug (read-only saat Play)")]
        [SerializeField] bool logPauseChanges = false;

        public static GameClock Instance { get; private set; }

        readonly Dictionary<string, int> _pauseReasons = new();
        int _pauseCount;
        float _elapsed;
        bool _initialized;

        static bool _missingInstanceWarned;

        // ---- API static (dipakai semua sistem gameplay) ------------------

        /// <summary>
        /// Delta frame yang sudah dikali multiplier, dan 0 saat clock di-pause.
        /// Kalau __Systems belum ada di scene, fallback ke Time.deltaTime supaya
        /// game tidak membeku total — sekali saja diperingatkan di console.
        /// </summary>
        public static float DeltaTime
        {
            get
            {
                if (Instance != null) return Instance.CurrentDelta;
                WarnMissingInstance();
                return Time.deltaTime;
            }
        }

        /// <summary>Delta mentah tanpa multiplier dan tanpa pause (untuk UI/animasi).</summary>
        public static float UnscaledDeltaTime => Time.unscaledDeltaTime;

        /// <summary>Total waktu gameplay yang sudah berjalan (sudah dikali multiplier, pause tidak dihitung).</summary>
        public static float ElapsedTime => Instance != null ? Instance._elapsed : 0f;

        public static float TimeMultiplier => Instance != null ? Instance.timeMultiplier : 1f;

        public static bool IsPaused => Instance != null && Instance._pauseCount > 0;

        public static int PauseCount => Instance != null ? Instance._pauseCount : 0;

        public static void SetMultiplier(float multiplier)
        {
            if (Instance == null) { WarnMissingInstance(); return; }
            Instance.timeMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>
        /// Tambah satu alasan pause. Setiap PushPause WAJIB dipasangkan dengan
        /// PopPause memakai string alasan yang sama.
        /// </summary>
        public static void PushPause(string reason)
        {
            if (Instance == null) { WarnMissingInstance(); return; }
            Instance.PushPauseInternal(reason);
        }

        /// <summary>Lepas satu alasan pause yang sebelumnya di-push.</summary>
        public static void PopPause(string reason)
        {
            if (Instance == null) { WarnMissingInstance(); return; }
            Instance.PopPauseInternal(reason);
        }

        /// <summary>Buang semua alasan pause sekaligus (dipakai saat restart / ganti hari).</summary>
        public static void ClearPauses()
        {
            if (Instance == null) return;
            Instance._pauseReasons.Clear();
            Instance._pauseCount = 0;
        }

        /// <summary>Daftar alasan pause yang sedang aktif — untuk debug key F1.</summary>
        public static string DescribePauses()
        {
            if (Instance == null || Instance._pauseCount == 0) return "-";

            var sb = new System.Text.StringBuilder();
            foreach (var kv in Instance._pauseReasons)
            {
                if (kv.Value <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(kv.Key).Append(" x").Append(kv.Value);
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        // ---- Instance ----------------------------------------------------

        public float CurrentDelta => _pauseCount > 0 ? 0f : Time.deltaTime * timeMultiplier;

        void Awake() => Initialize();

        /// <summary>
        /// Idempoten: dipanggil dari Awake sendiri maupun dari GameBootstrap, yang
        /// mana pun yang jalan duluan.
        /// </summary>
        public void Initialize()
        {
            if (_initialized && Instance == this) return;

            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameClock] Sudah ada instance di '{Instance.name}'. Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
            timeMultiplier = Mathf.Max(0f, timeMultiplier);
            _pauseReasons.Clear();
            _pauseCount = 0;
            _elapsed = 0f;
            _initialized = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            _elapsed += CurrentDelta;
        }

        void PushPauseInternal(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                Debug.LogWarning("[GameClock] PushPause dipanggil tanpa alasan. Pause diabaikan.", this);
                return;
            }

            _pauseReasons.TryGetValue(reason, out int count);
            _pauseReasons[reason] = count + 1;
            _pauseCount++;

            if (logPauseChanges)
                Debug.Log($"[GameClock] PushPause('{reason}') -> total {_pauseCount}", this);
        }

        void PopPauseInternal(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return;

            if (!_pauseReasons.TryGetValue(reason, out int count) || count <= 0)
            {
                Debug.LogWarning($"[GameClock] PopPause('{reason}') tanpa PushPause yang cocok. Diabaikan.", this);
                return;
            }

            count--;
            if (count <= 0) _pauseReasons.Remove(reason);
            else _pauseReasons[reason] = count;

            _pauseCount = Mathf.Max(0, _pauseCount - 1);

            if (logPauseChanges)
                Debug.Log($"[GameClock] PopPause('{reason}') -> total {_pauseCount}", this);
        }

        static void WarnMissingInstance()
        {
            if (_missingInstanceWarned) return;
            _missingInstanceWarned = true;
            Debug.LogWarning("[GameClock] Belum ada di scene. Jalankan Tools > MBG > Setup Systems Object. " +
                             "Sementara ini waktu memakai Time.deltaTime apa adanya.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Instance = null;
            _missingInstanceWarned = false;
        }
    }
}
