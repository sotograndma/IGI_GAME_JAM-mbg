using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Institusi pemesan catering. <see cref="patienceMultiplier"/> mengali
    /// deadline: pemesan yang sabar memberi waktu lebih panjang untuk pesanan
    /// yang sama.
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
    }
}
