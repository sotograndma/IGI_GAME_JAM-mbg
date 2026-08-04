using System.Collections;
using UnityEngine;

/// <summary>
/// Handles moving the player between the two placeholder areas (Interior &
/// Exterior). Each area is a root GameObject toggled on/off; the player, camera,
/// UI and this manager live outside those roots so they persist across a switch.
///
/// Each area also owns a movement mode: the exterior is top-down, the interior is
/// a side-scroller. The mode is applied as part of the transition.
///
/// A transition: freeze player -> fade to black -> activate target root, switch
/// movement mode, teleport player to its spawn, re-bound & snap the camera ->
/// fade back in -> unfreeze.
/// </summary>
public class AreaManager : MonoBehaviour
{
    public enum Area { Interior, Exterior }

    [Header("Areas")]
    [SerializeField] GameObject interiorRoot;
    [SerializeField] GameObject exteriorRoot;
    [SerializeField] Transform interiorSpawn;
    [SerializeField] Transform exteriorSpawn;

    [Header("Actors")]
    [SerializeField] PlayerController2D player;
    [SerializeField] CameraFollow2D cameraFollow;

    [Header("Movement mode per area")]
    [SerializeField] PlayerController2D.MoveMode interiorMode = PlayerController2D.MoveMode.SideView;
    [SerializeField] PlayerController2D.MoveMode exteriorMode = PlayerController2D.MoveMode.TopDown;

    [Header("Camera bounds")]
    [SerializeField] Vector2 interiorMin;
    [SerializeField] Vector2 interiorMax;
    [SerializeField] Vector2 exteriorMin;
    [SerializeField] Vector2 exteriorMax;

    [Header("Camera zoom per area")]
    [SerializeField] float interiorOrthoSize = 2.8125f;
    [SerializeField] float exteriorOrthoSize = 5f;

    [Header("Fade")]
    [SerializeField] CanvasGroup fadeGroup;
    [SerializeField] float fadeTime = 0.2f;

    public static AreaManager Instance { get; private set; }

    Area _current;
    bool _transitioning;

    void Awake() => Instance = this;

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        // Game starts inside the house.
        ApplyArea(Area.Interior);
        if (fadeGroup != null) { fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false; }
    }

    public void GoTo(Area target)
    {
        if (_transitioning || target == _current) return;
        StartCoroutine(Transition(target));
    }

    IEnumerator Transition(Area target)
    {
        _transitioning = true;
        if (player != null) player.SetFrozen(true);

        yield return Fade(1f);
        ApplyArea(target);
        yield return Fade(0f);

        if (player != null) player.SetFrozen(false);
        _transitioning = false;
    }

    void ApplyArea(Area target)
    {
        _current = target;
        bool interior = target == Area.Interior;

        if (interiorRoot != null) interiorRoot.SetActive(interior);
        if (exteriorRoot != null) exteriorRoot.SetActive(!interior);

        if (player != null)
        {
            // Mode first: it resets gravity/velocity, so any leftover fall speed
            // from the side-view area never carries into the top-down one.
            player.SetMode(interior ? interiorMode : exteriorMode);

            Transform spawn = interior ? interiorSpawn : exteriorSpawn;
            if (spawn != null) player.Teleport(spawn.position);
        }

        if (cameraFollow != null)
        {
            // Zoom before bounds: the clamp is derived from the viewport size, so a
            // stale orthographic size here would clamp the camera against the wrong
            // half-extents for one area switch.
            cameraFollow.SetOrthographicSize(interior ? interiorOrthoSize : exteriorOrthoSize);

            if (interior) cameraFollow.SetBounds(interiorMin, interiorMax);
            else cameraFollow.SetBounds(exteriorMin, exteriorMax);
            cameraFollow.SnapToTarget();
        }
    }

    IEnumerator Fade(float toAlpha)
    {
        if (fadeGroup == null) yield break;

        fadeGroup.blocksRaycasts = toAlpha > 0.01f;
        float from = fadeGroup.alpha;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, toAlpha, t / fadeTime);
            yield return null;
        }
        fadeGroup.alpha = toAlpha;
    }
}
