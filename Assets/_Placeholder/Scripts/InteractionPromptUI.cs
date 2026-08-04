using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space "press F" prompt shown at the bottom-center of the screen while
/// the player is near an interactable. Simple singleton for easy access from
/// <see cref="PlayerInteractor"/>.
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance { get; private set; }

    [SerializeField] GameObject panel;
    [SerializeField] Text label;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string message)
    {
        if (panel != null) panel.SetActive(true);
        if (label != null) label.text = $"[F] {message}";
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
