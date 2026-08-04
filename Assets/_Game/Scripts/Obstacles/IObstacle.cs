using System;

namespace MBG.Obstacles
{
    /// <summary>
    /// Satu gangguan yang bisa menginterupsi memasak.
    ///
    /// Sama seperti IQTEModule, gangguan tidak punya MonoBehaviour dan tidak
    /// membaca waktu sendiri: <see cref="ObstacleManager"/> yang men-tick-nya,
    /// sehingga jeda permainan otomatis ikut menghentikannya.
    /// </summary>
    public interface IObstacle
    {
        ObstacleType Type { get; }

        /// <summary>0 = baru mulai, 1 = konsekuensi buruk terjadi sekarang.</summary>
        float UrgencyNormalized { get; }

        bool IsActive { get; }

        /// <summary>Dipancarkan sekali saat gangguan selesai; true kalau pemain berhasil.</summary>
        event Action<bool> OnResolved;

        /// <summary>Mulai gangguan. <paramref name="difficulty"/> 0..1 dari jadwal hari.</summary>
        void Begin(float difficulty);

        /// <summary>Maju satu frame.</summary>
        void Tick(float deltaTime);

        /// <summary>Akhiri gangguan, berhasil maupun tidak.</summary>
        void Resolve(bool success);
    }
}
