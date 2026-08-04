namespace MBG.Core
{
    // ---------------------------------------------------------------------
    // PLACEHOLDER TYPES
    //
    // Type di file ini sengaja masih kosong. Tujuannya cuma satu: membuat
    // signature event di GameEventBus sudah final sejak sekarang, sehingga
    // sistem Order / QTE / Obstacle bisa dipasang di prompt berikutnya tanpa
    // mengubah bus-nya lagi.
    //
    // TODO(prompt berikutnya): isi field-nya. Kalau nanti type ini pindah ke
    // MBG.Data, cukup pindahkan file ini — GameEventBus hanya butuh nama type.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Satu pesanan yang sedang berjalan (instance runtime, bukan data asset).
    /// TODO: resep, timer sisa, daftar step yang sudah selesai.
    /// </summary>
    public class OrderRuntime
    {
    }

    /// <summary>
    /// Hasil akhir sebuah pesanan.
    /// TODO: referensi ke OrderRuntime, gold, skor, tip, grade.
    /// </summary>
    public struct OrderResult
    {
    }

    /// <summary>Nilai satu percobaan QTE. Urutan dari terburuk ke terbaik.</summary>
    public enum QTEGrade
    {
        Miss = 0,
        Good = 1,
        Perfect = 2
    }

    /// <summary>Jenis gangguan yang menginterupsi masakan. TODO: lengkapi.</summary>
    public enum ObstacleType
    {
        None = 0,
        DoorKnock = 1,
        Santet = 2
    }

    /// <summary>Sebab permainan berakhir. TODO: lengkapi.</summary>
    public enum GameOverReason
    {
        None = 0,
        ReputationZero = 1,
        OutOfGold = 2,
        DayFailed = 3
    }
}
