using UnityEngine;

/// <summary>
/// Layout data for the side-scroller interior: the values its collision shell is
/// derived from. The shell is always recomputed from these numbers and from the
/// background's bounds — never from the colliders' current positions — so applying
/// the layout repeatedly is idempotent and can't drift a little further each run.
///
/// This component is inert at runtime. It exists so the room can be re-fitted from
/// Tools &gt; Placeholder &gt; Fit Interior To Background without touching the
/// player, the door, or anything else that was hand-placed in the scene.
/// </summary>
[DisallowMultipleComponent]
public class InteriorRoomFitter : MonoBehaviour
{
    [Header("Room")]
    [SerializeField] SpriteRenderer background;

    [Header("Collision shell")]
    [SerializeField] BoxCollider2D floor;
    [SerializeField] BoxCollider2D wallLeft;
    [SerializeField] BoxCollider2D wallRight;
    [SerializeField] BoxCollider2D ceiling;

    [Header("Layout")]
    [Tooltip("World Y the player's feet should visually rest on. Tune until the feet " +
             "sit exactly on the floor drawn in the artwork, then re-apply.")]
    [SerializeField] float floorSurfaceY = -2.81f;
    [Tooltip("Thickness of the (invisible) floor collider. Generous is good: it stops " +
             "a fast fall from tunnelling through.")]
    [SerializeField] float floorThickness = 1f;
    [SerializeField] float wallThickness = 0.5f;

    [Header("Contact compensation")]
    [Tooltip("Box2D holds resting colliders this far apart — see Project Settings > " +
             "Physics 2D > Default Contact Offset. Each surface is pushed outward by " +
             "this much so the player's sprite comes to rest flush against the " +
             "artwork instead of a hair short of it. Set to 0 to disable.")]
    [SerializeField] float contactCompensation = 0.01f;

    public SpriteRenderer Background => background;
    public BoxCollider2D Floor => floor;
    public BoxCollider2D WallLeft => wallLeft;
    public BoxCollider2D WallRight => wallRight;
    public BoxCollider2D Ceiling => ceiling;

    public float FloorSurfaceY => floorSurfaceY;
    public float FloorThickness => floorThickness;
    public float WallThickness => wallThickness;
    public float ContactCompensation => contactCompensation;
}
