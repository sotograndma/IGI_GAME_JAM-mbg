#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// One-click placeholder generator for the top-down (Stardew-like) prototype.
/// Menu: Tools > Placeholder > Build Scene.
///
/// Builds two areas in one scene:
///   - Exterior (origin): top-down. Green tilemap + border walls + a solid house
///     with a door portal + spawn point.
///   - Interior (offset x=1000): side-scroller built around the artwork at
///     <see cref="InteriorBgPath"/>. Room size, camera zoom and floor height are
///     all derived from that sprite, so swapping the art or changing its import
///     settings only needs the two tuning knobs below.
/// Plus a player, a follow camera, an "[F] prompt" UI, a fade overlay and an
/// AreaManager wired to switch areas, movement modes and camera zoom. Idempotent.
/// </summary>
public static class PlaceholderSceneBuilder
{
    const string RootName = "__Placeholder";
    const string PlaceholderFolder = "Assets/_Placeholder";
    const string SpriteFolder = PlaceholderFolder + "/Sprites";
    const string SpritePath = SpriteFolder + "/square.png";
    const string TileFolder = PlaceholderFolder + "/Tiles";
    const string TilePath = TileFolder + "/green.asset";

    /// <summary>Interior artwork: 400x180 px, 64 PPU, sprite mode "Multiple".</summary>
    const string InteriorBgPath = "Assets/Assets/HighFIdelity.png";

    // ---- Interior tuning knobs ------------------------------------------------
    // Uniform scale applied to the background. At the asset's 64 PPU this makes the
    // room 12.5 x 5.625 units, which sizes the artwork's implied human (~30 px, from
    // the vending machines) to roughly the player's 1.1 units. Keep it a whole
    // number so the pixel art stays crisp.
    const float BgScale = 2f;
    // Row (counted from the bottom of the texture) the player's feet rest on.
    // 0 = the very bottom edge of the artwork, which is what the scene was hand-tuned
    // to. For reference, a pixel scan puts the top of the drawn wooden floor band at
    // row 11 — switch to that if the character should stand behind the band instead.
    const float FloorPixelsFromBottom = 0f;

    const float InteriorOffsetX = 1000f;

    static Sprite _square;

    // Colors approximated from the reference screenshots.
    static readonly Color ColGround = new Color(0.05f, 0.62f, 0.05f);
    static readonly Color ColPlayer = new Color(0.84f, 0.25f, 0.24f);
    static readonly Color ColHouse  = new Color(0.60f, 0.72f, 0.98f);
    static readonly Color ColDoor   = new Color(0.42f, 0.25f, 0.11f);
    static readonly Color ColKnob   = new Color(0.98f, 0.85f, 0.10f);

    // Exterior bounds (also the tilemap fill + camera clamp).
    static readonly Vector2 ExtMin = new Vector2(-25f, -18f);
    static readonly Vector2 ExtMax = new Vector2(25f, 18f);
    const float ExtOrthoSize = 5f;

    // Player body size; the interior spawn is derived from it so the player rests
    // exactly on the floor instead of clipping into it.
    static readonly Vector2 PlayerSize = new Vector2(0.8f, 1.1f);

    /// <summary>Everything BuildScene needs to wire an area into the AreaManager.</summary>
    struct AreaLayout
    {
        public Transform spawn;
        public Vector2 min, max;
        public float orthoSize;
    }

