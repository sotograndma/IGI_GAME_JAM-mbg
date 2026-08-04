using MBG.Kitchen;
using MBG.QTE;
using UnityEngine;

namespace MBG.Data
{
    /// <summary>
    /// Satu langkah memasak: di station mana dikerjakan, QTE apa yang harus
    /// dilewati, dan teks yang tampil di panel QTE.
    /// </summary>
    [CreateAssetMenu(fileName = "Step_", menuName = "MBG/Recipe Step")]
    public class RecipeStepSO : ScriptableObject
    {
        [Tooltip("Station tempat langkah ini dikerjakan. Station lain akan menjawab \"Belum saatnya\".")]
        public StationType station = StationType.Prep;

        [Tooltip("Kesulitan QTE untuk langkah ini.")]
        public QTEConfigSO qteConfig;

        [Tooltip("Mekanik QTE. Untuk sekarang baru TimingBar yang punya module.")]
        public QTEType qteType = QTEType.TimingBar;

        [Tooltip("Instruksi yang tampil di QTEPanel, misal \"Potong sayur\".")]
        public string actionLabel = "Potong sayur";

        public bool IsValid => qteConfig != null;
    }
}
