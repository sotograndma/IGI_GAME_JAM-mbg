namespace MBG.QTE
{
    /// <summary>
    /// Mekanik QTE yang tersedia. Tiap nilai dipetakan ke satu
    /// <see cref="IQTEModule"/> di <see cref="QTEController"/>.
    /// </summary>
    public enum QTEType
    {
        /// <summary>Skill check ala Dead by Daylight: indikator berjalan, tekan Space di zona.</summary>
        TimingBar = 0,

        /// <summary>Tekan tombol mengikuti ketukan. TODO: dipakai untuk santet.</summary>
        Rhythm = 1,

        /// <summary>Tekan tombol secepat mungkin. TODO.</summary>
        Mash = 2
    }
}
