using UnityEngine;

namespace MBG.QTE
{
    /// <summary>
    /// Semua angka kesulitan QTE. Tidak satu pun boleh ditulis ulang di dalam
    /// module atau UI — kalau sebuah QTE terasa terlalu sulit, yang diubah adalah
    /// asset ini, bukan kode.
    ///
    /// Preset bawaan: QTE_Easy, QTE_Normal, QTE_Hard di Assets/_Game/Data/QTE/
    /// (dibuat oleh Tools > MBG > Build QTE Setup).
    /// </summary>
    [CreateAssetMenu(fileName = "QTE_Config", menuName = "MBG/QTE Config")]
    public class QTEConfigSO : ScriptableObject
    {
        [Header("Waktu")]
        [Tooltip("Berapa detik indikator butuh untuk melintasi bar sekali, sebelum speedMultiplier.")]
        public float travelDuration = 1.2f;

        [Tooltip("Pengali kecepatan. >1 mempercepat indikator, <1 memperlambat.")]
        public float speedMultiplier = 1f;

        [Header("Zona (fraksi lebar bar, 0..1)")]
        [Range(0.02f, 1f)]
        public float goodZoneWidth = 0.30f;

        [Tooltip("Zona Perfect selalu berada di tengah zona Good, dan tidak pernah lebih lebar darinya.")]
        [Range(0.01f, 1f)]
        public float perfectZoneWidth = 0.10f;

        [Header("Sesi")]
        [Tooltip("Berapa kali berturut-turut pemain harus berhasil dalam satu sesi.")]
        [Min(1)]
        public int hitCount = 1;

        [Tooltip("Acak posisi zona tiap hit. Kalau mati, zona selalu di tengah bar.")]
        public bool randomizeZonePosition = true;

        [Tooltip("Berapa lama panel menahan teks hasil sebelum state dikembalikan, " +
                 "supaya pemain sempat membacanya.")]
        [Min(0f)]
        public float resultHoldSeconds = 0.4f;

        /// <summary>Durasi lintasan setelah memperhitungkan speedMultiplier.</summary>
        public float EffectiveDuration => travelDuration / Mathf.Max(0.05f, speedMultiplier);

        public float ClampedGoodWidth => Mathf.Clamp(goodZoneWidth, 0.02f, 1f);

        /// <summary>Zona Perfect tidak pernah melebihi zona Good.</summary>
        public float ClampedPerfectWidth => Mathf.Clamp(perfectZoneWidth, 0.01f, ClampedGoodWidth);

        public int ClampedHitCount => Mathf.Max(1, hitCount);

        void OnValidate()
        {
            travelDuration = Mathf.Max(0.1f, travelDuration);
            speedMultiplier = Mathf.Max(0.05f, speedMultiplier);
            if (perfectZoneWidth > goodZoneWidth) perfectZoneWidth = goodZoneWidth;
        }
    }
}
