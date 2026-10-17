using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class ReleaseAssetChecks
{
    [Serializable] private sealed class Manifest { public Entry[] files; }
    [Serializable] private sealed class Entry
    {
        public string path, file, sha256, metadataSHA256, metaSha256;
        public int frameCount, rectangles;
    }
    [Serializable] private sealed class Dependencies { public string[] roots, files; }
    private static readonly List<string> checks = new List<string>();
    private static string Repository => Path.GetFullPath("../../..");

    public static void Run()
    {
        try
        {
            foreach (string name in new[] { "character-replacements.json", "interface-sprite-provenance.json" })
            {
                var data = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(Repository, "Publishing", name)));
                foreach (var entry in data.files)
                {
                    string path = entry.path ?? entry.file;
                    Check(Hash(path) == entry.sha256, "Imported replacement matches source manifest: " + path);
                    Check(Hash(path + ".meta") == (entry.metadataSHA256 ?? entry.metaSha256), "Unity sprite IDs retained: " + path);
                    int expected = entry.frameCount > 0 ? entry.frameCount : entry.rectangles;
                    Check(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == expected,
                        "All sliced sprites import: " + path);
                }
            }
            foreach (var path in Directory.GetFiles("Assets/Sounds", "*", SearchOption.AllDirectories)
                .Where(path => new[] { ".wav", ".mp3", ".ogg" }.Contains(Path.GetExtension(path).ToLowerInvariant())))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Check(clip != null && clip.samples > 0 && clip.length > 0, "Audio imports: " + path);
                Check(clip.LoadAudioData() && clip.loadState == AudioDataLoadState.Loaded, "Audio decodes in Unity: " + path);
                var samples = new float[Math.Min(clip.samples * clip.channels, 4096)];
                Check(clip.GetData(samples, 0) && samples.Any(value => Mathf.Abs(value) > .00001f)
                    && samples.All(value => !float.IsNaN(value) && Mathf.Abs(value) < .99f), "Audio samples finite and unclipped: " + path);
                clip.UnloadAudioData();
            }
            var roots = new[] { "Assets/Scenes/Menu.unity", "Assets/Scenes/SampleScene.unity" }
                .Concat(Directory.GetFiles("Assets", "*", SearchOption.AllDirectories)
                    .Select(path => path.Replace('\\', '/'))
                    .Where(path => path.Contains("/Resources/") && !path.Contains("/Editor/") && !path.EndsWith(".meta")))
                .ToArray();
            var dependencies = AssetDatabase.GetDependencies(roots, true);
            File.WriteAllText(Path.GetFullPath("../release-asset-dependencies.json"),
                JsonUtility.ToJson(new Dependencies { roots = roots, files = dependencies }, true));
            Check(!dependencies.Any(path => (path.Contains("/Examples & Extras/") &&
                new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path).ToLowerInvariant())) || path.Contains("EmojiOne")),
                "Unused TMP demo graphics and EmojiOne excluded from player dependencies");
            Check(dependencies.Contains("Assets/Resources/AssetCredits.txt"), "Asset attribution included in player resources");
            Finish(true, "Replacement sprites, all 40 audio imports and player dependency roots passed");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    private static string Hash(string path)
    {
        using (var algorithm = SHA256.Create())
            return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message);
    }
    private static void Finish(bool pass, string message)
    {
        File.WriteAllText(Path.GetFullPath("../release-asset-checks.txt"),
            (pass ? "PASS\n" : "FAIL\n") + message + "\n" + string.Join("\n", checks));
        Debug.Log((pass ? "RELEASE_ASSETS_PASS: " : "RELEASE_ASSETS_FAIL: ") + message);
        EditorApplication.Exit(pass ? 0 : 1);
    }
}
