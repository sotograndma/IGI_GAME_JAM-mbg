using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player movement with two modes, switched per area by <see cref="AreaManager"/>:
///   - TopDown  (Stardew style): 8-directional WASD, no gravity, no jump.
///   - SideView (side-scroller): A/D only, gravity pulls the player onto the floor.
/// Both share the same acceleration model; only the axes they drive differ.
/// Uses the new Input System (legacy Input is disabled in this project).
///
/// Controls: WASD (or A/D in side view) = move, Shift = sprint.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController2D : MonoBehaviour
{
    public enum MoveMode { TopDown, SideView }

    [Header("Mode")]
    [SerializeField] MoveMode mode = MoveMode.TopDown;

    [Header("Move (top-down)")]
    [SerializeField] float walkSpeed = 5.5f;
    [SerializeField] float sprintSpeed = 9f;
    [SerializeField] float acceleration = 60f;
    [SerializeField] float deceleration = 70f;

    [Header("Move (side view)")]
    [SerializeField] float sideWalkSpeed = 5f;
    [SerializeField] float sideSprintSpeed = 8f;
    [SerializeField] float sideGravityScale = 3.5f;

    Rigidbody2D _rb;
    Collider2D _col;
    SpriteRenderer _sr;

    Vector2 _input;
    bool _sprint;
    bool _frozen;
    Vector2 _facing = Vector2.down;

    public Vector2 Facing => _facing;
    public MoveMode Mode => mode;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _sr = GetComponentInChildren<SpriteRenderer>();

        _rb.linearDamping = 0f;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Frictionless so the player never sticks to walls (and never drags along
        // the side-view floor, where X velocity is driven directly).
        _col.sharedMaterial = new PhysicsMaterial2D("PlayerNoFriction")
        {
            friction = 0f,
            bounciness = 0f
        };

        ApplyMode();
    }

    /// <summary>Switch movement mode (called by area transitions).</summary>
    public void SetMode(MoveMode newMode)
    {
        mode = newMode;
        ApplyMode();
    }

    void ApplyMode()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();

        _rb.gravityScale = (mode == MoveMode.SideView) ? sideGravityScale : 0f;
        _rb.linearVelocity = Vector2.zero;
        _input = Vector2.zero;
        _facing = (mode == MoveMode.SideView) ? Vector2.right : Vector2.down;
    }

    void Update()
    {
        if (_frozen) { _input = Vector2.zero; return; }

        var kb = Keyboard.current;
        if (kb == null) { _input = Vector2.zero; return; }

        float x = 0f;
        if (kb.aKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed) x += 1f;

        if (mode == MoveMode.SideView)
        {
            // Side view: horizontal only. W/S are deliberately ignored — the
            // vertical axis belongs to gravity.
            _input = new Vector2(x, 0f);
        }
        else
        {
            float y = 0f;
            if (kb.sKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed) y += 1f;

            _input = new Vector2(x, y);
            if (_input.sqrMagnitude > 1f) _input = _input.normalized; // normalize diagonals
        }

        _sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

        if (_input.sqrMagnitude > 0.01f)
        {
            _facing = _input;
            if (_sr != null && Mathf.Abs(x) > 0.01f) _sr.flipX = x < 0f;
        }
    }

    void FixedUpdate()
    {
        if (mode == MoveMode.SideView)
        {
            float targetX = _frozen ? 0f : _input.x * (_sprint ? sideSprintSpeed : sideWalkSpeed);
            float rateX = (!_frozen && Mathf.Abs(_input.x) > 0.01f) ? acceleration : deceleration;

            // Only drive X; Y stays whatever gravity/collisions produced.
            float vx = Mathf.MoveTowards(_rb.linearVelocity.x, targetX, rateX * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);
            return;
        }

        if (_frozen) { _rb.linearVelocity = Vector2.zero; return; }

        Vector2 target = _input * (_sprint ? sprintSpeed : walkSpeed);
        float rate = (_input.sqrMagnitude > 0.01f) ? acceleration : deceleration;
        _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, target, rate * Time.fixedDeltaTime);
    }

    /// <summary>Instantly move the player (used by area transitions).</summary>
    public void Teleport(Vector2 pos)
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.position = pos;
        transform.position = pos;
    }

    /// <summary>Freeze/unfreeze input+velocity (used while a transition fades).</summary>
    public void SetFrozen(bool frozen)
    {
        _frozen = frozen;
        _input = Vector2.zero;
        if (_rb == null) return;

        // In side view, zeroing Y as well would leave the player hanging in mid-air
        // for the duration of the fade; let gravity keep it grounded.
        _rb.linearVelocity = (mode == MoveMode.SideView)
            ? new Vector2(0f, _rb.linearVelocity.y)
            : Vector2.zero;
    }
}
