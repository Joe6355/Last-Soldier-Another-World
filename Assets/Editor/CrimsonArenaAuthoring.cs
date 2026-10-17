using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class CrimsonArenaAuthoring
{
    static T Get<T>(UnityEngine.Object obj, string field) => (T)(object)new SerializedObject(obj).FindProperty(field).objectReferenceValue;
    static void Set(UnityEngine.Object obj, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(obj); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SetInt(UnityEngine.Object obj, string field, int value)
    {
        var data = new SerializedObject(obj); data.FindProperty(field).intValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SetBool(UnityEngine.Object obj, string field, bool value)
    {
        var data = new SerializedObject(obj); data.FindProperty(field).boolValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    const string ArtFolder = "Assets/UI/ForestGarrison/Crimson";

    [MenuItem("Tools/Last Soldier/Configure Crimson Arena")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var old = GameObject.Find("CrimsonArena");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        foreach (var panel in Resources.FindObjectsOfTypeAll<RectTransform>().Where(t => t.gameObject.scene == scene
            && t.name.StartsWith("CrimsonBossHealth", StringComparison.Ordinal)).ToArray()) UnityEngine.Object.DestroyImmediate(panel.gameObject);
        var previousGate = GameObject.Find("CrimsonEntrance");
        if (previousGate != null) UnityEngine.Object.DestroyImmediate(previousGate);

        var normal = UnityEngine.Object.FindObjectsOfType<WaveSpawner>().First(w => !w.IsCrimson);
        var sourceBounds = normal.GetComponent<Collider2D>().bounds;
        var offset = new Vector3(160, 0, 0);
        var root = UnityEngine.Object.Instantiate(normal.transform.root.gameObject);
        root.name = "CrimsonArena";
        root.transform.position += offset;
        var red = root.GetComponentInChildren<WaveSpawner>();
        SetBool(red, "crimsonArena", true);
        SetInt(red, "maxConcurrentLateEnemies", 48);

        var box = red.GetComponent<BoxCollider2D>();
        Vector2 center = box.offset, half = box.size * .5f;
        UnityEngine.Object.DestroyImmediate(box);
        var island = red.gameObject.AddComponent<PolygonCollider2D>();
        island.isTrigger = true;
        const float cut = 5f;
        island.points = new[] { center + new Vector2(-half.x + cut, -half.y), center + new Vector2(half.x - cut, -half.y),
            center + new Vector2(half.x, -half.y + cut), center + new Vector2(half.x, half.y - cut),
            center + new Vector2(half.x - cut, half.y), center + new Vector2(-half.x + cut, half.y),
            center + new Vector2(-half.x, half.y - cut), center + new Vector2(-half.x, -half.y + cut) };
        Physics2D.SyncTransforms();
        var bounds = island.bounds;

        var terrain = Material("Terrain", 0, 1);
        var water = Material("Water", 1, 1);
        var gateMaterial = Material("Portal", 0, 1.65f);
        gateMaterial.SetFloat("_Portal", 1);
        EditorUtility.SetDirty(gateMaterial);
        var decor = AssetDatabase.LoadAssetAtPath<Material>(ArtFolder + "/Decor.mat");
        if (decor == null) { decor = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(decor, ArtFolder + "/Decor.mat"); }
        var square = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png");
        if (square == null) throw new InvalidOperationException("Existing square sprite not found");

        var grid = new GameObject("CrimsonTerrain", typeof(Grid));
        grid.transform.SetParent(root.transform, false);
        grid.transform.position = offset;
        // The old arena's Barear layers contain its surrounding water. Copy dry
        // ground and decoration only; the new shoreline must match the safe area.
        var sourceMaps = UnityEngine.Object.FindObjectsOfType<Tilemap>().Where(map => !map.transform.IsChildOf(root.transform)).ToArray();
        var dryMap = sourceMaps.First(map => map.name != "Decor" && map.HasTile(map.WorldToCell(sourceBounds.center)));
        var sourceGrid = (Grid)dryMap.layoutGrid;
        var targetGrid = grid.GetComponent<Grid>();
        targetGrid.cellSize = sourceGrid.cellSize; targetGrid.cellGap = sourceGrid.cellGap;
        targetGrid.cellLayout = sourceGrid.cellLayout; targetGrid.cellSwizzle = sourceGrid.cellSwizzle;
        foreach (var sourceMap in sourceMaps)
            Debug.Log("CRIMSON_GROUND_SOURCE: " + sourceMap.name + "; center=" + sourceMap.GetTile(sourceMap.WorldToCell(sourceBounds.center))?.name);
        var originalMaps = sourceMaps.Where(map => map == dryMap || map.name == "Decor").ToArray();
        int copied = 0;
        foreach (var original in originalMaps)
        {
            var host = new GameObject(original == dryMap ? "CrimsonGround" : "CrimsonDecor", typeof(Tilemap), typeof(TilemapRenderer));
            host.transform.SetParent(grid.transform, false);
            host.transform.localPosition = original.transform.localPosition;
            var map = host.GetComponent<Tilemap>();
            var renderer = host.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = terrain;
            renderer.sortingLayerID = original.GetComponent<TilemapRenderer>().sortingLayerID;
            renderer.sortingOrder = original.GetComponent<TilemapRenderer>().sortingOrder;
            Vector3Int low = original.WorldToCell(sourceBounds.min), high = original.WorldToCell(sourceBounds.max);
            var dryGround = original.GetTile(original.WorldToCell(sourceBounds.center));
            var cells = new List<Vector3Int>(); var tiles = new List<TileBase>();
            for (int y = low.y; y <= high.y; y++)
                for (int x = low.x; x <= high.x; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    var tile = original.GetTile(cell);
                    if (tile == null && original == dryMap) tile = dryGround;
                    if (tile == null || !island.OverlapPoint(original.GetCellCenterWorld(cell) + offset)) continue;
                    cells.Add(cell); tiles.Add(tile);
                }
            map.SetTiles(cells.ToArray(), tiles.ToArray()); copied += cells.Count;
            map.CompressBounds();
        }
        var sea = Block("CrimsonWater", root.transform, bounds.center, new Vector2(bounds.size.x + 80, bounds.size.y + 80),
            Color.white, square, water, -10);
        sea.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0);
        var seaCollider = sea.AddComponent<BoxCollider2D>(); seaCollider.isTrigger = true;
        var death = sea.AddComponent<DeadZone>(); death.damage = 80;
        Set(death, "arenaOwner", red); Set(death, "safeArea", island);

        var waveData = new SerializedObject(red);
        var waves = waveData.FindProperty("waves");
        for (int i = 0; i < waves.arraySize; i++)
        {
            var item = waves.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("waveDuration").floatValue = 90 + 4 * i;
            item.FindPropertyRelative("breakDuration").floatValue = 20;
            item.FindPropertyRelative("maxEnemies").intValue = 32 + 6 * i;
            item.FindPropertyRelative("minDifficulty").intValue = 0;
            item.FindPropertyRelative("maxDifficulty").intValue = 8;
            var types = item.FindPropertyRelative("enemyTypes"); types.arraySize = 9;
            for (int j = 0; j < 9; j++) types.GetArrayElementAtIndex(j).intValue = j;
        }
        waveData.ApplyModifiedPropertiesWithoutUndo();

        var normalExit = Get<GameObject>(normal, "breakIndicator").GetComponent<Portal>();
        var normalGate = normalExit.linkedPortal.GetComponent<Portal>();
        var redGateObject = UnityEngine.Object.Instantiate(normalGate.gameObject);
        redGateObject.name = "CrimsonEntrance";
        redGateObject.transform.position = normalGate.transform.position + Vector3.right * 4.2f;
        var redGate = redGateObject.GetComponent<Portal>();
        var redExitObject = UnityEngine.Object.Instantiate(normalExit.gameObject, root.transform);
        redExitObject.name = "CrimsonExit";
        redExitObject.transform.position = normalExit.transform.position + offset;
        var redExit = redExitObject.GetComponent<Portal>();
        redGate.linkedPortal = redExit.transform; redExit.linkedPortal = redGate.transform;
        Set(redGate, "destinationArena", red); SetInt(redGate, "requiredMmr", 2000);
        Set(redExit, "originArena", red); Set(normalExit, "originArena", normal);
        Set(normalGate, "destinationArena", normal);
        SetPortalVisual(redGate, "Красная арена", gateMaterial);
        SetPortalVisual(redExit, "В ЛАГЕРЬ", gateMaterial);
        Set(red, "breakIndicator", redExitObject);

        foreach (bool slime in new[] { true, false })
        {
            string bossField = slime ? "bossWave10" : "bossWave15";
            var originalBoss = Get<GameObject>(normal, bossField);
            var boss = UnityEngine.Object.Instantiate(originalBoss, originalBoss.transform.position + offset, originalBoss.transform.rotation, root.transform);
            boss.name = slime ? "CrimsonSlimeTemplate" : "CrimsonAssassinTemplate";
            boss.SetActive(false);
            Set(red, bossField, boss);
            UnityEngine.Object component = slime ? (UnityEngine.Object)boss.GetComponent<SlimeBoss>() : boss.GetComponent<Roberto>();
            var originalPanel = Get<GameObject>(component, slime ? "bossHpBarObject" : "hpBarContainer");
            var sourceFill = Get<Image>(component, slime ? "bossHpBar" : "hpBar");
            var clone = new GameObject("CrimsonBossHealth" + (slime ? "Slime" : "Assassin"), typeof(RectTransform));
            clone.transform.SetParent(originalPanel.transform.parent, false);
            var rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, slime ? .83f : .77f);
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(260, 36);
            rect.localScale = Vector3.one;
            var titleObject = new GameObject("BossName", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(rect, false);
            var title = titleObject.GetComponent<TextMeshProUGUI>();
            title.font = Get<TextMeshProUGUI>(normal, "arenaHint").font;
            title.fontSize = 20; title.alignment = TextAlignmentOptions.Center; title.raycastTarget = false;
            title.color = new Color32(249, 237, 211, 255);
            title.text = slime ? "Король слизней" : "Ассасин";
            title.rectTransform.anchorMin = new Vector2(0, 0.4f); title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            var trackObject = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackObject.transform.SetParent(rect, false);
            var track = trackObject.GetComponent<Image>(); track.color = new Color32(30, 16, 24, 240); track.raycastTarget = false;
            track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(.5f, 0);
            track.rectTransform.pivot = new Vector2(.5f, 0); track.rectTransform.anchoredPosition = Vector2.zero;
            track.rectTransform.sizeDelta = new Vector2(260, 12);
            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(track.rectTransform, false);
            var fill = fillObject.GetComponent<Image>(); fill.sprite = sourceFill.sprite;
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 1; fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.one * 2; fill.rectTransform.offsetMax = Vector2.one * -2;
            fill.color = slime ? new Color32(225, 181, 91, 255) : new Color32(239, 103, 99, 255);
            clone.SetActive(false);
            Set(red, slime ? "bossHPBar10" : "bossHPBar15", clone);
            Set(red, slime ? "pairedSlimeHealth" : "pairedAssassinHealth", fill);
            Set(red, slime ? "bossPlaceholder10" : "bossPlaceholder15", null);
        }

        var ruinOffsets = new[] { new Vector2(-12,-11), new Vector2(10,-12), new Vector2(-13,10), new Vector2(12,11),
            new Vector2(-5,13), new Vector2(6,-7), new Vector2(14,0), new Vector2(-8,1) };
        for (int i = 0; i < ruinOffsets.Length; i++)
            Ruin(root.transform, bounds.center + (Vector3)ruinOffsets[i], square, decor, i);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("CRIMSON_ARENA_AUTHORING_PASS: tiles=" + copied + "; bounds=" + bounds + "; portal=" + redGate.transform.position);
    }

    static Material Material(string name, float isWater, float intensity)
    {
        var path = ArtFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("LastSoldier/CrimsonPalette")); AssetDatabase.CreateAsset(material, path); }
        material.SetFloat("_Water", isWater); material.SetFloat("_Intensity", intensity); EditorUtility.SetDirty(material);
        return material;
    }
    static void SetPortalVisual(Portal portal, string label, Material material)
    {
        var data = new SerializedObject(portal); data.FindProperty("destinationLabel").stringValue = label;
        data.FindProperty("labelColor").colorValue = new Color32(255, 146, 129, 255); data.ApplyModifiedPropertiesWithoutUndo();
        foreach (var sprite in portal.GetComponentsInChildren<SpriteRenderer>(true)) sprite.sharedMaterial = material;
        var animator = portal.GetComponent<Animator>();
        if (animator != null)
        {
            animator.runtimeAnimatorController = LoopingPortalController();
            var existing = portal.transform.Find("PortalProximity");
            var hover = existing != null ? existing.gameObject : new GameObject("PortalProximity", typeof(CircleCollider2D), typeof(PortalAnim));
            hover.transform.SetParent(portal.transform, false);
            hover.GetComponent<CircleCollider2D>().isTrigger = true; hover.GetComponent<CircleCollider2D>().radius = 2;
            hover.GetComponent<PortalAnim>().portalAnimator = animator;
            hover.GetComponent<PortalAnim>().sounds = portal.sounds;
        }
    }
    static AnimatorOverrideController LoopingPortalController()
    {
        var original = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Anim/Portal/Portal.controller");
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Anim/Portal/PortalZapusk.anim");
        string clipPath = ArtFolder + "/PortalOpenLoop.anim";
        var loop = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (loop == null) { loop = new AnimationClip(); AssetDatabase.CreateAsset(loop, clipPath); }
        EditorUtility.CopySerialized(source, loop);
        loop.name = "PortalOpenLoop";
        var settings = AnimationUtility.GetAnimationClipSettings(loop); settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(loop, settings); loop.wrapMode = WrapMode.Loop; EditorUtility.SetDirty(loop);
        string controllerPath = ArtFolder + "/Portal.overrideController";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        if (controller == null) { controller = new AnimatorOverrideController(); AssetDatabase.CreateAsset(controller, controllerPath); }
        controller.runtimeAnimatorController = original;
        controller[source.name] = loop;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    [MenuItem("Tools/Last Soldier/Repair Crimson Portal Visuals")]
    public static void RepairPortalVisuals()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var red = WaveSpawner.FindArena(true);
        var exit = Get<GameObject>(red, "breakIndicator").GetComponent<Portal>();
        var material = Material("Portal", 0, 1);
        material.SetFloat("_Portal", 1); EditorUtility.SetDirty(material);
        SetPortalVisual(exit, "В ЛАГЕРЬ", material);
        SetPortalVisual(exit.linkedPortal.GetComponent<Portal>(), "Красная арена", material);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("CRIMSON_PORTAL_REPAIR_PASS");
    }
    static GameObject Block(string name, Transform parent, Vector3 position, Vector2 size, Color color,
        Sprite square, Material material, int order)
    {
        var host = new GameObject(name, typeof(SpriteRenderer)); host.transform.SetParent(parent, false);
        host.transform.position = new Vector3(position.x, position.y, -3);
        host.transform.localScale = new Vector3(size.x / square.bounds.size.x, size.y / square.bounds.size.y, 1);
        var sprite = host.GetComponent<SpriteRenderer>(); sprite.sprite = square; sprite.color = color;
        sprite.sharedMaterial = material; sprite.sortingOrder = order;
        return host;
    }
    static void Ruin(Transform parent, Vector3 center, Sprite square, Material material, int variant)
    {
        var ruin = new GameObject("RuinAndWeb" + variant).transform; ruin.SetParent(parent, false); ruin.position = center;
        for (int column = 0; column < 2; column++)
            for (int row = 0; row < (column == variant % 2 ? 3 : 5); row++)
            {
                var pos = center + new Vector3(column * 1.4f - .7f, row * .3f, 0);
                Block("BrokenStone", ruin, pos, new Vector2(.55f, .25f),
                    (row + column) % 2 == 0 ? new Color32(113, 91, 101, 255) : new Color32(81, 67, 82, 255), square, material, 2);
            }
        for (int i = 0; i < 4; i++)
        {
            var rubble = Block("Rubble", ruin, center + new Vector3(-.8f + i * .48f, -.4f - (i % 2) * .15f, 0),
                new Vector2(.36f,.16f), new Color32(105,78,83,255), square, material, 2);
            rubble.transform.rotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -15 : 20);
        }
        var webObject = new GameObject("Cobweb", typeof(LineRenderer)); webObject.transform.SetParent(ruin, false);
        webObject.transform.position = center + new Vector3(0, .6f, -1);
        var points = new List<Vector3>(); const int spokes = 8;
        for (int i = 0; i < spokes; i++)
        { float a = i * Mathf.PI * 2 / spokes; points.Add(Vector3.zero); points.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .8f); }
        for (int ring = 1; ring <= 4; ring++)
            for (int i = 0; i <= spokes; i++)
            { float a = i * Mathf.PI * 2 / spokes; points.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (.18f * ring)); }
        var line = webObject.GetComponent<LineRenderer>(); line.useWorldSpace = false;
        line.positionCount = points.Count; line.SetPositions(points.ToArray()); line.widthMultiplier = .018f;
        line.sharedMaterial = material; line.startColor = line.endColor = new Color32(216,197,190,145); line.sortingOrder = 3;
    }
    public static void Inspect()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var wave = UnityEngine.Object.FindObjectsOfType<WaveSpawner>().First(w => !w.IsCrimson);
        var root = wave.transform.root;
        var bounds = wave.GetComponent<Collider2D>().bounds;
        var lines = new List<string> { "ARENA: " + root.name + " bounds=" + bounds + " root=" + root.position };
        foreach (Transform child in root) lines.Add("CHILD: " + child.name + " position=" + child.position + " scale=" + child.lossyScale);
        foreach (var tilemap in UnityEngine.Object.FindObjectsOfType<Tilemap>())
            lines.Add("TILEMAP: " + tilemap.name + " transform=" + tilemap.transform.position + " cells=" + tilemap.cellBounds
                + " localbounds=" + tilemap.localBounds + " order=" + tilemap.GetComponent<TilemapRenderer>().sortingOrder);
        foreach (var portal in UnityEngine.Object.FindObjectsOfType<Portal>())
        {
            var anim = portal.GetComponent<PortalAnim>();
            lines.Add("PORTAL: " + portal.name + " pos=" + portal.transform.position + " active=" + portal.gameObject.activeSelf
                + " renderer=" + portal.GetComponentsInChildren<SpriteRenderer>(true).Length + " linked=" + portal.linkedPortal?.position
                + " animator=" + (anim != null ? anim.portalAnimator?.transform.position.ToString() : "none"));
        }
        foreach (string field in new[] { "bossWave10", "bossWave15", "breakIndicator", "bossHPBar10", "bossHPBar15" })
        {
            var obj = Get<GameObject>(wave, field);
            lines.Add(field + "=" + obj.name + " root=" + obj.transform.root.name + " pos=" + obj.transform.position + " scale=" + obj.transform.lossyScale);
        }
        File.WriteAllLines(Path.GetFullPath("../arena-v19-inspection.txt"), lines);
        Debug.Log("ARENA_INSPECTION_PASS\n" + string.Join("\n", lines));
    }
}
