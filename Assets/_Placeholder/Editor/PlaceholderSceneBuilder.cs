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
    // Row (counted from the bottom of the texture) where the wooden floor starts.
    const float FloorPixelsFromBottom = 11f;

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

    [MenuItem("Tools/Placeholder/Build Scene")]
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
        if (bg != null)
        {
            roomSize = (Vector2)bg.bounds.size * BgScale;
            texHeightPx = bg.rect.height;
            PlaceSpriteAsset("Background", parent, bg, new Vector2(ox, 0f), BgScale, -100);
        }
        else
        {
            // Degrade to a plain box of the same size rather than leaving a
            // half-built scene behind.
            roomSize = new Vector2(12.5f, 5.625f);
            texHeightPx = 180f;
            Sprite("Background_Fallback", parent, new Vector2(ox, 0f), roomSize, ColHouse, -100);
        }

        float halfW = roomSize.x * 0.5f;
        float halfH = roomSize.y * 0.5f;

        // Where the artwork's wooden floor starts, converted from pixels to units.
        float floorTop = -halfH + (FloorPixelsFromBottom / texHeightPx) * roomSize.y;

        // Invisible collision shell — the artwork already draws floor and walls.
        // The floor is deliberately thick so a fast fall can never tunnel through,
        // and the walls sit just outside the room so the playable area matches the
        // visible edges exactly.
        const float floorThickness = 1f;
        const float t = 0.5f;
        SolidBox("Floor", parent, new Vector2(ox, floorTop - floorThickness * 0.5f),
                 new Vector2(roomSize.x, floorThickness));
        SolidBox("Wall_Left", parent, new Vector2(ox - halfW - t * 0.5f, 0f), new Vector2(t, roomSize.y));
        SolidBox("Wall_Right", parent, new Vector2(ox + halfW + t * 0.5f, 0f), new Vector2(t, roomSize.y));
        SolidBox("Ceiling", parent, new Vector2(ox, halfH + t * 0.5f), new Vector2(roomSize.x, t));

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
