// Copy only to the isolated validation project's Assets/Editor directory.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using YG;

[InitializeOnLoad]
public static class VisualRestorationChecks
{
    const string Flag = "LastSoldier.VisualRestorationChecks";
    static int phase, readyFrame, directionIndex;
    static double deadline;
    static float began;
    static PlayerController player;
    static MobileControls mobile;
    static TouchControl movement;
    static PointerEventData pointer;
    static ArenaFeature barrel;
    static Enemy bomber;
    static TouchControl shield, mirror;
    static PointerEventData shieldPointer, mirrorPointer;
    static AudioSource playerAudio;
    static readonly HashSet<Sprite> frames = new HashSet<Sprite>();
    static readonly Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    static readonly List<string> checks = new List<string>();
    static VisualRestorationChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run()
    {
        PlayerSettings.companyName = "LastSoldierVisualRestorationCheck";
        PlayerSettings.productName = "VisualRestorationCheck";
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        SessionState.SetBool(Flag, true); Attach(); EditorApplication.EnterPlaymode();
    }
    static void Attach()
    {
        deadline = EditorApplication.timeSinceStartup + 100;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Finish(false, message + "\n" + stack);
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout phase " + phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount < readyFrame) return;
        try
        {
            YG2.infoYG.Simulation.pauseOnFocusGame = false;
            if (YG2.isPauseGame) { YG2.PauseGame(false); return; }
            if (phase == 0)
            {
                UnityEngine.Object.FindObjectOfType<MainMenuController>().Play();
                phase = 1; readyFrame = Time.frameCount + 15;
            }
            else if (phase == 1 && SceneManager.GetActiveScene().name == "SampleScene")
            {
                foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                UnityEngine.Object.FindObjectOfType<Ui>().StartGame();
                player = UnityEngine.Object.FindObjectOfType<PlayerController>();
                player.TeleportPlayerHome();
                YG2.envir.isDesktop = false; YG2.envir.isMobile = true; YG2.envir.deviceType = "mobile";
                mobile = UnityEngine.Object.FindObjectOfType<MobileControls>(); mobile.RefreshLayout();
                movement = (TouchControl)typeof(MobileControls).GetField("movement", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mobile);
                phase = 2; readyFrame = Time.frameCount + 10;
            }
            else if (phase == 2)
            {
                Check(mobile.CanControl, "Touch movement enabled in the real camp scene");
                var rect = (RectTransform)movement.transform;
                var origin = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                pointer = new PointerEventData(EventSystem.current) { pointerId = 77, button = PointerEventData.InputButton.Left, position = origin };
                movement.OnPointerDown(pointer);
                pointer.position = origin + directions[directionIndex] * 180;
                movement.OnDrag(pointer);
                Check(Vector2.Dot(mobile.Movement.normalized, directions[directionIndex]) > .99f, "Joystick direction " + directionIndex);
                began = Time.time; frames.Clear(); phase = 3;
            }
            else if (phase == 3)
            {
                frames.Add(player.GetComponent<SpriteRenderer>().sprite);
                if (Time.time - began < .85f) return;
                Check(frames.Count == 5, "All five walk frames play while moving: direction " + directionIndex);
                Check(Vector2.Dot(-(Vector2)player.transform.up, directions[directionIndex]) > .99f,
                    "Top-down hero faces movement direction " + directionIndex);
                movement.OnPointerUp(pointer); began = Time.time; phase = 4;
            }
            else if (phase == 4)
            {
                if (Time.time - began < .2f) return;
                Check(!player.GetComponent<Animator>().GetBool("IsMoving")
                    && player.GetComponent<SpriteRenderer>().sprite.name == "PlayerAll_0", "Hero returns to idle after release");
                if (++directionIndex < directions.Length) { phase = 2; return; }
                Capture("visual-restoration-camp.png");
                var waves = UnityEngine.Object.FindObjectOfType<WaveSpawner>();
                var destination = waves.GetComponent<Collider2D>().bounds.center;
                destination.z = player.transform.position.z;
                var delta = destination - player.transform.position;
                player.transform.position = destination; player.GetComponent<Rigidbody2D>().position = destination;
                foreach (var camera in UnityEngine.Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>())
                { camera.OnTargetObjectWarped(player.transform, delta); camera.PreviousStateIsValid = false; }
                phase = 5; began = Time.time;
            }
            else if (phase == 5)
            {
                if (Time.time - began < 3f) return;
                foreach (var feature in UnityEngine.Object.FindObjectsOfType<ArenaFeature>())
                    if (feature.Kind == ArenaFeature.FeatureKind.Altar)
                        Check(!feature.transform.Find("BuildingPad").GetComponent<SpriteRenderer>().enabled,
                            "Decorative rune also remains hidden on the actual arena instance");
                Capture("visual-restoration-arena.png");
                barrel = UnityEngine.Object.FindObjectsOfType<ArenaFeature>()
                    .First(f => f.Kind == ArenaFeature.FeatureKind.Barrel && !f.IsDestroyed);
                barrel.TakeEnvironmentDamage(999);
                began = Time.time; phase = 6;
            }
            else if (phase == 6)
            {
                if (Time.time - began < .82f) return;
                var burst = Get<AudioSource>(barrel, "explosionAudio");
                Check(barrel.IsDestroyed && burst.isPlaying && burst.clip.name.StartsWith("vzryiv"),
                    "Damaged barrel plays the new wooden explosion after its warning");
                player.TeleportPlayerHome();
                player.GetComponent<Rigidbody2D>().position = player.transform.position;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Bomber.prefab");
                bomber = UnityEngine.Object.Instantiate(prefab, player.transform.position + Vector3.right * 8, Quaternion.identity).GetComponent<Enemy>();
                began = Time.time; phase = 7;
            }
            else if (phase == 7)
            {
                if (Time.time - began < .12f) return;
                var clip = bomber.sounds[0];
                int before = UnityEngine.Object.FindObjectsOfType<AudioSource>().Count(source => source.clip == clip && source.name.StartsWith("OneShot_"));
                bomber.TakeDamage(999, false); bomber.TakeDamage(999, false);
                var bursts = UnityEngine.Object.FindObjectsOfType<AudioSource>()
                    .Where(source => source.clip == clip && source.name.StartsWith("OneShot_")).ToArray();
                Check(bursts.Length == before + 1 && bursts.Last().isPlaying,
                    "Projectile damage kills bomber and plays exactly one detached explosion");
                Check(bursts.Last().spatialBlend == 0 && !bursts.Last().ignoreListenerPause
                    && Mathf.Approximately(bursts.Last().volume, .85f * Ui.sfxVolume),
                    "Bomber burst stays audible after removal and follows effect volume and listener pause");
                playerAudio = player.audioSrc; playerAudio.Stop();
                shield = Get<TouchControl>(mobile, "shield"); shieldPointer = Press(shield, 78);
                shield.OnPointerUp(shieldPointer); // The mobile shield is a tap toggle.
                began = Time.time; phase = 8;
            }
            else if (phase == 8)
            {
                if (Time.time - began < .12f) return;
                Check(Get<bool>(player, "shieldActive") && playerAudio.isPlaying,
                    "Real shield button starts its activation sound");
                phase = 9;
            }
            else if (phase == 9)
            {
                if (Time.time - began < .7f) return;
                Check(Get<bool>(player, "shieldActive") && !playerAudio.isPlaying,
                    "Active shield does not repeat its activation sound every frame");
                float health = player.hp;
                player.TakeDamage(2);
                Check(player.hp == health && playerAudio.isPlaying, "Blocked hit plays the shield impact sound");
                shieldPointer = Press(shield, 80); shield.OnPointerUp(shieldPointer); playerAudio.Stop();
                var portal = UnityEngine.Object.FindObjectsOfType<Portal>().First(p => p.linkedPortal != null);
                var expected = portal.linkedPortal.position + (Vector3)portal.offset;
                var trigger = typeof(Portal).GetMethod("TryTeleport", BindingFlags.NonPublic | BindingFlags.Instance);
                trigger.Invoke(portal, new object[] { player.GetComponent<Collider2D>() });
                Check((player.transform.position - expected).sqrMagnitude < .001f && playerAudio.isPlaying,
                    "Portal plays its teleport sound on the traveller after the warp");
                playerAudio.Stop();
                trigger.Invoke(portal, new object[] { player.GetComponent<Collider2D>() });
                Check(!playerAudio.isPlaying, "Portal cooldown prevents duplicate teleport sound");
                var home = Get<Transform>(player, "mirrorHome").position;
                player.transform.position = home + Vector3.left * 3;
                player.GetComponent<Rigidbody2D>().position = player.transform.position;
                typeof(PlayerController).GetField("mirrorRemainder", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, 1);
                // Isolate the completion sound from the already verified combat interruption path.
                typeof(PlayerController).GetField("invulnerableUntil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, Time.time + 10f);
                Get<GameObject>(mobile, "utilitiesPanel").SetActive(true);
                mirror = Get<TouchControl>(mobile, "mirror"); mirrorPointer = Press(mirror, 79);
                began = Time.time; phase = 10;
            }
            else if (phase == 10)
            {
                if (Time.time - began < player.holdDuration + .2f) return;
                Check(player.MirrorCount == 0 && (player.transform.position - Get<Transform>(player, "mirrorHome").position).sqrMagnitude < .01f,
                    "Mirror consumes one charge and returns the player home");
                Check(playerAudio.isPlaying, "Mirror completion plays its distinct teleport sound");
                mirror.OnPointerUp(mirrorPointer);
                Finish(true, "Four-direction hero animation and v15 barrel, bomber, shield, portal and mirror audio events passed; listening quality remains for owner review");
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static PointerEventData Press(TouchControl control, int id)
    {
        var rect = (RectTransform)control.transform;
        var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { pointerId = id, button = PointerEventData.InputButton.Left, position = point };
        control.OnPointerDown(data);
        Check(control.IsHeld, "Touch control accepted press: " + control.name);
        return data;
    }
    static void Capture(string name)
    {
        var camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.GetFullPath("../../../Publishing/Previews/" + name), pixels.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
    }
    static void Check(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException(message);
        checks.Add(message);
    }
    static void Finish(bool pass, string message)
    {
        SessionState.SetBool(Flag, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.GetFullPath("../visual-restoration-runtime.txt"), (pass ? "PASS\n" : "FAIL\n") + message + "\n" + string.Join("\n", checks));
        Debug.Log((pass ? "VISUAL_RESTORATION_PASS: " : "VISUAL_RESTORATION_FAIL: ") + message);
        EditorApplication.Exit(pass ? 0 : 1);
    }
}
