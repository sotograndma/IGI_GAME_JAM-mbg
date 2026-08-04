namespace MBG.Core
{
    // ---------------------------------------------------------------------
    // Sisa placeholder GameEventBus.
    //
    // Type yang sudah punya rumah sendiri dan tidak lagi ada di sini:
    //   QTEGrade                  -> MBG.QTE      (QTE/QTEGrade.cs)
    //   OrderRuntime, OrderResult -> MBG.Catering (Catering/)
    //   GameOverReason            -> Core/GameOverReason.cs
    // ---------------------------------------------------------------------

    /// <summary>Jenis gangguan yang menginterupsi masakan. TODO: lengkapi di sistem obstacle.</summary>
    public enum ObstacleType
    {
        None = 0,
        DoorKnock = 1,
        Santet = 2
    }
}
