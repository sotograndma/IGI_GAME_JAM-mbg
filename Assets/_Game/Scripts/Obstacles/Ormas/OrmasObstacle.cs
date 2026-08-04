using System;
using MBG.Catering;
using MBG.Core;
using MBG.Data;
using MBG.QTE;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Ormas menggedor pintu minta jatah.
    ///
    /// Perubahan dari GDD yang sudah disepakati: menerobos BUKAN game over instan,
    /// melainkan Intrusion Meter. Selama meter mengisi, pemain punya pilihan:
    ///
    ///   - diam di dapur dan lanjut memasak → meter penuh, ormas masuk, satu batch
    ///     hancur, reputasi dan gold turun;
    ///   - keluar rumah → meter BERHENTI, dan pemain menghadapi mereka lewat
    ///     mini-game tarik-menarik. Menang berarti gangguan selesai baik-baik;
    ///     kalah tetap membuat mereka pergi, hanya dengan ongkos.
    ///
    /// Meter hanya beku selama pemain benar-benar di luar. Masuk kembali tanpa
    /// menghadapi membuat meter jalan lagi dari posisi terakhir, supaya keluar
    /// sebentar tidak jadi cara murah menghentikan waktu.
    /// </summary>
    public class OrmasObstacle : IObstacle
    {
        enum Phase { Idle, Warning, Outside, Fighting, Done }

        public ObstacleType Type => ObstacleType.Ormas;

        /// <summary>Isi Intrusion Meter, 0..1.</summary>
        public float UrgencyNormalized => _meter;

        public bool IsActive => _phase == Phase.Warning || _phase == Phase.Outside || _phase == Phase.Fighting;

        public event Action<bool> OnResolved;

        readonly OrmasConfigSO _config;

        Phase _phase = Phase.Idle;
        float _meter;
        float _duration;
        float _difficulty;
        bool _subscribed;

        public OrmasObstacle(OrmasConfigSO config)
        {
            _config = config != null ? config : OrmasConfigSO.Fallback;
        }

        OrmasConfigSO Config => _config;

        public void Begin(float difficulty)
        {
            _difficulty = Mathf.Clamp01(difficulty);
            _duration = Config.GetIntrusionDuration(_difficulty);
            _meter = 0f;
            _phase = Phase.Warning;

            OrmasEncounterController presenter = OrmasEncounterController.Instance;
            if (presenter != null)
            {
                presenter.OnConfronted += HandleConfronted;
                _subscribed = true;
                presenter.SpawnGroup();
            }
            else
            {
                Debug.LogWarning("[Ormas] OrmasEncounterController tidak ada — grup tidak bisa dimunculkan. " +
                                 "Jalankan Tools > MBG > Build Exterior Encounters.");
            }

            Debug.Log($"[Ormas] Gedoran dimulai — {_duration:0.0}s sebelum mereka menerobos.");
        }

        public void Tick(float deltaTime)
        {
            if (_phase == Phase.Fighting || !IsActive) return;

            // Meter beku selama pemain berada di luar rumah.
            if (IsPlayerOutside())
            {
                if (_phase != Phase.Outside)
                {
                    _phase = Phase.Outside;
                    Debug.Log("[Ormas] Pemain keluar rumah — Intrusion Meter berhenti.");
                }
                return;
            }

            if (_phase == Phase.Outside)
            {
                _phase = Phase.Warning;
                Debug.Log("[Ormas] Pemain kembali ke dapur — meter jalan lagi.");
            }

            _meter = _duration > 0f ? Mathf.Clamp01(_meter + deltaTime / _duration) : 1f;

            if (_meter >= 1f) Intrude();
        }

        public void Resolve(bool success)
        {
            if (!IsActive) return;

            _phase = Phase.Done;
            Unsubscribe();

            OrmasEncounterController presenter = OrmasEncounterController.Instance;
            if (presenter != null) presenter.DespawnGroup();

            OnResolved?.Invoke(success);
        }

        // ---- Menerobos -------------------------------------------------------------

        void Intrude()
        {
            OrmasConfigSO cfg = Config;

            OrmasEncounterController presenter = OrmasEncounterController.Instance;
            if (presenter != null) presenter.SpawnIntruder();

            int portionsLost = 0;
            CateringController catering = CateringController.Instance;
            if (catering != null) portionsLost = catering.DestroyBatches(cfg.batchesDestroyed);

            EconomyService economy = EconomyService.Instance;
            if (economy != null) economy.AddGold(-cfg.intrusionGoldPenalty);

            // Ini kasus "diabaikan sampai konsekuensinya jatuh" — potongannya jauh
            // lebih besar daripada sekadar kalah adu dorong, dan angkanya ada di
            // ReputationConfigSO.
            if (ReputationService.Instance != null)
                ReputationService.Instance.ApplyIgnoredPenalty(ObstacleType.Ormas);

            ScreenShakeService.Pulse(cfg.intrusionShakeAmplitude, cfg.intrusionShakeDuration);
            AudioService.PlaySFX(SfxId.DoorBang);

            Debug.Log($"[Ormas] MENEROBOS! {portionsLost} porsi hancur, -{cfg.intrusionGoldPenalty} gold.");

            Resolve(false);
        }

        // ---- Encounter di luar --------------------------------------------------------

        void HandleConfronted()
        {
            if (_phase != Phase.Outside && _phase != Phase.Warning) return;

            QTEController qte = QTEController.Instance;
            if (qte == null)
            {
                Debug.LogError("[Ormas] QTEController tidak ada — encounter tidak bisa dimulai.");
                return;
            }

            MashQTE mash = qte.GetModule<MashQTE>();
            if (mash == null)
            {
                Debug.LogError("[Ormas] MashQTE belum terdaftar di QTEController.");
                return;
            }

            OrmasConfigSO cfg = Config;

            mash.Configure(new MashSettings
            {
                pushPerPress = cfg.pushPerPress,
                opponentSpeed = cfg.GetOpponentSpeed(_difficulty),
                timeLimit = cfg.encounterTimeLimit,
                startPosition = cfg.barStartPosition,
                tauntInterval = cfg.tauntInterval,
                taunts = cfg.taunts
            });

            _phase = Phase.Fighting;

            // Perlambatan waktu gunanya memberi napas saat memasak. Di luar rumah
            // tidak ada yang dimasak, dan kalau multiplier tetap 0.5 maka batas
            // waktu 15 detik di config akan terasa 30 detik. Kembalikan ke normal;
            // ObstacleManager tetap menyetelnya ke 1 lagi saat gangguan selesai.
            GameClock.SetMultiplier(1f);

            // config sengaja null: angka mekaniknya sudah diisi lewat Configure.
            qte.Begin(new QTERequest(QTEType.Mash, null, "Dorong mereka keluar!", HandleEncounterResult));
        }

        void HandleEncounterResult(QTEResult result)
        {
            if (_phase != Phase.Fighting) return;

            OrmasConfigSO cfg = Config;
            EconomyService economy = EconomyService.Instance;

            if (result.IsSuccess)
            {
                // Reputasi untuk gangguan yang berhasil diatasi ditambahkan
                // ReputationService lewat OnObstacleResolved.
                Debug.Log("[Ormas] Berhasil diusir.");
                AudioService.PlaySFX(SfxId.OrderComplete);

                Resolve(true);
                ReturnToKitchen();
                return;
            }

            if (economy != null) economy.AddGold(-cfg.loseGoldPenalty);

            // Kalah adu dorong bukan "diabaikan": ongkosnya gold saja, reputasi aman.
            // Dan mereka tetap pergi — pemain tidak boleh terjebak di luar.
            Debug.Log($"[Ormas] Kalah adu dorong — -{cfg.loseGoldPenalty} gold. Mereka tetap pergi.");
            AudioService.PlaySFX(SfxId.OrderFail);

            Resolve(false);
        }

        static void ReturnToKitchen()
        {
            AreaManager area = AreaManager.Instance;
            if (area != null) area.GoTo(AreaManager.Area.Interior);
        }

        // ---- Util -----------------------------------------------------------------------

        static bool IsPlayerOutside()
        {
            AreaManager area = AreaManager.Instance;
            return area != null && area.Current == AreaManager.Area.Exterior;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;

            OrmasEncounterController presenter = OrmasEncounterController.Instance;
            if (presenter != null) presenter.OnConfronted -= HandleConfronted;

            _subscribed = false;
        }
    }
}
