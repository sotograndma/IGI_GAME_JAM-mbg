using System;
using MBG.Data;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// STUB. Gangguan yang untuk sementara hanya menaikkan urgency dari 0 ke 1
    /// seiring waktu; begitu penuh, konsekuensinya jatuh (Resolve dengan gagal).
    ///
    /// Yang dibutuhkan prompt-prompt berikutnya tinggal mengganti isi
    /// <see cref="Tick"/> dan menambahkan cara pemain menyelesaikannya — kontrak
    /// <see cref="IObstacle"/> dan seluruh sistem di sekitarnya sudah final.
    /// </summary>
    public abstract class TimedObstacleStub : IObstacle
    {
        public abstract ObstacleType Type { get; }

        public float UrgencyNormalized { get; private set; }
        public bool IsActive { get; private set; }

        public event Action<bool> OnResolved;

        readonly ObstacleConfigSO _config;

        float _elapsed;
        float _duration;

        protected TimedObstacleStub(ObstacleConfigSO config)
        {
            _config = config != null ? config : ObstacleConfigSO.Fallback;
        }

        protected ObstacleConfigSO Config => _config;

        /// <summary>Berapa detik lagi sebelum konsekuensi jatuh.</summary>
        public float TimeRemaining => Mathf.Max(0f, _duration - _elapsed);

        public void Begin(float difficulty)
        {
            _duration = _config.GetDuration(Type, difficulty);
            _elapsed = 0f;
            UrgencyNormalized = 0f;
            IsActive = true;

            Debug.Log($"[Obstacle] {Type.GetLabel()} dimulai — {_duration:0.0}s sampai konsekuensi " +
                      $"(difficulty {difficulty:0.00}).");
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive) return;

            _elapsed += deltaTime;
            UrgencyNormalized = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;

            if (UrgencyNormalized < 1f) return;

            // TODO: konsekuensi sesungguhnya (kehilangan gold, reputasi, pesanan
            // rusak) dipasang di prompt masing-masing gangguan.
            Debug.Log($"[Obstacle] {Type.GetLabel()}: urgency penuh — konsekuensi jatuh.");
            Resolve(false);
        }

        public void Resolve(bool success)
        {
            if (!IsActive) return;

            IsActive = false;
            UrgencyNormalized = success ? 0f : 1f;

            OnResolved?.Invoke(success);
        }
    }

    /// <summary>STUB Ormas: gedoran keras di pintu.</summary>
    public class OrmasObstacle : TimedObstacleStub
    {
        public OrmasObstacle(ObstacleConfigSO config) : base(config) { }

        public override ObstacleType Type => ObstacleType.Ormas;
    }

    /// <summary>STUB Santet: serangan gaib.</summary>
    public class SantetObstacle : TimedObstacleStub
    {
        public SantetObstacle(ObstacleConfigSO config) : base(config) { }

        public override ObstacleType Type => ObstacleType.Santet;
    }

    /// <summary>STUB Pajak Ilegal: ketukan sopan yang menagih.</summary>
    public class IllegalTaxObstacle : TimedObstacleStub
    {
        public IllegalTaxObstacle(ObstacleConfigSO config) : base(config) { }

        public override ObstacleType Type => ObstacleType.IllegalTax;
    }
}
