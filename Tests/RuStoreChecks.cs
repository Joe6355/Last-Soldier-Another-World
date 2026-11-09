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
    static int editorUpdateWarnings, editorAutoResumes;
    static double nextHeartbeat;
    static GameMonetization.CoinsRewardResult result;
    static string previousCompany, previousProduct;
    static string layoutKey;
    static float layoutX, layoutY;
    const string StatusLayoutKey = "MobileControls.v2.CombatStatus";
    static Vector2 statusPositionInMenu;
    static RuStoreChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run() => Run(false);
    public static void RunHudProfile() => Run(true);
    public static void RunGameplayChecks() => Run(false, true);
    private static void Run(bool hudProfile, bool gameplayOnly = false)
    {
        SessionState.SetBool(Flag + ".hudProfile", hudProfile);
        SessionState.SetBool(Flag + ".gameplayOnly", gameplayOnly);
        YandexBuild.PrepareRuStoreControlsPreview();
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
        deadline = EditorApplication.timeSinceStartup + 420;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    {
        // The vendor's Editor-only update checker is unrelated to the running game.
        // Keep all runtime errors fatal; only this exact external update check is excluded.
        if (type == LogType.Error && stack.Contains("YG.EditorScr.Server/<ReadFileFromURL>")
            && (message.StartsWith("Server info request failed:") || message.StartsWith("Server info request error:")
                || message == "The information for the PluginYG2 was not uploaded due to a lack of Internet or an incorrect URL."))
        { editorUpdateWarnings++; return; }
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup >= nextHeartbeat)
        {
            Directory.CreateDirectory("Builds/RuStoreValidation");
            File.WriteAllText("Builds/RuStoreValidation/editor-check-state.txt", "phase=" + phase + "; frame=" + Time.frameCount
                + "; playing=" + EditorApplication.isPlaying + "; paused=" + EditorApplication.isPaused + "; vendorWarnings=" + editorUpdateWarnings
                + "; focused=" + Application.isFocused + "; background=" + Application.runInBackground);
            nextHeartbeat = EditorApplication.timeSinceStartup + 10;
        }
        if (EditorApplication.isPaused && editorUpdateWarnings > 0)
        { editorAutoResumes++; EditorApplication.isPaused = false; }
        // RenderTexture captures must not stop subsequent Play Mode frames when the batch Editor loses focus.
        if (Application.isBatchMode && EditorApplication.isPlaying) Application.runInBackground = true;
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout at phase " + phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount < frame) return;
        try
        {
            switch (phase)
            {
                case 0:
                    YG2.envir.isMobile = true;
                    RuStoreMenu.SetInterfaceScale(1f);
                    if (SessionState.GetBool(Flag + ".hudProfile", false) || SessionState.GetBool(Flag + ".gameplayOnly", false))
                    {
                        UnityEngine.Object.FindObjectOfType<MainMenuController>().Play();
                        phase = 1; frame = Time.frameCount + 8; break;
                    }
                    UnityEngine.PlayerPrefs.DeleteKey("LastSoldier.RuStore.PrivacyNotice.v1");
                    Check(!YG2.player.auth, "Start as guest without requiring sign-in");
                    Check(!RuStoreMenu.IsOverlayOpen, "Startup has no forced document modal");
                    Call(RuStoreAds.Instance, "Update");
                    Check(Get<bool>(RuStoreAds.Instance, "initialized"), "Contextual ads initialize without a removed notice gate");
                    RuStoreMenu.OpenAbout();
                    Check(Time.timeScale == 0 && AudioListener.pause, "About dialog pauses combat and sound");
                    var documents = GameObject.Find("RuStoreDialog/Panel/Scroll/Documents").GetComponent<TMP_Text>();
                    Check(documents.text.Contains("dudin1918@mail.ru") && documents.text.Contains("CC BY") && !documents.richText,
                        "About includes the offline data policy and original attribution as plain text");
                    Capture("about-ru.png");
                    Click("RuStoreDialog", "CloseAbout");
                    Check(!RuStoreMenu.IsOverlayOpen && !UnityEngine.PlayerPrefs.HasKey("LastSoldier.RuStore.PrivacyNotice.v1"),
                        "About closes without inventing a privacy acknowledgement");
                    GameProgress.BeginAuthorization();
                    Check(!GameProgress.AuthPending && GameProgress.IsReady && !YG2.player.auth, "Disabled sign-in leaves the local game ready");
                    var menu = UnityEngine.Object.FindObjectOfType<MainMenuController>();
                    Check(!Get<Button>(menu, "authButton").gameObject.activeSelf && !Get<Button>(menu, "ratingButton").gameObject.activeSelf,
                        "Sign-in and shared leaderboard buttons are absent from the first release");
                    CheckLocalNickname(menu.gameObject.scene);
                    CheckMenuControlsSave();
                    Capture("menu-local.png");
                    CheckMenuLayouts(menu.gameObject.scene, menu.SettingsPanel, menu.ShowSettings, menu.CloseSettings);
                    RuStoreMenu.OpenNickname();
                    var keyboardInput = GameObject.Find("RuStoreDialog/Panel/Nickname").GetComponent<TMP_InputField>();
                    keyboardInput.text = "Проверка клавиатуры";
                    keyboardInput.onSubmit.Invoke(keyboardInput.text);
                    Check(RuStoreMenu.IsOverlayOpen && keyboardInput.isActiveAndEnabled
                        && GameProgress.LocalNickname == keyboardInput.text,
                        "Native keyboard submit saves the name without disabling TMP inside its callback");
                    phase = 10; frame = Time.frameCount + 2; break;
                case 10:
                    Check(!RuStoreMenu.IsOverlayOpen, "Keyboard-submitted name dialog closes on the following frame");
                    UnityEngine.Object.FindObjectOfType<MainMenuController>().Play(); phase = 1; frame = Time.frameCount + 8; break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    player = UnityEngine.Object.FindObjectOfType<PlayerController>();
                    ui = UnityEngine.Object.FindObjectOfType<Ui>();
                    monetization = UnityEngine.Object.FindObjectOfType<GameMonetization>();
                    var gameplayControls = UnityEngine.Object.FindObjectOfType<MobileControls>();
                    if (SessionState.GetBool(Flag + ".gameplayOnly", false))
                    {
                        foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                        ui.StartGame(); CheckGameplayChanges(gameplayControls);
                        Finish(true, "Mob drops, HUD refresh, separate scales, merchant layouts and HUD microbenchmark passed"); break;
                    }
                    if (SessionState.GetBool(Flag + ".hudProfile", false))
                    {
                        foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                        ui.StartGame();
                        MeasureHud(gameplayControls);
                        Finish(true, "HUD allocation and CPU microbenchmark completed in disposable Editor profile"); break;
                    }
                    Check(Mathf.Approximately(Get<Slider>(gameplayControls, "sizeSlider").value, 1.1f)
                        && Mathf.Approximately(Get<Slider>(gameplayControls, "opacitySlider").value, .73f),
                        "Gameplay loads appearance saved by the main-menu controls editor");
                    var movement = Get<RectTransform[]>(gameplayControls, "editableControls")[0];
                    var area = Get<RectTransform>(gameplayControls, "safeArea");
                    Check(Mathf.Abs(movement.anchoredPosition.x / area.rect.width - layoutX) < .001f
                        && Mathf.Abs(movement.anchoredPosition.y / area.rect.height - layoutY) < .001f,
                        "Gameplay uses the exact normalized layout saved in the main menu");
                    var statusBounds = ScreenBounds(Get<RectTransform>(gameplayControls, "combatStatus"));
                    Check(Vector2.Distance(new Vector2(statusBounds.xMin / Screen.width, statusBounds.yMax / Screen.height),
                        statusPositionInMenu) <= .002f, "Gameplay displays the status where it was dragged in the main menu");
                    foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                    ui.StartGame();
                    ui.ToggleMenu();
                    Check(!Get<Button>(ui, "ratingButton").gameObject.activeSelf, "Pause menu hides the shared leaderboard entry");
                    ui.ShowRating();
                    Check(!Get<GameObject>(ui, "ratingPanel").activeSelf, "Pause menu cannot reopen the disabled shared leaderboard");
                    CheckMenuLayouts(ui.gameObject.scene, ui.SettingsPanel, ui.ShowSettings, ui.CloseSettings);
                    ui.StartGame();
                    CheckGameplayChanges(gameplayControls);
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
                    GameLocalization.SetSelection("en");
                    RuStoreMenu.OpenAbout(); Capture("about-en.png"); RuStoreMenu.Close();
                    Check(ui.SettingsPanel.GetComponentsInChildren<Button>(true).All(button => button.name != "Privacy"
                        && button.name != "Support" && button.name != "AssetCredits"), "Separate privacy, support and credits buttons are absent");
                    GameLocalization.SetSelection("ru");
                    var demoSettings = UnityEngine.Object.Instantiate(RuStoreSettings.Load());
                    demoSettings.useTestAds = true;
                    bool rejected = false;
                    try { YandexBuild.ValidateRuStoreRelease(demoSettings); } catch (InvalidOperationException) { rejected = true; }
                    UnityEngine.Object.Destroy(demoSettings);
                    Check(rejected, "Production guard rejects vendor demo IDs");
                    Check(ui.SettingsPanel.GetComponentsInChildren<Button>(true).Count(button => button.name == "About") == 1,
                        "Settings have one compact document entry point");
                    Finish(true, "Local startup without forced documents, full-screen menus, profile controls, coin persistence and queued native ad callbacks passed with vendor Editor stubs; device results are recorded separately."); break;
            }
        }
        catch (Exception error) { Finish(false, "Phase " + phase + ": " + error); }
    }
    static void MeasureHud(MobileControls controls)
    {
        const int iterations = 10000;
        var rows = new List<string> { "method\titerations\tallocatedBytes\telapsedMs" };
        long allocationProbe = GC.GetAllocatedBytesForCurrentThread();
        var probe = new byte[1024]; GC.KeepAlive(probe);
        bool allocationsAvailable = GC.GetAllocatedBytesForCurrentThread() > allocationProbe;
        var statistics = UnityEngine.Object.FindObjectOfType<Stats>();
        foreach (var entry in new[] { (owner:(object)player, name:"UpdateHudValues"), (owner:(object)player, name:"UpdateCoinText"),
            (owner:(object)statistics, name:"UpdateUI"), (owner:(object)controls, name:"RefreshLayout") })
        {
            var method = entry.owner.GetType().GetMethod(entry.name, Fields);
            var action = (Action)Delegate.CreateDelegate(typeof(Action), entry.owner, method);
            for (int i = 0; i < 32; i++) action();
            var watch = new System.Diagnostics.Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread();
            watch.Start(); for (int i = 0; i < iterations; i++) action(); watch.Stop();
            long allocated = allocationsAvailable ? GC.GetAllocatedBytesForCurrentThread() - before : -1;
            rows.Add(entry.name + "\t" + iterations + "\t" + allocated + "\t"
                + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }
        Directory.CreateDirectory("Builds/RuStoreValidation");
        File.WriteAllLines("Builds/RuStoreValidation/hud-profile.tsv", rows);
    }
    static void CheckGameplayChanges(MobileControls controls)
    {
        CheckHudRefresh(controls); CheckMobCoins(); CheckHudScaling(controls); CheckShopScaling(); CheckOtherUiScaling(); MeasureHud(controls);
    }
    static void CheckOtherUiScaling()
    {
        var deathPanel = Get<GameObject>(monetization, "deathPanel");
        var tutorial = UnityEngine.Object.FindObjectsOfType<TipsManager>().First(t => Get<bool>(t, "guidedTutorial"));
        try
        {
            foreach (float scale in new[] { .8f, 1f, 1.2f })
            foreach (string language in new[] { "ru", "en" })
            {
                RuStoreMenu.SetInterfaceScale(scale); GameLocalization.SetSelection(language);
                deathPanel.SetActive(true); GameLocalization.RefreshAll();
                Check(Mathf.Approximately(deathPanel.transform.Find("RuStoreDeathSafeContent/DeathDialog").localScale.x, scale),
                    "Death menu uses the interface scale without changing its reward handlers");
                CaptureLayout("Builds/RuStoreValidation/Previews/death-" + language + "-scale" + Mathf.RoundToInt(scale * 100) + ".png",
                    new Vector2Int(1280, 720), player.gameObject.scene, screen:deathPanel.transform);
                deathPanel.SetActive(false);
                tutorial.RestartGuide(); Call(tutorial, "TickGuided"); Canvas.ForceUpdateCanvases();
                var body = Get<TextMeshProUGUI>(tutorial, "guideBodyText"); body.ForceMeshUpdate();
                Check(Get<RectTransform>(tutorial, "guideRoot").gameObject.activeInHierarchy, "Scaled tutorial stays visible");
                Capture("tutorial-" + language + "-scale" + Mathf.RoundToInt(scale * 100) + ".png");
                foreach (TipsManager.GuideStep lesson in Enum.GetValues(typeof(TipsManager.GuideStep)))
                {
                    Set(tutorial, "step", lesson); Call(tutorial, "RenderCurrent");
                    typeof(TipsManager).GetMethod("PositionGuide", Fields).Invoke(tutorial, new object[] { null, null, false, false });
                    GameLocalization.RefreshAll(); Canvas.ForceUpdateCanvases(); body.ForceMeshUpdate();
                    if (body.isTextOverflowing) Capture("tutorial-overflow-" + lesson + "-" + language + ".png");
                    Check(!body.isTextOverflowing,
                        "Tutorial text fits after interface scaling: " + lesson + " " + language + " " + scale
                        + "; height=" + body.rectTransform.rect.height + "; preferred=" + body.preferredHeight);
                }
                tutorial.SkipGuide();
                foreach (var arena in UnityEngine.Object.FindObjectsOfType<WaveSpawner>(true))
                {
                    Call(arena, "LayoutPairedBossHud");
                    var bar = Get<GameObject>(arena, "bossHPBar10").GetComponent<RectTransform>();
                    float safeWidth = Screen.safeArea.width / Screen.width * ((RectTransform)bar.parent).rect.width;
                    Check(Mathf.Approximately(bar.localScale.x, Mathf.Min(scale, safeWidth * .5f / 260f)),
                        "Boss bars react to interface scale while remaining within their safe width");
                }
            }
        }
        finally { deathPanel.SetActive(false); tutorial.SkipGuide(); RuStoreMenu.SetInterfaceScale(1f); GameLocalization.SetSelection("ru"); }
    }
    static void CheckMobCoins()
    {
        var randomState = UnityEngine.Random.state;
        var activeField = typeof(WaveSpawner).GetField("<ActiveArena>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
        var originalArena = WaveSpawner.ActiveArena;
        var crimson = UnityEngine.Object.FindObjectsOfType<WaveSpawner>(true).First(value => value.IsCrimson);
        var rows = new List<string> { "baseValue\tcrimson\trolls\tcoins\treward\toldReward\tincomeRatio" };
        try
        {
            foreach (bool crimsonMode in new[] { false, true })
            {
                activeField.SetValue(null, crimsonMode ? crimson : null);
                Vector3 position = crimsonMode ? crimson.GetComponent<Collider2D>().bounds.center : new Vector3(2500, 2500, 0);
                var paths = new[] { "Assets/Prefabs/LvlComponent/Money.prefab" }.Concat(
                    Directory.GetFiles("Assets/Prefabs/Money", "*.prefab")).ToArray();
                foreach (string path in paths)
                {
                    const int rolls = 900;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    int value = Get<int>(prefab.GetComponent<Coin>(), "coinValue");
                    UnityEngine.Random.InitState(4108 + value);
                    var existing = new HashSet<int>(UnityEngine.Object.FindObjectsOfType<Coin>().Select(c => c.GetInstanceID()));
                    for (int i = 0; i < rolls; i++) WaveSpawner.SpawnEnemyLoot(prefab, position, Quaternion.identity);
                    var drops = UnityEngine.Object.FindObjectsOfType<Coin>().Where(c => !existing.Contains(c.GetInstanceID())).ToArray();
                    long total = drops.Sum(c => (long)Get<int>(c, "coinValue"));
                    long oldReward = (long)value * rolls * (crimsonMode ? 2 : 1);
                    double ratio = (double)total / oldReward;
                    Check(drops.Length > rolls * 1.27 && drops.Length < rolls * 1.40, "Enemy coins appear about one third more often: " + path);
                    Check(ratio > 1.90 && ratio < 2.10, "Expected mob coin income is x2, not x4: " + path + "; crimson=" + crimsonMode + "; ratio=" + ratio);
                    int baseReward = value * (crimsonMode ? 2 : 1);
                    Check(drops.All(c => Get<int>(c, "coinValue") >= baseReward * 3 / 2
                        && Get<int>(c, "coinValue") <= (baseReward * 3 + 1) / 2), "Each coin gets only one 50 percent value increase");
                    rows.Add(value + "\t" + crimsonMode + "\t" + rolls + "\t" + drops.Length + "\t" + total + "\t" + oldReward
                        + "\t" + ratio.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                    foreach (var drop in drops) UnityEngine.Object.DestroyImmediate(drop.gameObject);
                }
            }
            activeField.SetValue(null, null);
            var source = new GameObject("NonCoinEnemyLoot");
            var other = WaveSpawner.SpawnEnemyLoot(source, Vector3.zero, Quaternion.identity);
            Check(other != null && UnityEngine.Object.FindObjectsOfType<Transform>().Count(t => t.name == "NonCoinEnemyLoot(Clone)") == 1,
                "Non-coin loot is not duplicated");
            UnityEngine.Object.DestroyImmediate(other); UnityEngine.Object.DestroyImmediate(source);
            var coinObject = new GameObject("UnscaledCoin", typeof(Coin));
            var coin = coinObject.GetComponent<Coin>();
            coin.ConfigureTrainingCoin(5, null);
            Check(Get<int>(coin, "coinValue") == 5, "Training and direct coins keep their configured value");
            Set(coin, "coinValue", int.MaxValue); coin.ScaleReward(6, 2);
            Check(Get<int>(coin, "coinValue") == int.MaxValue, "Scaled coin values saturate safely at int.MaxValue");
            UnityEngine.Object.DestroyImmediate(coinObject);
            File.WriteAllLines("Builds/RuStoreValidation/mob-coins.tsv", rows);
        }
        finally { activeField.SetValue(null, originalArena); UnityEngine.Random.state = randomState; }
    }
    static void CheckHudRefresh(MobileControls controls)
    {
        float hp = player.hp, maximum = player.maxHp;
        int originalCoins = player.totalCoins;
        var statistics = UnityEngine.Object.FindObjectOfType<Stats>();
        int originalKills = statistics.countEnemyDead;
        var arrows = Get<int[]>(player.crossbowController, "arrowCounts"); int originalArrows = arrows[0];
        player.hp = 99.1f; player.maxHp = 123.1f; Call(player, "UpdateHudValues");
        Check(Get<TMP_Text>(player, "hpValueText").text == "100 / 124", "Cached health display still rounds both values up");
        player.hp = 90.1f; Call(player, "UpdateHudValues");
        Check(Get<TMP_Text>(player, "hpValueText").text == "91 / 124", "Health changes invalidate the HUD cache");
        player.totalCoins = 1234; Call(player, "UpdateCoinText");
        Check(Get<Text>(player, "coinValueText").text == 1234.ToString("N0"), "Direct balance changes invalidate the coin display cache");
        statistics.countEnemyDead++; statistics.UpdateUI();
        Check(Get<TextMeshProUGUI>(statistics, "profileKillsText").text == statistics.TotalKills.ToString("N0"),
            "Direct kill-count changes invalidate the statistics cache");
        player.hp = hp; player.maxHp = maximum; player.totalCoins = originalCoins;
        statistics.countEnemyDead = originalKills; statistics.UpdateUI();
        Call(player, "UpdateHudValues"); Call(player, "UpdateCoinText");
        arrows[0] = 1234; controls.RefreshLayout();
        Check(Get<TextMeshProUGUI[]>(controls, "directArrowCounts")[0].text == CrossbowController.FormatArrowCount(1234),
            "Arrow inventory changes invalidate touch HUD counts");
        arrows[0] = originalArrows; controls.RefreshLayout();
    }
    static void CheckHudScaling(MobileControls controls)
    {
        var hud = Get<RectTransform>(controls, "hudSafeArea");
        var status = Get<RectTransform>(controls, "combatStatus");
        var wallet = Get<RectTransform>(controls, "wallet");
        var mirror = Get<RectTransform>(controls, "mirrorProgress");
        var buttons = Get<RectTransform[]>(controls, "editableControls").Where(rect => rect != status).ToArray();
        var sizes = buttons.Select(rect => rect.sizeDelta).ToArray();
        var positions = buttons.Select(rect => rect.anchoredPosition).ToArray();
        var scales = buttons.Select(rect => rect.localScale).ToArray();
        RuStoreMenu.SetInterfaceScale(1f); controls.RefreshLayout(); Canvas.ForceUpdateCanvases();
        var walletPosition = ScreenPoint(wallet, Vector2.zero);
        var mirrorPosition = ScreenPoint(mirror, Vector2.zero);
        Rect originalStatusBounds = ScreenBounds(status);
        Rect viewport = ScreenBounds((RectTransform)status.GetComponentInParent<Canvas>().rootCanvas.transform);
        float statusWidth = ScreenBounds(status).width;
        Rect originalHudBounds = ScreenBounds(hud);
        foreach (float scale in new[] { .8f, 1f, 1.2f })
        {
            RuStoreMenu.SetInterfaceScale(scale); controls.RefreshLayout(); Canvas.ForceUpdateCanvases();
            Check(SameBounds(ScreenBounds(hud), originalHudBounds), "HUD safe area does not shrink or drift at " + scale);
            CheckStatusAnchor(originalStatusBounds.xMin, originalStatusBounds.yMax);
            Check(Vector2.Distance(ScreenPoint(wallet, Vector2.zero), walletPosition) <= .5f
                && Vector2.Distance(ScreenPoint(mirror, Vector2.zero), mirrorPosition) <= .5f,
                "Wallet and mirror HUD keep their positions at " + scale);
            Check(Mathf.Approximately(UnityEngine.PlayerPrefs.GetFloat("LastSoldier.RuStore.InterfaceScale.v1"), scale), "Interface scale persists locally");
            for (int i = 0; i < buttons.Length; i++) Check(buttons[i].sizeDelta == sizes[i] && buttons[i].anchoredPosition == positions[i]
                && buttons[i].localScale == scales[i], "Interface scale leaves touch controls independent: " + buttons[i].name);
            // Simulate both landscape cutouts without changing the Editor display.
            Vector2 min = hud.anchorMin, max = hud.anchorMax;
            foreach (bool leftCutout in new[] { true, false })
            {
                SetViewportArea(hud, new Vector2(leftCutout ? .055f : 0f, .03f), new Vector2(leftCutout ? 1f : .945f, .98f));
                Canvas.ForceUpdateCanvases();
                CheckStatusAnchor(originalStatusBounds.xMin, originalStatusBounds.yMax);
            }
            SetViewportArea(hud, min, max);
            Capture("hud-scale-" + Mathf.RoundToInt(scale * 100) + ".png");

            void CheckStatusAnchor(float x, float y)
            {
                Rect statusBounds = ScreenBounds(status);
                Check(Mathf.Abs(statusBounds.xMin - x) <= .5f && Mathf.Abs(statusBounds.yMax - y) <= .5f,
                    "Combat status keeps its saved display position at " + scale);
                Check(Mathf.Abs(statusBounds.width - statusWidth * scale) <= .5f, "Combat status resizes around its top-left corner");
            }
        }
        RuStoreMenu.SetInterfaceScale(1f); controls.RefreshLayout();
        var statusX = UnityEngine.PlayerPrefs.GetFloat(StatusLayoutKey + ".X");
        var statusY = UnityEngine.PlayerPrefs.GetFloat(StatusLayoutKey + ".Y");
        bool hasStatus = UnityEngine.PlayerPrefs.HasKey(StatusLayoutKey + ".X");
        try
        {
            UnityEngine.PlayerPrefs.DeleteKey(StatusLayoutKey + ".X");
            UnityEngine.PlayerPrefs.DeleteKey(StatusLayoutKey + ".Y");
            Call(controls, "ApplySavedLayout");
            Vector2 min = hud.anchorMin, max = hud.anchorMax;
            try
            {
                SetViewportArea(hud, new Vector2(.055f, 0), Vector2.one);
                Canvas.ForceUpdateCanvases();
                Check(Mathf.Abs(ScreenBounds(status).xMin - viewport.xMin) <= .5f
                    && Mathf.Abs(ScreenBounds(status).yMax - viewport.yMax) <= .5f
                    && ScreenBounds(hud).xMin > viewport.xMin + 1f,
                    "Default combat status touches the physical top-left even when the HUD safe area has an inset");
                LocalizationChecks.CaptureView(Path.GetFullPath("Builds/RuStoreValidation/Previews/hud-full-edge.png"),
                    Screen.width, Screen.height, () =>
                    {
                        var hits = new List<RaycastResult>();
                        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = ScreenBounds(status).center }, hits);
                        Check(!hits.Any(hit => hit.gameObject.transform.IsChildOf(status)), "Combat status passes taps through during gameplay");
                    });
            }
            finally { SetViewportArea(hud, min, max); }
        }
        finally
        {
            if (hasStatus)
            {
                UnityEngine.PlayerPrefs.SetFloat(StatusLayoutKey + ".X", statusX);
                UnityEngine.PlayerPrefs.SetFloat(StatusLayoutKey + ".Y", statusY);
            }
            Call(controls, "ApplySavedLayout");
        }
    }
    static void CheckShopScaling()
    {
        var shop = UnityEngine.Object.FindObjectOfType<Shop>();
        bool inRange = Get<bool>(shop, "isPlayerInRange");
        Set(shop, "isPlayerInRange", true); shop.OpenShop();
        Check(shop.IsOpen, "Existing merchant opens through the trade handler");
        var panel = Get<GameObject>(shop, "panelShop");
        var content = panel.transform.Find("RuStoreShopSafeContent");
        var title = content.Find("ForestShopTitle");
        var quantityDialog = Get<GameObject>(shop, "quantityPanel").transform.Find("QuantityDialog");
        try
        {
            CheckWindowPositions(null, title, content.Find("ForestWallet"), content.Find("GoodsTab"));
            shop.BuyItem(0);
            Check(shop.IsQuantityOpen, "Purchase quantity dialog opens before the scale check");
            CheckWindowPositions(null, quantityDialog);
            foreach (float scale in new[] { .8f, 1f, 1.2f })
            foreach (string language in new[] { "ru", "en" })
            foreach (var size in new[] { new Vector2Int(854, 393), new Vector2Int(1280, 720) })
            {
                RuStoreMenu.SetInterfaceScale(scale); GameLocalization.SetSelection(language);
                foreach (var tab in new[] { (name:"goods", open:(Action)shop.ShowGoods), (name:"upgrades", open:(Action)shop.ShowUpgrades),
                    (name:"bonuses", open:(Action)shop.ShowBonuses), (name:"achievements", open:(Action)shop.ShowAchievements),
                    (name:"quantity", open:(Action)(() => { shop.ShowGoods(); shop.BuyItem(0); })) })
                {
                    tab.open(); GameLocalization.RefreshAll();
                    string name = "Builds/RuStoreValidation/Previews/shop-" + tab.name + "-" + size.x + "-" + language + "-scale" + Mathf.RoundToInt(scale * 100) + ".png";
                    LocalizationChecks.CaptureView(Path.GetFullPath(name), size.x, size.y, () =>
                    {
                        Check(Mathf.Approximately(title.localScale.x, scale) && Mathf.Approximately(quantityDialog.localScale.x, scale),
                            "Merchant and purchase dialog use the saved interface scale");
                        foreach (var text in panel.GetComponentsInChildren<TMP_Text>().Where(t => t.isActiveAndEnabled))
                        { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Merchant text fits: " + text.name + " " + name); }
                    });
                }
            }
        }
        finally
        {
            shop.CloseShop(); Set(shop, "isPlayerInRange", inRange);
            RuStoreMenu.SetInterfaceScale(1f); GameLocalization.SetSelection("ru");
        }
    }
    static void Next() { phase++; frame = Time.frameCount + 8; }
    static void CheckLocalNickname(Scene scene)
    {
        Check(!GameProgress.SetLocalNickname(" \n\t "), "Blank local names are rejected");
        Check(GameProgress.NormalizeLocalNickname(new string('я', 24)).Length == GameProgress.LocalNicknameLimit,
            "Local name length is bounded");
        Check(GameProgress.NormalizeLocalNickname(new string('x', 19) + "😀").Length == 19,
            "Local name truncation does not split a surrogate pair");
        Check(GameProgress.SetLocalNickname("  Сохранить  ") && GameProgress.LocalNickname == "Сохранить", "Local name is trimmed and saved");
        var display = UnityEngine.Object.FindObjectsOfType<UserDisplay>().First(value => value.gameObject.scene == scene
            && Get<TextMeshProUGUI>(value, "profileNameText") != null);
        var label = Get<TextMeshProUGUI>(display, "profileNameText");
        GameLocalization.SetSelection("en"); GameLocalization.RefreshAll();
        Check(label.text == "Сохранить" && !label.richText && !YG2.player.auth, "Custom names are not translated, interpreted as markup or treated as authentication");
        GameLocalization.SetSelection("ru");
        Check(GameProgress.SetLocalNickname("<b>Имя</b>") && label.text == "<b>Имя</b>" && !label.richText,
            "Profile shows name markup literally");
        label.GetComponent<Button>().onClick.Invoke();
        var input = GameObject.Find("RuStoreDialog/Panel/Nickname").GetComponent<TMP_InputField>();
        input.text = "Солдат тест"; Click("RuStoreDialog", "SaveNickname");
        Check(!RuStoreMenu.IsOverlayOpen && label.text == "Солдат тест"
            && UnityEngine.PlayerPrefs.GetString("LastSoldier.RuStore.Nickname.v1") == label.text,
            "Profile name opens the editor and Save refreshes the persistent local display");
    }
    static void CheckMenuControlsSave()
    {
        RuStoreMenu.OpenMainControls();
        var preview = UnityEngine.Object.FindObjectOfType<MobileControls>();
        Check(preview != null && preview.IsControlsSettingsOpen && !preview.CanControl
            && UnityEngine.Object.FindObjectOfType<PlayerController>() == null, "Menu controls editor opens without a gameplay scene or combat input");
        preview.ResetControls();
        Get<Slider>(preview, "sizeSlider").value = 1.1f;
        Get<Slider>(preview, "opacitySlider").value = .73f;
        preview.BeginLayoutEdit();
        var movement = Get<RectTransform[]>(preview, "editableControls")[0];
        var before = movement.anchoredPosition;
        var center = RectTransformUtility.WorldToScreenPoint(null, movement.position);
        Check(preview.BeginControlDrag(movement, center), "Main-menu preview uses the existing control drag handler");
        preview.DragControl(center + new Vector2(18, 10)); preview.EndControlDrag();
        Check(movement.anchoredPosition != before, "A menu layout drag changes the preview position");
        var status = Get<RectTransform>(preview, "combatStatus");
        Canvas.ForceUpdateCanvases();
        var statusBounds = ScreenBounds(status);
        Check(status.gameObject.activeInHierarchy && Mathf.Abs(statusBounds.xMin) <= .5f
            && Mathf.Abs(statusBounds.yMax - Screen.height) <= .5f, "Main-menu preview starts with the status at the actual display corner");
        // Use the Editor's rendered view for raycasting; the batch overlay is not drawn on screen.
        LocalizationChecks.CaptureView(Path.GetFullPath("Builds/RuStoreValidation/Previews/main-status-layout.png"),
            Screen.width, Screen.height, () => DragStatus(status, new Vector2(Screen.width * .04f, -Screen.height * .28f)));
        var movedStatus = ScreenBounds(status);
        Check(movedStatus.xMin > statusBounds.xMin && movedStatus.yMax < statusBounds.yMax,
            "The visible panel responds to pointer dragging in the main menu");
        statusPositionInMenu = new Vector2(movedStatus.xMin / Screen.width, movedStatus.yMax / Screen.height);
        LocalizationChecks.CaptureView(Path.GetFullPath("Builds/RuStoreValidation/Previews/main-controls-layout.png"), 1280, 720);
        preview.EndLayoutEdit(true);
        layoutKey = "MobileControls.v2." + movement.name;
        layoutX = UnityEngine.PlayerPrefs.GetFloat(layoutKey + ".X");
        layoutY = UnityEngine.PlayerPrefs.GetFloat(layoutKey + ".Y");
        preview.BeginLayoutEdit(); before = movement.anchoredPosition;
        var savedStatus = status.anchoredPosition;
        preview.ResetLayout(); preview.EndLayoutEdit(false);
        Check(movement.anchoredPosition == before && Mathf.Approximately(UnityEngine.PlayerPrefs.GetFloat(layoutKey + ".X"), layoutX),
            "Cancelling a menu layout edit restores the saved layout without overwriting it");
        Check(status.anchoredPosition == savedStatus && Mathf.Abs(UnityEngine.PlayerPrefs.GetFloat(StatusLayoutKey + ".X") - statusPositionInMenu.x) < .001f,
            "Cancelling a reset preserves the HUD position");
        preview.CloseControlsSettings();
        Check(!RuStoreMenu.IsOverlayOpen, "Closing the preview releases its modal state");
        RuStoreMenu.OpenMainControls();
        preview = UnityEngine.Object.FindObjectOfType<MobileControls>();
        preview.BeginLayoutEdit();
        Canvas.ForceUpdateCanvases();
        Check(SameBounds(ScreenBounds(Get<RectTransform>(preview, "combatStatus")), movedStatus),
            "Reopening the main-menu editor reloads the moved status");
        preview.EndLayoutEdit(false); preview.CloseControlsSettings();
    }
    static void DragStatus(RectTransform status, Vector2 delta)
    {
        var pointer = new PointerEventData(EventSystem.current) { position = ScreenBounds(status).center, pointerId = -1 };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(status),
            "Pointer hits the panel in the layout editor at " + pointer.position + "; hits=" + string.Join(", ", hits
                .Take(5).Select(hit => hit.gameObject.name + "/" + hit.module.name + "/" + hit.sortingOrder)));
        var target = hits[0].gameObject;
        ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
        pointer.position += delta;
        ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerUpHandler);
    }
    static void CheckMenuLayouts(Scene scene, GameObject settings, Action openSettings, Action closeSettings)
    {
        Directory.CreateDirectory("Builds/RuStoreValidation/Previews");
        var menuViewport = UnityEngine.Object.FindObjectsOfType<RectTransform>()
            .First(value => value.gameObject.scene == scene && value.name == (scene.name == "Menu" ? "RuStoreBannerSafeContent" : "RuStorePauseSafeContent"));
        CheckWindowPositions(null, menuViewport.Cast<Transform>().Where(child => child.gameObject.activeSelf).ToArray());
        openSettings();
        var settingsViewport = settings.transform.Find("RuStoreSettingsSafeContent");
        var scaleControl = settingsViewport.Find("InterfaceScale/ScaleSlider").GetComponent<Slider>();
        CheckWindowPositions(scaleControl, settingsViewport.Find("ForestAudioSection"), settingsViewport.Find("ForestControlsSection"),
            settingsViewport.Find("LanguageSelector"));
        settingsViewport.Find("About").GetComponent<Button>().onClick.Invoke();
        CheckWindowPositions(null, GameObject.Find("RuStoreDialog/Panel").transform);
        Click("RuStoreDialog", "CloseAbout");
        closeSettings();
        foreach (var size in new[] { new Vector2Int(854, 393), new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2400, 1080) })
        foreach (string language in new[] { "ru", "en" })
        foreach (float scale in new[] { .8f, 1f, 1.2f })
        {
            RuStoreMenu.SetInterfaceScale(scale);
            GameLocalization.SetSelection(language);
            string prefix = "Builds/RuStoreValidation/Previews/mobile-" + scene.name + "-" + size.x + "x" + size.y + "-" + language
                + "-scale" + Mathf.RoundToInt(scale * 100);
            CaptureLayout(prefix + ".png", size, scene);
            var viewport = UnityEngine.Object.FindObjectsOfType<RectTransform>()
                .First(value => value.gameObject.scene == scene && value.name == (scene.name == "Menu" ? "RuStoreBannerSafeContent" : "RuStorePauseSafeContent"));
            var min = viewport.anchorMin; var max = viewport.anchorMax;
            // Simulate a cutout and a loaded bottom banner without making ad requests.
            SetViewportArea(viewport, new Vector2(.055f, scene.name == "Menu" ? .17f : .03f), new Vector2(.975f, .98f));
            CaptureLayout(prefix + "-insets.png", size, scene);
            viewport.anchorMin = min; viewport.anchorMax = max;
            var stats = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Stats>(true))
                .First(value => Get<Image>(value, "profileMedal") != null);
            Get<Image>(stats, "profileMedal").GetComponent<Button>().onClick.Invoke();
            var medals = GameObject.Find("RuStoreDialog/Panel/Medals").transform;
            Check(medals.childCount == Stats.MedalCount, "Medal click shows all eight medals: " + scene.name);
            GameLocalization.RefreshAll();
            Check(medals.Find("Medal_3/Name/Text").GetComponent<TMP_Text>().text == (language == "en" ? "Platinum" : "Платина"),
                "Fourth medal is displayed as Platinum in the selected language");
            for (int index = 0; index < Stats.MedalCount; index++)
            {
                Check(medals.GetChild(index).Find("Icon").GetComponent<Image>().sprite == stats.MedalForMmr(Stats.MedalMinimumMmr(index)),
                    "Catalog uses the existing medal sprite and threshold: " + index);
                Check(Stats.MedalIndexForMmr(Stats.MedalMinimumMmr(index)) == index
                    && (index == 0 || Stats.MedalIndexForMmr(Stats.MedalMinimumMmr(index) - 1) == index - 1),
                    "Medal threshold and previous value remain correct: " + index);
            }
            CaptureLayout(prefix + "-medals.png", size, scene, screen: medals.parent, backdrop: medals.parent.parent);
            Click("RuStoreDialog", "CloseMedals");
            var profileName = Get<Image>(stats, "profileMedal").transform.parent.Find("ProfileName");
            profileName.GetComponent<Button>().onClick.Invoke();
            var namePanel = GameObject.Find("RuStoreDialog/Panel").transform;
            var oldName = GameProgress.LocalNickname;
            namePanel.Find("Nickname").GetComponent<TMP_InputField>().text = "Другое имя";
            CaptureLayout(prefix + "-nickname.png", size, scene, screen: namePanel, backdrop: namePanel.parent);
            Click("RuStoreDialog", "Cancel");
            Check(GameProgress.LocalNickname == oldName, "Cancelling the name editor leaves the stored name unchanged");
            openSettings();
            CaptureLayout(prefix + "-settings.png", size, scene, screen: settings.transform, backdrop: settings.transform);
            var settingsContent = settings.transform.Find("RuStoreSettingsSafeContent").GetComponent<RectTransform>();
            var interfaceSlider = settingsContent.Find("InterfaceScale/ScaleSlider").GetComponent<Slider>();
            Check(interfaceSlider.value == Mathf.RoundToInt(scale * 100), "Settings slider reloads the current interface scale in both scenes");
            min = settingsContent.anchorMin; max = settingsContent.anchorMax;
            foreach (bool leftCutout in new[] { true, false })
            {
                SetViewportArea(settingsContent, new Vector2(leftCutout ? .055f : 0, scene.name == "Menu" ? .17f : .03f),
                    new Vector2(leftCutout ? 1 : .945f, .98f));
                CaptureLayout(prefix + (leftCutout ? "-settings-left-cutout.png" : "-settings-right-cutout.png"), size, scene,
                    screen: settings.transform, backdrop: settings.transform);
            }
            settingsContent.anchorMin = min; settingsContent.anchorMax = max;
            settingsContent.Find("About").GetComponent<Button>().onClick.Invoke();
            var about = GameObject.Find("RuStoreDialog/Panel").transform;
            CaptureLayout(prefix + "-about.png", size, scene, screen: about, backdrop: about.parent);
            Click("RuStoreDialog", "CloseAbout");
            // Use the real serialized Back handler, not a direct hide of the panel.
            settingsContent.Find("BackTyMenu").GetComponent<Button>().onClick.Invoke();
            Check(!settings.activeSelf, "Settings Back returns to menu: " + scene.name + " " + size + " " + language);
            if (scene.name == "Menu")
            {
                Click("RuStoreBannerSafeContent", "MainControlsSettings");
                var preview = UnityEngine.Object.FindObjectOfType<MobileControls>();
                Check(preview.IsControlsSettingsOpen && SceneManager.GetActiveScene() == scene, "Control customization is directly available in the main menu");
                CaptureLayout(prefix + "-controls.png", size, scene, "RuStoreControlsSafeContent", backdrop: Get<GameObject>(preview, "settingsBackdrop").transform);
                CheckLayoutTouches(preview, prefix, size);
                Get<Button>(preview, "closeControlsSettingsButton").onClick.Invoke();
                openSettings();
                settingsContent.Find("ForestControlsSection/MobileControlsCard/MobileControlsSettings").GetComponent<Button>().onClick.Invoke();
                preview = UnityEngine.Object.FindObjectOfType<MobileControls>();
                Check(preview.IsControlsSettingsOpen, "Main-menu settings open the same controls editor");
                Get<Button>(preview, "closeControlsSettingsButton").onClick.Invoke();
                Check(settings.activeSelf && !RuStoreMenu.IsOverlayOpen, "Closing menu controls returns to the original settings screen");
            }
            else if (scene.name == "SampleScene")
            {
                var mobile = UnityEngine.Object.FindObjectOfType<MobileControls>();
                Get<Button>(mobile, "pauseControlsSettingsButton").onClick.Invoke();
                Check(mobile.IsControlsSettingsOpen && Time.timeScale == 0, "Control customization opens from pause without resuming combat");
                CaptureLayout(prefix + "-controls.png", size, scene, "RuStoreControlsSafeContent", backdrop: Get<GameObject>(mobile, "settingsBackdrop").transform);
                CheckLayoutTouches(mobile, prefix, size);
                Get<Button>(mobile, "closeControlsSettingsButton").onClick.Invoke();
                Check(settings.activeSelf && !mobile.IsControlsSettingsOpen, "Control customization Back returns to settings");
            }
            closeSettings();
        }
        GameLocalization.SetSelection("ru");
        RuStoreMenu.SetInterfaceScale(1f);
    }
    static void CheckLayoutTouches(MobileControls mobile, string prefix, Vector2Int size)
    {
        var safe = Get<RectTransform>(mobile, "safeArea");
        var min = safe.anchorMin; var max = safe.anchorMax;
        try
        {
            foreach (bool leftCutout in new[] { true, false })
            {
                safe.anchorMin = new Vector2(leftCutout ? .055f : 0, .03f);
                safe.anchorMax = new Vector2(leftCutout ? 1 : .945f, .98f);
                CaptureLayout(prefix + (leftCutout ? "-controls-left-cutout.png" : "-controls-right-cutout.png"), size,
                    mobile.gameObject.scene, "RuStoreControlsSafeContent", backdrop: Get<GameObject>(mobile, "settingsBackdrop").transform);
            }
        }
        finally { safe.anchorMin = min; safe.anchorMax = max; }
        mobile.BeginLayoutEdit();
        // Verify the toolbar against the default layout, independent of an offset
        // dragged earlier at the Editor's real Game View resolution.
        mobile.ResetLayout();
        LocalizationChecks.CaptureView(Path.GetFullPath(prefix + "-layout.png"), size.x, size.y, () =>
        {
            foreach (var rect in Get<RectTransform[]>(mobile, "editableControls").Where(value => value.gameObject.activeInHierarchy))
            {
                var buttons = rect.GetComponentsInChildren<Button>().Where(value => value.isActiveAndEnabled).ToArray();
                var targets = buttons.Length > 0 ? buttons.Select(value => value.GetComponent<RectTransform>()).ToArray() : new[] { rect };
                foreach (var target in targets)
                {
                    var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
                    var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    var corners = new Vector3[4]; target.GetWorldCorners(corners);
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, (corners[0] + corners[2]) * .5f);
                    var pointer = new PointerEventData(EventSystem.current) { position = point };
                    var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                    Check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(target),
                        "Layout toolbar leaves the draggable control accessible: " + rect.name + "/" + target.name + " " + prefix
                        + "; hit=" + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
                }
            }
            foreach (var text in Get<GameObject>(mobile, "layoutToolbar").GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Layout toolbar text fits: " + text.text + " " + prefix);
            }
        });
        mobile.EndLayoutEdit(false);
    }
    static void SetViewportArea(RectTransform viewport, Vector2 min, Vector2 max)
        => typeof(RuStoreMenu).GetMethod("FitViewportBounds", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { viewport, min, max });
    static void CheckWindowPositions(Slider slider, params Transform[] windows)
    {
        float original = RuStoreMenu.InterfaceScale;
        void Apply(float scale)
        {
            if (slider != null) slider.value = Mathf.RoundToInt(scale * 100);
            else RuStoreMenu.SetInterfaceScale(scale);
            Canvas.ForceUpdateCanvases();
        }
        try
        {
            Apply(1f);
            var positions = windows.Select(window => ScreenPoint((RectTransform)window, Vector2.zero)).ToArray();
            var track = slider != null ? slider.handleRect.parent as RectTransform : null;
            Rect trackBounds = track != null ? ScreenBounds(track) : default;
            foreach (float scale in new[] { .8f, 1.2f, 1f })
            {
                Apply(scale);
                for (int i = 0; i < windows.Length; i++)
                    Check(Vector2.Distance(ScreenPoint((RectTransform)windows[i], Vector2.zero), positions[i]) <= .5f,
                        "Window anchor remains at the same screen position at " + scale + ": " + windows[i].name);
                if (track != null)
                {
                    Check(SameBounds(ScreenBounds(track), trackBounds),
                        "Scale slider track remains stationary while changing scale");
                }
            }
        }
        finally { Apply(original); }
    }
    static Vector2 ScreenPoint(RectTransform rect, Vector2 point)
    {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(point));
    }
    static Rect ScreenBounds(RectTransform rect)
    {
        Vector2 min = ScreenPoint(rect, rect.rect.min), max = ScreenPoint(rect, rect.rect.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    static bool SameBounds(Rect a, Rect b)
        => Vector2.Distance(a.min, b.min) <= .5f && Vector2.Distance(a.max, b.max) <= .5f;
    static void CaptureLayout(string filename, Vector2Int size, Scene scene, string rootName = null, Transform screen = null, Transform backdrop = null)
    {
        LocalizationChecks.CaptureView(Path.GetFullPath(filename), size.x, size.y, () =>
        {
            var root = screen ?? UnityEngine.Object.FindObjectsOfType<RectTransform>()
                .First(value => value.gameObject.scene == scene && value.name == (rootName ?? (scene.name == "Menu" ? "RuStoreBannerSafeContent" : "RuStorePauseSafeContent")));
            if (backdrop != null)
            {
                Check(backdrop.GetComponent<Image>().color.a >= .999f, "Menu fill is opaque: " + filename);
                var corners = new Vector3[4]; ((RectTransform)backdrop).GetWorldCorners(corners);
                var camera = backdrop.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
                var points = corners.Select(value => RectTransformUtility.WorldToScreenPoint(camera, value)).ToArray();
                Check(points.Min(value => value.x) <= 1 && points.Min(value => value.y) <= 1
                    && points.Max(value => value.x) >= size.x - 1 && points.Max(value => value.y) >= size.y - 1,
                    "Backdrop covers every edge independently of cutout and banner insets: " + filename);
                foreach (float x in new[] { 1f, size.x - 1f })
                {
                    var hits = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(x, size.y * .5f) }, hits);
                    Check(hits.Count > 0 && (hits[0].gameObject.transform == backdrop || hits[0].gameObject.transform.IsChildOf(backdrop)),
                        "Menu backdrop blocks touches at the screen edge: " + x + " " + filename);
                }
            }
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
                Check(rect.height >= size.y * .095f * Mathf.Min(1f, RuStoreMenu.InterfaceScale), "Touch target has usable height: " + control.name + " " + filename);
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
        File.WriteAllText("Builds/RuStoreValidation/editor-checks.txt", (success ? "PASS" : "FAIL") + "\n" + message + "\nAssertions: " + checks.Count
            + "\nUnrelated vendor Editor update warnings: " + editorUpdateWarnings + "; Editor resumes: " + editorAutoResumes + "\n" + string.Join("\n", checks));
        EditorApplication.Exit(success ? 0 : 1);
    }
}
#endif
