using UnityEngine;

/// <summary>
/// Smooth top-down follow camera with optional clamping to an area's bounds so
/// the view never shows outside the current room/world. Bounds and snapping are
/// driven by <see cref="AreaManager"/> during transitions.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float smoothTime = 0.15f;
    [SerializeField] Vector2 offset = Vector2.zero;
    [SerializeField] float zPosition = -10f;

    [Header("Bounds")]
    [SerializeField] bool useBounds = false;
    [SerializeField] Vector2 boundsMin;
    [SerializeField] Vector2 boundsMax;

    Camera _cam;
    Vector3 _velocity;

    void Awake() => _cam = GetComponent<Camera>();

    public void SetTarget(Transform t) => target = t;

    public void SetBounds(Vector2 min, Vector2 max)
    {
        boundsMin = min;
        boundsMax = max;
        useBounds = true;
    }

    /// <summary>
    /// Zoom level for the current area. Must be applied before <see cref="SetBounds"/>
    /// so the clamp is computed against the new viewport size, not the old one.
    /// </summary>
    public void SetOrthographicSize(float size)
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        _cam.orthographicSize = size;
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = ClampToBounds(Desired());
        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        pos.z = zPosition;
        transform.position = pos;
    }

    /// <summary>Instantly place the camera on the target (used after a teleport).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        Vector3 desired = ClampToBounds(Desired());
        desired.z = zPosition;
        transform.position = desired;
        _velocity = Vector3.zero;
    }

    Vector3 Desired() => new Vector3(target.position.x + offset.x, target.position.y + offset.y, zPosition);

    Vector3 ClampToBounds(Vector3 p)
    {
        if (!useBounds || _cam == null) return p;

        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        float x = (boundsMax.x - boundsMin.x > 2f * halfW)
            ? Mathf.Clamp(p.x, boundsMin.x + halfW, boundsMax.x - halfW)
            : (boundsMin.x + boundsMax.x) * 0.5f;

        float y = (boundsMax.y - boundsMin.y > 2f * halfH)
            ? Mathf.Clamp(p.y, boundsMin.y + halfH, boundsMax.y - halfH)
            : (boundsMin.y + boundsMax.y) * 0.5f;

        return new Vector3(x, y, p.z);
    }
}
