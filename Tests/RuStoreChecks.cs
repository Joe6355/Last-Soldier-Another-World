#if LAST_SOLDIER_RUSTORE
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
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YandexMobileAds;
using YandexMobileAds.Base;
using YG;

// Runs in the isolated worktree with a disposable Editor save, not on a phone.
[InitializeOnLoad]
public static class RuStoreChecks
{
    const string Flag = "LastSoldier.RuStoreChecks";
    const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly List<string> checks = new List<string>();
    static int phase, frame, coins;
    static double deadline;
    static PlayerController player;
    static Ui ui;
    static GameMonetization monetization;
    static RewardedAd ad;
    static object client;
    static bool resultCompleted;
    static GameMonetization.CoinsRewardResult result;
    static string previousCompany, previousProduct;
    static RuStoreChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run()
    {
        previousCompany = PlayerSettings.companyName; previousProduct = PlayerSettings.productName;
        SessionState.SetString(Flag + ".company", previousCompany); SessionState.SetString(Flag + ".product", previousProduct);
        PlayerSettings.companyName = "LastSoldierRuStoreValidation";
        PlayerSettings.productName = "RuStoreValidation";
        var info = YG2.infoYG;
        info.Authorization.authorized = false; info.Simulation.pauseOnFocusGame = false;
        info.Storage.saveLocal = false; info.Storage.saveCloud = true;
        string editorFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(info));
        string save = Path.Combine("Assets/PluginYourGames/Editor", "SavesEditorYG2.guest.json");
        if (File.Exists(save)) File.WriteAllText(save, "{}");
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        SessionState.SetBool(Flag, true); Attach(); EditorApplication.EnterPlaymode();
    }
    static void Attach()
    {
        deadline = EditorApplication.timeSinceStartup + 210;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Finish(false, message + "\n" + stack); }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout at phase " + phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount < frame) return;
        try
        {
            switch (phase)
            {
                case 0:
                    YG2.envir.isMobile = true;
                    UnityEngine.PlayerPrefs.DeleteKey(RuStoreAds.PrivacyNoticeKey);
                    Check(!YG2.player.auth, "Start as guest without requiring sign-in");
                    RuStoreMenu.OpenPrivacy(true);
                    Check(Time.timeScale == 0 && AudioListener.pause, "Privacy sheet pauses combat and sound");
                    Check(GameObject.Find("RuStoreDialog/Panel/Scroll/Content/Policy") != null, "Full local privacy document has scrollable content");
                    Capture("privacy-ru.png");
                    Click("RuStoreDialog", "Continue");
                    Check(!RuStoreMenu.IsOverlayOpen && UnityEngine.PlayerPrefs.GetInt(RuStoreAds.PrivacyNoticeKey) == 1, "Notice acknowledgement closes overlay and persists");
                    GameProgress.BeginAuthorization();
                    Check(!GameProgress.AuthPending && GameProgress.IsReady && !YG2.player.auth, "Disabled sign-in leaves the local game ready");
                    var menu = UnityEngine.Object.FindObjectOfType<MainMenuController>();
                    Check(!Get<Button>(menu, "authButton").gameObject.activeSelf && !Get<Button>(menu, "ratingButton").gameObject.activeSelf,
                        "Sign-in and shared leaderboard buttons are absent from the first release");
                    Capture("menu-local.png");
                    CheckMenuLayouts(menu.gameObject.scene, menu.SettingsPanel, menu.ShowSettings, menu.CloseSettings);
                    UnityEngine.Object.FindObjectOfType<MainMenuController>().Play(); Next(); break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    player = UnityEngine.Object.FindObjectOfType<PlayerController>();
                    ui = UnityEngine.Object.FindObjectOfType<Ui>();
                    monetization = UnityEngine.Object.FindObjectOfType<GameMonetization>();
                    foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                    ui.StartGame();
                    ui.ToggleMenu();
                    Check(!Get<Button>(ui, "ratingButton").gameObject.activeSelf, "Pause menu hides the shared leaderboard entry");
                    ui.ShowRating();
                    Check(!Get<GameObject>(ui, "ratingPanel").activeSelf, "Pause menu cannot reopen the disabled shared leaderboard");
                    CheckMenuLayouts(ui.gameObject.scene, ui.SettingsPanel, ui.ShowSettings, ui.CloseSettings);
                    ui.StartGame();
                    Check(UnityEngine.Object.FindObjectOfType<MobileControls>().UseTouch, "Native mobile control layout remains available");
                    Capture("camp-mobile.png");
                    coins = player.totalCoins;
                    Set(RuStoreAds.Instance, "rewarded", null);
                    resultCompleted = false;
                    Check(monetization.RequestCoinsForVideo(player, 500, (value, amount) => { result = value; resultCompleted = true; }), "Reward request enters the existing game callback path");
                    Check(resultCompleted && result == GameMonetization.CoinsRewardResult.Unavailable && !YG2.nowAdsShow && !YG2.isPauseGame,
                        "Unavailable ad releases SDK pause and reports failure without a fake video");
                    Check(player.totalCoins == coins, "Unavailable ad grants no coins");
                    Call(RuStoreAds.Instance, "LoadRewarded"); Next(); break;
                case 2:
                    ad = Get<RewardedAd>(RuStoreAds.Instance, "rewarded"); if (ad == null) return;
                    client = Get<object>(ad, "_client");
                    resultCompleted = false;
                    Check(monetization.RequestCoinsForVideo(player, 500, (value, amount) => { result = value; resultCompleted = true; }), "Loaded rewarded ad is requested once");
                    // Native AdActivity can suspend Unity before its queued OnAdShown runs.
                    Set(RuStoreAds.Instance, "showDeadline", Time.unscaledTime - 1f);
                    Emit("OnAdShown", EventArgs.Empty);
                    Call(RuStoreAds.Instance, "Update");
                    Check(Get<RewardedAd>(RuStoreAds.Instance, "activeRewarded") == ad,
                        "Expired show deadline waits for queued SDK callbacks before cancelling a native ad");
                    Next(); break;
                case 3:
                    Check(Get<bool>(RuStoreAds.Instance, "adShown"), "Queued SDK shown callback survives an expired deadline on resume");
                    Check(YG2.isPauseGame && YG2.nowRewardAdv && AudioListener.pause, "Real adapter pauses gameplay while an ad is shown");
                    Emit("OnRewarded", new Reward(1, "reward")); Emit("OnRewarded", new Reward(1, "reward")); Next(); break;
                case 4:
                    Check(player.totalCoins == coins + 500, "Duplicate vendor reward callbacks grant exactly one coin reward");
                    Emit("OnAdDismissed", EventArgs.Empty); Next(); break;
                case 5:
                    Check(resultCompleted && result == GameMonetization.CoinsRewardResult.Granted, "Dismissal completes the granted game reward");
                    Check(!YG2.nowAdsShow && !YG2.isPauseGame && !AudioListener.pause, "Ad dismissal restores normal gameplay and audio");
                    GameProgress.SaveNow();
                    Check(YG2.saves.intKeys.Contains("Coins") && YG2.saves.intValues[YG2.saves.intKeys.IndexOf("Coins")] == player.totalCoins, "Granted reward is in the saved progress");
                    Call(RuStoreAds.Instance, "LoadRewarded"); Next(); break;
                case 6:
                    ad = Get<RewardedAd>(RuStoreAds.Instance, "rewarded"); if (ad == null) return;
                    client = Get<object>(ad, "_client"); coins = player.totalCoins; resultCompleted = false;
                    monetization.RequestCoinsForVideo(player, 500, (value, amount) => { result = value; resultCompleted = true; });
                    Emit("OnAdShown", EventArgs.Empty); Emit("OnAdDismissed", EventArgs.Empty); Next(); break;
                case 7:
                    Check(resultCompleted && result == GameMonetization.CoinsRewardResult.Cancelled && player.totalCoins == coins, "Closing before reward grants nothing and remains playable");
                    Call(RuStoreAds.Instance, "LoadRewarded"); Next(); break;
                case 8:
                    ad = Get<RewardedAd>(RuStoreAds.Instance, "rewarded"); if (ad == null) return;
                    resultCompleted = false;
                    monetization.RequestCoinsForVideo(player, 500, (value, amount) => { result = value; resultCompleted = true; });
                    Call(RuStoreAds.Instance, "OnApplicationFocus", false);
                    Call(RuStoreAds.Instance, "OnApplicationPause", true);
                    Set(RuStoreAds.Instance, "showDeadline", Time.unscaledTime - 1f);
                    Call(RuStoreAds.Instance, "LateUpdate");
                    Check(Get<RewardedAd>(RuStoreAds.Instance, "activeRewarded") == ad,
                        "Native activity suspension does not consume the show timeout");
                    Call(RuStoreAds.Instance, "OnApplicationPause", false);
                    Call(RuStoreAds.Instance, "OnApplicationFocus", true);
                    Check(Get<float>(RuStoreAds.Instance, "showDeadline") > Time.unscaledTime,
                        "Returning from the native activity gives queued callbacks time to arrive");
                    Set(RuStoreAds.Instance, "showDeadline", Time.unscaledTime - 1f);
                    Call(RuStoreAds.Instance, "LateUpdate");
                    Check(resultCompleted && result == GameMonetization.CoinsRewardResult.Unavailable && player.totalCoins == coins
                        && !YG2.nowAdsShow && !YG2.isPauseGame, "Missing SDK response still times out without granting coins or leaving the game paused");
                    ui.ShowSettings();
                    RuStoreMenu.OpenPrivacy(false); GameLocalization.SetSelection("en");
                    RuStoreMenu.Close(); RuStoreMenu.OpenPrivacy(false); Capture("privacy-en.png"); RuStoreMenu.Close();
                    Check(ui.SettingsPanel.transform.Find("RuStoreLinks") != null, "Privacy and support entry points exist in game settings");
                    GameLocalization.SetSelection("ru");
                    var demoSettings = UnityEngine.Object.Instantiate(RuStoreSettings.Load());
                    demoSettings.useTestAds = true;
                    bool rejected = false;
                    try { YandexBuild.ValidateRuStoreRelease(demoSettings); } catch (InvalidOperationException) { rejected = true; }
                    UnityEngine.Object.Destroy(demoSettings);
                    Check(rejected, "Production guard rejects vendor demo IDs");
                    Check(ui.SettingsPanel.transform.Find("RuStoreLinks/Account") == null,
                        "Settings contain privacy and support without an account entry point");
                    Finish(true, "Local startup, hidden online features, privacy UI, coin persistence and queued native ad callbacks passed with vendor Editor stubs; device results are recorded separately."); break;
            }
        }
        catch (Exception error) { Finish(false, "Phase " + phase + ": " + error); }
    }
    static void Next() { phase++; frame = Time.frameCount + 8; }
    static void CheckMenuLayouts(Scene scene, GameObject settings, Action openSettings, Action closeSettings)
    {
        Directory.CreateDirectory("Builds/RuStoreValidation/Previews");
        foreach (var size in new[] { new Vector2Int(854, 393), new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2400, 1080) })
        foreach (string language in new[] { "ru", "en" })
        {
            GameLocalization.SetSelection(language);
            string prefix = "Builds/RuStoreValidation/Previews/mobile-" + scene.name + "-" + size.x + "x" + size.y + "-" + language;
            CaptureLayout(prefix + ".png", size, scene);
            var viewport = UnityEngine.Object.FindObjectsOfType<RectTransform>()
                .First(value => value.gameObject.scene == scene && value.name == (scene.name == "Menu" ? "RuStoreBannerSafeContent" : "RuStorePauseSafeContent"));
            var min = viewport.anchorMin; var max = viewport.anchorMax;
            // Simulate a cutout and a loaded bottom banner without making ad requests.
            viewport.anchorMin = new Vector2(.055f, scene.name == "Menu" ? .17f : .03f);
            viewport.anchorMax = new Vector2(.975f, .98f);
            CaptureLayout(prefix + "-insets.png", size, scene);
            viewport.anchorMin = min; viewport.anchorMax = max;
            openSettings();
            CaptureLayout(prefix + "-settings.png", size, scene, screen: settings.transform);
            // Use the real serialized Back handler, not a direct hide of the panel.
            settings.transform.Find("BackTyMenu").GetComponent<Button>().onClick.Invoke();
            Check(!settings.activeSelf, "Settings Back returns to menu: " + scene.name + " " + size + " " + language);
            if (scene.name == "SampleScene")
            {
                var mobile = UnityEngine.Object.FindObjectOfType<MobileControls>();
                Get<Button>(mobile, "pauseControlsSettingsButton").onClick.Invoke();
                Check(mobile.IsControlsSettingsOpen && Time.timeScale == 0, "Control customization opens from pause without resuming combat");
                CaptureLayout(prefix + "-controls.png", size, scene, "RuStoreControlsSafeContent");
                Get<Button>(mobile, "closeControlsSettingsButton").onClick.Invoke();
                Check(settings.activeSelf && !mobile.IsControlsSettingsOpen, "Control customization Back returns to settings");
            }
            closeSettings();
        }
        GameLocalization.SetSelection("ru");
    }
    static void CaptureLayout(string filename, Vector2Int size, Scene scene, string rootName = null, Transform screen = null)
    {
        LocalizationChecks.CaptureView(Path.GetFullPath(filename), size.x, size.y, () =>
        {
            var root = screen ?? UnityEngine.Object.FindObjectsOfType<RectTransform>()
                .First(value => value.gameObject.scene == scene && value.name == (rootName ?? (scene.name == "Menu" ? "RuStoreBannerSafeContent" : "RuStorePauseSafeContent")));
            var controls = root.GetComponentsInChildren<Selectable>().Where(value => value.isActiveAndEnabled).ToArray();
            var bounds = new Dictionary<Selectable, Rect>();
            foreach (var control in controls)
            {
                var canvas = control.GetComponentInParent<Canvas>();
                var camera = canvas.rootCanvas.worldCamera;
                var corners = new Vector3[4]; ((RectTransform)control.transform).GetWorldCorners(corners);
                var points = corners.Select(value => RectTransformUtility.WorldToScreenPoint(camera, value)).ToArray();
                var rect = Rect.MinMaxRect(points.Min(value => value.x), points.Min(value => value.y), points.Max(value => value.x), points.Max(value => value.y));
                Check(rect.xMin >= -1 && rect.yMin >= -1 && rect.xMax <= size.x + 1 && rect.yMax <= size.y + 1,
                    "Touch target is inside viewport: " + control.name + " " + filename);
                Check(rect.height >= size.y * .095f, "Touch target has usable height: " + control.name + " " + filename);
                bounds[control] = rect;
                foreach (float x in new[] { .2f, .5f, .8f })
                {
                    var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, x), rect.center.y) };
                    var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                    Check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(control.transform),
                        "Touch reaches intended control: " + control.name + " " + x + " " + filename);
                }
                foreach (var text in control.GetComponentsInChildren<TMP_Text>().Where(value => value.isActiveAndEnabled))
                {
                    text.ForceMeshUpdate();
                    Check(!text.isTextOverflowing, "Button label is not clipped: " + text.text + " " + filename);
                }
            }
            for (int i = 0; i < controls.Length; i++) for (int j = i + 1; j < controls.Length; j++)
                Check(!bounds[controls[i]].Overlaps(bounds[controls[j]]), "Touch targets do not overlap: " + controls[i].name + "/" + controls[j].name + " " + filename);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>().Where(value => value.isActiveAndEnabled))
            {
                text.ForceMeshUpdate();
                Check(!text.isTextOverflowing, "Menu text fits: " + text.name + " " + filename);
            }
        });
    }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Fields).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Fields).SetValue(obj, value);
    static void Call(object obj, string method) => obj.GetType().GetMethod(method, Fields).Invoke(obj, null);
    static void Call(object obj, string method, bool value) => obj.GetType().GetMethod(method, Fields).Invoke(obj, new object[] { value });
    static void Emit(string eventName, object value)
    { ((Delegate)client.GetType().GetField(eventName, Fields).GetValue(client)).DynamicInvoke(client, value); }
    static void Click(string root, string name) => GameObject.Find(root).GetComponentsInChildren<Button>(true).First(button => button.name == name).onClick.Invoke();
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks.Add(message); }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Builds/RuStoreValidation/Previews");
        typeof(LocalizationChecks).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { Path.GetFileName(Directory.GetCurrentDirectory()) + "/Builds/RuStoreValidation/Previews/" + name });
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        PlayerSettings.companyName = SessionState.GetString(Flag + ".company", "Joe6355");
        PlayerSettings.productName = SessionState.GetString(Flag + ".product", "Последний солдат — проверка");
        Directory.CreateDirectory("Builds/RuStoreValidation");
        File.WriteAllText("Builds/RuStoreValidation/editor-checks.txt", (success ? "PASS" : "FAIL") + "\n" + message + "\nAssertions: " + checks.Count + "\n" + string.Join("\n", checks));
        EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif
