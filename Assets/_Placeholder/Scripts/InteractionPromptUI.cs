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
