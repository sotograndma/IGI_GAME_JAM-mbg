using System;
using MBG.Core;
using UnityEngine;

namespace MBG.QTE
{
    /// <summary>
    /// Skill check ala Dead by Daylight: indikator berjalan dari kiri ke kanan,
    /// pemain menekan Space saat indikator berada di dalam zona.
    ///
    /// Zona Perfect selalu berada di tengah zona Good, jadi menekan "agak meleset"
    /// dari Perfect masih menghasilkan Good — bukan langsung Miss.
    ///
    /// Mode multi-hit: sesi terdiri dari <c>hitCount</c> lintasan berturut-turut,
    /// zona diacak ulang tiap lintasan. Satu CriticalMiss (indikator sampai ujung
    /// tanpa input) langsung mengakhiri sesi — tidak ada lintasan sisa.
    ///
    /// Plain C# class: tidak ada MonoBehaviour dan tidak ada Time.deltaTime di sini.
    /// </summary>
    public class TimingBarQTE : IQTEModule, ITimingBarReadout
    {
        public QTEType Type => QTEType.TimingBar;
        public bool IsRunning => _running;

        public event Action<QTEResult> OnFinished;

        // ---- ITimingBarReadout -------------------------------------------
        public float Cursor01 { get; private set; }
        public float GoodMin { get; private set; }
        public float GoodMax { get; private set; }
        public float PerfectMin { get; private set; }
        public float PerfectMax { get; private set; }
        public int CurrentHit => Mathf.Min(_hitIndex + 1, _totalHits);
        public int TotalHits => _totalHits;

        QTEConfigSO _config;
        bool _running;
        float _elapsed;

        int _hitIndex;
        int _totalHits;
        int _perfectCount;
        int _inputCount;
        int _judgedCount;
        float _accuracySum;
        QTEGrade _worstGrade;

        public void StartQTE(QTERequest request)
        {
            _config = request.config;
            if (_config == null)
            {
                Debug.LogError("[TimingBarQTE] QTERequest tanpa QTEConfigSO — sesi dibatalkan.");
                _running = false;
                OnFinished?.Invoke(QTEResult.Failed());
                return;
            }

            _totalHits = _config.ClampedHitCount;
            _hitIndex = 0;
            _perfectCount = 0;
            _inputCount = 0;
            _judgedCount = 0;
            _accuracySum = 0f;
            _worstGrade = QTEGrade.Perfect;
            _running = true;

            BeginHit();
        }

        public void Tick(float deltaTime)
        {
            if (!_running || _config == null) return;

            float duration = _config.EffectiveDuration;
            _elapsed += deltaTime;
            Cursor01 = duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / duration);

            if (InputService.ConfirmPressed)
            {
                Judge(Evaluate(Cursor01), countsAsInput: true);
                return;
            }

            if (_elapsed >= duration)
                Judge(QTEGrade.CriticalMiss, countsAsInput: false);
        }

        public void Cancel()
        {
            _running = false;
            _config = null;
        }

        // ---- Internal -----------------------------------------------------

        void BeginHit()
        {
            _elapsed = 0f;
            Cursor01 = 0f;
            PlaceZones();
        }

        /// <summary>
        /// Tempatkan zona untuk lintasan berikutnya. Perfect selalu di tengah Good,
        /// dan Good tidak pernah keluar dari bar.
        /// </summary>
        void PlaceZones()
        {
            float good = _config.ClampedGoodWidth;
            float perfect = _config.ClampedPerfectWidth;

            float start = _config.randomizeZonePosition
                ? UnityEngine.Random.Range(0f, 1f - good)
                : 0.5f - good * 0.5f;

            GoodMin = start;
            GoodMax = start + good;

            float center = start + good * 0.5f;
            PerfectMin = center - perfect * 0.5f;
            PerfectMax = center + perfect * 0.5f;
        }

        QTEGrade Evaluate(float cursor)
        {
            if (cursor >= PerfectMin && cursor <= PerfectMax) return QTEGrade.Perfect;
            if (cursor >= GoodMin && cursor <= GoodMax) return QTEGrade.Good;
            return QTEGrade.Miss;
        }

        void Judge(QTEGrade grade, bool countsAsInput)
        {
            if (countsAsInput) _inputCount++;
            if (grade == QTEGrade.Perfect) _perfectCount++;

            _judgedCount++;
            _accuracySum += ComputeAccuracy(Cursor01);
            _worstGrade = (QTEGrade)Mathf.Max((int)_worstGrade, (int)grade);

            GameEventBus.RaiseQTEHit(grade);
            AudioService.PlaySFX(GetHitSfx(grade));

            _hitIndex++;

            // CriticalMiss mengakhiri sesi seketika: tidak ada gunanya melanjutkan
            // lintasan sisa kalau pemain sudah kehilangan satu sepenuhnya.
            if (grade == QTEGrade.CriticalMiss || _hitIndex >= _totalHits) Finish();
            else BeginHit();
        }

        /// <summary>1 = tepat di tengah zona Perfect, 0 = sejauh mungkin darinya.</summary>
        float ComputeAccuracy(float cursor)
        {
            float center = (PerfectMin + PerfectMax) * 0.5f;
            return 1f - Mathf.Clamp01(Mathf.Abs(cursor - center) / 0.5f);
        }

        static SfxId GetHitSfx(QTEGrade grade)
        {
            switch (grade)
            {
                case QTEGrade.Perfect: return SfxId.QtePerfect;
                case QTEGrade.Good: return SfxId.QteGood;
                default: return SfxId.QteMiss;
            }
        }

        void Finish()
        {
            _running = false;

            float accuracy = _judgedCount > 0 ? _accuracySum / _judgedCount : 0f;
            var result = new QTEResult(_worstGrade, accuracy, _perfectCount, _inputCount);

            OnFinished?.Invoke(result);
        }
    }
}
