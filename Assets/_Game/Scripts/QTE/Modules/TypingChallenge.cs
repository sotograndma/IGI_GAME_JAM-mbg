using System;
using System.Text;
using MBG.Core;
using UnityEngine;

namespace MBG.QTE
{
    /// <summary>Pengaturan satu sesi mengetik, diisi pemanggil sebelum sesi dimulai.</summary>
    public struct TypingSettings
    {
        public string targetText;
        public float timeLimit;
        public float perfectAccuracy;
        public float goodAccuracy;

        public static TypingSettings Default => new TypingSettings
        {
            targetText = "",
            timeLimit = 45f,
            perfectAccuracy = 0.95f,
            goodAccuracy = 0.8f
        };
    }

    /// <summary>
    /// Ketik ulang kutipan peraturan persis seperti tertulis, sebelum waktu habis.
    ///
    /// Selama sesi berjalan <see cref="InputService.TextInputMode"/> dinyalakan,
    /// sehingga gerakan pemain dan tombol aksi mati — huruf yang diketik tidak akan
    /// membuat karakter berjalan atau membuka pintu.
    ///
    /// PENTING: TextInputMode dimatikan lagi di SEMUA jalur keluar — selesai,
    /// kehabisan waktu, menekan Escape, maupun dibatalkan dari luar lewat
    /// <see cref="Cancel"/>. QTEController juga mematikannya sekali lagi sebagai
    /// jaring pengaman, karena pemain yang terkunci tidak bisa berjalan sama sekali.
    /// </summary>
    public class TypingChallenge : IQTEModule, ITypingReadout
    {
        public QTEType Type => QTEType.Typing;
        public bool IsRunning => _running;

        public event Action<QTEResult> OnFinished;

        // ---- ITypingReadout ---------------------------------------------------
        public string TargetText => _settings.targetText ?? "";
        public string TypedText => _typed.ToString();
        public float TimeRemaining => Mathf.Max(0f, _timeLeft);
        public float TimeRemaining01 => _settings.timeLimit > 0f ? Mathf.Clamp01(_timeLeft / _settings.timeLimit) : 0f;

        public float Accuracy01
            => _totalKeystrokes > 0 ? Mathf.Clamp01(1f - _errorKeystrokes / (float)_totalKeystrokes) : 1f;

        public float WordsPerMinute
        {
            get
            {
                if (_elapsed <= 0.01f) return 0f;
                return _typed.Length / 5f / (_elapsed / 60f);
            }
        }

        TypingSettings _settings = TypingSettings.Default;

        readonly StringBuilder _typed = new();

        bool _running;
        bool _subscribed;
        float _timeLeft;
        float _elapsed;
        int _totalKeystrokes;
        int _errorKeystrokes;

        public void Configure(TypingSettings settings)
        {
            _settings = settings;

            if (_settings.timeLimit <= 0f) _settings.timeLimit = TypingSettings.Default.timeLimit;
            if (_settings.perfectAccuracy <= 0f) _settings.perfectAccuracy = TypingSettings.Default.perfectAccuracy;
            if (_settings.goodAccuracy <= 0f) _settings.goodAccuracy = TypingSettings.Default.goodAccuracy;
        }

        public void StartQTE(QTERequest request)
        {
            if (string.IsNullOrWhiteSpace(_settings.targetText))
            {
                Debug.LogError("[TypingChallenge] Tidak ada teks untuk diketik — sesi dibatalkan.");
                _running = false;
                OnFinished?.Invoke(new QTEResult(QTEGrade.CriticalMiss, 0f, 0, 0));
                return;
            }

            _typed.Clear();
            _timeLeft = _settings.timeLimit;
            _elapsed = 0f;
            _totalKeystrokes = 0;
            _errorKeystrokes = 0;
            _running = true;

            InputService.OnTextInput += HandleTextInput;
            _subscribed = true;

            // Matikan gerakan: mulai sekarang keyboard milik tantangan ini.
            InputService.TextInputMode = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_running) return;

            // Escape menyerah lebih awal — tetap dihitung gagal, tapi pemain tidak
            // terpaksa menunggu timer habis.
            if (InputService.CancelPressed)
            {
                Debug.Log("[TypingChallenge] Dibatalkan pemain.");
                Finish(QTEGrade.CriticalMiss);
                return;
            }

            if (InputService.BackspacePressed && _typed.Length > 0)
                _typed.Length--;

            _elapsed += deltaTime;
            _timeLeft -= deltaTime;

            if (IsComplete())
            {
                Finish(GradeForCompletion());
                return;
            }

            if (_timeLeft <= 0f) Finish(QTEGrade.CriticalMiss);
        }

        public void Cancel()
        {
            if (!_running) { Cleanup(); return; }

            _running = false;
            Cleanup();
        }

        // ---- Internal -------------------------------------------------------------

        void HandleTextInput(char c)
        {
            if (!_running) return;

            string target = TargetText;
            if (_typed.Length >= target.Length) return;

            _totalKeystrokes++;

            // Karakter dihitung salah kalau tidak cocok dengan posisi yang seharusnya.
            // Backspace boleh memperbaiki teksnya, tapi tidak menghapus catatan
            // kesalahannya — itulah yang membedakan cepat-tepat dari cepat-asal.
            if (c != target[_typed.Length]) _errorKeystrokes++;

            _typed.Append(c);
        }

        bool IsComplete()
        {
            string target = TargetText;
            if (_typed.Length != target.Length) return false;

            for (int i = 0; i < target.Length; i++)
            {
                if (_typed[i] != target[i]) return false;
            }

            return true;
        }

        QTEGrade GradeForCompletion()
        {
            float accuracy = Accuracy01;

            if (accuracy >= _settings.perfectAccuracy) return QTEGrade.Perfect;
            if (accuracy >= _settings.goodAccuracy) return QTEGrade.Good;

            // Selesai tapi berantakan: tetap dihitung gagal sebagian, bukan gagal total.
            return QTEGrade.Miss;
        }

        void Finish(QTEGrade grade)
        {
            _running = false;

            float accuracy = Accuracy01;
            Cleanup();

            Debug.Log($"[TypingChallenge] {grade} — akurasi {accuracy:0.00}, " +
                      $"{WordsPerMinute:0} WPM, {_typed.Length}/{TargetText.Length} karakter.");

            GameEventBus.RaiseQTEHit(grade);
            AudioService.PlaySFX(grade == QTEGrade.Perfect ? SfxId.QtePerfect
                : grade == QTEGrade.Good ? SfxId.QteGood
                : SfxId.QteMiss);

            OnFinished?.Invoke(new QTEResult(grade, accuracy, grade == QTEGrade.Perfect ? 1 : 0, _totalKeystrokes));
        }

        /// <summary>Satu-satunya tempat langganan dilepas dan TextInputMode dimatikan.</summary>
        void Cleanup()
        {
            if (_subscribed)
            {
                InputService.OnTextInput -= HandleTextInput;
                _subscribed = false;
            }

            InputService.TextInputMode = false;
        }
    }
}
