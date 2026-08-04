using System;
using System.Collections.Generic;
using MBG.Core;
using MBG.Data;
using MBG.Kitchen;
using MBG.QTE;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MBG.Catering
{
    /// <summary>
    /// Mengelola satu pesanan yang sedang dikerjakan: langkah mana yang aktif,
    /// QTE apa yang dipicu station, kapan batch selesai, dan kapan deadline habis.
    ///
    /// Cara kerjanya sengaja dua arah lewat perantara, bukan referensi langsung:
    ///   - station bertanya "apakah aku relevan?" lewat
    ///     <see cref="KitchenStation.RelevanceCheck"/> yang diisi controller ini;
    ///   - station memberi tahu pemakaiannya lewat
    ///     <see cref="GameEventBus.OnStationUsed"/>.
    /// Jadi KitchenStation tidak tahu apa pun tentang resep, dan controller ini
    /// tidak menyimpan referensi ke satu pun station.
    ///
    /// Aturan penting:
    ///   - CriticalMiss mengulang batch dari langkah pertama. Hukumannya waktu:
    ///     tidak ada gold yang dipotong.
    ///   - Saat GameState = InObstacle, semua station menjadi tidak relevan
    ///     sehingga QTE tidak bisa dipicu. Deadline TETAP berjalan — obstacle nanti
    ///     memperlambatnya lewat GameClock.SetMultiplier, bukan menghentikannya.
    /// </summary>
    [DisallowMultipleComponent]
    public class CateringController : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Nilai grade & ambang kualitas. Diisi otomatis oleh Tools > MBG > Build Catering Data.")]
        [SerializeField] CateringBalanceSO balance;

        [Tooltip("Rumus gold & skor. Diisi otomatis oleh Tools > MBG > Build Catering Data.")]
        [SerializeField] ScoringConfigSO scoring;

        [Header("Debug")]
        [Tooltip("F4 menyelesaikan pesanan aktif secara bertahap.")]
        [SerializeField] bool enableDebugKeys = true;

        public static CateringController Instance { get; private set; }

        /// <summary>Pesanan terakhir yang dimulai. Tetap terisi setelah selesai/gagal.</summary>
        public OrderRuntime ActiveOrder { get; private set; }

        public bool HasActiveOrder => ActiveOrder != null && ActiveOrder.IsActive;

        /// <summary>Rumus bayaran yang dipakai; jatuh ke nilai bawaan kalau asset belum diikat.</summary>
        public ScoringConfigSO Scoring => scoring != null ? scoring : ScoringConfigSO.Fallback;

        /// <summary>Progress memasak dibekukan selama gangguan berlangsung.</summary>
        public bool IsBlockedByObstacle
            => GameManager.Instance != null && GameManager.Instance.State == GameState.InObstacle;

        Func<StationType, bool> _relevanceCheck;

        /// <summary>Preset QTE dari hari berjalan; menimpa preset milik langkah resep.</summary>
        QTEConfigSO _qteOverride;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[CateringController] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
            _relevanceCheck = IsStationRelevant;
        }

        void OnEnable()
        {
            if (Instance != this) return;

            GameEventBus.OnStationUsed += HandleStationUsed;
            KitchenStation.RelevanceCheck = _relevanceCheck;
        }

        void OnDisable()
        {
            if (Instance != this) return;

            GameEventBus.OnStationUsed -= HandleStationUsed;

            // Hanya lepas kalau memang punya kita — jangan menimpa milik sistem lain.
            if (ReferenceEquals(KitchenStation.RelevanceCheck, _relevanceCheck))
                KitchenStation.RelevanceCheck = null;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (enableDebugKeys) HandleDebugKeys();
            TickDeadline();
        }

        // ---- API -----------------------------------------------------------

        /// <summary>
        /// Mulai pesanan baru. Mengembalikan null kalau ditolak.
        /// Antrian dan pergantian pesanan diurus <c>DayManager</c>, bukan di sini —
        /// controller ini hanya tahu satu pesanan yang sedang dikerjakan.
        /// </summary>
        public OrderRuntime StartOrder(OrderSO order, float deadlineMultiplier = 1f,
                                       QTEConfigSO qteOverride = null)
        {
            if (order == null)
            {
                Debug.LogError("[Catering] StartOrder dipanggil tanpa OrderSO.", this);
                return null;
            }

            if (!order.IsValid)
            {
                Debug.LogError($"[Catering] '{order.name}' belum punya resep dengan langkah — pesanan ditolak.", order);
                return null;
            }

            if (HasActiveOrder)
            {
                Debug.LogWarning($"[Catering] Masih ada pesanan berjalan: {ActiveOrder.Describe()}", this);
                return null;
            }

            ActiveOrder = new OrderRuntime(order, balance, deadlineMultiplier);
            _qteOverride = qteOverride;

            Debug.Log($"[Catering] Pesanan dimulai — {ActiveOrder.Describe()}", this);
            AudioService.PlaySFX(SfxId.OrderIncoming);

            GameEventBus.RaiseOrderStarted(ActiveOrder);
            GameEventBus.RaiseOrderProgress(0, ActiveOrder.totalPortions);

            return ActiveOrder;
        }

        /// <summary>
        /// Hancurkan sejumlah batch yang sudah jadi — dipakai gangguan yang
        /// menyabotase dapur. Mengembalikan berapa porsi yang hilang.
        ///
        /// Pesanan yang tadinya sudah siap diserahkan otomatis kembali ke tahap
        /// memasak, karena porsinya jadi kurang lagi.
        /// </summary>
        public int DestroyBatches(int batches)
        {
            OrderRuntime order = ActiveOrder;
            if (order == null || !order.IsActive || batches <= 0) return 0;

            int lost = Mathf.Min(order.portionsCompleted, order.PortionsPerBatch * batches);
            if (lost <= 0) return 0;

            order.portionsCompleted -= lost;
            order.currentStepIndex = 0;
            order.currentBatchIndex = Mathf.Max(0, order.currentBatchIndex - batches);

            if (order.state == OrderState.ReadyToDeliver && order.portionsCompleted < order.totalPortions)
                order.state = OrderState.Cooking;

            Debug.Log($"[Catering] {lost} porsi hancur — sisa {order.portionsCompleted}/{order.totalPortions}.", this);
            GameEventBus.RaiseOrderProgress(order.portionsCompleted, order.totalPortions);

            return lost;
        }

        /// <summary>Gagalkan pesanan berjalan (deadline habis, atau dipanggil sistem lain).</summary>
        public void FailActiveOrder()
        {
            OrderRuntime order = ActiveOrder;
            if (order == null || !order.IsActive) return;

            order.state = OrderState.Failed;
            CancelRunningQTE();

            Debug.Log($"[Catering] Pesanan GAGAL — {order.Describe()}", this);
            AudioService.PlaySFX(SfxId.OrderFail);

            GameEventBus.RaiseOrderFailed(order);
        }

        // ---- Alur pesanan ---------------------------------------------------

        void TickDeadline()
        {
            OrderRuntime order = ActiveOrder;
            if (order == null || !order.IsActive) return;

            order.timeRemaining -= GameClock.DeltaTime;
            if (order.timeRemaining > 0f) return;

            order.timeRemaining = 0f;
            FailActiveOrder();
        }

        /// <summary>
        /// Diisi ke <see cref="KitchenStation.RelevanceCheck"/>: station hanya
        /// relevan kalau ia memang langkah yang sedang ditunggu.
        /// </summary>
        bool IsStationRelevant(StationType type)
        {
            OrderRuntime order = ActiveOrder;
            if (order == null) return false;
            if (IsBlockedByObstacle) return false;

            switch (order.state)
            {
                case OrderState.ReadyToDeliver:
                    return type == StationType.Handover;

                case OrderState.Cooking:
                    RecipeStepSO step = order.CurrentStep;
                    return step != null && step.station == type;

                default:
                    return false;
            }
        }

        void HandleStationUsed(StationType type)
        {
            OrderRuntime order = ActiveOrder;
            if (order == null) return;

            if (IsBlockedByObstacle)
            {
                Debug.Log("[Catering] Ada gangguan — station tidak bisa dipakai dulu.", this);
                return;
            }

            if (order.state == OrderState.ReadyToDeliver)
            {
                if (type == StationType.Handover) DeliverOrder();
                return;
            }

            if (order.state != OrderState.Cooking) return;

            RecipeStepSO step = order.CurrentStep;
            if (step == null || step.station != type) return;

            BeginStepQTE(step);
        }

        void BeginStepQTE(RecipeStepSO step)
        {
            // Hari yang naik kelas boleh menimpa preset tiap langkah sekaligus.
            QTEConfigSO config = _qteOverride != null ? _qteOverride : step.qteConfig;

            if (config == null)
            {
                Debug.LogError($"[Catering] Langkah '{step.actionLabel}' belum punya QTEConfigSO.", step);
                return;
            }

            QTEController qte = QTEController.Instance;
            if (qte == null)
            {
                Debug.LogError("[Catering] QTEController tidak ada di scene. " +
                               "Jalankan Tools > MBG > Build QTE Setup.", this);
                return;
            }

            qte.Begin(new QTERequest(step.qteType, config, step.actionLabel, HandleStepQTEFinished));
        }

        void HandleStepQTEFinished(QTEResult result)
        {
            OrderRuntime order = ActiveOrder;

            // State bisa saja sudah berubah selama QTE berjalan (deadline habis,
            // atau debug key melompati batch). Kalau begitu hasilnya diabaikan.
            if (order == null || order.state != OrderState.Cooking) return;

            order.RecordGrade(result.grade);

            if (result.grade == QTEGrade.CriticalMiss)
            {
                order.RestartBatch();

                Debug.Log($"[Catering] Gagal total — batch {order.currentBatchIndex + 1} diulang dari langkah pertama. " +
                          $"Tidak ada gold yang dipotong, hanya waktu yang hilang.", this);
                AudioService.PlaySFX(SfxId.OrderFail);

                GameEventBus.RaiseOrderProgress(order.portionsCompleted, order.totalPortions);
                return;
            }

            bool batchDone = order.AdvanceStep();
            if (!batchDone)
            {
                RecipeStepSO next = order.CurrentStep;
                Debug.Log($"[Catering] {result.grade} — lanjut ke \"{(next != null ? next.actionLabel : "-")}\" " +
                          $"di station {(next != null ? next.station.ToString() : "-")}.", this);
                return;
            }

            order.CompleteBatch();
            AudioService.PlaySFX(SfxId.Plate);

            GameEventBus.RaiseOrderProgress(order.portionsCompleted, order.totalPortions);

            if (order.state == OrderState.ReadyToDeliver)
            {
                Debug.Log($"[Catering] Semua {order.totalPortions} porsi siap. " +
                          "Serahkan di station SERAH TERIMA.", this);
            }
            else
            {
                Debug.Log($"[Catering] Batch selesai — {order.portionsCompleted}/{order.totalPortions} porsi. " +
                          $"Mulai batch {order.currentBatchIndex + 1}/{order.TotalBatches}.", this);
            }
        }

        void DeliverOrder()
        {
            OrderRuntime order = ActiveOrder;
            if (order == null || order.state != OrderState.ReadyToDeliver) return;

            order.state = OrderState.Completed;

            // Rumusnya hanya ada di ScoringConfigSO; controller cuma memakainya.
            OrderResult result = Scoring.CalculateResult(order, success: true,
                                                        ReputationService.CurrentGoldMultiplier);

            Debug.Log($"[Catering] Pesanan diserahkan — {result}", this);
            AudioService.PlaySFX(SfxId.OrderComplete);

            GameEventBus.RaiseOrderCompleted(result);
        }

        void CancelRunningQTE()
        {
            QTEController qte = QTEController.Instance;
            if (qte != null && qte.IsRunning) qte.CancelActive();
        }

        // ---- Debug ----------------------------------------------------------

        void HandleDebugKeys()
        {
            if (InputService.WasKeyPressedThisFrame(Key.F4)) DebugFinishActiveOrder();
        }

        /// <summary>
        /// F4 bekerja dua tahap: tekan pertama melompati semua batch sehingga
        /// pesanan siap diserahkan (supaya station Handover tetap bisa diuji),
        /// tekan kedua menyerahkannya.
        /// </summary>
        void DebugFinishActiveOrder()
        {
            OrderRuntime order = ActiveOrder;
            if (order == null || !order.IsActive)
            {
                Debug.LogWarning("[Catering] F4: tidak ada pesanan berjalan. Tekan F3 dulu.", this);
                return;
            }

            if (order.state == OrderState.Cooking)
            {
                order.portionsCompleted = order.totalPortions;
                order.currentBatchIndex = Mathf.Max(0, order.TotalBatches);
                order.currentStepIndex = 0;
                order.state = OrderState.ReadyToDeliver;

                // State sudah bukan Cooking, jadi hasil QTE yang batal ini diabaikan
                // oleh HandleStepQTEFinished.
                CancelRunningQTE();

                GameEventBus.RaiseOrderProgress(order.portionsCompleted, order.totalPortions);

                Debug.Log("[Catering] F4: semua batch dilewati. Serahkan di station SERAH TERIMA, " +
                          "atau tekan F4 sekali lagi.", this);
                return;
            }

            DeliverOrder();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
