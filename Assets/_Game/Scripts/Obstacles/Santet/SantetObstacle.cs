using System;
using MBG.Catering;
using MBG.Core;
using MBG.Data;
using MBG.QTE;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Santet tidak mengetuk pintu. Ia menyerang pemain langsung di dapur.
    ///
    /// Fase 1 — serangan: layar memerah dan meredup, pemain dibekukan, lalu muncul
    /// RhythmQTE. Berhasil menahan berarti efeknya hilang, TAPI gangguan belum
    /// selesai.
    ///
    /// Fase 2 — perburuan: muncul objektif "Cari dukun di luar" dengan penunjuk
    /// arah. Pemain keluar, menghampiri dukun, lalu menutupnya dengan satu
    /// TimingBarQTE tingkat Hard.
    ///
    /// Gagal di fase mana pun TIDAK pernah langsung mengakhiri permainan — hanya
    /// reputasi 0 yang bisa. Efek layar selalu dibersihkan supaya pemain bisa
    /// melanjutkan.
    /// </summary>
    public class SantetObstacle : IObstacle
    {
        enum Phase { Idle, Attack, Hunt, Confront, Done }

        public ObstacleType Type => ObstacleType.Santet;
        public bool IsActive => _phase == Phase.Attack || _phase == Phase.Hunt || _phase == Phase.Confront;

        public event Action<bool> OnResolved;

        readonly SantetConfigSO _config;

        Phase _phase = Phase.Idle;
        float _difficulty;
        bool _subscribed;
        IRhythmReadout _rhythmReadout;

        public SantetObstacle(SantetConfigSO config)
        {
            _config = config != null ? config : SantetConfigSO.Fallback;
        }

        SantetConfigSO Config => _config;

        /// <summary>
        /// Selama serangan, urgency mengikuti seberapa buruk permainan ritmenya;
        /// di fase lain nilainya tetap, karena tidak ada hitung mundur menuju
        /// konsekuensi.
        /// </summary>
        public float UrgencyNormalized
        {
            get
            {
                switch (_phase)
                {
                    case Phase.Attack:
                        return _rhythmReadout != null && _rhythmReadout.NotesJudged > 0
                            ? Mathf.Clamp01(1f - _rhythmReadout.Accuracy01)
                            : 0.5f;
                    case Phase.Hunt: return 0.4f;
                    case Phase.Confront: return 0.6f;
                    default: return 0f;
                }
            }
        }

        public void Begin(float difficulty)
        {
            _difficulty = Mathf.Clamp01(difficulty);
            _phase = Phase.Attack;

            SantetPresenter presenter = SantetPresenter.Instance;
            if (presenter != null)
            {
                presenter.OnConfronted += HandleDukunConfronted;
                _subscribed = true;
                presenter.ShowOverlay();
            }
            else
            {
                Debug.LogWarning("[Santet] SantetPresenter tidak ada — efek layar dilewati. " +
                                 "Jalankan Tools > MBG > Build Santet Setup.");
            }

            FreezePlayer(true);

            AudioService.PlaySFX(SfxId.SantetHit);
            Debug.Log($"[Santet] Serangan dimulai — musik dipelankan ke {Config.musicDuckVolume:0.00} " +
                      "(AudioService masih stub, jadi baru dicatat di log).");

            BeginRhythm();
        }

        public void Tick(float deltaTime)
        {
            // Seluruh fase digerakkan oleh QTE dan interaksi pemain, bukan hitung
            // mundur — jadi tidak ada yang perlu dimajukan tiap frame di sini.
        }

        public void Resolve(bool success)
        {
            if (!IsActive) return;

            _phase = Phase.Done;
            _rhythmReadout = null;

            Unsubscribe();

            SantetPresenter presenter = SantetPresenter.Instance;
            if (presenter != null)
            {
                presenter.HideOverlay();
                presenter.HideObjective();
                presenter.DespawnDukun();
            }

            FreezePlayer(false);

            OnResolved?.Invoke(success);
        }

        // ---- Fase 1: ritme ---------------------------------------------------------

        void BeginRhythm()
        {
            QTEController qte = QTEController.Instance;
            if (qte == null)
            {
                Debug.LogError("[Santet] QTEController tidak ada — serangan dilewati.");
                Resolve(false);
                return;
            }

            RhythmQTE rhythm = qte.GetModule<RhythmQTE>();
            if (rhythm == null)
            {
                Debug.LogError("[Santet] RhythmQTE belum terdaftar di QTEController.");
                Resolve(false);
                return;
            }

            SantetConfigSO cfg = Config;

            rhythm.Configure(new RhythmSettings
            {
                pattern = cfg.GetPattern(_difficulty),
                perfectWindow = cfg.perfectWindow,
                goodWindow = cfg.goodWindow,
                missWindow = cfg.missWindow,
                perfectValue = cfg.perfectValue,
                goodValue = cfg.goodValue,
                missValue = cfg.missValue,
                accuracyThreshold = cfg.accuracyThreshold
            });

            _rhythmReadout = rhythm;

            // Pola ritme berbasis waktu absolut, jadi clock harus normal — kalau
            // masih 0.5x, seluruh not datang dua kali lebih lambat dari yang ditulis.
            GameClock.SetMultiplier(1f);

            qte.Begin(new QTERequest(QTEType.Rhythm, null, "Tahan santetnya!", HandleRhythmResult));
        }

        void HandleRhythmResult(QTEResult result)
        {
            if (_phase != Phase.Attack) return;

            _rhythmReadout = null;

            SantetPresenter presenter = SantetPresenter.Instance;

            // Apa pun hasilnya, efek layar hilang dan pemain bebas bergerak lagi.
            if (presenter != null) presenter.HideOverlay();
            FreezePlayer(false);

            if (!result.IsSuccess)
            {
                ApplyRhythmFailure();
                return;
            }

            _phase = Phase.Hunt;
            GameClock.SetMultiplier(Config.phase2TimeMultiplier);

            if (presenter != null)
            {
                presenter.SpawnDukun();
                presenter.ShowObjective();
            }

            Debug.Log($"[Santet] Serangan tertahan (akurasi {result.accuracy:0.00}). " +
                      $"Sekarang: {Config.objectiveText}.");
        }

        void ApplyRhythmFailure()
        {
            SantetConfigSO cfg = Config;

            int portionsLost = 0;
            CateringController catering = CateringController.Instance;
            if (catering != null) portionsLost = catering.DestroyBatches(cfg.failBatchesLost);

            EconomyService economy = EconomyService.Instance;
            if (economy != null)
            {
                economy.AddGold(-cfg.failGoldPenalty);
                economy.AddReputation(-cfg.failReputationPenalty);
            }

            Debug.Log($"[Santet] Santet tembus — {portionsLost} porsi hancur, " +
                      $"-{cfg.failGoldPenalty} gold, -{cfg.failReputationPenalty} reputasi. " +
                      "Permainan tetap lanjut.");

            Resolve(false);
        }

        // ---- Fase 2: konfrontasi ------------------------------------------------------

        void HandleDukunConfronted()
        {
            if (_phase != Phase.Hunt) return;

            QTEController qte = QTEController.Instance;
            if (qte == null) return;

            SantetConfigSO cfg = Config;
            if (cfg.confrontationConfig == null)
            {
                Debug.LogError("[Santet] Confrontation Config belum diisi (harusnya QTE_Hard).");
                return;
            }

            _phase = Phase.Confront;

            // Konfrontasi memakai TimingBar biasa, jadi clock harus normal juga.
            GameClock.SetMultiplier(1f);

            SantetPresenter presenter = SantetPresenter.Instance;
            if (presenter != null) presenter.HideObjective();

            qte.Begin(QTERequest.TimingBar(cfg.confrontationConfig, cfg.confrontationLabel,
                                           HandleConfrontationResult));
        }

        void HandleConfrontationResult(QTEResult result)
        {
            if (_phase != Phase.Confront) return;

            SantetConfigSO cfg = Config;
            EconomyService economy = EconomyService.Instance;

            if (result.IsSuccess)
            {
                if (economy != null) economy.AddReputation(cfg.winReputationGain);

                Debug.Log($"[Santet] Santet dipatahkan — +{cfg.winReputationGain} reputasi.");
                AudioService.PlaySFX(SfxId.OrderComplete);

                Resolve(true);
                return;
            }

            if (economy != null) economy.AddGold(-cfg.confrontationFailGoldPenalty);

            // Dukun tetap pergi: pemain tidak boleh terjebak mengulang konfrontasi.
            Debug.Log($"[Santet] Konfrontasi gagal — -{cfg.confrontationFailGoldPenalty} gold. " +
                      "Dukun tetap pergi.");
            AudioService.PlaySFX(SfxId.SantetHit);

            Resolve(false);
        }

        // ---- Util ---------------------------------------------------------------------

        static void FreezePlayer(bool frozen)
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);
            if (player != null) player.SetFrozen(frozen);
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;

            SantetPresenter presenter = SantetPresenter.Instance;
            if (presenter != null) presenter.OnConfronted -= HandleDukunConfronted;

            _subscribed = false;
        }
    }
}
