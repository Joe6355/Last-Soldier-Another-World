using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ShaulaAuthoring
{
    const string Folder = "Assets/Bosses/Shaula";
    [Serializable] public class Frame { public string phase, name; public int clip, frame, x, y, width, height; public float pivotX, pivotY; }
    [Serializable] public class Clip { public string name; public float[] durations; public bool loop; }
    [Serializable] public class Import { public Frame[] frames; public Clip[] clips; }

    [MenuItem("Tools/Last Soldier/Configure Shaula Wave 20")]
    public static void Apply()
    {
        AssetDatabase.Refresh();
        ImportRig();
        foreach (string path in Directory.GetFiles(Folder + "/Audio", "*.wav").Select(p => p.Replace('\\', '/')))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var sample = importer.defaultSampleSettings;
            sample.loadType = path.Contains("music-") ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat = AudioCompressionFormat.Vorbis; sample.quality = .7f;
            sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            sample.preloadAudioData = true;
            importer.defaultSampleSettings = sample; importer.SaveAndReimport();
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var wave = UnityEngine.Object.FindObjectsOfType<WaveSpawner>().First(w => !w.IsCrimson);
        var player = UnityEngine.Object.FindObjectOfType<PlayerController>();
        var hero = player.GetComponent<SpriteRenderer>();
        var parts = AssetDatabase.LoadAllAssetsAtPath(Folder + "/rig-parts.png").OfType<Sprite>().ToDictionary(s => s.name);
        var oldPanel = Resources.FindObjectsOfTypeAll<RectTransform>().FirstOrDefault(t => t.gameObject.scene == scene && t.name == "ShaulaHealth");
        if (oldPanel != null) UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);

        var obj = new GameObject("Shaula"); obj.SetActive(false);
        var rigidbody = obj.AddComponent<Rigidbody2D>(); rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.gravityScale = 0; rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var boss = obj.AddComponent<ShaulaBoss>();
        boss.audioSrc.playOnAwake = false; boss.audioSrc.spatialBlend = 0;
        var visual = new GameObject("Visual").transform; visual.SetParent(obj.transform, false);
        var art = new GameObject("AnimatedSprite").AddComponent<SpriteRenderer>(); art.transform.SetParent(visual, false);
        art.sprite = parts["body"]; art.sortingLayerID = hero.sortingLayerID; art.sortingOrder = hero.sortingOrder - 4;
        float visibleHeroHeight = hero.bounds.size.y;
        float scale = visibleHeroHeight * 6f / 3.4f;
        obj.transform.localScale = Vector3.one * scale;
        var shell = HitZone(visual, boss, "Shell", ShaulaHitZone.Zone.Shell, Vector2.zero, .92f);
        var left = HitZone(visual, boss, "LeftJoint", ShaulaHitZone.Zone.Joint, new Vector2(-.65f, -.8f), .4f);
        var right = HitZone(visual, boss, "RightJoint", ShaulaHitZone.Zone.Joint, new Vector2(.65f, -.8f), .4f);
        var tail = HitZone(visual, boss, "TailWeakPoint", ShaulaHitZone.Zone.Tail, new Vector2(.48f, 2.04f), .3f);
        Set(boss, "bodySprite", art); Set(boss, "visual", visual); Set(boss, "tailZone", tail);
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Magic.mat");
        if (material == null) { material = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material, Folder + "/Magic.mat"); }
        Set(boss, "effectMaterial", material);
        Set(boss, "musicNormal", Audio("music-phase-1")); Set(boss, "musicCrimson", Audio("music-phase-2"));
        var serialized = new SerializedObject(boss);
        serialized.FindProperty("maxHealth").floatValue = 1120;
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Carapace.shader");
        var carapace = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Carapace.mat");
        if (carapace == null) { carapace = new Material(shader); AssetDatabase.CreateAsset(carapace, Folder + "/Carapace.mat"); }
        carapace.shader = shader; EditorUtility.SetDirty(carapace);
        serialized.FindProperty("carapaceMaterial").objectReferenceValue = carapace;
        var rig = serialized.FindProperty("rigParts"); rig.arraySize = RigNames.Length;
        for (int i = 0; i < RigNames.Length; i++) rig.GetArrayElementAtIndex(i).objectReferenceValue = parts[RigNames[i]];
        var effects = Directory.GetFiles(Folder + "/Audio", "*.wav").Select(p => p.Replace('\\', '/')).Where(p => !p.Contains("music-")).OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
        var sounds = serialized.FindProperty("effects"); sounds.arraySize = effects.Length;
        for (int i = 0; i < effects.Length; i++) sounds.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
        serialized.FindProperty("motions").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        string prefabPath = Folder + "/Shaula.prefab";
        PrefabUtility.SaveAsPrefabAsset(obj, prefabPath); UnityEngine.Object.DestroyImmediate(obj);

        var hud = Get<GameObject>(wave, "waveHudPanel");
        var canvas = hud.GetComponentInParent<Canvas>();
        var panel = Rect("ShaulaHealth", canvas.transform, new Vector2(760, 76), new Vector2(0, 12));
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0);
        panel.gameObject.AddComponent<Image>().color = new Color(.06f, .15f, .12f, .94f);
        panel.GetComponent<Image>().raycastTarget = false;
        TMP_FontAsset font = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>().First(t => t.font != null && t.gameObject.scene == scene).font;
        var name = Label("Name", panel, font, 28, new Vector2(12, -3), new Vector2(736, 34)); name.text = "Шаула";
        var hp = Bar("Health", panel, new Vector2(12, -37), new Vector2(736, 12), new Color(.9f, .72f, .35f));
        var tailLabel = Label("TailLabel", panel, font, 16, new Vector2(545, -52), new Vector2(65, 20)); tailLabel.text = "Хвост";
        var tailFill = Bar("TailHealth", panel, new Vector2(610, -60), new Vector2(138, 7), new Color(.8f, .95f, 1));
        Set(wave, "shaulaPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<ShaulaBoss>());
        Set(wave, "shaulaHealthPanel", panel.gameObject); Set(wave, "shaulaHealthFill", hp); Set(wave, "shaulaTailFill", tailFill);
        Set(wave, "shaulaTitle", name); Set(wave, "shaulaState", null);
        var oldHint = new SerializedObject(wave).FindProperty("arenaHint").objectReferenceValue as TMP_Text;
        if (oldHint != null) { oldHint.text = ""; oldHint.gameObject.SetActive(false); EditorUtility.SetDirty(oldHint); }
        panel.gameObject.SetActive(false);
        // The red arena keeps its current paired bosses and receives no Shaula HUD/prefab reference.
        foreach (var red in UnityEngine.Object.FindObjectsOfType<WaveSpawner>().Where(w => w.IsCrimson))
            foreach (string field in new[] { "shaulaPrefab", "shaulaHealthPanel", "shaulaHealthFill", "shaulaTailFill", "shaulaTitle", "shaulaState" }) Set(red, field, null);
        PlayerSettings.bundleVersion = "0.0.0.24";
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets(); GameSpriteAtlas.Synchronize();
        Debug.Log("SHAULA_AUTHORING_PASS: articulated parts=" + parts.Count + "; sounds=" + effects.Length
            + "; heroHeight=" + visibleHeroHeight + "; bossWidth=" + visibleHeroHeight * 6 + "; scale=" + scale);
    }
    static readonly string[] RigNames = { "body", "claw-open", "claw-closed", "leg-upper", "leg-lower", "forearm", "tail-segment", "stinger", "little" };
    static void ImportRig()
    {
        // Individually measured alpha bounds, with padding. No guessed uniform grid slices.
        var boxes = new[] { new Rect(179, 16, 266, 433), new Rect(616, 70, 305, 353), new Rect(1139, 71, 235, 345),
            new Rect(156, 507, 280, 116), new Rect(599, 509, 311, 113), new Rect(1040, 490, 405, 149),
            new Rect(224, 695, 144, 280), new Rect(680, 710, 177, 289), new Rect(1125, 677, 253, 323) };
        var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/rig-parts.png");
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 256; importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.spritesheet = boxes.Select((box, i) => new SpriteMetaData { name = RigNames[i], alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(.5f, .5f), rect = new Rect(box.x, 1024 - box.y - box.height, box.width, box.height) }).ToArray();
        importer.SaveAndReimport();
    }
    static ShaulaHitZone HitZone(Transform parent, ShaulaBoss boss, string name, ShaulaHitZone.Zone type, Vector2 point, float radius)
    {
        var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = point;
        var zone = obj.AddComponent<ShaulaHitZone>(); zone.boss = boss; zone.zone = type;
        var collider = obj.AddComponent<CircleCollider2D>(); collider.radius = radius;
        return zone;
    }
    static AudioClip Audio(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/" + name + ".wav");
    static T Get<T>(UnityEngine.Object obj, string field) => (T)(object)new SerializedObject(obj).FindProperty(field).objectReferenceValue;
    static void Set(UnityEngine.Object obj, string field, UnityEngine.Object value)
    { var data = new SerializedObject(obj); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    static RectTransform ChildRect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = Rect(name, parent, size, position);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); return rect;
    }
    static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, int size, Vector2 position, Vector2 bounds)
    {
        var label = ChildRect(name, parent, bounds, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.fontSize = size; label.color = new Color(.95f, .91f, .78f); label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.enableWordWrapping = false;
        return label;
    }
    static Image Bar(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var background = ChildRect(name + "Background", parent, size, position).gameObject.AddComponent<Image>();
        background.color = new Color(.25f, .33f, .3f); background.raycastTarget = false;
        var image = ChildRect(name, background.transform, size, Vector2.zero).gameObject.AddComponent<Image>();
        // Filled UI needs a sprite; use an existing small square from the project/package.
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png");
        image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0;
        image.color = color; image.raycastTarget = false; return image;
    }
}
