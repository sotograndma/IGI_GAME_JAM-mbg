namespace MBG.Core
{
    /// <summary>Sebab permainan berakhir.</summary>
    public enum GameOverReason
    {
        /// <summary>Reputasi habis — tidak ada lagi yang mau memesan.</summary>
        ReputationZero = 0,

        /// <summary>Uang habis dan tidak bisa lagi membeli bahan.</summary>
        Bankrupt = 1,

        /// <summary>Ormas menyerbu dapur. TODO: dipasang bersama sistem obstacle.</summary>
        OrmasInvasion = 2,

        /// <summary>Santet terlalu parah. TODO: dipasang bersama sistem obstacle.</summary>
        SantetFatal = 3
    }

    public static class GameOverReasonExtensions
    {
        /// <summary>Kalimat penjelasan untuk layar kalah.</summary>
        public static string GetLabel(this GameOverReason reason)
        {
            switch (reason)
            {
                case GameOverReason.ReputationZero:
                    return "Reputasi habis. Tidak ada lagi yang mau memesan catering darimu.";
                case GameOverReason.Bankrupt:
                    return "Uang habis. Dapur tidak bisa lagi membeli bahan.";
                case GameOverReason.OrmasInvasion:
                    return "Ormas menyerbu dapur. Usaha catering ditutup paksa.";
                case GameOverReason.SantetFatal:
                    return "Santet terlalu parah. Kamu tumbang di tengah masakan.";
                default:
                    return "Permainan berakhir.";
            }
        }

        /// <summary>Judul pendek untuk layar kalah.</summary>
        public static string GetTitle(this GameOverReason reason)
        {
            switch (reason)
            {
                case GameOverReason.ReputationZero: return "REPUTASI HANCUR";
                case GameOverReason.Bankrupt: return "BANGKRUT";
                case GameOverReason.OrmasInvasion: return "DIGREBEK ORMAS";
                case GameOverReason.SantetFatal: return "KENA SANTET";
                default: return "PERMAINAN BERAKHIR";
            }
        }
    }
}
