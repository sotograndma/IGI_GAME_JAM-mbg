namespace MBG.Kitchen
{
    /// <summary>
    /// Station dapur, diurutkan sesuai alur memasak: siapkan bahan, masak,
    /// kemas, lalu serahkan ke pelanggan.
    /// </summary>
    public enum StationType
    {
        Prep = 0,
        Cooking = 1,
        Packing = 2,
        Handover = 3
    }

    public static class StationTypeExtensions
    {
        /// <summary>Teks prompt bawaan tiap station (bahasa Indonesia).</summary>
        public static string GetDefaultPrompt(this StationType type)
        {
            switch (type)
            {
                case StationType.Prep: return "Potong bahan";
                case StationType.Cooking: return "Masak";
                case StationType.Packing: return "Kemas";
                case StationType.Handover: return "Serahkan catering";
                default: return "Pakai station";
            }
        }

        /// <summary>
        /// Prompt saat station belum boleh dipakai. Handover punya kalimatnya
        /// sendiri karena "Belum saatnya" tidak menjelaskan apa yang kurang.
        /// </summary>
        public static string GetDefaultIrrelevantPrompt(this StationType type)
        {
            switch (type)
            {
                case StationType.Handover: return "Catering belum siap";
                default: return "Belum saatnya";
            }
        }

        /// <summary>
        /// Sebutan tempat untuk dipakai di dalam kalimat, misal
        /// "Berikutnya: Potong sayur di <b>meja persiapan</b>".
        /// </summary>
        public static string GetLocationPhrase(this StationType type)
        {
            switch (type)
            {
                case StationType.Prep: return "meja persiapan";
                case StationType.Cooking: return "kompor";
                case StationType.Packing: return "meja pengemasan";
                case StationType.Handover: return "meja serah terima";
                default: return "station";
            }
        }

        /// <summary>Nama yang tampil di label placeholder di atas station.</summary>
        public static string GetDisplayName(this StationType type)
        {
            switch (type)
            {
                case StationType.Prep: return "PERSIAPAN";
                case StationType.Cooking: return "MASAK";
                case StationType.Packing: return "KEMAS";
                case StationType.Handover: return "SERAH TERIMA";
                default: return type.ToString().ToUpperInvariant();
            }
        }
    }
}
