using System;
using MBG.Catering;
using MBG.Kitchen;
using MBG.QTE;
using UnityEngine;

namespace MBG.Core
{
    /// <summary>
    /// Titik komunikasi tunggal antar sistem. Sistem penerbit memanggil Raise*(),
    /// sistem pendengar subscribe ke event-nya — tidak ada yang menyimpan referensi
    /// langsung ke sistem lain.
    ///
    /// Karena semua event di sini static, subscriber WAJIB unsubscribe di
    /// OnDisable/OnDestroy. <see cref="ClearAll"/> adalah jaring pengaman terakhir:
    /// dipanggil otomatis sebelum scene pertama dimuat (juga saat "Enter Play Mode
    /// Options" mematikan domain reload) dan oleh <see cref="GameManager.RestartGame"/>,
    /// supaya tidak ada subscriber hantu dari sesi Play sebelumnya.
    /// </summary>
    public static class GameEventBus
    {
        // ---- Game state -------------------------------------------------
        public static event Action<GameState, GameState> OnGameStateChanged;

        // ---- Order ------------------------------------------------------
        public static event Action<OrderRuntime> OnOrderStarted;
        public static event Action<int, int> OnOrderProgress;
        public static event Action<OrderResult> OnOrderCompleted;
        public static event Action<OrderRuntime> OnOrderFailed;

        /// <summary>
        /// Pemain menutup layar hasil pesanan. Ini sinyal bagi sistem catering
        /// untuk melanjutkan ke pesanan berikutnya di antrian hari itu.
        /// </summary>
        public static event Action OnResultAcknowledged;

        // ---- Kitchen ----------------------------------------------------
        // Catatan: ini satu-satunya tempat MBG.Core menyebut tipe dari MBG.Kitchen.
        // Aman selama semuanya masih di assembly Assembly-CSharp. TODO: kalau nanti
        // dibuat asmdef per-namespace, pindahkan StationType ke MBG.Core agar arah
        // dependensinya tidak terbalik.
        public static event Action<StationType> OnStationUsed;

        // ---- QTE --------------------------------------------------------

        /// <summary>Satu input QTE dinilai. Dipakai UI untuk umpan balik per hit.</summary>
        public static event Action<QTEGrade> OnQTEHit;

        /// <summary>Satu sesi QTE selesai — nilai akhirnya (terburuk dari semua hit).</summary>
        public static event Action<QTEGrade> OnQTEResult;

        // ---- Obstacle ---------------------------------------------------
        public static event Action<ObstacleType> OnObstacleStarted;
        public static event Action<ObstacleType, bool> OnObstacleResolved;

        // ---- Economy ----------------------------------------------------
        public static event Action<int> OnGoldChanged;
        public static event Action<int> OnScoreChanged;
        public static event Action<int> OnReputationChanged;

        // ---- Day flow ---------------------------------------------------
        public static event Action<int> OnDayStarted;
        public static event Action<int> OnDayCompleted;
        public static event Action<GameOverReason> OnGameOver;

        // ---- Raisers ----------------------------------------------------
        // Event static hanya bisa di-invoke dari dalam kelas ini, jadi setiap
        // event punya satu method Raise yang dipakai sistem penerbit.

        public static void RaiseGameStateChanged(GameState prev, GameState next)
            => OnGameStateChanged?.Invoke(prev, next);

        public static void RaiseOrderStarted(OrderRuntime order)
            => OnOrderStarted?.Invoke(order);

        public static void RaiseOrderProgress(int done, int total)
            => OnOrderProgress?.Invoke(done, total);

        public static void RaiseOrderCompleted(OrderResult result)
            => OnOrderCompleted?.Invoke(result);

        public static void RaiseOrderFailed(OrderRuntime order)
            => OnOrderFailed?.Invoke(order);

        public static void RaiseResultAcknowledged()
            => OnResultAcknowledged?.Invoke();

        public static void RaiseStationUsed(StationType type)
            => OnStationUsed?.Invoke(type);

        public static void RaiseQTEHit(QTEGrade grade)
            => OnQTEHit?.Invoke(grade);

        public static void RaiseQTEResult(QTEGrade grade)
            => OnQTEResult?.Invoke(grade);

        public static void RaiseObstacleStarted(ObstacleType type)
            => OnObstacleStarted?.Invoke(type);

        public static void RaiseObstacleResolved(ObstacleType type, bool success)
            => OnObstacleResolved?.Invoke(type, success);

        public static void RaiseGoldChanged(int value)
            => OnGoldChanged?.Invoke(value);

        public static void RaiseScoreChanged(int value)
            => OnScoreChanged?.Invoke(value);

        public static void RaiseReputationChanged(int value)
            => OnReputationChanged?.Invoke(value);

        public static void RaiseDayStarted(int day)
            => OnDayStarted?.Invoke(day);

        public static void RaiseDayCompleted(int day)
            => OnDayCompleted?.Invoke(day);

        public static void RaiseGameOver(GameOverReason reason)
            => OnGameOver?.Invoke(reason);

        // ---- Lifecycle --------------------------------------------------

        /// <summary>
        /// Lepas semua subscriber. Dipanggil sebelum scene reload / restart.
        /// </summary>
        public static void ClearAll()
        {
            OnGameStateChanged = null;

            OnOrderStarted = null;
            OnOrderProgress = null;
            OnOrderCompleted = null;
            OnOrderFailed = null;
            OnResultAcknowledged = null;

            OnStationUsed = null;

            OnQTEHit = null;
            OnQTEResult = null;

            OnObstacleStarted = null;
            OnObstacleResolved = null;

            OnGoldChanged = null;
            OnScoreChanged = null;
            OnReputationChanged = null;

            OnDayStarted = null;
            OnDayCompleted = null;
            OnGameOver = null;
        }

        /// <summary>
        /// Dijalankan sebelum Awake apapun di scene pertama. Ini yang membuat bus
        /// tetap bersih walau domain reload dimatikan — dan alasan kenapa
        /// GameBootstrap TIDAK boleh memanggil ClearAll() di Awake-nya (itu akan
        /// menghapus subscriber yang baru saja mendaftar di frame yang sama).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => ClearAll();
    }
}
