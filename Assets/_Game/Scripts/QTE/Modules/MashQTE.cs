using System;
using System.Collections.Generic;
using MBG.Core;
using UnityEngine;

namespace MBG.QTE
{
    /// <summary>Pengaturan satu sesi tarik-menarik, diisi pemanggil sebelum sesi dimulai.</summary>
    public struct MashSettings
    {
        public float pushPerPress;
        public float opponentSpeed;
        public float timeLimit;
        public float startPosition;
        public float tauntInterval;
        public IReadOnlyList<string> taunts;

        public static MashSettings Default => new MashSettings
        {
            pushPerPress = 0.045f,
            opponentSpeed = 0.16f,
            timeLimit = 15f,
            startPosition = 0.5f,
            tauntInterval = 2.2f,
            taunts = null
        };
    }

    /// <summary>
    /// Tarik-menarik: bar mulai di tengah, pemain menekan Spasi berulang untuk
    /// mendorongnya ke kanan sementara lawan mendorong ke kiri dengan kecepatan
    /// tetap.
    ///
    /// Menang kalau bar mencapai ujung kanan sebelum waktu habis. Kalah kalau bar
    /// mencapai ujung kiri, atau waktu habis di posisi mana pun.
    ///
    /// Angkanya TIDAK datang dari QTEConfigSO: mekanik ini dipakai beberapa
    /// gangguan dengan lawan yang berbeda-beda, jadi pemanggil mengisinya lewat
    /// <see cref="Configure"/> dari config gangguan masing-masing.
    /// </summary>
    public class MashQTE : IQTEModule, ITugOfWarReadout
    {
        public QTEType Type => QTEType.Mash;
        public bool IsRunning => _running;

        public event Action<QTEResult> OnFinished;

        // ---- ITugOfWarReadout -------------------------------------------------
        public float Position01 { get; private set; }
        public float TimeRemaining01 => _settings.timeLimit > 0f ? Mathf.Clamp01(_timeLeft / _settings.timeLimit) : 0f;
        public string CurrentTaunt { get; private set; } = "";

        MashSettings _settings = MashSettings.Default;

        bool _running;
        float _timeLeft;
        float _tauntTimer;
        int _tauntIndex = -1;
        int _pressCount;

        /// <summary>Isi angka mekanik sebelum <see cref="StartQTE"/> dipanggil.</summary>
        public void Configure(MashSettings settings)
        {
            _settings = settings;

            if (_settings.pushPerPress <= 0f) _settings.pushPerPress = MashSettings.Default.pushPerPress;
            if (_settings.opponentSpeed <= 0f) _settings.opponentSpeed = MashSettings.Default.opponentSpeed;
            if (_settings.timeLimit <= 0f) _settings.timeLimit = MashSettings.Default.timeLimit;
            if (_settings.tauntInterval <= 0f) _settings.tauntInterval = MashSettings.Default.tauntInterval;
        }

        public void StartQTE(QTERequest request)
        {
            Position01 = Mathf.Clamp01(_settings.startPosition);
            _timeLeft = _settings.timeLimit;
            _tauntTimer = 0f;
            _tauntIndex = -1;
            _pressCount = 0;
            _running = true;

            NextTaunt();
        }

        public void Tick(float deltaTime)
        {
            if (!_running) return;

            if (InputService.ConfirmPressed)
            {
                _pressCount++;
                Position01 = Mathf.Clamp01(Position01 + _settings.pushPerPress);
                AudioService.PlaySFX(SfxId.UiClick);
            }

            Position01 = Mathf.Clamp01(Position01 - _settings.opponentSpeed * deltaTime);

            TickTaunt(deltaTime);

            _timeLeft -= deltaTime;

            if (Position01 >= 1f) { Finish(true); return; }
            if (Position01 <= 0f) { Finish(false); return; }
            if (_timeLeft <= 0f) Finish(false);
        }

        public void Cancel() => _running = false;

        // ---- Internal ----------------------------------------------------------

        void TickTaunt(float deltaTime)
        {
            _tauntTimer += deltaTime;
            if (_tauntTimer < _settings.tauntInterval) return;

            _tauntTimer = 0f;
            NextTaunt();
        }

        void NextTaunt()
        {
            IReadOnlyList<string> taunts = _settings.taunts;
            if (taunts == null || taunts.Count == 0)
            {
                CurrentTaunt = "";
                return;
            }

            // Maju berurutan lalu berputar, supaya tidak ada kalimat yang muncul
            // dua kali berturut-turut.
            _tauntIndex = (_tauntIndex + 1) % taunts.Count;
            CurrentTaunt = taunts[_tauntIndex];
        }

        void Finish(bool won)
        {
            _running = false;

            // Menang dengan sisa waktu banyak dihargai Perfect; menang mepet Good.
            QTEGrade grade = won
                ? (TimeRemaining01 >= 0.5f ? QTEGrade.Perfect : QTEGrade.Good)
                : QTEGrade.Miss;

            GameEventBus.RaiseQTEHit(grade);
            AudioService.PlaySFX(won ? SfxId.QtePerfect : SfxId.QteMiss);

            var result = new QTEResult(grade, Position01, won ? 1 : 0, _pressCount);
            OnFinished?.Invoke(result);
        }
    }
}
