using System.Collections.Generic;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Resep satu menu: urutan langkah yang harus dikerjakan untuk menghasilkan
    /// satu batch porsi.
    /// </summary>
    [CreateAssetMenu(fileName = "Recipe_", menuName = "MBG/Recipe")]
    public class RecipeSO : ScriptableObject
    {
        public string displayName = "Nasi Kotak";

        [Tooltip("Dikerjakan berurutan dari indeks 0.")]
        public List<RecipeStepSO> steps = new();

        [Tooltip("Berapa porsi yang dihasilkan sekali menyelesaikan seluruh langkah.")]
        [Min(1)]
        public int portionsPerBatch = 20;

        public int StepCount => steps != null ? steps.Count : 0;

        public RecipeStepSO GetStep(int index)
        {
            if (steps == null || index < 0 || index >= steps.Count) return null;
            return steps[index];
        }

        /// <summary>Berapa batch yang dibutuhkan untuk sekian porsi.</summary>
        public int GetBatchCount(int totalPortions)
            => Mathf.Max(1, Mathf.CeilToInt(totalPortions / (float)Mathf.Max(1, portionsPerBatch)));
    }
}
