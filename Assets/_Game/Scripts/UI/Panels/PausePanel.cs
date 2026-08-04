namespace MBG.UI
{
    /// <summary>
    /// Menu jeda: Lanjut, Ulangi, Keluar.
    ///
    /// TODO: tombol TMP + GameManager.ChangeState(GameState.Paused/Playing).
    /// Jangan mem-pause lewat Time.timeScale — GameManager sudah mengurus
    /// GameClock.PushPause/PopPause saat masuk & keluar state Paused.
    /// </summary>
    public class PausePanel : UIPanel
    {
    }
}
