using System.Collections.Generic;
using MBG.Core;
using UnityEngine;

/// <summary>
/// Lives on the player. Interactables register themselves while the player is in
/// their trigger zone; this component shows a prompt for the nearest one and runs
/// its <see cref="IInteractable.Interact"/> when F is pressed.
///
/// The list is type-agnostic — doors and kitchen stations sit in it side by side,
/// and overlapping trigger zones are resolved by picking the closest one. Anything
/// that also implements <see cref="IInteractableFocus"/> is told when it becomes
/// (or stops being) that closest target, which is what drives station highlights.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    readonly List<IInteractable> _inRange = new();

    IInteractable _focused;

    public void Add(IInteractable i)
    {
        if (i != null && !_inRange.Contains(i)) _inRange.Add(i);
    }

    public void Remove(IInteractable i) => _inRange.Remove(i);

    void Update()
    {
        // Drop destroyed / disabled interactables (e.g. doors in a deactivated area).
        _inRange.RemoveAll(i =>
        {
            var mb = i as MonoBehaviour;
            return mb == null || !mb.isActiveAndEnabled;
        });

        IInteractable nearest = GetNearest();
        SetFocus(nearest);

        if (nearest != null)
        {
            if (InteractionPromptUI.Instance != null) InteractionPromptUI.Instance.Show(nearest.Prompt);

            if (InputService.InteractPressed)
                nearest.Interact(this);
        }
        else if (InteractionPromptUI.Instance != null)
        {
            InteractionPromptUI.Instance.Hide();
        }
    }

    void OnDisable() => SetFocus(null);

    void SetFocus(IInteractable next)
    {
        if (ReferenceEquals(next, _focused)) return;

        NotifyFocus(_focused, entered: false);
        _focused = next;
        NotifyFocus(_focused, entered: true);
    }

    static void NotifyFocus(IInteractable target, bool entered)
    {
        if (target is not IInteractableFocus focusTarget) return;

        // The previous target may have been destroyed since we cached it; Unity's
        // overloaded == catches that case where a plain null check would not.
        if (target is MonoBehaviour mb && mb == null) return;

        if (entered) focusTarget.OnFocusEnter();
        else focusTarget.OnFocusExit();
    }

    IInteractable GetNearest()
    {
        IInteractable best = null;
        float bestSqr = float.MaxValue;
        Vector2 p = transform.position;

        foreach (var i in _inRange)
        {
            var mb = i as MonoBehaviour;
            if (mb == null) continue;
            float d = ((Vector2)mb.transform.position - p).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = i; }
        }
        return best;
    }
}
