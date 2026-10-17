using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public static class SpriteAtlasChecks
{
    [Serializable] public sealed class Page { public int width, height; public string format; public long estimatedBytes; }
    [Serializable] public sealed class Group
    {
        public string path, guid;
        public int textureCount, spriteCount;
        public Page[] pages;
    }
    [Serializable] public sealed class Report
    {
        public string result, atlasPath, atlasGuid, buildTarget, scope;
        public int textureCount, sourceSpriteCount, packedSpriteCount, checks;
        public string[] texturePaths;
        public Page[] pages;
        public Group[] groups;
    }
    static readonly List<string> checks = new List<string>();

    public static void CheckLoadedScene()
    {
        var sprites = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>()
            .Where(renderer => renderer.enabled && renderer.sprite != null).Select(renderer => renderer.sprite)
            .Concat(UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Image>()
                .Where(image => image.enabled && image.sprite != null).Select(image => image.overrideSprite ?? image.sprite))
            .Where(sprite => AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) is TextureImporter importer
                && importer.textureType == TextureImporterType.Sprite).Distinct().ToArray();
        var missing = sprites.Where(sprite => !sprite.packed).Select(sprite => sprite.name).ToArray();
        if (sprites.Length == 0 || missing.Length > 0)
            throw new InvalidOperationException("Scene sprites are not bound to atlas textures: " + string.Join(", ", missing));
        Debug.Log("LIVE_ATLAS_BINDING_PASS: " + sprites.Length + " unique renderer/UI sprites in "
            + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public static void Run()
    {
        try
        {
            string guid = AssetDatabase.AssetPathToGUID(GameSpriteAtlas.Path);
            string[] paths = GameSpriteAtlas.CollectTexturePaths();
            var hashes = paths.Where(path => File.Exists(path) && File.Exists(path + ".meta"))
                .SelectMany(path => new[] { path, path + ".meta" }).ToDictionary(path => path, Hash);
            var atlases = GameSpriteAtlas.Synchronize();
            Check(AssetDatabase.AssetPathToGUID(GameSpriteAtlas.Path) == guid, "Existing atlas GUID preserved");
            Check(EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2
                || EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2Build, "Sprite Atlas V2 is enabled");
            Check(paths.Contains("Assets/Sprits/Holy/HolyOrc.png") && paths.Contains("Assets/Sprits/Holy/HolyGhost.png"),
                "Both new enemies are included with existing character artwork");
            Check(!paths.Any(path => path.Contains("EmojiOne") || path.Contains("/Examples & Extras/Textures/")),
                "Unused demo artwork is excluded");
            var packables = atlases.SelectMany(atlas => atlas.GetPackables()).Select(AssetDatabase.GetAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Check(packables.Length == paths.Length && packables.SequenceEqual(paths), "Every runtime sprite texture is packed exactly once across both atlases");
            foreach (var atlas in atlases)
            {
                string atlasPath = AssetDatabase.GetAssetPath(atlas);
                Check(atlas.GetPackables().All(texture => GameSpriteAtlas.IsLargeBackdrop(AssetDatabase.GetAssetPath(texture))
                    == (atlasPath == GameSpriteAtlas.BackgroundPath)), "Large menu backdrops remain separate from game sprites: " + atlasPath);
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(atlasPath);
                Check(importer.includeInBuild, "Atlas textures are included and bound in player builds");
                Check(!importer.packingSettings.enableRotation && !importer.packingSettings.enableTightPacking
                    && importer.packingSettings.padding >= 4, "UI and tile sprites use unrotated rectangular packing with padding");
                Check(importer.textureSettings.filterMode == FilterMode.Point && !importer.textureSettings.generateMipMaps
                    && !importer.textureSettings.readable, "Pixel-art filtering, no mipmaps, no CPU texture copy");
                foreach (string platform in new[] { "DefaultTexturePlatform", "WebGL", "Android" })
                {
                    var settings = importer.GetPlatformSettings(platform);
                    Check(settings.maxTextureSize == GameSpriteAtlas.PageSize && settings.format == TextureImporterFormat.RGBA32
                        && settings.textureCompression == TextureImporterCompression.Uncompressed,
                        "Lossless 2048px atlas pages: " + platform);
                }
            }
            var atlasHashes = atlases.Select(AssetDatabase.GetAssetPath).SelectMany(path => new[] { path, path + ".meta" })
                .ToDictionary(path => path, Hash);
            atlases = GameSpriteAtlas.Synchronize();
            foreach (var pair in atlasHashes) Check(Hash(pair.Key) == pair.Value, "Repeated atlas update is deterministic: " + pair.Key);
            SpriteAtlasUtility.PackAtlases(atlases, EditorUserBuildSettings.activeBuildTarget, false);
            int total = 0;
            foreach (string path in paths)
            {
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                total += sprites.Length;
                Check(sprites.Length > 0 && sprites.All(sprite => atlases.Count(atlas => atlas.CanBindTo(sprite)) == 1), "All sprite slices bind to exactly one atlas: " + path);
            }
            Check(atlases.Sum(atlas => atlas.spriteCount) == total, "Packed count equals all original animation/UI/tile slices: " + total);
            foreach (var pair in hashes) Check(Hash(pair.Key) == pair.Value, "Original texture or sprite metadata unchanged: " + pair.Key);

            var preview = typeof(SpriteAtlasExtensions).GetMethod("GetPreviewTextures", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (preview == null) throw new InvalidOperationException("Unity atlas preview API not found");
            var groups = atlases.Select(atlas => new Group { path = AssetDatabase.GetAssetPath(atlas),
                guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(atlas)),
                textureCount = atlas.GetPackables().Length, spriteCount = atlas.spriteCount,
                pages = ((Texture2D[])preview.Invoke(null, new object[] { atlas })).Select(texture => new Page {
                    width = texture.width, height = texture.height, format = texture.format.ToString(),
                    estimatedBytes = (long)texture.width * texture.height * 4 }).ToArray() }).ToArray();
            var textures = groups.SelectMany(group => group.pages).ToArray();
            Check(textures != null && textures.Length > 0, "Atlas contains packed texture pages");
            Check(textures.All(texture => texture.width <= GameSpriteAtlas.PageSize && texture.height <= GameSpriteAtlas.PageSize),
                "Every packed page respects the mobile texture limit");
            var report = new Report { result = "PASS", atlasPath = GameSpriteAtlas.Path, atlasGuid = guid,
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(), textureCount = paths.Length,
                sourceSpriteCount = total, packedSpriteCount = atlases.Sum(atlas => atlas.spriteCount), checks = checks.Count, texturePaths = paths,
                pages = textures, groups = groups,
                scope = "All imported sprite textures referenced by enabled build scenes and Resources, including animation, UI and tiles. Runtime-generated tutorial pointer and font SDF textures are not imported sprites." };
            File.WriteAllText(System.IO.Path.GetFullPath("../sprite-atlas-v18-checks.json"), JsonUtility.ToJson(report, true));
            Finish(true, "All runtime sprite textures and slices pack into game/background atlases without changing source images or IDs");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static string Hash(string path)
    {
        using (var algorithm = SHA256.Create())
            return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message);
    }
    static void Finish(bool pass, string message)
    {
        File.WriteAllText(System.IO.Path.GetFullPath("../sprite-atlas-v18-checks.txt"),
            (pass ? "PASS\n" : "FAIL\n") + message + "\n" + string.Join("\n", checks));
        Debug.Log((pass ? "SPRITE_ATLAS_PASS: " : "SPRITE_ATLAS_FAIL: ") + message);
        EditorApplication.Exit(pass ? 0 : 1);
    }
}
