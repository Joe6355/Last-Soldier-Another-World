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
                    UnityEngine.Object.FindObjectOfType<MainMenuController>().Play(); Next(); break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    player = UnityEngine.Object.FindObjectOfType<PlayerController>();
                    ui = UnityEngine.Object.FindObjectOfType<Ui>();
                    monetization = UnityEngine.Object.FindObjectOfType<GameMonetization>();
                    foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
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
                    Emit("OnAdShown", EventArgs.Empty); Next(); break;
                case 3:
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
                    Finish(true, "Local startup, hidden online features, privacy UI, coin persistence and native ad callbacks passed with vendor Editor stubs; real Android ads remain unverified."); break;
            }
        }
        catch (Exception error) { Finish(false, "Phase " + phase + ": " + error); }
    }
    static void Next() { phase++; frame = Time.frameCount + 8; }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Fields).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Fields).SetValue(obj, value);
    static void Call(object obj, string method) => obj.GetType().GetMethod(method, Fields).Invoke(obj, null);
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
        File.WriteAllText("Builds/RuStoreValidation/editor-checks.txt", (success ? "PASS" : "FAIL") + "\n" + message + "\n" + string.Join("\n", checks));
        EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif
