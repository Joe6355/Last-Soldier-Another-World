using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YG;

[InitializeOnLoad]
public static class LocalizationChecks
{
    private const string Flag = "LastSoldier.LocalizationChecks";
    private static int phase, afterFrame, sample;
    private static double deadline, afterTime;
    private static readonly List<string> checks = new List<string>();
    private static Ui ui;
    private static Shop shop;
    private static AudioSource music;
    private static TipsManager tutorial;
    [Serializable] private sealed class Catalog { public Entry[] entries; }
    [Serializable] private sealed class Entry { public string ru, en; }
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static string Output => Path.GetFullPath("../localization-checks.txt");
    static LocalizationChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run()
    {
        PlayerSettings.companyName = "LastSoldierLocalizationValidation";
        PlayerSettings.productName = "LocalizationValidation";
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        SessionState.SetBool(Flag, true); Attach(); EditorApplication.EnterPlaymode();
    }
    private static void Attach()
    {
        deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    private static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, Private).GetValue(value);
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks.Add(label); Debug.Log("LOCALIZATION_CHECK: " + label);
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout phase " + phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount < 12 || Time.frameCount < afterFrame
            || EditorApplication.timeSinceStartup < afterTime) return;
        try
        {
            YG2.infoYG.Simulation.pauseOnFocusGame = false;
            switch (phase)
            {
                case 0:
                    if (YG2.isPauseGame) { YG2.PauseGame(false); return; }
                    // Repeated runs must not inherit a wave/demo from another isolated check.
                    RedefineYG.PlayerPrefs.DeleteAll();
                    RedefineYG.PlayerPrefs.SetInt("GuidedTutorial.v1.Done", 1);
                    RedefineYG.PlayerPrefs.SetInt("GuidedTutorial.v1.ArenaDone", 1);
                    CatalogChecks();
                    var menu = UnityEngine.Object.FindObjectOfType<MainMenuController>();
                    GameLocalization.SetSelection("en"); menu.ShowSettings();
                    Check(GameLocalization.Language == "en", "Explicit English overrides SDK");
                    Check(UnityEngine.PlayerPrefs.GetString("LastSoldier.Language") == "en", "Language choice is saved locally");
                    Check(menu.SettingsPanel.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Settings"), "Main settings translated");
                    Check(menu.SettingsPanel.GetComponentsInChildren<LanguageSelector>().Length == 1, "One selector in main settings");
                    CheckLanguageCard(menu.SettingsPanel);
                    Capture("localization-menu-en.png");
                    var creditsPanel = Get<GameObject>(menu.SettingsPanel.GetComponentInChildren<LanguageSelector>(), "creditsPanel");
                    Check(!creditsPanel.activeSelf, "Asset credits do not block settings on entry");
                    menu.SettingsPanel.transform.Find("LanguageSelector/AssetCredits").GetComponent<Button>().onClick.Invoke();
                    Check(creditsPanel.activeInHierarchy, "Asset credits open inside the existing settings");
                    var creditsBody = creditsPanel.transform.Find("CreditsScroll/CreditsText").GetComponent<TMP_Text>();
                    Check(!string.IsNullOrWhiteSpace(creditsBody.text) && !creditsBody.richText,
                        "Bundled attribution is readable plain text without external navigation");
                    GameLocalization.RefreshAll();
                    Check(creditsPanel.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Art and audio licenses"),
                        "Asset credits heading follows English selection");
                    Capture("localization-credits-en.png");
                    creditsPanel.transform.Find("CloseCredits").GetComponent<Button>().onClick.Invoke();
                    Check(!creditsPanel.activeSelf, "Closing attribution returns to settings");
                    menu.CloseSettings(); menu.ShowRating(); GameLocalization.SetSelection("ru"); menu.CloseRating();
                    GameLocalization.SetSelection("en"); menu.ShowRating(); GameLocalization.RefreshAll();
                    foreach (var row in UnityEngine.Object.FindObjectsOfType<LeaderboardRow>())
                        if (Get<bool>(row,"placeholder")) Check(Get<TextMeshProUGUI>(row,"nickname").text == "—  Player", "Reopened placeholder uses the selected language");
                    menu.CloseRating(); GameLocalization.RefreshAll(); Capture("localization-menu-home-en.png");
                    menu.Play(); phase++; afterFrame = Time.frameCount + 12; break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    ui = UnityEngine.Object.FindObjectOfType<Ui>(); shop = UnityEngine.Object.FindObjectOfType<Shop>();
                    if (ui == null || shop == null) return;
                    foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                    Check(GameLocalization.Language == "en", "English survives scene switch");
                    ui.ShowSettings(); GameLocalization.RefreshAll();
                    Check(ui.SettingsPanel.GetComponentsInChildren<LanguageSelector>().Length == 1, "One selector in in-game settings");
                    CheckLanguageCard(ui.SettingsPanel);
                    Capture("localization-settings-en.png");
                    ui.StartGame();
                    typeof(Shop).GetField("isPlayerInRange", Private).SetValue(shop, true);
                    music = Get<AudioSource[]>(ui, "gameMusicSources").First(source => source != null);
                    Check(music.isPlaying, "Gameplay music is playing before trade");
                    sample = music.timeSamples; shop.OpenShop();
                    Check(shop.IsOpen && Time.timeScale == 0 && !YG2.isGameplaying, "Merchant pauses combat and GameplayAPI");
                    Check(music.isPlaying && !AudioListener.pause, "Merchant leaves background music playing");
                    phase++; afterTime = EditorApplication.timeSinceStartup + .3; afterFrame = Time.frameCount + 3; break;
                case 2:
                    Check(music.isPlaying && music.timeSamples != sample, "Music advances while the shop is open");
                    GameLocalization.RefreshAll(); Capture("localization-shop-en.png");
                    Check(!Get<GameObject>(shop,"goodsContent").GetComponentsInChildren<TMP_Text>().Any(text => HasRussian(text.text)), "All supplies and item descriptions translated");
                    shop.BuyItem(0); GameLocalization.RefreshAll();
                    Check(Get<TextMeshProUGUI>(shop,"quantityTitle").text == "Normal arrows", "Quantity dialog translated");
                    Capture("localization-quantity-en.png");
                    shop.CloseQuantity(); shop.ShowAchievements(); GameLocalization.RefreshAll();
                    var titles = Get<GameObject>(shop,"achievementsContent").GetComponentsInChildren<TMP_Text>();
                    Check(!titles.Any(text => HasRussian(text.text)), "All 50 achievement cards translated");
                    Capture("localization-achievements-en.png");
                    shop.ShowUpgrades(); GameLocalization.RefreshAll();
                    Check(!Get<GameObject>(shop,"upgradesContent").GetComponentsInChildren<TMP_Text>().Any(text => HasRussian(text.text)), "Visible hero upgrades translated");
                    Capture("localization-upgrades-en.png");
                    var trainer = Get<Beka>(shop,"upgradeShop"); trainer.ShowWeaponUpgrades();
                    for (int type=0;type<4;type++)
                    {
                        trainer.SelectArrowUpgrades(type); GameLocalization.RefreshAll();
                        Check(!Get<GameObject>(shop,"upgradesContent").GetComponentsInChildren<TMP_Text>().Any(text => HasRussian(text.text)), "Weapon and arrow upgrades translated: " + type);
                    }
                    Capture("localization-weapon-en.png");
                    shop.ShowArena(); GameLocalization.RefreshAll();
                    Check(!Get<GameObject>(shop,"upgradesContent").GetComponentsInChildren<TMP_Text>().Any(text => HasRussian(text.text)), "Arena upgrades translated");
                    Capture("localization-arena-en.png");
                    shop.ShowBonuses(); GameLocalization.RefreshAll();
                    Check(!Get<GameObject>(shop,"bonusesContent").GetComponentsInChildren<TMP_Text>().Any(text => HasRussian(text.text)), "Video bonus translated");
                    YG2.PauseGame(true);
                    phase++; afterFrame = Time.frameCount + 3; break;
                case 3:
                    Check(AudioListener.pause && Time.timeScale == 0, "SDK interruption pauses music and combat during trade");
                    YG2.PauseGame(false); phase++; afterFrame = Time.frameCount + 3; break;
                case 4:
                    Check(!AudioListener.pause && Time.timeScale == 0 && shop.IsOpen, "SDK resume restores shop music while combat remains paused");
                    Check(music.isPlaying, "Music still plays after SDK resume");
                    shop.CloseShop();
                    Check(Time.timeScale == 1 && music.isPlaying, "Closing trade resumes combat without restarting music");
                    ui.ToggleMenu();
                    Check(!music.isPlaying && Time.timeScale == 0, "Ordinary pause keeps its previous music behaviour");
                    ui.StartGame();
                    var mobile = UnityEngine.Object.FindObjectOfType<MobileControls>();
                    YG2.envir.isDesktop = false; YG2.envir.isMobile = true; YG2.envir.deviceType = "mobile";
                    mobile.RefreshLayout(); ui.ShowSettings(); GameLocalization.RefreshAll();
                    Capture("localization-settings-mobile-en.png");
                    GameLocalization.SetSelection("ru");
                    Check(ui.SettingsPanel.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Настройки"), "Switching back restores Russian serialized labels");
                    Capture("localization-settings-ru.png");
                    ui.StartGame(); GameLocalization.SetSelection("en");
                    tutorial = UnityEngine.Object.FindObjectsOfType<TipsManager>().First(value => Get<bool>(value,"guidedTutorial"));
                    tutorial.RestartGuide();
                    Check(TipsManager.IsGuideActive, "Tutorial replay starts from the isolated camp save");
                    var render = typeof(TipsManager).GetMethod("RenderCurrent",Private);
                    var step = typeof(TipsManager).GetField("step",Private);
                    foreach (bool touch in new[] {false,true})
                    {
                        YG2.envir.isDesktop = !touch; YG2.envir.isMobile = touch; YG2.envir.deviceType = touch?"mobile":"desktop";
                        foreach (TipsManager.GuideStep lesson in Enum.GetValues(typeof(TipsManager.GuideStep)))
                        {
                            step.SetValue(tutorial,lesson); render.Invoke(tutorial,null); GameLocalization.RefreshAll();
                            Check(!HasRussian(Get<TextMeshProUGUI>(tutorial,"guideBodyText").text), "Tutorial translated: " + lesson + (touch?" touch":" PC"));
                        }
                    }
                    step.SetValue(tutorial,TipsManager.GuideStep.ArenaBreak);
                    typeof(TipsManager).GetField("prepared",Private).SetValue(tutorial,false);
                    render.Invoke(tutorial,null);
                    phase++; afterFrame=Time.frameCount+8; break;
                case 5:
                    Capture("localization-guide-mobile-en.png");
                    CheckGuideGeometry();
                    YG2.envir.isDesktop=true; YG2.envir.isMobile=false; YG2.envir.deviceType="desktop";
                    phase++; afterFrame=Time.frameCount+8; break;
                case 6:
                    Capture("localization-guide-en.png"); CheckGuideGeometry();
                    Finish(true, "Catalog, live language selection, scenes, achievements, tutorial and shop audio passed"); break;
            }
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    private static bool HasRussian(string text) => Regex.IsMatch(text ?? "", "[А-Яа-яЁё]") && text != "Русский";
    private static void CheckGuideGeometry()
    {
        var body=Get<TextMeshProUGUI>(tutorial,"guideBodyText");
        body.ForceMeshUpdate();
        Check(body.preferredHeight <= body.rectTransform.rect.height+4f,"English tutorial body fits its card on "+YG2.envir.deviceType);
        Check(Get<RectTransform>(tutorial,"guideRoot").gameObject.activeInHierarchy,"Live tutorial overlay remains visible");
    }
    private static void CheckLanguageCard(GameObject settings)
    {
        Canvas.ForceUpdateCanvases();
        var card = settings.transform.Find("LanguageSelector").GetComponent<RectTransform>();
        if (settings.scene.name == "Menu")
        {
            var bounds = settings.GetComponent<RectTransform>();
            var cardCorners = new Vector3[4]; card.GetWorldCorners(cardCorners);
            Check(cardCorners.All(p => bounds.InverseTransformPoint(p).x < bounds.rect.xMin + bounds.rect.width * .8f),
                "Main-menu language and credits stay outside the banner strip");
        }
        float lastBottom = card.rect.yMax;
        foreach (string name in new[] { "LanguageTitle", "Choose_auto", "Choose_ru", "Choose_en", "AssetCredits" })
        {
            var node = card.Find(name).GetComponent<RectTransform>();
            var corners = new Vector3[4]; node.GetWorldCorners(corners);
            var local = corners.Select(p => card.InverseTransformPoint(p)).ToArray();
            Check(local.All(p => p.x >= card.rect.xMin - .1f && p.x <= card.rect.xMax + .1f
                && p.y >= card.rect.yMin - .1f && p.y <= card.rect.yMax + .1f),
                "Language card contains its complete control: " + name);
            Check(local.Max(p => p.y) <= lastBottom + .1f, "Language card controls do not overlap: " + name);
            lastBottom = local.Min(p => p.y);
        }
    }
    private static void CatalogChecks()
    {
        var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Localization/English").text);
        Check(catalog.entries.Length >= 440, "Complete English catalog loaded");
        foreach (var entry in catalog.entries)
        {
            var ru = Regex.Matches(entry.ru, @"\{\d+\}").Cast<Match>().Select(value => value.Value).OrderBy(value => value);
            var en = Regex.Matches(entry.en, @"\{\d+\}").Cast<Match>().Select(value => value.Value).OrderBy(value => value);
            Check(ru.SequenceEqual(en), "Placeholders preserved: " + entry.ru.Replace("\n", " / "));
            string sample = Regex.Replace(entry.ru, @"\{(\d+)\}", value => "83" + value.Groups[1].Value);
            string translated = GameLocalization.English(sample);
            Check(!HasRussian(translated), "English template resolves: " + sample.Replace("\n", " / "));
        }
        Check(GameLocalization.Resolve("auto", "en", SystemLanguage.Russian) == "en", "Automatic follows SDK English");
        Check(GameLocalization.Resolve("auto", "ru", SystemLanguage.English) == "ru", "Automatic follows SDK Russian");
        Check(GameLocalization.Resolve("auto", "tr", SystemLanguage.Russian) == "en", "Unsupported SDK languages use English");
        Check(GameLocalization.Resolve("auto", "kk", SystemLanguage.English) == "ru", "Regional fallback follows Yandex language guidance");
        Check(GameLocalization.Resolve("ru", "en", SystemLanguage.English) == "ru", "Manual Russian overrides SDK");
        Check(GameLocalization.English("Волна 4 сохранена. Посмотрите видео, чтобы продолжить с полным здоровьем") == "Wave 4 saved. Watch a video to continue with full health", "Nested reward message translated");
        Check(GameLocalization.English("2 набора · 10 шт.") == "2 packs · 10 items", "Pack quantity and count translated");
        Check(GameLocalization.English("Восстановить алтарь\nСтой рядом 2 с для лечения\nУровень 0 / 8").StartsWith("Restore the altar"), "Composite arena upgrade translated");
        var textObject = new GameObject("LocalizationFixture", typeof(RectTransform), typeof(TextMeshProUGUI));
        var text = textObject.GetComponent<TextMeshProUGUI>(); text.text = "Итого: 20 монет"; GameLocalization.Register(text);
        GameLocalization.SetSelection("en"); Check(text.text == "Total: 20 coins", "Existing TMP value translated immediately");
        text.text = "Итого: 30 монет"; GameLocalization.RefreshAll(); Check(text.text == "Total: 30 coins", "Dynamic prices translated");
        GameLocalization.SetSelection("ru"); Check(text.text == "Итого: 30 монет", "Russian restores original dynamic value");
        GameLocalization.Protect(text); text.text = "Игрок"; GameLocalization.SetSelection("en");
        Check(text.text == "Игрок", "Player names are excluded from translation");
        UnityEngine.Object.Destroy(textObject);
    }
    private static void Capture(string filename)
    {
        GameLocalization.RefreshAll();
        var cameras = UnityEngine.Object.FindObjectsOfType<Camera>().Where(camera => camera.enabled).OrderBy(camera => camera.depth).ToArray();
        var cameraForUi = cameras.Last();
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(canvas => canvas.isRootCanvas).ToArray();
        var modes = canvases.Select(canvas => canvas.renderMode).ToArray();
        var worldCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        var distances = canvases.Select(canvas => canvas.planeDistance).ToArray();
        var targets = cameras.Select(camera => camera.targetTexture).ToArray();
        var target = new RenderTexture(1280,720,24); target.Create();
        for (int i=0;i<canvases.Length;i++) if (modes[i] == RenderMode.ScreenSpaceOverlay)
        { canvases[i].renderMode=RenderMode.ScreenSpaceCamera; canvases[i].worldCamera=cameraForUi; canvases[i].planeDistance=5f; }
        foreach (var camera in cameras) camera.targetTexture=target;
        Canvas.ForceUpdateCanvases(); foreach (var camera in cameras) camera.Render();
        var previous=RenderTexture.active; RenderTexture.active=target;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
        File.WriteAllBytes(Path.GetFullPath("../"+filename),texture.EncodeToPNG()); RenderTexture.active=previous;
        for (int i=0;i<cameras.Length;i++) cameras[i].targetTexture=targets[i];
        for (int i=0;i<canvases.Length;i++) { canvases[i].renderMode=modes[i]; canvases[i].worldCamera=worldCameras[i]; canvases[i].planeDistance=distances[i]; }
        UnityEngine.Object.Destroy(texture); target.Release(); UnityEngine.Object.Destroy(target);
    }
    private static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag,false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output, (success?"PASS":"FAIL")+"\n"+message+"\n"+string.Join("\n",checks));
        Debug.Log((success?"LOCALIZATION_PASS: ":"LOCALIZATION_FAIL: ")+message);
        EditorApplication.Exit(success?0:1);
    }
}
