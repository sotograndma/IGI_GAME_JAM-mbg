using System;

namespace MBG.QTE
{
    /// <summary>
    /// Satu mekanik QTE. Module tidak punya MonoBehaviour dan tidak pernah membaca
    /// Time.deltaTime sendiri: <see cref="QTEController"/> yang men-tick-nya dengan
    /// <c>GameClock.DeltaTime</c>, sehingga semua QTE ikut berhenti saat clock
    /// gameplay di-pause.
    /// </summary>
    public interface IQTEModule
    {
        /// <summary>Mekanik yang ditangani module ini.</summary>
        QTEType Type { get; }

        /// <summary>True selama sesi berjalan.</summary>
        bool IsRunning { get; }

        /// <summary>Dipancarkan sekali saat sesi selesai (berhasil maupun gagal).</summary>
        event Action<QTEResult> OnFinished;

        /// <summary>Mulai sesi baru. Module boleh menolak diam-diam kalau config tidak valid.</summary>
        void StartQTE(QTERequest request);

        /// <summary>Maju satu frame. <paramref name="deltaTime"/> selalu waktu gameplay.</summary>
        void Tick(float deltaTime);

        /// <summary>Hentikan sesi tanpa memancarkan <see cref="OnFinished"/>.</summary>
        void Cancel();
    }

    /// <summary>
    /// Data yang dibutuhkan UI untuk menggambar mekanik timing bar. Dipisah dari
    /// <see cref="IQTEModule"/> supaya module lain (rhythm, mash) tidak dipaksa
    /// mengeksposnya, dan supaya panel bisa memeriksanya dengan pattern matching.
    /// </summary>
    public interface ITimingBarReadout
    {
        /// <summary>Posisi indikator sepanjang bar, 0 = kiri, 1 = kanan.</summary>
        float Cursor01 { get; }

        float GoodMin { get; }
        float GoodMax { get; }
        float PerfectMin { get; }
        float PerfectMax { get; }

        /// <summary>Hit ke berapa dalam sesi ini, mulai dari 1.</summary>
        int CurrentHit { get; }

        /// <summary>Total hit dalam sesi ini.</summary>
        int TotalHits { get; }
    }
}
