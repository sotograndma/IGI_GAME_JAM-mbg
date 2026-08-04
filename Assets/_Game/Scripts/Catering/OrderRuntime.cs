using System.Collections.Generic;
using MBG.Data;
using MBG.QTE;
using UnityEngine;

namespace MBG.Catering
{
    /// <summary>
    /// State satu pesanan yang sedang berjalan. OrderSO adalah datanya yang tidak
    /// berubah; class ini yang menyimpan kemajuannya.
    ///
    /// Pesanan dikerjakan per batch: satu batch = seluruh langkah resep sekali
    /// jalan, menghasilkan <c>portionsPerBatch</c> porsi. Pesanan selesai setelah
    /// cukup batch untuk menutup <see cref="totalPortions"/>.
    /// </summary>
    public class OrderRuntime
    {
        public OrderSO source;

        public int portionsCompleted;
        public int totalPortions;

        public int currentBatchIndex;
        public int currentStepIndex;

        public float timeRemaining;

        /// <summary>Semua nilai QTE selama pesanan ini, termasuk yang gagal dan diulang.</summary>
        public readonly List<QTEGrade> gradeHistory = new();

        public OrderState state = OrderState.None;

        /// <summary>Berapa kali batch harus diulang gara-gara CriticalMiss.</summary>
        public int batchRestarts;

        readonly CateringBalanceSO _balance;

        /// <summary>Deadline awal pesanan ini setelah dikali pengali hari.</summary>
        public float TotalDeadline { get; }

        public OrderRuntime(OrderSO source, CateringBalanceSO balance, float deadlineMultiplier = 1f)
        {
            this.source = source;
            _balance = balance != null ? balance : CateringBalanceSO.Fallback;

            totalPortions = source != null ? source.totalPortions : 0;

            float baseDeadline = source != null ? source.EffectiveDeadline : 0f;
            TotalDeadline = baseDeadline * Mathf.Max(0.1f, deadlineMultiplier);
            timeRemaining = TotalDeadline;

            state = OrderState.Cooking;
        }

        public CateringBalanceSO Balance => _balance;

        public RecipeSO Recipe => source != null ? source.recipe : null;

        public int PortionsPerBatch => Recipe != null ? Mathf.Max(1, Recipe.portionsPerBatch) : 1;

        public int TotalBatches => Recipe != null ? Recipe.GetBatchCount(totalPortions) : 0;

        public int StepCount => Recipe != null ? Recipe.StepCount : 0;

        /// <summary>Langkah yang sedang ditunggu, atau null kalau pesanan tidak sedang memasak.</summary>
        public RecipeStepSO CurrentStep
            => state == OrderState.Cooking && Recipe != null ? Recipe.GetStep(currentStepIndex) : null;

        public bool IsActive => state == OrderState.Cooking || state == OrderState.ReadyToDeliver;

        public bool IsFinished => state == OrderState.Completed || state == OrderState.Failed;

        /// <summary>Kemajuan porsi, 0..1.</summary>
        public float Progress01
            => totalPortions > 0 ? Mathf.Clamp01(portionsCompleted / (float)totalPortions) : 0f;

        /// <summary>Sisa waktu sebagai fraksi deadline awal, 0..1.</summary>
        public float TimeRemaining01
            => TotalDeadline > 0f ? Mathf.Clamp01(timeRemaining / TotalDeadline) : 0f;

        /// <summary>
        /// Rata-rata nilai seluruh QTE, 0..1. Sebelum ada satu pun QTE nilainya 1 —
        /// pesanan baru belum melakukan kesalahan apa pun.
        /// </summary>
        public float AverageQuality
        {
            get
            {
                if (gradeHistory.Count == 0) return 1f;

                float sum = 0f;
                for (int i = 0; i < gradeHistory.Count; i++)
                    sum += _balance.GetGradeValue(gradeHistory[i]);

                return Mathf.Clamp01(sum / gradeHistory.Count);
            }
        }

        public FoodQuality QualityTier => _balance.GetQualityTier(AverageQuality);

        /// <summary>Berapa QTE yang mendapat Perfect selama pesanan ini.</summary>
        public int PerfectCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < gradeHistory.Count; i++)
                    if (gradeHistory[i] == QTEGrade.Perfect) count++;

                return count;
            }
        }

        /// <summary>Catat hasil satu QTE.</summary>
        public void RecordGrade(QTEGrade grade) => gradeHistory.Add(grade);

        /// <summary>Kembali ke langkah pertama batch ini — hukuman untuk CriticalMiss.</summary>
        public void RestartBatch()
        {
            currentStepIndex = 0;
            batchRestarts++;
        }

        /// <summary>Maju satu langkah; true kalau seluruh langkah batch sudah selesai.</summary>
        public bool AdvanceStep()
        {
            currentStepIndex++;
            return currentStepIndex >= StepCount;
        }

        /// <summary>Selesaikan satu batch dan tambahkan porsinya.</summary>
        public void CompleteBatch()
        {
            int gained = Mathf.Min(PortionsPerBatch, Mathf.Max(0, totalPortions - portionsCompleted));
            portionsCompleted += gained;

            currentBatchIndex++;
            currentStepIndex = 0;

            if (portionsCompleted >= totalPortions) state = OrderState.ReadyToDeliver;
        }

        public string Describe()
        {
            string step = CurrentStep != null ? CurrentStep.actionLabel : "-";
            return $"{(source != null ? source.DisplayTitle : "tanpa OrderSO")} | {state} | " +
                   $"porsi {portionsCompleted}/{totalPortions} | batch {Mathf.Min(currentBatchIndex + 1, Mathf.Max(1, TotalBatches))}/{TotalBatches} | " +
                   $"langkah {Mathf.Min(currentStepIndex + 1, Mathf.Max(1, StepCount))}/{StepCount} ({step}) | " +
                   $"sisa {timeRemaining:0.0}s | kualitas {AverageQuality:0.00} ({QualityTier})";
        }
    }
}
