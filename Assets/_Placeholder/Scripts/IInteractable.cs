/// <summary>
/// Anything the player can interact with by pressing F while in range.
/// Implementers are expected to be MonoBehaviours (so their Transform can be used
/// for distance checks by <see cref="PlayerInteractor"/>).
/// </summary>
public interface IInteractable
{
    /// <summary>Short label shown in the interaction prompt, e.g. "Masuk ke rumah".</summary>
    string Prompt { get; }

    /// <summary>Called when the player presses F on this interactable.</summary>
    void Interact(PlayerInteractor interactor);
}
