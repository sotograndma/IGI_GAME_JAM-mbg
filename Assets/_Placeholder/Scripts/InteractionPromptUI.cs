using TMPro;
using UnityEngine;

/// <summary>
/// Screen-space "press F" prompt shown at the bottom-center of the screen while
/// the player is near an interactable. Simple singleton for easy access from
/// <see cref="PlayerInteractor"/>.
///
/// The label is a TextMeshPro text — legacy UnityEngine.UI.Text is not used
/// anywhere in this project. Run Tools > MBG > Build UI Hierarchy after pulling
/// this change: it swaps the component on the existing Label object and re-binds
/// the reference below, keeping its RectTransform untouched.
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance { get; private set; }

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text label;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string message) => Show(message, Color.white);

    /// <summary>Tinted variant, used when an interactable overrides its prompt.</summary>
    public void Show(string message, Color tint)
    {
        if (panel != null) panel.SetActive(true);
        if (label == null) return;

        label.text = $"[F] {message}";
        label.color = tint;
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
