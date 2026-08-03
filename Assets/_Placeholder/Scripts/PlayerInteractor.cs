using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lives on the player. Interactables register themselves while the player is in
/// their trigger zone; this component shows a prompt for the nearest one and runs
/// its <see cref="IInteractable.Interact"/> when F is pressed.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    readonly List<IInteractable> _inRange = new();

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

        if (nearest != null)
        {
            if (InteractionPromptUI.Instance != null) InteractionPromptUI.Instance.Show(nearest.Prompt);

            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
                nearest.Interact(this);
        }
        else if (InteractionPromptUI.Instance != null)
        {
            InteractionPromptUI.Instance.Hide();
        }
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
