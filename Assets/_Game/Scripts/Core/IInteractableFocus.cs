namespace MBG.Core
{
    /// <summary>
    /// Tambahan opsional untuk <c>IInteractable</c>: memberi tahu objek saat ia
    /// menjadi — atau berhenti menjadi — target interaksi terdekat pemain.
    /// Dipakai untuk highlight visual.
    ///
    /// Sengaja interface terpisah, bukan member baru di IInteractable, supaya
    /// interactable lama (DoorPortal) tidak perlu ikut mengimplementasikannya.
    /// </summary>
    public interface IInteractableFocus
    {
        /// <summary>Objek ini baru saja menjadi target interaksi terdekat.</summary>
        void OnFocusEnter();

        /// <summary>Objek ini berhenti menjadi target interaksi terdekat.</summary>
        void OnFocusExit();
    }
}
