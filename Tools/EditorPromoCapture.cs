// Copy to Assets/Editor in the isolated validation project only.
// Records the real SampleScene and normal first-wave spawner; no fabricated HUD or rewards.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

[InitializeOnLoad]
public static class EditorPromoCapture
{
    private const string Flag = "LastSoldier.EditorPromoCapture";
    private static int phase, readyFrame;
    private static double deadline;
    private static bool mobile;
    private static string language;
    static EditorPromoCapture() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Pc() => Run(false, "en");
    public static void Mobile() => Run(true, "en");
    public static void PcRussian() => Run(false, "ru");
    public static void MobileRussian() => Run(true, "ru");
    private static void Run(bool touch, string locale)
    {
        PlayerSettings.companyName = "LastSoldierPromoCapture" + DateTime.UtcNow.Ticks;
        SessionState.SetBool(Flag + ".Mobile", touch);
        SessionState.SetString(Flag + ".Language", locale);
        // The SDK simulation saves are confined to this disposable validation project.
        string saves = "Assets/PluginYourGames/Editor";
        string backup = Path.GetFullPath("../PromoBackups/" + DateTime.UtcNow.Ticks);
        Directory.CreateDirectory(backup);
        foreach (string path in Directory.GetFiles(saves, "SavesEditorYG2*.json"))
            File.Move(path, Path.Combine(backup, Path.GetFileName(path)));
        UnityEngine.PlayerPrefs.SetString("LastSoldier.Language", locale);
        UnityEngine.PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        SessionState.SetBool(Flag, true); Attach(); EditorApplication.EnterPlaymode();
    }
    private static void Attach()
    {
        mobile = SessionState.GetBool(Flag + ".Mobile", false);
        language = SessionState.GetString(Flag + ".Language", "en");
        deadline = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Finish(false, message + "\n" + stack);
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Capture timeout phase " + phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount < 12 || Time.frameCount < readyFrame) return;
        try
        {
            YG2.infoYG.Simulation.pauseOnFocusGame = false;
            if (YG2.isPauseGame) { YG2.PauseGame(false); return; }
            if (phase == 0)
            {
                GameLocalization.SetSelection(language);
                UnityEngine.Object.FindObjectOfType<MainMenuController>().Play();
                phase = 1; readyFrame = Time.frameCount + 12;
            }
            else if (phase == 1 && SceneManager.GetActiveScene().name == "SampleScene")
            {
                foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                var ui = UnityEngine.Object.FindObjectOfType<Ui>(); ui.StartGame();
                YG2.envir.isDesktop = !mobile; YG2.envir.isMobile = mobile; YG2.envir.isTablet = false;
                YG2.envir.deviceType = mobile ? "mobile" : "desktop";
                UnityEngine.Object.FindObjectOfType<MobileControls>().RefreshLayout();
                var player = UnityEngine.Object.FindObjectOfType<PlayerController>();
                var waves = UnityEngine.Object.FindObjectOfType<WaveSpawner>();
                Vector3 destination = waves.GetComponent<Collider2D>().bounds.center;
                destination.z = player.transform.position.z;
                Vector3 delta = destination - player.transform.position;
                player.transform.position = destination;
                player.GetComponent<Rigidbody2D>().position = destination;
                foreach (var camera in UnityEngine.Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>())
                { camera.OnTargetObjectWarped(player.transform, delta); camera.PreviousStateIsValid = false; }
                var driver = player.gameObject.AddComponent<PromoCaptureDriver>();
                driver.player = player; driver.waves = waves; driver.mobile = mobile; driver.language = language;
                phase = 2;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    public static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag, false); Time.captureFramerate = 0;
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.GetFullPath("../promo-" + (language == "ru" ? "ru-" : "") + (mobile ? "mobile" : "pc") + ".txt"),
            (success ? "PASS\n" : "FAIL\n") + message);
        Debug.Log((success ? "PROMO_CAPTURE_PASS: " : "PROMO_CAPTURE_FAIL: ") + message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}

[DefaultExecutionOrder(33000)]
public sealed class PromoCaptureDriver : MonoBehaviour
{
    public PlayerController player;
    public WaveSpawner waves;
    public bool mobile;
    public string language;
    private readonly List<MonoBehaviour> targets = new List<MonoBehaviour>();
    private int frames, lastFrame = -1;
    private Camera[] cameras;
    private RenderTexture target;
    private Texture2D pixels;
    private string directory;
    private Vector2 centre;
    private float began;
    private void Start()
    {
        Time.captureFramerate = 30;
        directory = Path.GetFullPath("../../CardCapture/" + (language == "ru" ? "Russian/" : "English/") + (mobile ? "mobile" : "pc"));
        Directory.CreateDirectory(directory);
        cameras = FindObjectsOfType<Camera>().Where(camera => camera.enabled).OrderBy(camera => camera.depth).ToArray();
        target = new RenderTexture(1280, 720, 24); target.Create();
        pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        foreach (var canvas in FindObjectsOfType<Canvas>().Where(canvas => canvas.isRootCanvas))
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameras.Last(); canvas.planeDistance = 5;
        }
        foreach (var camera in cameras) camera.targetTexture = target;
        centre = player.transform.position;
        began = Time.time;
    }
    private void LateUpdate()
    {
        if (!waves.IsCombatActive || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        if (player.hp <= 0) { EditorPromoCapture.Finish(false, "Player defeated during recording"); return; }
        // Choose targets within the visible arena, as a player would do during normal play.
        waves.FillArenaTargets(targets, player.transform.position, 5f);
        var nearest = targets.Where(enemy => enemy != null)
            .OrderBy(enemy => ((Vector2)enemy.transform.position - (Vector2)player.transform.position).sqrMagnitude).FirstOrDefault();
        Vector2 waypoint = centre + new Vector2(Mathf.Sin((Time.time - began) * .7f), Mathf.Cos((Time.time - began) * .7f)) * 2.1f;
        Vector2 move = (waypoint - (Vector2)player.transform.position).normalized;
        player.GetComponent<Rigidbody2D>().velocity = move * 1.7f;
        if (nearest != null)
        {
            Vector2 direction = nearest.transform.position - player.transform.position;
            player.transform.up = -direction.normalized;
            player.crossbowController.Shoot(direction.normalized);
        }
        // Allow the first wave HUD/camera to settle before writing any promo frame.
        if (Time.time - began < 3f) return;
        try
        {
            GameLocalization.RefreshAll(); Canvas.ForceUpdateCanvases();
            foreach (var camera in cameras) camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(directory, "frame" + frames.ToString("D4") + ".png"), pixels.EncodeToPNG());
            frames++;
            if (frames >= 600)
                EditorPromoCapture.Finish(true, "600 real Unity PlayMode frames, 1280x720, 30fps, normal first-wave spawning; " +
                    (mobile ? "Android control layout in SDK simulator" : "PC control layout") + "; automated movement and firing. No progression/health/ammo cheats; fresh isolated profile.");
        }
        catch (Exception error) { EditorPromoCapture.Finish(false, error.ToString()); }
    }
}
