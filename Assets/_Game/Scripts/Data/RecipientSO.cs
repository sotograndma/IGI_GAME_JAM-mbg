using System.Collections.Generic;
using MBG.Catering;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Institusi pemesan catering. <see cref="patienceMultiplier"/> mengali
    /// deadline: pemesan yang sabar memberi waktu lebih panjang untuk pesanan
    /// yang sama.
    ///
    /// Kalimat reaksi dipisah per tingkat kualitas dan dipilih acak, supaya layar
    /// hasil tidak terasa itu-itu saja.
    /// </summary>
    [CreateAssetMenu(fileName = "Recipient_", menuName = "MBG/Recipient")]
    public class RecipientSO : ScriptableObject
    {
        public string institutionName = "SD Negeri 03 Sukamaju";

        [Tooltip("Ikon untuk HUD dan papan pesanan. Boleh kosong untuk sekarang.")]
        public Sprite icon;

        [Tooltip("Pengali deadline. 1 = normal, 1.3 = lebih sabar, 0.8 = menuntut cepat.")]
        [Min(0.1f)]
        public float patienceMultiplier = 1f;

        [Header("Reaksi (bahasa Indonesia)")]
        [Tooltip("Dipakai saat kualitas PERFECT.")]
        public List<string> reactionPerfect = new();

        [Tooltip("Dipakai saat kualitas BAIK.")]
        public List<string> reactionGood = new();

        [Tooltip("Dipakai saat kualitas BURUK.")]
        public List<string> reactionBad = new();

        [Tooltip("Dipakai saat pesanan gagal atau kualitasnya GAGAL.")]
        public List<string> reactionFailed = new();

        /// <summary>Satu kalimat reaksi acak untuk tingkat kualitas tertentu.</summary>
        public string GetReaction(FoodQuality quality)
        {
            List<string> lines = GetLines(quality);
            if (lines == null || lines.Count == 0) return "";

            return lines[Random.Range(0, lines.Count)];
        }

        List<string> GetLines(FoodQuality quality)
        {
            switch (quality)
            {
                case FoodQuality.Perfect: return reactionPerfect;
                case FoodQuality.Good: return reactionGood;
                case FoodQuality.Bad: return reactionBad;
                default: return reactionFailed;
            }
        }
    }
}
