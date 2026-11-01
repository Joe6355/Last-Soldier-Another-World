#if LAST_SOLDIER_RUSTORE
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class RuStoreMenu : MonoBehaviour
{
    private static GameObject overlay;
    private static float savedTimeScale;
    private static bool savedAudioPause;
    private static int consumedBackFrame = -1;
    private static RectTransform menuViewport;
    private static RectTransform pauseViewport;
    private static RectTransform controlsViewport;
    private static int bannerInset;
    private bool firstNoticeChecked;
    public static bool IsOverlayOpen => overlay != null;

    public static void Install(GameObject settings)
    {
        if (settings == null || settings.transform.Find("RuStoreLinks") != null) return;
        var root = Node(settings.transform, "RuStoreLinks", .02f, .015f, .61f, .105f);
        Button(root, "Privacy", "Конфиденциальность", .00f, .02f, .67f, .98f, () => OpenPrivacy(false));
        Button(root, "Support", "Поддержка", .70f, .02f, 1f, .98f, OpenSupport);
        var host = RuStoreAds.Instance.gameObject;
        if (host.GetComponent<RuStoreMenu>() == null) host.AddComponent<RuStoreMenu>();
        ConfigureSettings(settings.transform);
        GameLocalization.Register(root.gameObject);
    }
    private void Update()
    {
        FitSafeArea(menuViewport, bannerInset);
        FitSafeArea(pauseViewport, 0);
        FitSafeArea(controlsViewport, 0);
        if (!firstNoticeChecked && SceneManager.GetActiveScene().buildIndex == 0 && GameProgress.IsReady)
        {
            firstNoticeChecked = true;
            if (PlayerPrefs.GetInt(RuStoreAds.PrivacyNoticeKey, 0) != 1 && !IsOverlayOpen) OpenPrivacy(true);
        }
    }
    public static bool HandleBack()
    {
        if (consumedBackFrame == Time.frameCount) return true;
        if (!IsOverlayOpen || !Input.GetKeyDown(KeyCode.Escape)) return false;
        Close(); consumedBackFrame = Time.frameCount; return true;
    }
    public static void OpenPrivacy(bool firstLaunch)
    {
        var panel = Open("Конфиденциальность и реклама");
        if (panel == null) return;
        string suffix = GameLocalization.Language == "en" ? "-en" : "";
        var policy = Resources.Load<TextAsset>("RuStorePrivacy" + suffix) ?? Resources.Load<TextAsset>("RuStorePrivacy");
        var scroll = Node(panel, "Scroll", .04f, .23f, .96f, .84f);
        scroll.gameObject.AddComponent<Image>().color = new Color32(20, 42, 36, 255);
        scroll.gameObject.AddComponent<RectMask2D>();
        var content = Node(scroll, "Content", 0, 0, 1, 1);
        content.pivot = new Vector2(.5f, 1f);
        var text = Text(content, "Policy", policy != null ? policy.text : "Политика недоступна. Связь: dudin1918@mail.ru", 26);
        text.alignment = TextAlignmentOptions.TopLeft; text.enableAutoSizing = false;
        text.margin = new Vector4(14, 8, 14, 8);
        GameLocalization.Protect(text);
        Canvas.ForceUpdateCanvases();
        content.sizeDelta = new Vector2(0, Mathf.Max(scroll.rect.height, text.GetPreferredValues(text.text, scroll.rect.width, 0).y + 32));
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.anchoredPosition = Vector2.zero;
        var rect = scroll.gameObject.AddComponent<ScrollRect>();
        rect.viewport = scroll; rect.content = content; rect.horizontal = false;
        rect.movementType = ScrollRect.MovementType.Clamped; rect.scrollSensitivity = 30;
        Button(panel, "Support", "Поддержка", .05f, .03f, .35f, .19f, OpenSupport);
        Button(panel, "ReadOnline", "Полная политика", .37f, .03f, .67f, .19f,
            () => Application.OpenURL(RuStoreSettings.Load().privacyUrl));
        Button(panel, "Continue", firstLaunch ? "Продолжить" : "Закрыть", .69f, .03f, .95f, .19f, () =>
        {
            PlayerPrefs.SetInt(RuStoreAds.PrivacyNoticeKey, 1); PlayerPrefs.Save(); Close();
        });
        GameLocalization.Register(panel.gameObject);
    }
    public static void ConfirmExit() => Confirm("Выйти из игры? Прогресс будет сохранён.", () => { GameProgress.SaveNow(); Application.Quit(); });
    private static void Confirm(string message, Action action)
    {
        var panel = Open("Подтверждение"); if (panel == null) return;
        Text(Node(panel, "Question", .06f, .30f, .94f, .78f), "Text", message, 27);
        Button(panel, "Cancel", "Отмена", .06f, .07f, .47f, .22f, Close);
        Button(panel, "Confirm", "Подтвердить", .53f, .07f, .94f, .22f, () => { Close(); action(); });
        GameLocalization.Register(panel.gameObject);
    }
    private static RectTransform Open(string title)
    {
        if (YG.YG2.nowAdsShow) return null;
        Close(); GameProgress.SaveNow(); savedTimeScale = Time.timeScale; savedAudioPause = AudioListener.pause;
        Time.timeScale = 0f; AudioListener.pause = true;
        overlay = new GameObject("RuStoreDialog", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(overlay);
        var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
        var scaler = overlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        overlay.AddComponent<Image>().color = new Color32(8, 20, 17, 238);
        var safe = Screen.safeArea;
        var panel = Node(overlay.transform, "Panel", Mathf.Max(.02f, safe.xMin / Screen.width), Mathf.Max(.03f, safe.yMin / Screen.height),
            Mathf.Min(.98f, safe.xMax / Screen.width), Mathf.Min(.97f, safe.yMax / Screen.height));
        panel.gameObject.AddComponent<Image>().color = new Color32(26, 50, 43, 255);
        Text(Node(panel, "Title", .04f, .87f, .96f, .97f), "Text", title, 31);
        return panel;
    }
    public static void Close()
    {
        if (overlay == null) return;
        Destroy(overlay); overlay = null;
        Time.timeScale = savedTimeScale; AudioListener.pause = savedAudioPause;
    }
    private static void OpenSupport()
    {
        var settings = RuStoreSettings.Load();
        Application.OpenURL("mailto:" + settings.supportEmail + "?subject=" + Uri.EscapeDataString("Последний солдат RuStore " + Application.version));
    }
    private static RectTransform Node(Transform parent, string name, float left, float bottom, float right, float top)
    {
        var node = new GameObject(name, typeof(RectTransform)); node.transform.SetParent(parent, false);
        var rect = node.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(left, bottom); rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }
    private static TextMeshProUGUI Text(RectTransform parent, string name, string value, int size)
    {
        var rect = Node(parent, name, 0, 0, 1, 1); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset; text.text = value; text.richText = false; text.raycastTarget = false;
        text.fontSize = text.fontSizeMax = size; text.fontSizeMin = 13; text.enableAutoSizing = true;
        text.color = new Color32(245, 239, 219, 255); text.alignment = TextAlignmentOptions.Center; return text;
    }
    private static void Button(Transform parent, string name, string caption, float left, float bottom, float right, float top, Action action)
    {
        var rect = Node(parent, name, left, bottom, right, top); rect.gameObject.AddComponent<Image>().color = new Color32(62, 91, 69, 255);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(() => action());
        Text(rect, "Label", caption, 28);
    }
    public static void SetBannerInset(int pixels)
    {
        bannerInset = SceneManager.GetActiveScene().buildIndex == 0 ? Mathf.Max(0, pixels) : 0;
        FitSafeArea(menuViewport, bannerInset);
    }

    public static void ConfigureMainMenu(GameObject menu, GameObject title, GameObject profile, GameObject settings,
        UnityEngine.UI.Button play)
    {
        var canvas = menu.GetComponentInParent<Canvas>().rootCanvas;
        // The WebGL layout reserves a right-hand ad column. Native banners sit below the content.
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.matchWidthOrHeight = 1f;
        Place(canvas.transform.Find("ForestBackground"), 0, 0, 1, 1);
        Place(canvas.transform.Find("ForestShade"), 0, 0, 1, 1);
        menuViewport = Node(canvas.transform, "RuStoreBannerSafeContent", 0, 0, 1, 1);
        foreach (var panel in new[] { menu, title, profile, settings })
            if (panel != null) panel.transform.SetParent(menuViewport, false);
        FitSafeArea(menuViewport, bannerInset);
        Place(menu.transform, .06f, .10f, .48f, .49f);
        Place(play.transform, 0, .54f, 1, .96f);
        Place(menu.transform.Find("ForestSettingsButton"), 0, .04f, 1, .46f);
        Font(play.transform, 56);
        Font(menu.transform.Find("ForestSettingsButton"), 52);
        Place(title.transform, .06f, .56f, .49f, .93f);
        Font(title.transform.Find("GameTitle"), 94);
        Font(title.transform.Find("ForestWorldTitle"), 54);
        Place(profile.transform, .55f, .12f, .94f, .90f);
        ConfigureProfile(profile.transform);
    }

    public static void ConfigurePauseMenu(GameObject menu, UnityEngine.UI.Button play)
    {
        // Leave both backdrops full-screen; only interactive content follows the safe area.
        var children = new Transform[menu.transform.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = menu.transform.GetChild(i);
        pauseViewport = Node(menu.transform, "RuStorePauseSafeContent", 0, 0, 1, 1);
        foreach (var child in children)
            if (child.name != "ForestShade") child.SetParent(pauseViewport, false);
        Place(menu.transform.Find("ForestShade"), 0, 0, 1, 1);
        FitSafeArea(pauseViewport, 0);
        Place(play.transform, .06f, .55f, .48f, .70f);
        Place(pauseViewport.Find("Setings"), .06f, .36f, .48f, .51f);
        Place(pauseViewport.Find("PauseControlsSettings"), .06f, .17f, .48f, .32f);
        Font(play.transform, 54);
        Font(pauseViewport.Find("Setings"), 48);
        Font(pauseViewport.Find("PauseControlsSettings"), 44);
        Place(pauseViewport.Find("ForestPauseTitle"), .06f, .78f, .48f, .95f);
        Place(pauseViewport.Find("ForestPauseSubtitle"), .06f, .71f, .48f, .77f);
        Font(pauseViewport.Find("ForestPauseTitle"), 86);
        Font(pauseViewport.Find("ForestPauseSubtitle"), 34);
        var profile = pauseViewport.Find("ForestProfile");
        Place(profile, .54f, .27f, .94f, .92f);
        ConfigureProfile(profile);
        Place(pauseViewport.Find("ReplayTutorial"), .54f, .07f, .94f, .22f);
        Font(pauseViewport.Find("ReplayTutorial"), 44);
    }

    private static void ConfigureProfile(Transform profile)
    {
        if (profile == null) return;
        Place(profile.Find("Hero"), .40f, .74f, .60f, .95f);
        Place(profile.Find("ProfileName"), .06f, .63f, .94f, .73f);
        Place(profile.Find("ProfileSubtitle"), .06f, .54f, .94f, .62f);
        Place(profile.Find("ProfileMedal"), .16f, .34f, .38f, .53f);
        Place(profile.Find("ProfileMmr"), .44f, .40f, .90f, .53f);
        Place(profile.Find("MmrCaption"), .44f, .32f, .90f, .40f);
        Place(profile.Find("Waves"), .06f, .18f, .48f, .30f);
        Place(profile.Find("Kills"), .52f, .18f, .94f, .30f);
        Place(profile.Find("WavesCaption"), .04f, .10f, .49f, .18f);
        Place(profile.Find("KillsCaption"), .51f, .10f, .96f, .18f);
        Place(profile.Find("ProgressStatus"), .04f, .01f, .96f, .09f);
        foreach (string name in new[] { "ProfileName", "ProfileMmr", "Waves", "Kills" }) Font(profile.Find(name), 52);
        foreach (string name in new[] { "ProfileSubtitle", "MmrCaption", "WavesCaption", "KillsCaption", "ProgressStatus" }) Font(profile.Find(name), 34);
    }

    public static void ConfigureControlsSettings(GameObject settings)
    {
        var dialog = settings.transform.Find("SettingsDialog");
        var toolbar = settings.transform.Find("LayoutToolbar");
        controlsViewport = Node(settings.transform, "RuStoreControlsSafeContent", 0, 0, 1, 1);
        dialog.SetParent(controlsViewport, false); toolbar.SetParent(controlsViewport, false);
        FitSafeArea(controlsViewport, 0);
        Place(dialog, .03f, .03f, .97f, .97f); Font(dialog, 44);
        Place(dialog.Find("Title"), .05f, .83f, .73f, .98f); Font(dialog.Find("Title"), 56);
        Place(dialog.Find("Close"), .76f, .83f, .95f, .98f);
        Place(dialog.Find("SizeLabel"), .05f, .65f, .25f, .79f);
        Place(dialog.Find("SizeValue"), .80f, .65f, .95f, .79f);
        Place(dialog.Find("Size"), .28f, .61f, .77f, .80f);
        Place(dialog.Find("OpacityLabel"), .05f, .39f, .25f, .53f);
        Place(dialog.Find("OpacityValue"), .80f, .39f, .95f, .53f);
        Place(dialog.Find("Opacity"), .28f, .35f, .77f, .54f);
        Place(dialog.Find("EditLayout"), .05f, .14f, .62f, .31f);
        Place(dialog.Find("ResetControls"), .66f, .14f, .95f, .31f);
        Place(dialog.Find("Hint"), .05f, .02f, .95f, .12f); Font(dialog.Find("Hint"), 34);
        foreach (var slider in dialog.GetComponentsInChildren<Slider>(true)) ConfigureSliderTouchArea(slider);
        Place(toolbar, .04f, .75f, .96f, .99f); Font(toolbar, 36);
        Place(toolbar.Find("Title"), .02f, .48f, .40f, .98f);
        Place(toolbar.Find("Hint"), .02f, .02f, .98f, .33f); Font(toolbar.Find("Hint"), 30);
        Place(toolbar.Find("Save"), .42f, .38f, .60f, .98f);
        Place(toolbar.Find("Cancel"), .62f, .38f, .80f, .98f);
        Place(toolbar.Find("Reset"), .82f, .38f, .98f, .98f);
    }

    private static void ConfigureSettings(Transform settings)
    {
        Place(settings, 0, 0, 1, 1);
        Place(settings.Find("BackTyMenu"), .04f, .85f, .23f, .98f);
        Place(settings.Find("ForestSettingsTitle"), .27f, .85f, .96f, .98f);
        Font(settings.Find("BackTyMenu"), 42);
        Font(settings.Find("ForestSettingsTitle"), 58);
        var audio = settings.Find("ForestAudioSection");
        Place(audio, .04f, .34f, .48f, .82f);
        Place(audio.Find("Heading"), .07f, .84f, .93f, .99f);
        Place(audio.Find("МузыкаCaption"), .07f, .69f, .72f, .83f);
        Place(audio.Find("МузыкаValue"), .74f, .69f, .93f, .83f);
        Place(audio.Find("SoungsSlider"), .07f, .44f, .93f, .68f);
        Place(audio.Find("ЭффектыCaption"), .07f, .28f, .72f, .43f);
        Place(audio.Find("ЭффектыValue"), .74f, .28f, .93f, .43f);
        Place(audio.Find("EffectSlider"), .07f, .03f, .93f, .27f);
        Font(audio, 40);
        foreach (string name in new[] { "Kursorchic", "KursorchicOn", "KursorchicOff" })
            if (audio.Find(name) != null) audio.Find(name).gameObject.SetActive(false);
        foreach (var slider in audio.GetComponentsInChildren<Slider>(true))
            ConfigureSliderTouchArea(slider);
        var controls = settings.Find("ForestControlsSection");
        Place(controls, .52f, .50f, .96f, .82f);
        Place(controls.Find("Heading"), .07f, .82f, .93f, .99f);
        Font(controls, 36);
        var card = controls.Find("MobileControlsCard");
        Place(card, 0, 0, 1, .82f);
        if (card != null)
        {
            var heading = card.Find("ControlsTitle"); if (heading != null) heading.gameObject.SetActive(false);
            Place(card.Find("ControlsDescription"), .07f, .49f, .93f, .98f);
            Place(card.Find("MobileControlsSettings"), .07f, .02f, .93f, .47f);
            Place(card.Find("ControlsHint"), .07f, .02f, .93f, .47f);
        }
        var language = settings.Find("LanguageSelector");
        Place(language, .52f, .18f, .96f, .47f);
        Place(language.Find("LanguageTitle"), .04f, .64f, .96f, .96f);
        Place(language.Find("Choose_auto"), .04f, .09f, .33f, .59f);
        Place(language.Find("Choose_ru"), .355f, .09f, .645f, .59f);
        Place(language.Find("Choose_en"), .67f, .09f, .96f, .59f);
        Font(language, 38);
        var credits = language.Find("AssetCredits");
        if (credits != null) { credits.SetParent(settings, false); Place(credits, .04f, .02f, .31f, .15f); Font(credits, 36); }
        var creditsPanel = settings.Find("AssetCreditsPanel");
        Place(creditsPanel, .02f, .02f, .98f, .98f);
        if (creditsPanel != null)
        {
            Font(creditsPanel.Find("Title"), 44);
            Place(creditsPanel.Find("CloseCredits"), .76f, .84f, .97f, .98f);
            Font(creditsPanel.Find("CloseCredits"), 38);
            Place(creditsPanel.Find("CreditsScroll"), .03f, .03f, .97f, .80f);
            var body = creditsPanel.GetComponentInChildren<ScrollRect>(true).content.GetComponent<TMP_Text>();
            body.fontSize = 34;
        }
        var damage = settings.Find("DamageNumbersSetting");
        Place(damage, .04f, .18f, .48f, .31f); Font(damage, 38);
        var links = settings.Find("RuStoreLinks");
        Place(links, .35f, .02f, .96f, .15f); Font(links, 36);
        var replay = settings.Find("ReplayTutorial");
        // This action remains directly available in the pause menu.
        if (replay != null) replay.gameObject.SetActive(false);
    }

    private static void FitSafeArea(RectTransform viewport, int adHeight)
    {
        if (viewport == null) return;
        var safe = Screen.safeArea;
        float width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        float bottom = safe.yMin + (adHeight > 0 ? adHeight + 12f : 0);
        viewport.anchorMin = new Vector2(safe.xMin / width, Mathf.Min(bottom / height, .30f));
        viewport.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;
    }
    private static void ConfigureSliderTouchArea(Slider slider)
    {
        var touchArea = slider.GetComponent<Image>() ?? slider.gameObject.AddComponent<Image>();
        touchArea.color = Color.clear; touchArea.raycastTarget = true;
        if (slider.handleRect == null) return;
        var area = slider.handleRect.parent as RectTransform;
        area.anchorMin = new Vector2(area.anchorMin.x, 0);
        area.anchorMax = new Vector2(area.anchorMax.x, 1);
        area.offsetMin = area.offsetMax = Vector2.zero;
        slider.handleRect.sizeDelta = new Vector2(56, 0);
        slider.handleRect.anchorMin = new Vector2(slider.handleRect.anchorMin.x, .08f);
        slider.handleRect.anchorMax = new Vector2(slider.handleRect.anchorMax.x, .92f);
    }
    private static void Place(Transform target, float left, float bottom, float right, float top)
    {
        var rect = target as RectTransform; if (rect == null) return;
        rect.anchorMin = new Vector2(left, bottom); rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Font(Transform target, float size)
    {
        if (target == null) return;
        foreach (var text in target.GetComponentsInChildren<TMP_Text>(true))
        {
            text.enableAutoSizing = true; text.fontSize = text.fontSizeMax = size;
            text.fontSizeMin = size * .70f;
        }
    }
}
#endif
