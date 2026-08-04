using System;
using MBG.Core;
using MBG.Data;
using MBG.QTE;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Pungutan liar berkedok pajak. Ketukannya sopan dan sabar — urgency-nya
    /// mengisi jauh lebih lambat daripada gedoran ormas — tapi mengabaikannya mahal.
    ///
    /// Diabaikan sampai penuh: usaha "disegel sementara", gold turun besar,
    /// reputasi turun sedikit.
    ///
    /// Dilayani: pemain keluar, petugas muncul, menyampaikan tuntutannya, lalu
    /// pemain bisa menantangnya dengan menunjukkan dokumen resmi — sebuah
    /// TypingChallenge berisi kutipan peraturan fiktif yang harus diketik persis.
    /// </summary>
    public class IllegalTaxObstacle : IObstacle
    {
        enum Phase { Idle, Knocking, Outside, Talking, Typing, Closing, Done }

        public ObstacleType Type => ObstacleType.IllegalTax;

        public float UrgencyNormalized => _meter;

        public bool IsActive => _phase != Phase.Idle && _phase != Phase.Done;

        public event Action<bool> OnResolved;

        readonly IllegalTaxConfigSO _config;

        Phase _phase = Phase.Idle;
        float _meter;
        float _duration;
        float _difficulty;
        bool _subscribed;
        bool _collectorSpawned;

        public IllegalTaxObstacle(IllegalTaxConfigSO config)
        {
            _config = config != null ? config : IllegalTaxConfigSO.Fallback;
        }

        IllegalTaxConfigSO Config => _config;

        public void Begin(float difficulty)
        {
            _difficulty = Mathf.Clamp01(difficulty);
            _duration = Config.GetSealDuration(_difficulty);
            _meter = 0f;
            _collectorSpawned = false;
            _phase = Phase.Knocking;

            TaxPresenter presenter = TaxPresenter.Instance;
            if (presenter != null)
            {
                presenter.OnMet += HandleMet;
                _subscribed = true;
            }
            else
            {
                Debug.LogWarning("[Pungli] TaxPresenter tidak ada — petugas tidak bisa dimunculkan. " +
                                 "Jalankan Tools > MBG > Build Tax Setup.");
            }

            Debug.Log($"[Pungli] Ketukan dimulai — {_duration:0.0}s sebelum usaha disegel.");
        }

        public void Tick(float deltaTime)
        {
            if (_phase != Phase.Knocking && _phase != Phase.Outside) return;

            if (IsPlayerOutside())
            {
                if (_phase != Phase.Outside)
                {
                    _phase = Phase.Outside;
                    Debug.Log("[Pungli] Pemain keluar — ketukan berhenti, petugas menunggu.");
                }

                // Petugas baru muncul begitu pemain benar-benar keluar menemuinya.
                if (!_collectorSpawned)
                {
                    TaxPresenter presenter = TaxPresenter.Instance;
                    if (presenter != null) presenter.SpawnCollector();

                    GameClock.SetMultiplier(Config.outsideTimeMultiplier);
                    _collectorSpawned = true;
                }

                return;
            }

            if (_phase == Phase.Outside) _phase = Phase.Knocking;

            _meter = _duration > 0f ? Mathf.Clamp01(_meter + deltaTime / _duration) : 1f;

            if (_meter >= 1f) SealBusiness();
        }

        public void Resolve(bool success)
        {
            if (!IsActive) return;

            _phase = Phase.Done;
            Unsubscribe();

            TaxPresenter presenter = TaxPresenter.Instance;
            if (presenter != null)
            {
                presenter.HideDialogue();
                presenter.DespawnCollector();
            }

            FreezePlayer(false);

            // Jaring pengaman terakhir: apa pun jalan keluarnya, mode teks harus mati.
            InputService.TextInputMode = false;

            OnResolved?.Invoke(success);
        }

        // ---- Diabaikan ----------------------------------------------------------------

        void SealBusiness()
        {
            IllegalTaxConfigSO cfg = Config;

            EconomyService economy = EconomyService.Instance;
            if (economy != null) economy.AddGold(-cfg.sealGoldPenalty);

            if (ReputationService.Instance != null)
                ReputationService.Instance.ApplyIgnoredPenalty(ObstacleType.IllegalTax);

            Debug.Log($"[Pungli] Usaha disegel sementara — -{cfg.sealGoldPenalty} gold.");
            AudioService.PlaySFX(SfxId.DoorBang);

            Resolve(false);
        }

        // ---- Dialog & tantangan -----------------------------------------------------------

        void HandleMet()
        {
            if (_phase != Phase.Outside) return;

            _phase = Phase.Talking;
            FreezePlayer(true);

            TaxPresenter presenter = TaxPresenter.Instance;
            if (presenter == null)
            {
                BeginTyping();
                return;
            }

            presenter.PlayDemands(BeginTyping);
        }

        void BeginTyping()
        {
            if (_phase != Phase.Talking) return;

            QTEController qte = QTEController.Instance;
            TypingChallenge typing = qte != null ? qte.GetModule<TypingChallenge>() : null;

            if (qte == null || typing == null)
            {
                Debug.LogError("[Pungli] TypingChallenge belum terdaftar di QTEController.");
                Resolve(false);
                return;
            }

            IllegalTaxConfigSO cfg = Config;
            TypingChallengeSO source = cfg.challenges;

            if (source == null || !source.HasAnyText)
            {
                Debug.LogError("[Pungli] TypingChallengeSO kosong — tantangan dibatalkan.");
                Resolve(false);
                return;
            }

            typing.Configure(new TypingSettings
            {
                targetText = source.GetText(_difficulty),
                timeLimit = cfg.typingTimeLimit,
                perfectAccuracy = cfg.perfectAccuracy,
                goodAccuracy = cfg.goodAccuracy
            });

            _phase = Phase.Typing;

            // Batas waktu mengetik dihitung dalam detik sungguhan.
            GameClock.SetMultiplier(1f);

            qte.Begin(new QTERequest(QTEType.Typing, null, "Ketik ulang persis!", HandleTypingResult));
        }

        void HandleTypingResult(QTEResult result)
        {
            if (_phase != Phase.Typing) return;

            _phase = Phase.Closing;

            IllegalTaxConfigSO cfg = Config;
            TaxDialogueSO dialogue = cfg.dialogue;
            EconomyService economy = EconomyService.Instance;
            TaxPresenter presenter = TaxPresenter.Instance;

            string closing;
            bool success;

            switch (result.grade)
            {
                case QTEGrade.Perfect:
                case QTEGrade.Good:
                    // Reputasi ditambahkan ReputationService lewat OnObstacleResolved.
                    closing = dialogue != null
                        ? (result.grade == QTEGrade.Perfect ? dialogue.GetClosingPerfect() : dialogue.GetClosingGood())
                        : "";
                    success = true;

                    Debug.Log("[Pungli] Pemeras kabur.");
                    AudioService.PlaySFX(SfxId.OrderComplete);
                    break;

                case QTEGrade.Miss:
                    // Selesai tapi berantakan: bayar sebagian, reputasi aman.
                    if (economy != null) economy.AddGold(-cfg.partialGoldPenalty);

                    closing = dialogue != null ? dialogue.GetClosingPartial() : "";
                    success = false;

                    Debug.Log($"[Pungli] Ketikan berantakan — bayar sebagian, -{cfg.partialGoldPenalty} gold.");
                    break;

                default:
                    // Tidak selesai berarti tidak melawan sama sekali, jadi tarifnya
                    // sama dengan gangguan yang diabaikan.
                    if (economy != null) economy.AddGold(-cfg.fullGoldPenalty);

                    if (ReputationService.Instance != null)
                        ReputationService.Instance.ApplyIgnoredPenalty(ObstacleType.IllegalTax);

                    closing = dialogue != null ? dialogue.GetClosingFail() : "";
                    success = false;

                    Debug.Log($"[Pungli] Menyerah — bayar penuh, -{cfg.fullGoldPenalty} gold.");
                    break;
            }

            if (presenter == null || string.IsNullOrWhiteSpace(closing))
            {
                Resolve(success);
                return;
            }

            // QTEController sudah melepas pembekuan saat sesinya selesai; bekukan
            // lagi supaya pemain tidak berjalan pergi di tengah kalimat penutup.
            FreezePlayer(true);
            presenter.PlayClosing(closing, () => Resolve(success));
        }

        // ---- Util ---------------------------------------------------------------------------

        static bool IsPlayerOutside()
        {
            AreaManager area = AreaManager.Instance;
            return area != null && area.Current == AreaManager.Area.Exterior;
        }

        static void FreezePlayer(bool frozen)
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);
            if (player != null) player.SetFrozen(frozen);
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;

            TaxPresenter presenter = TaxPresenter.Instance;
            if (presenter != null) presenter.OnMet -= HandleMet;

            _subscribed = false;
        }
    }
}
