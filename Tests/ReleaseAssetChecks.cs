using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
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
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Anim/Player/Marker.anim");
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Anim/Player/Shoot.anim");
            var spriteBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var idleFrames = AnimationUtility.GetObjectReferenceCurve(idle, spriteBinding);
            var walkFrames = AnimationUtility.GetObjectReferenceCurve(walk, spriteBinding);
            Check(idleFrames != null && idleFrames.Select(f => f.value).Distinct().Count() == 1,
                "Hero stands on the original idle frame");
            Check(walkFrames != null && walkFrames.Select(f => f.value).Distinct().Count() == 5
                && walkFrames.All(f => AssetDatabase.GetAssetPath(f.value) == "Assets/Sprits/PlayerAll.png"),
                "Hero walk loops through all five original frames");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Anim/Player/Player.controller");
            Check(controller.parameters.Any(p => p.name == "IsMoving" && p.type == AnimatorControllerParameterType.Bool)
                && controller.layers[0].stateMachine.defaultState.motion == idle,
                "Movement controls the existing hero animator; default state is idle");
            foreach (var state in controller.layers[0].stateMachine.states)
                Check(state.state.transitions.Length == 1 && state.state.transitions.All(t => !t.hasExitTime && t.duration == 0),
                    "Hero animation switches without waiting: " + state.state.name);
            var merchant = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Anim/Shoper/Shoper.controller");
            Check(merchant.animationClips.Length == 4 && merchant.animationClips.All(clip =>
                AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(binding =>
                    AnimationUtility.GetObjectReferenceCurve(clip, binding).Any(frame =>
                        AssetDatabase.GetAssetPath(frame.value) == "Assets/Sprits/ShoperAllAnim.png"))),
                "All four original merchant animations retain their sprite bindings");
            var altar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Arena/Altar.prefab");
            Check(!altar.transform.Find("BuildingPad").GetComponent<SpriteRenderer>().enabled,
                "Orphan decorative rune below the locked altar is hidden");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var player = UnityEngine.Object.FindObjectOfType<PlayerController>();
            Check(new SerializedObject(player).FindProperty("anim").objectReferenceValue is Animator animator
                && animator.runtimeAnimatorController == controller, "Game scene uses the verified hero animator");
            var mage = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Anim/Enemy/SceletMag.controller");
            foreach (var clip in mage.animationClips)
                Check(AnimationUtility.GetObjectReferenceCurveBindings(clip).All(binding =>
                    AnimationUtility.GetObjectReferenceCurve(clip, binding).All(frame =>
                        new[] { "Assets/Sprits/allEnemy.png", "Assets/Sprits/SciDE.png" }.Contains(AssetDatabase.GetAssetPath(frame.value)))),
                    "Mage state uses restored artwork: " + clip.name);
            foreach (var name in new[] { "SlimeAt", "SlimeIde" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Anim/Enemy/" + name + ".anim");
                Check(AnimationUtility.GetObjectReferenceCurveBindings(clip).All(binding =>
                    AnimationUtility.GetObjectReferenceCurve(clip, binding).All(frame =>
                        AssetDatabase.GetAssetPath(frame.value) == "Assets/Sprits/SlimeVariantAtlas.png")),
                    "Slime state uses the retained v13 artwork: " + name);
            }
            var playerData = new SerializedObject(player);
            Check(playerData.FindProperty("shieldRaiseSound").objectReferenceValue != null
                && playerData.FindProperty("shieldBlockSound").objectReferenceValue != null,
                "Shield raise and block clips are configured on the game player");
            var barrel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Arena/Barrel.prefab");
            var barrelSound = (AudioSource)new SerializedObject(barrel.GetComponent<ArenaFeature>())
                .FindProperty("explosionAudio").objectReferenceValue;
            var bomber = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Bomber.prefab").GetComponent<Enemy>();
            Check(barrelSound.clip != bomber.sounds[0] && barrelSound.clip.name.StartsWith("vzryiv")
                && bomber.sounds[0].name == "BomberDead", "Barrel and bomber use distinct explosion clips");
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