    /// <summary>
    /// Guarded entry point. The build itself wipes the whole placeholder hierarchy,
    /// which also throws away any hand-tuning done in the Inspector, so it asks
    /// first and leaves a copy of the scene file behind.
    /// </summary>
    [MenuItem("Tools/Placeholder/Rebuild Scene (DESTRUCTIVE)")]
    public static void RebuildSceneGuarded()
    {
        if (GameObject.Find(RootName) != null)
        {
            Scene open = SceneManager.GetActiveScene();
            string dirtyWarning = open.isDirty
                ? "\n\nWARNING: this scene has UNSAVED changes. They will not be in the " +
                  "backup. Cancel and press Ctrl+S first if you want them kept."
                : "";

            bool ok = EditorUtility.DisplayDialog(
                "Rebuild placeholder scene?",
                $"This deletes the entire '{RootName}' hierarchy and regenerates it from code.\n\n" +
                "Everything hand-adjusted inside it is LOST, including floor height, wall " +
                "positions, door position/scale, spawn points, and anything you added " +
                "under that root yourself.\n\n" +
                "A copy of the scene file on disk will be written to SceneBackups/ first.\n\n" +
                "To only re-fit the interior colliders while keeping every manual change, " +
                "cancel and use Tools > Placeholder > Fit Interior To Background instead." +
                dirtyWarning,
                "Rebuild (destroy)", "Cancel");

            if (!ok) return;
            BackupSceneFile();
        }

        BuildScene();
    }

