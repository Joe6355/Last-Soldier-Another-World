using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public static class GameSpriteAtlas
{
    public const string Path = "Assets/SpriteAtlas.spriteatlasv2";
    public const string BackgroundPath = "Assets/BackgroundSpriteAtlas.spriteatlasv2";
    public const int PageSize = 2048;

    public static bool IsLargeBackdrop(string path)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        return sprites.Length == 1 && sprites[0].rect.width >= 512 && sprites[0].rect.height >= 256;
    }

    public static string[] CollectTexturePaths()
    {
        var roots = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path)
            .Concat(AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)
                && path.Contains("/Resources/") && !path.Contains("/Editor/") && !AssetDatabase.IsValidFolder(path)))
            .Distinct().ToArray();
        return AssetDatabase.GetDependencies(roots, true)
            .Where(path => AssetImporter.GetAtPath(path) is TextureImporter importer
                && importer.textureType == TextureImporterType.Sprite
                && AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Any())
            .Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    [MenuItem("Tools/Last Soldier/Update Game Sprite Atlas")]
    public static void UpdateAtlas()
    {
        Synchronize();
    }

    public static SpriteAtlas[] Synchronize()
    {
        if (!File.Exists(Path)) throw new InvalidOperationException("Existing game sprite atlas is missing: " + Path);
        string[] paths = CollectTexturePaths();
        if (paths.Length == 0) throw new InvalidOperationException("No game sprites found in build scenes and resources.");
        var backdrops = paths.Where(IsLargeBackdrop).ToArray();
        var gameplay = paths.Except(backdrops).ToArray();
        var atlases = new[] { Configure(Path, gameplay), Configure(BackgroundPath, backdrops) };
        AssetDatabase.SaveAssets();
        Debug.Log("GAME_SPRITE_ATLAS_UPDATED: " + gameplay.Length + " game textures; " + backdrops.Length
            + " large backdrops in a separate atlas; max page " + PageSize);
        return atlases;
    }

    static SpriteAtlas Configure(string path, string[] textures)
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
        var source = File.Exists(path) ? SpriteAtlasAsset.Load(path) : new SpriteAtlasAsset();
        if (atlas != null) source.Remove(atlas.GetPackables());
        source.Add(textures.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Cast<UnityEngine.Object>().ToArray());
        SpriteAtlasAsset.Save(source, path);
        UnityEngine.Object.DestroyImmediate(source);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
        importer.includeInBuild = true;
        var packing = importer.packingSettings;
        packing.enableRotation = false;
        packing.enableTightPacking = false;
        packing.padding = 4;
        importer.packingSettings = packing;
        var texture = importer.textureSettings;
        texture.filterMode = FilterMode.Point;
        texture.generateMipMaps = false;
        texture.readable = false;
        texture.sRGB = true;
        importer.textureSettings = texture;
        foreach (string platform in new[] { "DefaultTexturePlatform", "WebGL", "Android" })
        {
            var settings = importer.GetPlatformSettings(platform);
            settings.name = platform;
            settings.maxTextureSize = PageSize;
            settings.format = TextureImporterFormat.RGBA32;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.crunchedCompression = false;
            settings.overridden = platform != "DefaultTexturePlatform";
            importer.SetPlatformSettings(settings);
        }
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
    }
}
