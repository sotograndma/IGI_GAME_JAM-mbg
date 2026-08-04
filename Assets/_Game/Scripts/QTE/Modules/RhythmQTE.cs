using System;
using System.Collections.Generic;
using MBG.Core;
using MBG.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MBG.QTE
{
    /// <summary>Pengaturan satu sesi ritme, diisi pemanggil sebelum sesi dimulai.</summary>
    public struct RhythmSettings
    {
        public RhythmPatternSO pattern;
        public float perfectWindow;
        public float goodWindow;
        public float missWindow;
        public float perfectValue;
        public float goodValue;
        public float missValue;
        public float accuracyThreshold;

        public static RhythmSettings Default => new RhythmSettings
        {
            pattern = null,
            perfectWindow = 0.09f,
            goodWindow = 0.2f,
            missWindow = 0.28f,
            perfectValue = 1f,
            goodValue = 0.7f,
            missValue = 0f,
            accuracyThreshold = 0.6f
        };
    }

    /// <summary>
    /// Mekanik ritme sederhana ala osu: ring luar mengecil menuju ring dalam, dan
    /// pemain menekan Spasi (atau klik) tepat saat keduanya bertemu.
    ///
    /// Pola murni berbasis waktu — TIDAK disinkronkan ke audio track apa pun, jadi
    /// mengganti musik tidak akan merusak satu pun pola.
    ///
    /// Sesi dinilai dari rata-rata akurasi seluruh not; di atas ambang berarti
    /// berhasil. Angkanya datang dari config gangguan lewat <see cref="Configure"/>,
    /// bukan dari QTEConfigSO.
    /// </summary>
    public class RhythmQTE : IQTEModule, IRhythmReadout
    {
        class ActiveNote
        {
            public RhythmNote data;
            public bool judged;
        }

        public QTEType Type => QTEType.Rhythm;
        public bool IsRunning => _running;

        public event Action<QTEResult> OnFinished;

        // ---- IRhythmReadout -------------------------------------------------
        public IReadOnlyList<RhythmNoteView> ActiveNotes => _views;
        public float Accuracy01 => _judgedCount > 0 ? _accuracySum / _judgedCount : 0f;
        public int NotesJudged => _judgedCount;
        public int TotalNotes => _notes.Count;

        RhythmSettings _settings = RhythmSettings.Default;

        readonly List<ActiveNote> _notes = new();
        readonly List<RhythmNoteView> _views = new();

        bool _running;
        float _time;
        float _duration;
        int _judgedCount;
        int _perfectCount;
        int _pressCount;
        float _accuracySum;
        QTEGrade _worstGrade;

        public void Configure(RhythmSettings settings)
        {
            _settings = settings;

            if (_settings.goodWindow <= _settings.perfectWindow)
                _settings.goodWindow = _settings.perfectWindow * 2f;

            if (_settings.missWindow <= _settings.goodWindow)
                _settings.missWindow = _settings.goodWindow * 1.4f;
        }

        public void StartQTE(QTERequest request)
        {
            _notes.Clear();
            _views.Clear();

            RhythmPatternSO pattern = _settings.pattern;
            if (pattern == null || pattern.NoteCount == 0)
            {
                Debug.LogError("[RhythmQTE] Pola ritme kosong — sesi dibatalkan.");
                _running = false;
                OnFinished?.Invoke(new QTEResult(QTEGrade.CriticalMiss, 0f, 0, 0));
                return;
            }

            foreach (RhythmNote note in pattern.GetSortedNotes())
                _notes.Add(new ActiveNote { data = note });

            _duration = pattern.GetDuration(_settings.missWindow + 0.5f);
            _time = 0f;
            _judgedCount = 0;
            _perfectCount = 0;
            _pressCount = 0;
            _accuracySum = 0f;
            _worstGrade = QTEGrade.Perfect;
            _running = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_running) return;

            _time += deltaTime;

            if (WasPressed()) JudgeNearest();

            AutoMissPassedNotes();
            RebuildViews();

            if (_judgedCount >= _notes.Count || _time >= _duration) Finish();
        }

        public void Cancel()
        {
            _running = false;
            _views.Clear();
        }

        // ---- Input --------------------------------------------------------------

        static bool WasPressed()
        {
            if (InputService.ConfirmPressed) return true;

            // Klik kiri diterima juga — mekanik ini memang terasa alami dengan mouse.
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        void JudgeNearest()
        {
            _pressCount++;

            ActiveNote nearest = null;
            float nearestDelta = float.MaxValue;

            foreach (ActiveNote note in _notes)
            {
                if (note.judged) continue;

                float delta = Mathf.Abs(_time - note.data.time);
                if (delta >= nearestDelta) continue;

                nearestDelta = delta;
                nearest = note;
            }

            // Tekanan yang jauh dari not mana pun diabaikan saja — pemain yang
            // menekan terlalu awal tidak langsung dihukum.
            if (nearest == null || nearestDelta > _settings.missWindow) return;

            QTEGrade grade = nearestDelta <= _settings.perfectWindow ? QTEGrade.Perfect
                : nearestDelta <= _settings.goodWindow ? QTEGrade.Good
                : QTEGrade.Miss;

            Judge(nearest, grade);
        }

        void AutoMissPassedNotes()
        {
            foreach (ActiveNote note in _notes)
            {
                if (note.judged) continue;
                if (_time - note.data.time <= _settings.missWindow) continue;

                Judge(note, QTEGrade.Miss);
            }
        }

        void Judge(ActiveNote note, QTEGrade grade)
        {
            note.judged = true;
            _judgedCount++;

            _accuracySum += grade == QTEGrade.Perfect ? _settings.perfectValue
                : grade == QTEGrade.Good ? _settings.goodValue
                : _settings.missValue;

            if (grade == QTEGrade.Perfect) _perfectCount++;
            _worstGrade = (QTEGrade)Mathf.Max((int)_worstGrade, (int)grade);

            GameEventBus.RaiseQTEHit(grade);
            AudioService.PlaySFX(grade == QTEGrade.Perfect ? SfxId.QtePerfect
                : grade == QTEGrade.Good ? SfxId.QteGood
                : SfxId.QteMiss);
        }

        // ---- Tampilan --------------------------------------------------------------

        void RebuildViews()
        {
            _views.Clear();

            foreach (ActiveNote note in _notes)
            {
                if (note.judged) continue;

                float approachDuration = Mathf.Max(0.15f, note.data.approachDuration);
                float remaining = note.data.time - _time;

                // Belum waktunya terlihat.
                if (remaining > approachDuration) continue;

                _views.Add(new RhythmNoteView
                {
                    position01 = note.data.normalizedPosition,
                    approach01 = remaining / approachDuration
                });
            }
        }

        void Finish()
        {
            _running = false;
            _views.Clear();

            float accuracy = Accuracy01;
            bool success = accuracy >= _settings.accuracyThreshold;

            // Grade sesi mengikuti hasil akhir, bukan not terburuk: satu meleset di
            // tengah tidak boleh membatalkan sesi yang secara keseluruhan rapi.
            QTEGrade grade = success
                ? (accuracy >= 0.9f ? QTEGrade.Perfect : QTEGrade.Good)
                : QTEGrade.Miss;

            Debug.Log($"[RhythmQTE] Selesai — akurasi {accuracy:0.00} " +
                      $"(ambang {_settings.accuracyThreshold:0.00}), {_perfectCount} perfect dari {_notes.Count} not.");

            OnFinished?.Invoke(new QTEResult(grade, accuracy, _perfectCount, _pressCount));
        }
    }
}
