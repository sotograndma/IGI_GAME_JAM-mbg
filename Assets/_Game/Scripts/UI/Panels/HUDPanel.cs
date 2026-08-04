namespace MBG.UI
{
    /// <summary>
    /// HUD gameplay: uang, skor, reputasi, hari, timer pesanan.
    /// Satu-satunya panel yang terlihat sejak scene mulai.
    ///
    /// TODO: field TMP_Text + subscribe GameEventBus.OnGoldChanged /
    /// OnScoreChanged / OnReputationChanged / OnOrderProgress di OnEnable,
    /// unsubscribe di OnDisable.
    /// </summary>
    public class HUDPanel : UIPanel
    {
    }
}
