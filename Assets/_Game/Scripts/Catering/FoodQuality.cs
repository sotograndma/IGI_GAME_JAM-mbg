namespace MBG.Catering
{
    /// <summary>
    /// Tingkat kualitas makanan yang dihasilkan satu pesanan, diturunkan dari
    /// rata-rata nilai QTE selama memasak. Ambangnya diatur di CateringBalanceSO.
    /// </summary>
    public enum FoodQuality
    {
        Perfect = 0,
        Good = 1,
        Bad = 2,
        Failed = 3
    }

    /// <summary>Tahapan hidup satu pesanan.</summary>
    public enum OrderState
    {
        None = 0,

        /// <summary>Sedang dimasak batch demi batch.</summary>
        Cooking = 1,

        /// <summary>Semua porsi siap; tinggal diserahkan di station Handover.</summary>
        ReadyToDeliver = 2,

        Completed = 3,

        /// <summary>Deadline habis sebelum diserahkan.</summary>
        Failed = 4
    }

    public static class FoodQualityExtensions
    {
        /// <summary>Label bahasa Indonesia untuk UI.</summary>
        public static string GetLabel(this FoodQuality quality)
        {
            switch (quality)
            {
                case FoodQuality.Perfect: return "SEMPURNA";
                case FoodQuality.Good: return "LAYAK";
                case FoodQuality.Bad: return "SEADANYA";
                default: return "GAGAL";
            }
        }
    }
}
