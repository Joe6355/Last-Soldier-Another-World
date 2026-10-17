// Isolated validation project only; does not access the user's progress.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Prefs = RedefineYG.PlayerPrefs;

[InitializeOnLoad]
public static class PortalTutorialChecks
{
    const string Flag = "LastSoldier.PortalV20Checks";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int phase, afterFrame, sample;
    static float began, pausedTime;
    static double deadline, realBegan;
    static Portal gate;
    static Animator animator;
    static SpriteRenderer renderer;
    static PlayerController player;
    static TipsManager guide;
    static readonly List<string> checks = new List<string>();
    static readonly HashSet<string> frames = new HashSet<string>();
    static PortalTutorialChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run()
    {
        PlayerSettings.companyName = "LastSoldierPortalV20Checks"; PlayerSettings.productName = "PortalV20Checks";
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        SessionState.SetBool(Flag, true); Attach(); EditorApplication.EnterPlaymode();
    }
    static void Attach()
    {
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        deadline = EditorApplication.timeSinceStartup + 120;
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack); }
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks.Add(message); Debug.Log("PORTAL_V20_CHECK: " + message); }
    static void Next(int step)
    { phase = step; afterFrame = Time.frameCount + 3; began = Time.time; realBegan = EditorApplication.timeSinceStartup; frames.Clear(); }
    static void Place(Vector3 position)
    {
        var delta = position - player.transform.position;
        player.transform.position = position; player.GetComponent<Rigidbody2D>().position = position;
        foreach (var camera in UnityEngine.Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>())
        { camera.OnTargetObjectWarped(player.transform, delta); camera.PreviousStateIsValid = false; }
        Physics2D.SyncTransforms();
    }
    static void Tick()
    {
        Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout at phase " + phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount < afterFrame) return;
        try
        {
            YG.YG2.infoYG.Simulation.pauseOnFocusGame = false;
            if (YG.YG2.isPauseGame) { YG.YG2.PauseGame(false); return; }
            switch (phase)
            {
                case 0:
                    Prefs.DeleteAll(); Prefs.SetInt("GuidedTutorial.v1.Done", 1); Prefs.SetInt("GuidedTutorial.v1.ArenaDone", 1);
                    UnityEngine.Object.FindObjectOfType<MainMenuController>().Play(); Next(1); break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    YG.YG2.envir.isDesktop = true; YG.YG2.envir.isMobile = YG.YG2.envir.isTablet = false;
                    UnityEngine.Object.FindObjectOfType<Ui>().SetMobileOrientationPause(false);
                    UnityEngine.Object.FindObjectOfType<Ui>().StartGame(); Time.timeScale = 1;
                    player = UnityEngine.Object.FindObjectOfType<PlayerController>(); player.crossbowController.canShoot = false;
                    gate = GameObject.Find("CrimsonEntrance").GetComponent<Portal>(); animator = gate.GetComponent<Animator>(); renderer = gate.GetComponent<SpriteRenderer>();
                    guide = UnityEngine.Object.FindObjectsOfType<TipsManager>().First(g => Get<bool>(g, "guidedTutorial"));
                    var overridden = animator.runtimeAnimatorController as AnimatorOverrideController;
                    Check(overridden != null, "Red portal uses an override of the existing controller");
                    Check(overridden["PortalZapusk"].isLooping, "Red open clip is imported as a genuine loop");
                    Check(!AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Anim/Portal/PortalZapusk.anim").isLooping, "Original green portal clip remains unchanged");
                    Check(gate.linkedPortal.GetComponent<Animator>().runtimeAnimatorController == overridden, "Red return portal shares the repaired controller");
                    Check(gate.GetComponentsInChildren<PortalAnim>().Length == 1, "Portal proximity callback is not duplicated");
                    CheckPortalPixels();
                    Place(gate.transform.position + Vector3.down * 3); Next(2); break;
                case 2:
                    frames.Add(renderer.sprite.name);
                    if (Time.time - began < 1.5f) return;
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("PortalStart") && frames.Count >= 12, "Distant red portal keeps its original animated idle cycle");
                    Place(gate.transform.position + Vector3.down * 1.5f); Next(3); break;
                case 3:
                    if (Time.time - began < .4f) return;
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("PortalZapusk"), "Approaching actually switches red portal to its open animation");
                    Next(4); break;
                case 4:
                    frames.Add(renderer.sprite.name);
                    if (Time.time - began < 1.5f) return;
                    Check(frames.Count == 5 && animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 3,
                        "All five open frames keep playing for multiple cycles without freezing");
                    Check(Vector2.Distance(player.transform.position, gate.transform.position) < 2 && !gate.IsUnlocked, "Locked portal still animates without teleporting a new player");
                    Capture("portal-v20-open.png");
                    pausedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime; Time.timeScale = 0; Next(5); break;
                case 5:
                    if (EditorApplication.timeSinceStartup - realBegan < .25) return;
                    Check(Mathf.Approximately(pausedTime, animator.GetCurrentAnimatorStateInfo(0).normalizedTime), "Open animation respects game pause");
                    Time.timeScale = 1; Place(gate.transform.position + Vector3.down * 3); Next(6); break;
                case 6:
                    if (Time.time - began < .4f) return;
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("PortalStart"), "Leaving proximity returns to the original idle cycle");
                    var money = player.totalCoins; var stats = UnityEngine.Object.FindObjectOfType<Stats>(); var rating = stats.Mmr;
                    guide.RestartGuide(); Set(guide, "step", TipsManager.GuideStep.ArenaFinished);
                    var entrance = WaveSpawner.FindArena(false).TutorialExit.GetComponent<Portal>().linkedPortal;
                    Place(entrance.position + Vector3.down * 1.6f);
                    Set(guide, "result", false); Set(guide, "outcome", null); Set(guide, "prepared", false);
                    guide.AdvanceGuide();
                    Check(guide.CurrentStep == TipsManager.GuideStep.CrimsonArena, "Arena lesson advances to the new red-arena explanation before completion");
                    Check(player.totalCoins == money && stats.Mmr == rating, "Information step does not award supplies or change progress");
                    Check(!TipsManager.AllowsPortal(gate), "Explanation cannot accidentally send the player into hard combat");
                    Next(7); break;
                case 7:
                    bool touch = sample >= 2; bool english = sample % 2 == 1;
                    YG.YG2.envir.isDesktop = !touch; YG.YG2.envir.isMobile = touch;
                    GameLocalization.SetSelection(english ? "en" : "ru");
                    Call(guide, "RenderCurrent"); Call(guide, "RefreshTarget");
                    Call(guide, "PositionGuide", null, gate.transform, false, false);
                    GameLocalization.RefreshAll(); Canvas.ForceUpdateCanvases();
                    var body = Get<TextMeshProUGUI>(guide, "guideBodyText"); body.ForceMeshUpdate();
                    Check(body.text.Contains("2000 MMR") && body.text.Contains(english ? "Two bosses" : "два босса"), "Difficulty, unlock and boss pair explained: " + sample);
                    Check(body.preferredHeight <= body.rectTransform.rect.height + 3, "Complete red-arena explanation fits: " + sample);
                    Check(Get<Transform>(guide, "targetWorld") == gate.transform && Get<RectTransform>(guide, "guideFocus").gameObject.activeSelf,
                        "Tutorial visibly highlights the actual red portal: " + sample);
                    Check(Get<TextMeshProUGUI>(guide, "guideActionText").text == (english ? "Done" : "Готово"), "Final information step has a clear finish button: " + sample);
                    Check(Get<TextMeshProUGUI>(guide, "guideCounterText").text == (english ? "ARENA · 8 / 8" : "АРЕНА · 8 / 8"), "New eighth arena step counter is localized: " + sample);
                    Capture("portal-v20-tutorial-" + (touch ? "mobile-" : "pc-") + (english ? "en" : "ru") + ".png");
                    if (++sample < 4) { Next(7); break; }
                    guide.AdvanceGuide();
                    Check(!TipsManager.IsGuideActive && Prefs.GetInt("GuidedTutorial.v1.ArenaDone", 0) == 1, "Final acknowledgement completes and saves the tutorial");
                    Check((int)TipsManager.GuideStep.ArenaFinished == 22, "Saved indices of all previous lessons remain compatible");
                    Finish(true, "Portal preserves stone pixels, loops open/idle correctly, pauses/resumes, and explains hard arena on PC/mobile in Russian/English"); break;
            }
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }
    static void CheckPortalPixels()
    {
        var shaderMaterial = renderer.sharedMaterial;
        var plainMaterial = new Material(Shader.Find("Sprites/Default"));
        var probe = new GameObject("PortalPixelProbe", typeof(SpriteRenderer)); probe.layer = 31; probe.transform.position = new Vector3(4000,4000,0);
        var spriteRenderer = probe.GetComponent<SpriteRenderer>();
        var cameraObject = new GameObject("PortalPixelCamera", typeof(Camera)); var camera = cameraObject.GetComponent<Camera>();
        camera.transform.position = new Vector3(4000,4000,-10); camera.orthographic = true; camera.orthographicSize = .62f;
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        var target = new RenderTexture(96,128,24); target.Create(); camera.targetTexture = target;
        Color32[] Render(Material material)
        {
            spriteRenderer.sharedMaterial = material; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(96,128,TextureFormat.RGBA32,false); texture.ReadPixels(new Rect(0,0,96,128),0,0); texture.Apply();
            var pixels = texture.GetPixels32(); RenderTexture.active = previous; UnityEngine.Object.Destroy(texture); return pixels;
        }
        foreach (string path in new[] { "Assets/Anim/Portal/PortalStart.anim", "Assets/Anim/Portal/PortalZapusk.anim" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var sprites = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
            foreach (int index in new[] { 0, sprites.Length / 2, sprites.Length - 1 })
            {
                spriteRenderer.sprite = (Sprite)sprites[index].value; var original = Render(plainMaterial); var crimson = Render(shaderMaterial);
                int stone = 0, glow = 0;
                for (int i = 0; i < original.Length; i++)
                {
                    var a = original[i]; var b = crimson[i]; if (a.a < 250) continue;
                    if (a.g > Mathf.Max(a.r,a.b) + 12) { glow++; CheckPixel(b.r > b.g, "Green glow did not turn red"); }
                    else if (Mathf.Max(a.r,a.g,a.b) - Mathf.Min(a.r,a.g,a.b) < 12 && a.r > 20)
                    { stone++; CheckPixel(Mathf.Abs(a.r-b.r) < 3 && Mathf.Abs(a.g-b.g) < 3 && Mathf.Abs(a.b-b.b) < 3, "Neutral stone pixel was tinted"); }
                }
                Check(stone > 100 && glow > 10, "Original stone preserved; magic alone recolored: " + clip.name + "/" + index);
            }
        }
        UnityEngine.Object.Destroy(probe); UnityEngine.Object.Destroy(cameraObject); UnityEngine.Object.Destroy(plainMaterial); target.Release(); UnityEngine.Object.Destroy(target);
    }
    static void CheckPixel(bool condition, string error) { if (!condition) throw new Exception(error); }
    static void Capture(string name)
    {
        var center = gate.transform.position + new Vector3(-1.5f,-1,-6);
        foreach (var virtualCamera in UnityEngine.Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>()) virtualCamera.enabled = false;
        Camera.main.transform.position = center; Camera.main.orthographicSize = 4;
        var cameras = UnityEngine.Object.FindObjectsOfType<Camera>().Where(camera => camera.enabled).OrderBy(camera => camera.depth).ToArray();
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(canvas => canvas.isRootCanvas).ToArray();
        var modes = canvases.Select(canvas => canvas.renderMode).ToArray();
        var worldCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        var distances = canvases.Select(canvas => canvas.planeDistance).ToArray();
        var targets = cameras.Select(camera => camera.targetTexture).ToArray();
        // Keep the real Game View size: changing aspect ratio after measuring the
        // overlay makes a correct world highlight appear displaced in a capture.
        var target = new RenderTexture(Screen.width, Screen.height, 24); target.Create();
        foreach (var camera in cameras) camera.targetTexture = target;
        if (TipsManager.IsGuideActive)
        {
            Canvas.ForceUpdateCanvases(); Call(guide, "PositionGuide", null, gate.transform, false, false);
            Canvas.ForceUpdateCanvases();
            Vector2 portalPoint = Camera.main.WorldToScreenPoint(renderer.bounds.center);
            Check(RectTransformUtility.RectangleContainsScreenPoint(Get<RectTransform>(guide, "guideFocus"), portalPoint, null),
                "On-screen focus contains the visible portal: " + sample);
            Check(!RectTransformUtility.RectangleContainsScreenPoint(Get<RectTransform>(guide, "guideCard"), portalPoint, null),
                "Explanation leaves the portal visible: " + sample);
        }
        for (int i = 0; i < canvases.Length; i++) if (modes[i] == RenderMode.ScreenSpaceOverlay)
        { canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = cameras.Last(); canvases[i].planeDistance = 5f; }
        Canvas.ForceUpdateCanvases(); foreach (var camera in cameras) camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0); texture.Apply();
        File.WriteAllBytes(Path.GetFullPath("../" + name), texture.EncodeToPNG()); RenderTexture.active = previous;
        for (int i = 0; i < cameras.Length; i++) cameras[i].targetTexture = targets[i];
        for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = worldCameras[i]; canvases[i].planeDistance = distances[i]; }
        UnityEngine.Object.Destroy(texture); target.Release(); UnityEngine.Object.Destroy(target);
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.GetFullPath("../portal-v20-checks.txt"), (success ? "PASS" : "FAIL") + "\n" + message + "\n" + string.Join("\n", checks));
        Debug.Log((success ? "PORTAL_V20_PASS: " : "PORTAL_V20_FAIL: ") + message); EditorApplication.Exit(success ? 0 : 1);
    }
}
