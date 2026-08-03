using UnityEngine;

/// <summary>
/// A door the player interacts with (press F) to travel between the interior and
/// the exterior area. Requires a trigger collider that acts as the proximity zone.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DoorPortal : MonoBehaviour, IInteractable
{
    [SerializeField] AreaManager.Area targetArea = AreaManager.Area.Exterior;
    [SerializeField] string promptText = "Keluar";

    public string Prompt => promptText;

    public void Interact(PlayerInteractor interactor)
    {
        if (AreaManager.Instance != null)
            AreaManager.Instance.GoTo(targetArea);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var pi = other.GetComponentInParent<PlayerInteractor>();
        if (pi != null) pi.Add(this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var pi = other.GetComponentInParent<PlayerInteractor>();
        if (pi != null) pi.Remove(this);
    }
}