    /// <summary>Copies the active scene file to SceneBackups/ outside Assets/.</summary>
    static void BackupSceneFile()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path)) return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, scene.path);
        if (!File.Exists(source)) return;

        // Kept outside Assets/ so Unity does not import the backups as extra scenes.
        string dir = Path.Combine(projectRoot, "SceneBackups");
        Directory.CreateDirectory(dir);

        string dest = Path.Combine(dir,
            $"{Path.GetFileNameWithoutExtension(scene.path)}-{System.DateTime.Now:yyyyMMdd-HHmmss}.unity");
        File.Copy(source, dest, true);
        Debug.Log($"[PlaceholderSceneBuilder] Scene backed up to {dest}");
    }

    public static void BuildScene()
    {
        _square = GetOrCreateSquareSprite();
        TileBase greenTile = GetOrCreateGreenTile();

        Scene scene = SceneManager.GetActiveScene();

        var existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);

        var root = new GameObject(RootName);

        // ---- Areas -------------------------------------------------------
        var exteriorRoot = new GameObject("ExteriorRoot");
        exteriorRoot.transform.SetParent(root.transform);
        AreaLayout exterior = BuildExterior(exteriorRoot.transform, greenTile);

        var interiorRoot = new GameObject("InteriorRoot");
        interiorRoot.transform.SetParent(root.transform);
        AreaLayout interior = BuildInterior(interiorRoot.transform);

        // ---- Player (lives outside the area roots) -----------------------
        var player = BuildPlayer(root.transform, interior.spawn.position);
        var playerController = player.GetComponent<PlayerController2D>();

        // ---- UI ----------------------------------------------------------
        BuildUI(root.transform, out InteractionPromptUI promptUI, out CanvasGroup fadeGroup);

        // ---- Camera ------------------------------------------------------
        CameraFollow2D cameraFollow = SetupCamera(player.transform);

        // ---- AreaManager -------------------------------------------------
        var amGo = new GameObject("AreaManager");
        amGo.transform.SetParent(root.transform);
        var am = amGo.AddComponent<AreaManager>();
        var so = new SerializedObject(am);
        so.FindProperty("interiorRoot").objectReferenceValue = interiorRoot;
        so.FindProperty("exteriorRoot").objectReferenceValue = exteriorRoot;
        so.FindProperty("interiorSpawn").objectReferenceValue = interior.spawn;
        so.FindProperty("exteriorSpawn").objectReferenceValue = exterior.spawn;
        so.FindProperty("player").objectReferenceValue = playerController;
        so.FindProperty("cameraFollow").objectReferenceValue = cameraFollow;
        so.FindProperty("interiorMode").enumValueIndex = (int)PlayerController2D.MoveMode.SideView;
        so.FindProperty("exteriorMode").enumValueIndex = (int)PlayerController2D.MoveMode.TopDown;
        so.FindProperty("interiorMin").vector2Value = interior.min;
        so.FindProperty("interiorMax").vector2Value = interior.max;
        so.FindProperty("exteriorMin").vector2Value = exterior.min;
        so.FindProperty("exteriorMax").vector2Value = exterior.max;
        so.FindProperty("interiorOrthoSize").floatValue = interior.orthoSize;
        so.FindProperty("exteriorOrthoSize").floatValue = exterior.orthoSize;
        so.FindProperty("fadeGroup").objectReferenceValue = fadeGroup;
        so.FindProperty("fadeTime").floatValue = 0.2f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = player;
        Debug.Log("[PlaceholderSceneBuilder] Scene built: top-down exterior + side-view interior. " +
                  "Press Play (start inside the house). Save with Ctrl+S.");
    }

    // ================================================================ areas

    static AreaLayout BuildExterior(Transform parent, TileBase greenTile)
    {
        // Ground tilemap.
        var gridGo = new GameObject("Grid");
        gridGo.transform.SetParent(parent);
        var grid = gridGo.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        var tmGo = new GameObject("Ground_Tilemap");
        tmGo.transform.SetParent(gridGo.transform);
        var tilemap = tmGo.AddComponent<Tilemap>();
        var tmr = tmGo.AddComponent<TilemapRenderer>();
        tmr.sortingOrder = -10;
        for (int x = (int)ExtMin.x; x < (int)ExtMax.x; x++)
            for (int y = (int)ExtMin.y; y < (int)ExtMax.y; y++)
                tilemap.SetTile(new Vector3Int(x, y, 0), greenTile);

        // Border walls (invisible) keep the player inside the green world.
        float t = 1f;
        SolidBox("Border_Top", parent, new Vector2(0f, ExtMax.y), new Vector2(ExtMax.x - ExtMin.x + t, t));
        SolidBox("Border_Bottom", parent, new Vector2(0f, ExtMin.y), new Vector2(ExtMax.x - ExtMin.x + t, t));
        SolidBox("Border_Left", parent, new Vector2(ExtMin.x, 0f), new Vector2(t, ExtMax.y - ExtMin.y + t));
        SolidBox("Border_Right", parent, new Vector2(ExtMax.x, 0f), new Vector2(t, ExtMax.y - ExtMin.y + t));

        // House (solid) with a door on its bottom edge.
        var house = Sprite("House", parent, new Vector2(0f, 4f), new Vector2(10f, 6f), ColHouse, 0);
        house.AddComponent<BoxCollider2D>(); // default size (1,1) * scale = 10x6

        Sprite("DoorVisual", parent, new Vector2(0f, 1.4f), new Vector2(1.6f, 2.6f), ColDoor, 1);
        Sprite("DoorKnob", parent, new Vector2(0.45f, 1.4f), new Vector2(0.16f, 0.16f), ColKnob, 2);

        MakeDoor("Door_Exterior", parent, new Vector2(0f, -0.3f), new Vector2(2.6f, 2.4f),
                 AreaManager.Area.Interior, "Masuk ke rumah");

        return new AreaLayout
        {
            spawn = Empty("ExteriorSpawn", parent, new Vector2(0f, -2.5f)).transform,
            min = ExtMin,
            max = ExtMax,
            orthoSize = ExtOrthoSize
        };
    }

    /// <summary>
    /// Side-scroller room built around the interior artwork. The room extents,
    /// camera zoom and floor height are all derived from the sprite, so the
    /// collision shell can never drift away from what the player sees.
    /// </summary>
    static AreaLayout BuildInterior(Transform parent)
    {
        float ox = InteriorOffsetX;

        Sprite bg = LoadInteriorBackground();

        Vector2 roomSize;
        float texHeightPx;
        GameObject bgGo;
        if (bg != null)
        {
            roomSize = (Vector2)bg.bounds.size * BgScale;
            texHeightPx = bg.rect.height;
            bgGo = PlaceSpriteAsset("Background", parent, bg, new Vector2(ox, 0f), BgScale, -100);
        }
        else
        {
            // Degrade to a plain box of the same size rather than leaving a
            // half-built scene behind. Same name, so the fit tool still finds it.
            roomSize = new Vector2(12.5f, 5.625f);
            texHeightPx = 180f;
            bgGo = Sprite("Background", parent, new Vector2(ox, 0f), roomSize, ColHouse, -100);
        }

        float halfW = roomSize.x * 0.5f;
        float halfH = roomSize.y * 0.5f;

        // Where the player's feet rest, converted from texture pixels to units.
        float floorTop = -halfH + (FloorPixelsFromBottom / texHeightPx) * roomSize.y;

        // Invisible collision shell — the artwork already draws floor and walls.
        // Created here but positioned by ApplyRoomLayout, so a full rebuild and a
        // non-destructive fit run through exactly the same layout code.
        const float floorThickness = 1f;
        const float wallThickness = 0.5f;
        var floorGo = SolidBox("Floor", parent, Vector2.zero, Vector2.one);
        var wallLeftGo = SolidBox("Wall_Left", parent, Vector2.zero, Vector2.one);
        var wallRightGo = SolidBox("Wall_Right", parent, Vector2.zero, Vector2.one);
        var ceilingGo = SolidBox("Ceiling", parent, Vector2.zero, Vector2.one);

        var fitter = parent.gameObject.AddComponent<InteriorRoomFitter>();
        var fso = new SerializedObject(fitter);
        fso.FindProperty("background").objectReferenceValue = bgGo.GetComponent<SpriteRenderer>();
        fso.FindProperty("floor").objectReferenceValue = floorGo.GetComponent<BoxCollider2D>();
        fso.FindProperty("wallLeft").objectReferenceValue = wallLeftGo.GetComponent<BoxCollider2D>();
        fso.FindProperty("wallRight").objectReferenceValue = wallRightGo.GetComponent<BoxCollider2D>();
        fso.FindProperty("ceiling").objectReferenceValue = ceilingGo.GetComponent<BoxCollider2D>();
        fso.FindProperty("floorSurfaceY").floatValue = floorTop;
        fso.FindProperty("floorThickness").floatValue = floorThickness;
        fso.FindProperty("wallThickness").floatValue = wallThickness;
        fso.FindProperty("contactCompensation").floatValue = Physics2D.defaultContactOffset;
        fso.ApplyModifiedPropertiesWithoutUndo();

        ApplyRoomLayout(fitter, recordUndo: false);

        // Door: centred in the room, standing on the floor.
        const float doorW = 0.9f, doorH = 1.7f;
        float doorY = floorTop + doorH * 0.5f;
        Sprite("DoorVisual_Int", parent, new Vector2(ox, doorY), new Vector2(doorW, doorH), ColDoor, 1);
        Sprite("DoorKnob_Int", parent, new Vector2(ox + 0.28f, doorY - 0.1f), new Vector2(0.1f, 0.1f), ColKnob, 2);

        MakeDoor("Door_Interior", parent, new Vector2(ox, doorY), new Vector2(doorW + 1.2f, doorH + 0.3f),
                 AreaManager.Area.Exterior, "Keluar rumah");

        // Spawn resting on the floor, clear of the door trigger so the prompt only
        // appears once the player actually walks up to the door.
        Transform spawn = Empty("InteriorSpawn", parent,
                                new Vector2(ox - 3f, floorTop + PlayerSize.y * 0.5f + 0.02f)).transform;

        return new AreaLayout
        {
            spawn = spawn,
            min = new Vector2(ox - halfW, -halfH),
            max = new Vector2(ox + halfW, halfH),
            // Exactly half the room height: this makes CameraFollow2D's
            // "bounds taller than the viewport" test fail, so it centres — and
            // therefore locks — the Y axis and only pans along X.
            orthoSize = halfH
        };
    }

    /// <summary>
    /// The interior artwork imports with sprite mode "Multiple", which makes the
    /// Sprite a sub-asset: LoadAssetAtPath&lt;Sprite&gt; returns null for it. Scan
    /// the sub-assets instead. Returns null (after logging) if it cannot be found.
    /// </summary>
    static Sprite LoadInteriorBackground()
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(InteriorBgPath))
            if (o is Sprite s) return s;

        Debug.LogError($"[PlaceholderSceneBuilder] Interior background not found at '{InteriorBgPath}'. " +
                       "Using a plain blue box instead. Check that the file exists and that its " +
                       "Texture Type is 'Sprite (2D and UI)'.");
        return null;
    }

    // ======================================================= non-destructive fit

    /// <summary>
    /// Re-fits only the interior's collision shell (floor, side walls, ceiling) to
    /// the background artwork. Everything else in the scene — player, door, spawns,
    /// anything hand-added — is left untouched, which makes this safe to run on a
    /// scene that has been tuned by hand.
    /// </summary>
    [MenuItem("Tools/Placeholder/Fit Interior To Background")]
    public static void FitInteriorToBackground()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[PlaceholderSceneBuilder] Exit Play mode first — scene changes made " +
                           "while playing are discarded when you stop. Nothing was changed.");
            return;
        }

        Transform interiorRoot = FindInteriorRoot();
        if (interiorRoot == null) return;

        var fitter = interiorRoot.GetComponent<InteriorRoomFitter>();
        bool fresh = fitter == null;
        if (fresh) fitter = Undo.AddComponent<InteriorRoomFitter>(interiorRoot.gameObject);

        WireFitter(fitter, interiorRoot, seedFromScene: fresh);
        if (!ApplyRoomLayout(fitter, recordUndo: true)) return;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = fitter.gameObject;

        float c = fitter.ContactCompensation;
        Debug.Log($"[PlaceholderSceneBuilder] Interior shell re-fitted. Floor surface " +
                  $"{fitter.FloorSurfaceY}, contact compensation {c}. Player sprite should now " +
                  $"rest flush against the artwork. Tune 'floorSurfaceY' on the InteriorRoomFitter " +
                  $"and run this again if the feet are still off. Save with Ctrl+S.");
    }

    static Transform FindInteriorRoot()
    {
        var root = GameObject.Find(RootName);
        Transform interiorRoot = root != null ? root.transform.Find("InteriorRoot") : null;
        if (interiorRoot == null)
        {
            Debug.LogError($"[PlaceholderSceneBuilder] Could not find '{RootName}/InteriorRoot' in " +
                           "the open scene. Nothing was changed.");
        }
        return interiorRoot;
    }

    /// <summary>
    /// Fills in any unassigned references by child name. When the component has just
    /// been added, it also adopts the floor's current height so hand-tuning already
    /// done in the Inspector becomes the value the layout rebuilds from, rather than
    /// being silently overwritten on the very first run.
    /// </summary>
    static void WireFitter(InteriorRoomFitter fitter, Transform interiorRoot, bool seedFromScene)
    {
        var so = new SerializedObject(fitter);

        if (fitter.Background == null)
        {
            Transform t = interiorRoot.Find("Background");
            so.FindProperty("background").objectReferenceValue =
                t != null ? t.GetComponent<SpriteRenderer>() : null;
        }
        BindCollider(so, "floor", fitter.Floor, interiorRoot, "Floor");
        BindCollider(so, "wallLeft", fitter.WallLeft, interiorRoot, "Wall_Left");
        BindCollider(so, "wallRight", fitter.WallRight, interiorRoot, "Wall_Right");
        BindCollider(so, "ceiling", fitter.Ceiling, interiorRoot, "Ceiling");
        so.ApplyModifiedPropertiesWithoutUndo();

        if (!seedFromScene || fitter.Floor == null) return;

        Transform ft = fitter.Floor.transform;
        float worldThickness = fitter.Floor.size.y * Mathf.Abs(ft.lossyScale.y);
        so.Update();
        so.FindProperty("floorSurfaceY").floatValue = ft.position.y + worldThickness * 0.5f;
        so.FindProperty("floorThickness").floatValue = worldThickness;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BindCollider(SerializedObject so, string field, BoxCollider2D current,
                             Transform parent, string childName)
    {
        if (current != null) return;
        Transform t = parent.Find(childName);
        so.FindProperty(field).objectReferenceValue = t != null ? t.GetComponent<BoxCollider2D>() : null;
    }

    /// <summary>
    /// The single source of truth for the interior's collision shell, shared by the
    /// full rebuild and the non-destructive fit so the two can never disagree.
    /// </summary>
    static bool ApplyRoomLayout(InteriorRoomFitter fitter, bool recordUndo)
    {
        if (fitter.Background == null)
        {
            Debug.LogError("[PlaceholderSceneBuilder] InteriorRoomFitter has no Background " +
                           "SpriteRenderer assigned and none named 'Background' was found. " +
                           "Nothing was changed.");
            return false;
        }

        Bounds bg = fitter.Background.bounds;
        float c = fitter.ContactCompensation;
        float wt = fitter.WallThickness;
        float ft = fitter.FloorThickness;

        // Each surface is pushed outward by the contact offset, because Box2D parks a
        // resting body that far from what it is touching. Compensating here means the
        // player's *sprite* ends up flush with the artwork.
        float leftInner = bg.min.x - c;
        float rightInner = bg.max.x + c;
        float ceilInner = bg.max.y + c;
        float floorTop = fitter.FloorSurfaceY - c;

        bool ok = true;
        ok &= SetBox(fitter.Floor, "Floor",
                     new Vector2(bg.center.x, floorTop - ft * 0.5f), new Vector2(bg.size.x, ft), recordUndo);
        ok &= SetBox(fitter.WallLeft, "Wall_Left",
                     new Vector2(leftInner - wt * 0.5f, bg.center.y), new Vector2(wt, bg.size.y), recordUndo);
        ok &= SetBox(fitter.WallRight, "Wall_Right",
                     new Vector2(rightInner + wt * 0.5f, bg.center.y), new Vector2(wt, bg.size.y), recordUndo);
        ok &= SetBox(fitter.Ceiling, "Ceiling",
                     new Vector2(bg.center.x, ceilInner + wt * 0.5f), new Vector2(bg.size.x, wt), recordUndo);
        return ok;
    }

    /// <summary>Places a box collider by world position and world size.</summary>
    static bool SetBox(BoxCollider2D box, string label, Vector2 worldPos, Vector2 worldSize, bool recordUndo)
    {
        if (box == null)
        {
            Debug.LogError($"[PlaceholderSceneBuilder] Interior collider '{label}' is missing. " +
                           "Assign it on the InteriorRoomFitter, or run the destructive rebuild.");
            return false;
        }

        Transform t = box.transform;
        if (recordUndo)
        {
            Undo.RecordObject(t, "Fit Interior To Background");
            Undo.RecordObject(box, "Fit Interior To Background");
        }

        t.position = worldPos;

        // m_Size is in local space, so undo the transform's scale to land on the
        // requested world size even if the object was scaled by hand.
        Vector3 s = t.lossyScale;
        float sx = Mathf.Approximately(s.x, 0f) ? 1f : Mathf.Abs(s.x);
        float sy = Mathf.Approximately(s.y, 0f) ? 1f : Mathf.Abs(s.y);
        box.size = new Vector2(worldSize.x / sx, worldSize.y / sy);
        return true;
    }

    // ============================================================== diagnostics

    /// <summary>
    /// Prints the interior's real measurements. Run this while in Play mode with the
    /// player pressed against a wall: it reports the actual gap between the player's
    /// collider and each surface, in world units and in on-screen pixels, so the
    /// cause of any visible gap can be read off instead of guessed at.
    /// </summary>
    [MenuItem("Tools/Placeholder/Log Interior Diagnostics")]
    public static void LogInteriorDiagnostics()
    {
        Transform interiorRoot = FindInteriorRoot();
        if (interiorRoot == null) return;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        var playerCol = playerGo != null ? playerGo.GetComponent<Collider2D>() : null;
        var cam = Camera.main;

        if (playerCol == null || cam == null)
        {
            Debug.LogError("[Diagnostics] Need a 'Player' tagged object with a Collider2D and a " +
                           "MainCamera in the scene.");
            return;
        }

        SpriteRenderer bgSr = null;
        Transform bgT = interiorRoot.Find("Background");
        if (bgT != null) bgSr = bgT.GetComponent<SpriteRenderer>();

        BoxCollider2D left = FindBox(interiorRoot, "Wall_Left");
        BoxCollider2D right = FindBox(interiorRoot, "Wall_Right");
        BoxCollider2D floor = FindBox(interiorRoot, "Floor");

        // Pixels per world unit, taken from the camera's own render target rather than
        // the editor window, so it is correct regardless of which view has focus.
        float pxPerUnit = cam.orthographic && cam.orthographicSize > 0f
            ? cam.pixelHeight / (2f * cam.orthographicSize)
            : 0f;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== INTERIOR DIAGNOSTICS =====");
        sb.AppendLine($"Play mode          : {Application.isPlaying}");
        sb.AppendLine($"Camera             : pos {cam.transform.position}  ortho {cam.orthographicSize}  " +
                      $"aspect {cam.aspect:F4}  viewport {cam.pixelWidth}x{cam.pixelHeight} px");
        sb.AppendLine($"Scale              : {pxPerUnit:F2} screen px per world unit " +
                      $"(1 px = {(pxPerUnit > 0f ? 1f / pxPerUnit : 0f):F5} units)");
        sb.AppendLine($"Physics2D contact  : {Physics2D.defaultContactOffset}");

        Bounds pb = playerCol.bounds;
        sb.AppendLine($"Player collider    : x {pb.min.x:F4} .. {pb.max.x:F4}   y {pb.min.y:F4} .. {pb.max.y:F4}");

        if (bgSr != null)
        {
            Bounds bb = bgSr.bounds;
            sb.AppendLine($"Background artwork : x {bb.min.x:F4} .. {bb.max.x:F4}   y {bb.min.y:F4} .. {bb.max.y:F4}");
            Report(sb, "Player -> art LEFT  ", pb.min.x - bb.min.x, pxPerUnit);
            Report(sb, "Player -> art RIGHT ", bb.max.x - pb.max.x, pxPerUnit);
            Report(sb, "Player -> art BOTTOM", pb.min.y - bb.min.y, pxPerUnit);
        }

        if (left != null) Report(sb, "Player -> Wall_Left ", pb.min.x - left.bounds.max.x, pxPerUnit);
        if (right != null) Report(sb, "Player -> Wall_Right", right.bounds.min.x - pb.max.x, pxPerUnit);
        if (floor != null) Report(sb, "Player -> Floor top ", pb.min.y - floor.bounds.max.y, pxPerUnit);

        var follow = cam.GetComponent<CameraFollow2D>();
        if (follow != null && bgSr != null)
        {
            float halfW = cam.orthographicSize * cam.aspect;
            Bounds bb = bgSr.bounds;
            bool clampsX = (bb.max.x - bb.min.x) > 2f * halfW;
            sb.AppendLine($"Camera X clamp     : {(clampsX ? $"[{bb.min.x + halfW:F4} .. {bb.max.x - halfW:F4}]" : "DISABLED - view is wider than the room, camera centres X")}");
            sb.AppendLine($"Camera view spans  : x {cam.transform.position.x - halfW:F4} .. {cam.transform.position.x + halfW:F4}");
        }

        sb.Append("================================");
        Debug.Log(sb.ToString());
    }

    static void Report(System.Text.StringBuilder sb, string label, float gapUnits, float pxPerUnit)
    {
        sb.AppendLine($"{label}: {gapUnits,9:F4} units = {gapUnits * pxPerUnit,8:F2} px");
    }

    static BoxCollider2D FindBox(Transform parent, string childName)
    {
        Transform t = parent.Find(childName);
        return t != null ? t.GetComponent<BoxCollider2D>() : null;
    }

    // ============================================================== helpers

    static GameObject Sprite(string name, Transform parent, Vector2 pos, Vector2 size, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _square;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    /// <summary>
    /// Places a real sprite asset by its <em>bounds centre</em> rather than by its
    /// transform origin, which makes placement independent of the sprite's pivot.
    /// Setting transform.position directly would only be correct for a centred
    /// pivot; this stays correct if the pivot or PPU is changed in the importer,
    /// which matters because the colliders are derived from these same bounds.
    /// </summary>
    static GameObject PlaceSpriteAsset(string name, Transform parent, Sprite sprite,
                                       Vector2 center, float scale, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;

        Vector2 pivotToCenter = (Vector2)sprite.bounds.center * scale;
        go.transform.position = center - pivotToCenter;
        return go;
    }

    static GameObject SolidBox(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var c = go.AddComponent<BoxCollider2D>();
        c.size = size;
        return go;
    }

    static GameObject Empty(string name, Transform parent, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        return go;
    }

    static void MakeDoor(string name, Transform parent, Vector2 pos, Vector2 size, AreaManager.Area target, string prompt)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = true;
        var dp = go.AddComponent<DoorPortal>();

        var so = new SerializedObject(dp);
        so.FindProperty("targetArea").enumValueIndex = (int)target;
        so.FindProperty("promptText").stringValue = prompt;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject BuildPlayer(Transform parent, Vector2 pos)
    {
        var go = new GameObject("Player") { tag = "Player" };
        go.transform.SetParent(parent);
        go.transform.position = pos;

        var vis = new GameObject("Visual");
        vis.transform.SetParent(go.transform);
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localScale = new Vector3(PlayerSize.x, PlayerSize.y, 1f);
        var sr = vis.AddComponent<SpriteRenderer>();
        sr.sprite = _square;
        sr.color = ColPlayer;
        sr.sortingOrder = 10;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = PlayerSize;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        go.AddComponent<PlayerController2D>();
        go.AddComponent<PlayerInteractor>();
        return go;
    }

    static CameraFollow2D SetupCamera(Transform target)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = ExtOrthoSize; // AreaManager overrides this per area
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = ColGround;
        cam.transform.position = new Vector3(target.position.x, target.position.y, -10f);

        var follow = cam.GetComponent<CameraFollow2D>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow2D>();

        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = target;
        so.FindProperty("offset").vector2Value = Vector2.zero;
        so.FindProperty("smoothTime").floatValue = 0.15f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return follow;
    }

    static void BuildUI(Transform parent, out InteractionPromptUI promptUI, out CanvasGroup fadeGroup)
    {
        var canvasGo = new GameObject("UICanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(parent);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Prompt panel (bottom-center).
        var panel = new GameObject("PromptPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 70f);
        prt.sizeDelta = new Vector2(460f, 76f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(panel.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var label = labelGo.GetComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 34;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = "[F] Masuk ke rumah";

        promptUI = canvasGo.AddComponent<InteractionPromptUI>();
        var so = new SerializedObject(promptUI);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Fade overlay (full screen, on top).
        var fade = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        fade.transform.SetParent(canvasGo.transform, false);
        var frt = fade.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        fade.GetComponent<Image>().color = Color.black;
        fadeGroup = fade.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
    }

    // ========================================================= asset gen

    static Sprite GetOrCreateSquareSprite()
    {
        EnsureFolder(SpriteFolder);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite != null) return sprite;

        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var px = new Color32[32 * 32];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 32f;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    static TileBase GetOrCreateGreenTile()
    {
        EnsureFolder(TileFolder);
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
        if (tile != null) return tile;

        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = _square;
        tile.color = ColGround;
        tile.colliderType = Tile.ColliderType.None;
        AssetDatabase.CreateAsset(tile, TilePath);
        AssetDatabase.SaveAssets();
        return tile;
    }

    static void EnsureFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(PlaceholderFolder))
            AssetDatabase.CreateFolder("Assets", "_Placeholder");
        if (!AssetDatabase.IsValidFolder(folder))
        {
            string leaf = folder.Substring(folder.LastIndexOf('/') + 1);
            AssetDatabase.CreateFolder(PlaceholderFolder, leaf);
        }
    }
}
#endif
