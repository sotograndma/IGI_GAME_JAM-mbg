namespace MBG.Core
{
    // ---------------------------------------------------------------------
    // Sisa placeholder GameEventBus.
    //
    // Type yang sudah punya rumah sendiri dan tidak lagi ada di sini:
    //   QTEGrade                  -> MBG.QTE  (QTE/QTEGrade.cs)
    //   OrderRuntime, OrderResult -> MBG.Catering (Catering/)
    // ---------------------------------------------------------------------

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
