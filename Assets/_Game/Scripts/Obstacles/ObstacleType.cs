namespace MBG.Obstacles
{
    /// <summary>
    /// Gangguan yang bisa menginterupsi memasak. Semuanya datang dari luar rumah —
    /// pintu adalah sistem peringatannya.
    /// </summary>
    public enum ObstacleType
    {
        /// <summary>Ormas menggedor pintu minta jatah.</summary>
        Ormas = 0,

        /// <summary>Serangan gaib dari tetangga yang iri.</summary>
        Santet = 1,

        /// <summary>Pungutan liar berkedok pajak.</summary>
        IllegalTax = 2
    }

    /// <summary>Cara pintu memberi peringatan untuk sebuah gangguan.</summary>
    public enum DoorAlertState
    {
        Idle = 0,

        /// <summary>Ketukan sopan — getaran pelan, ikon tanda tanya.</summary>
        Knock = 1,

        /// <summary>Gedoran keras — getaran kuat, ikon seru merah, layar ikut bergoyang.</summary>
        Bang = 2
    }

    public static class ObstacleTypeExtensions
    {
        /// <summary>Nama gangguan untuk HUD dan log (bahasa Indonesia).</summary>
        public static string GetLabel(this ObstacleType type)
        {
            switch (type)
            {
                case ObstacleType.Ormas: return "ORMAS";
                case ObstacleType.Santet: return "SANTET";
                case ObstacleType.IllegalTax: return "PAJAK ILEGAL";
                default: return type.ToString().ToUpperInvariant();
            }
        }

        /// <summary>
        /// Ikon placeholder. Sengaja karakter ASCII: font TMP bawaan hanya memuat
        /// ASCII, jadi simbol Unicode akan tampil sebagai kotak kosong.
        /// </summary>
        public static string GetIcon(this ObstacleType type)
        {
            switch (type)
            {
                case ObstacleType.Ormas: return "!";
                case ObstacleType.Santet: return "*";
                case ObstacleType.IllegalTax: return "?";
                default: return "?";
            }
        }

        /// <summary>
        /// Cara pintu memperingatkan gangguan ini.
        /// TODO: santet sebenarnya tidak datang lewat pintu — untuk sekarang ia
        /// memakai ketukan supaya stub-nya bisa diuji.
        /// </summary>
        public static DoorAlertState GetDoorAlert(this ObstacleType type)
        {
            switch (type)
            {
                case ObstacleType.Ormas: return DoorAlertState.Bang;
                case ObstacleType.IllegalTax: return DoorAlertState.Knock;
                default: return DoorAlertState.Knock;
            }
        }

        /// <summary>Teks onomatope yang muncul di dekat pintu.</summary>
        public static string GetOnomatope(this DoorAlertState state)
        {
            switch (state)
            {
                case DoorAlertState.Knock: return "TOK TOK TOK";
                case DoorAlertState.Bang: return "BRAK! BRAK! BRAK!";
                default: return "";
            }
        }
    }
}
