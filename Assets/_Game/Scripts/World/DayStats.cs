namespace MBG.World
{
    /// <summary>
    /// Rekap satu hari kerja, dikirim lewat <c>GameEventBus.OnDaySummary</c> tepat
    /// sebelum hari ditutup. Snapshot — nilainya tidak berubah lagi setelah dibuat.
    /// </summary>
    public struct DayStats
    {
        public int day;

        /// <summary>Gold bersih hari itu; bisa negatif kalau banyak denda.</summary>
        public int goldEarned;

        public int scoreEarned;

        public int ordersSucceeded;
        public int ordersFailed;
        public int totalOrders;

        /// <summary>Reputasi setelah hari ini selesai.</summary>
        public int reputation;
        public int maxReputation;

        /// <summary>True kalau ini hari terakhir yang dijadwalkan.</summary>
        public bool isFinalDay;

        public override string ToString()
            => $"Hari {day}: {ordersSucceeded} sukses / {ordersFailed} gagal dari {totalOrders}, " +
               $"{goldEarned:+#;-#;0} gold, +{scoreEarned} skor, reputasi {reputation}/{maxReputation}";
    }
}
