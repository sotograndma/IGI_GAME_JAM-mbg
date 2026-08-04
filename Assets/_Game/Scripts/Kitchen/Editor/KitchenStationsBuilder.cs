using System.Collections.Generic;
using MBG.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MBG.Kitchen
{
    /// <summary>
    /// Menata station dapur di bawah InteriorRoot/Stations dari data
    /// <see cref="KitchenLayoutSO"/>.
    ///
    /// Additive dan idempoten: station dikenali dari <see cref="StationType"/>-nya,
    /// bukan dari nama GameObject, jadi menjalankan tool berkali-kali hanya
    /// memperbarui yang sudah ada — tidak pernah menduplikasi.
    ///
    /// Yang TIDAK pernah disentuh: Door_Interior, InteriorSpawn, Background, Floor,
    /// Wall_Left, Wall_Right, Ceiling. Pintu dan spawn hanya DIBACA — pintu untuk
    /// memeriksa tabrakan, spawn untuk menentukan tinggi berdiri pemain.
    ///
    /// Sumber kebenaran layout adalah KitchenLayoutSO. Tuning manual pada transform
    /// station akan ditimpa saat tool dijalankan lagi — ubah asset-nya, bukan scene.
    /// </summary>
    public static class KitchenStationsBuilder
    {
        const string MenuPath = "Tools/MBG/Build Kitchen Stations";

        const string InteriorRootName = "InteriorRoot";
        const string StationsRootName = "Stations";
        const string DoorName = "Door_Interior";
        const string SpawnName = "InteriorSpawn";

        const string DataFolder = "Assets/_Game/Data";
        const string LayoutAssetPath = DataFolder + "/KitchenLayout.asset";
        const string PrefabFolder = "Assets/_Game/Prefabs/Kitchen";
        const string PrefabPath = PrefabFolder + "/Station_Generic.prefab";
        const string SquareSpritePath = "Assets/_Placeholder/Sprites/square.png";

        // Background interior ada di sorting order -100 dan pemain di 10, jadi
        // station aman di antara keduanya.
        const int HighlightSortingOrder = 0;
        const int VisualSortingOrder = 1;
        const int LabelSortingOrder = 4;

        const float DefaultStandOffsetY = 0.55f;

        [MenuItem(MenuPath, false, 102)]
        public static void BuildKitchenStations()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Kitchen Stations",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("TMP Essentials belum di-import",
                    "Label station memakai TextMeshPro. Import dulu lewat:\n" +
                    "Window > TextMeshPro > Import TMP Essential Resources", "OK");
                Debug.LogError("[MBG] Build Kitchen Stations dibatalkan: TMP Essential Resources belum di-import.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MBG] Tidak ada scene aktif yang valid. Buka Assets/Scenes/SampleScene.unity dulu.");
                return;
            }

            GameObject interiorRoot = FindInScene(scene, InteriorRootName);
            if (interiorRoot == null)
            {
                Debug.LogError($"[MBG] '{InteriorRootName}' tidak ditemukan di scene aktif. " +
                               "Tool ini hanya menambah station ke interior yang sudah ada.");
                return;
            }

            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (square == null)
            {
                Debug.LogError($"[MBG] Sprite placeholder '{SquareSpritePath}' tidak ditemukan. " +
                               "Station butuh sprite kotak untuk visual dan highlight.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Kitchen Stations");

            var log = new List<string>();
            bool changed = false;

            KitchenLayoutSO layout = EnsureLayoutAsset(log, ref changed);
            GameObject prefab = EnsureStationPrefab(square, log, ref changed);
            if (prefab == null)
            {
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            WarnAboutLayoutProblems(layout, interiorRoot);

            Transform stationsRoot = EnsureStationsRoot(interiorRoot, log, ref changed);
            float standY = ResolveStandY(scene, layout);

            changed |= ApplyStations(layout, prefab, stationsRoot, square, standY, log);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = stationsRoot.gameObject;
                Debug.Log("[MBG] Build Kitchen Stations selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).", stationsRoot);
            }
            else
            {
                Debug.Log("[MBG] Station dapur sudah sesuai layout. Tidak ada perubahan.", stationsRoot);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildKitchenStations() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- Asset -------------------------------------------------------

        static KitchenLayoutSO EnsureLayoutAsset(List<string> log, ref bool changed)
        {
            var layout = AssetDatabase.LoadAssetAtPath<KitchenLayoutSO>(LayoutAssetPath);
            if (layout != null) return layout;

            EnsureFolder(DataFolder);

            layout = ScriptableObject.CreateInstance<KitchenLayoutSO>();
            layout.ApplyDefaultLayout();
            AssetDatabase.CreateAsset(layout, LayoutAssetPath);
            AssetDatabase.SaveAssets();

            changed = true;
            log.Add($"{LayoutAssetPath} dibuat dengan layout bawaan " +
                    "(Cooking -4.8, Prep -2.6, Packing +2.6, Handover +4.8). Tidak ikut ter-undo.");
            return layout;
        }

        /// <summary>
        /// Prefab placeholder generik. Ukurannya di sini hanya nilai awal — tiap
        /// instance di-resize dari KitchenLayoutSO.
        /// </summary>
        static GameObject EnsureStationPrefab(Sprite square, List<string> log, ref bool changed)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            EnsureFolder(PrefabFolder);

            var root = new GameObject("Station_Generic");
            var trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(2.5f, 2.3f);
            root.AddComponent<KitchenStation>();

            GameObject highlight = CreateSpriteChild("Highlight", root.transform, square, HighlightSortingOrder);
            highlight.GetComponent<SpriteRenderer>().enabled = false;

            CreateSpriteChild("Visual", root.transform, square, VisualSortingOrder);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.text = "STATION";
            var labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (labelRenderer != null) labelRenderer.sortingOrder = LabelSortingOrder;

            var stand = new GameObject("StandPoint");
            stand.transform.SetParent(root.transform, false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            if (prefab == null)
            {
                Debug.LogError($"[MBG] Gagal menyimpan prefab ke {PrefabPath}.");
                return null;
            }

            changed = true;
            log.Add($"{PrefabPath} dibuat (root + Highlight + Visual + Label + StandPoint). Tidak ikut ter-undo.");
            return prefab;
        }

        static GameObject CreateSpriteChild(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        // ---- Scene -------------------------------------------------------

        static Transform EnsureStationsRoot(GameObject interiorRoot, List<string> log, ref bool changed)
        {
            Transform existing = interiorRoot.transform.Find(StationsRootName);
            if (existing != null) return existing;

            var go = new GameObject(StationsRootName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {StationsRootName}");
            Undo.SetTransformParent(go.transform, interiorRoot.transform, $"Parent {StationsRootName}");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            changed = true;
            log.Add($"'{StationsRootName}' dibuat di bawah {InteriorRootName}.");
            return go.transform;
        }

        /// <summary>
        /// Tinggi berdiri pemain, dibaca (tanpa diubah) dari InteriorSpawn supaya
        /// StandPoint sejajar dengan posisi pemain sungguhan.
        /// </summary>
        static float ResolveStandY(Scene scene, KitchenLayoutSO layout)
        {
            GameObject spawn = FindInScene(scene, SpawnName);
            if (spawn != null) return spawn.transform.position.y;

            return layout.floorSurfaceY + DefaultStandOffsetY;
        }

        static void WarnAboutLayoutProblems(KitchenLayoutSO layout, GameObject interiorRoot)
        {
            foreach (string problem in layout.Validate())
                Debug.LogWarning($"[MBG] Layout dapur: {problem}", layout);

            // Verifikasi silang terhadap pintu yang sungguhan ada di scene — angka
            // doorOffsetX di asset bisa saja tertinggal dari scene.
            Transform door = interiorRoot.transform.Find(DoorName);
            if (door == null) return;

            float actualOffset = door.position.x - layout.roomCenterX;
            if (Mathf.Abs(actualOffset - layout.doorOffsetX) > 0.05f)
            {
                Debug.LogWarning($"[MBG] doorOffsetX di KitchenLayout ({layout.doorOffsetX:0.###}) tidak cocok " +
                                 $"dengan posisi {DoorName} di scene ({actualOffset:0.###}). " +
                                 "Perbarui asset-nya supaya validasi tabrakan tetap benar.", layout);
            }
        }

        // ---- Station -----------------------------------------------------

        static bool ApplyStations(KitchenLayoutSO layout, GameObject prefab, Transform stationsRoot,
                                  Sprite square, float standY, List<string> log)
        {
            bool changed = false;

            var existing = new Dictionary<StationType, KitchenStation>();
            foreach (KitchenStation station in stationsRoot.GetComponentsInChildren<KitchenStation>(true))
            {
                if (existing.ContainsKey(station.Type))
                {
                    Debug.LogWarning($"[MBG] Ada lebih dari satu station bertipe {station.Type} " +
                                     $"('{station.name}' diabaikan).", station);
                    continue;
                }
                existing[station.Type] = station;
            }

            foreach (KitchenLayoutSO.StationEntry entry in layout.stations)
            {
                if (entry == null) continue;

                if (!existing.TryGetValue(entry.type, out KitchenStation station) || station == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stationsRoot);
                    Undo.RegisterCreatedObjectUndo(instance, $"Create Station_{entry.type}");
                    instance.name = $"Station_{entry.type}";

                    station = instance.GetComponent<KitchenStation>();
                    changed = true;
                    log.Add($"Station_{entry.type} dibuat di offset x {entry.offsetX:0.##}.");
                }

                changed |= ConfigureStation(station, entry, layout, square, standY);
            }

            return changed;
        }

        static bool ConfigureStation(KitchenStation station, KitchenLayoutSO.StationEntry entry,
                                     KitchenLayoutSO layout, Sprite square, float standY)
        {
            GameObject go = station.gameObject;
            Undo.RecordObject(go.transform, "Configure Station");

            bool changed = false;

            Vector2 center = layout.GetStationCenter(entry);
            if ((Vector2)go.transform.position != center)
            {
                go.transform.position = new Vector3(center.x, center.y, go.transform.position.z);
                changed = true;
            }

            Vector3 spriteSize = square.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) spriteSize = Vector3.one;

            // Zona interaksi
            var trigger = go.GetComponent<BoxCollider2D>();
            if (trigger != null)
            {
                Undo.RecordObject(trigger, "Configure Station");
                var size = new Vector2(entry.width + layout.interactionPadding * 2f,
                                       entry.height + layout.interactionPadding * 2f);
                if (trigger.size != size || !trigger.isTrigger || trigger.offset != Vector2.zero)
                {
                    trigger.size = size;
                    trigger.offset = Vector2.zero;
                    trigger.isTrigger = true;
                    changed = true;
                }
            }

            // Visual
            Transform visual = go.transform.Find("Visual");
            var visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (visualRenderer != null)
            {
                changed |= ApplySpriteBox(visual, visualRenderer, spriteSize,
                                          entry.width, entry.height, entry.color, VisualSortingOrder);
            }

            // Highlight: sedikit lebih besar dan digambar di belakang visual.
            Transform highlight = go.transform.Find("Highlight");
            var highlightRenderer = highlight != null ? highlight.GetComponent<SpriteRenderer>() : null;
            if (highlightRenderer != null)
            {
                var color = layout.highlightColor;
                color.a = 0f;
                changed |= ApplySpriteBox(highlight, highlightRenderer, spriteSize,
                                          entry.width + layout.highlightPadding * 2f,
                                          entry.height + layout.highlightPadding * 2f,
                                          color, HighlightSortingOrder);

                if (highlightRenderer.enabled)
                {
                    Undo.RecordObject(highlightRenderer, "Configure Station");
                    highlightRenderer.enabled = false;
                    changed = true;
                }
            }

            // Label
            Transform labelTransform = go.transform.Find("Label");
            var label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                Undo.RecordObject(label, "Configure Station");
                Undo.RecordObject(labelTransform, "Configure Station");

                string text = entry.ResolveLabel();
                Color labelColor = UIStyleTextColor();
                var labelPos = new Vector3(0f, entry.height * 0.5f + layout.labelOffsetY, 0f);

                if (label.text != text || !Mathf.Approximately(label.fontSize, layout.labelFontSize)
                    || label.color != labelColor || labelTransform.localPosition != labelPos)
                {
                    label.text = text;
                    label.fontSize = layout.labelFontSize;
                    label.color = labelColor;
                    label.alignment = TextAlignmentOptions.Center;
                    labelTransform.localPosition = labelPos;
                    changed = true;
                }

                if (label.rectTransform != null)
                {
                    var sizeDelta = new Vector2(Mathf.Max(entry.width + 1f, 2f), 0.6f);
                    if (label.rectTransform.sizeDelta != sizeDelta)
                    {
                        label.rectTransform.sizeDelta = sizeDelta;
                        changed = true;
                    }
                }
            }

            // StandPoint: sejajar dengan tinggi berdiri pemain.
            Transform stand = go.transform.Find("StandPoint");
            if (stand != null)
            {
                Undo.RecordObject(stand, "Configure Station");
                var standLocal = new Vector3(0f, standY - center.y, 0f);
                if (stand.localPosition != standLocal)
                {
                    stand.localPosition = standLocal;
                    changed = true;
                }
            }

            // Field komponen
            var so = new SerializedObject(station);
            changed |= SetEnum(so, "stationType", (int)entry.type);
            changed |= SetString(so, "promptText", entry.promptOverride ?? "");
            changed |= SetObject(so, "playerStandPoint", stand);
            changed |= SetObject(so, "highlight", highlightRenderer);
            changed |= SetFloat(so, "highlightAlpha", layout.highlightAlpha);
            changed |= SetFloat(so, "highlightFadeSpeed", layout.highlightFadeSpeed);
            so.ApplyModifiedProperties();

            return changed;
        }

        static bool ApplySpriteBox(Transform target, SpriteRenderer renderer, Vector3 spriteSize,
                                   float width, float height, Color color, int sortingOrder)
        {
            Undo.RecordObject(target, "Configure Station");
            Undo.RecordObject(renderer, "Configure Station");

            bool changed = false;

            var scale = new Vector3(width / spriteSize.x, height / spriteSize.y, 1f);
            if (target.localScale != scale)
            {
                target.localScale = scale;
                changed = true;
            }

            if (target.localPosition != Vector3.zero)
            {
                target.localPosition = Vector3.zero;
                changed = true;
            }

            if (renderer.color != color)
            {
                renderer.color = color;
                changed = true;
            }

            if (renderer.sortingOrder != sortingOrder)
            {
                renderer.sortingOrder = sortingOrder;
                changed = true;
            }

            return changed;
        }

        /// <summary>Warna label diambil dari UIStyle kalau asset-nya sudah ada.</summary>
        static Color UIStyleTextColor()
        {
            var style = AssetDatabase.LoadAssetAtPath<UIStyle>(DataFolder + "/UIStyle.asset");
            return style != null ? style.textPrimary : Color.white;
        }

        // ---- SerializedProperty helper -----------------------------------

        static bool SetEnum(SerializedObject so, string path, int value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null || p.enumValueIndex == value) return false;
            p.enumValueIndex = value;
            return true;
        }

        static bool SetString(SerializedObject so, string path, string value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null || p.stringValue == value) return false;
            p.stringValue = value;
            return true;
        }

        static bool SetFloat(SerializedObject so, string path, float value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null || Mathf.Approximately(p.floatValue, value)) return false;
            p.floatValue = value;
            return true;
        }

        static bool SetObject(SerializedObject so, string path, Object value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null || p.objectReferenceValue == value) return false;
            p.objectReferenceValue = value;
            return true;
        }

        // ---- Util --------------------------------------------------------

        static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;

                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name) return t.gameObject;
                }
            }
            return null;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
