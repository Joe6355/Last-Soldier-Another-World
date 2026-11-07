#if LAST_SOLDIER_RUSTORE
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class RuStoreMenu : MonoBehaviour
{
    private static GameObject overlay;
    private static GameObject deferredClose;
    private static int deferredCloseFrame;
    private static MobileControls controlsPreview;
    private static float savedTimeScale;
    private static bool savedAudioPause;
    private static int consumedBackFrame = -1;
    private static RectTransform menuViewport;
    private static RectTransform pauseViewport;
    private static RectTransform controlsViewport;
    private static RectTransform mainSettingsViewport;
    private static RectTransform pauseSettingsViewport;
    private static int bannerInset;
    private const string InterfaceScaleKey = "LastSoldier.RuStore.InterfaceScale.v1";
    private static float? interfaceScale;
    private static RectTransform hudViewport;
    private static readonly List<RectTransform> panelViewports = new List<RectTransform>();
    private static readonly List<Slider> scaleSliders = new List<Slider>();
    private static (Rect, int, int, int, float)? viewportLayout;
    public static float InterfaceScale => interfaceScale ?? (interfaceScale = ReadInterfaceScale()).Value;
    public static bool IsOverlayOpen => overlay != null || controlsPreview != null;

    private static float ReadInterfaceScale()
    {
        float value = PlayerPrefs.GetFloat(InterfaceScaleKey, 1f);
        return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, .8f, 1.2f);
    }

    public static void SetInterfaceScale(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        interfaceScale = Mathf.Clamp(value, .8f, 1.2f);
        PlayerPrefs.SetFloat(InterfaceScaleKey, InterfaceScale); PlayerPrefs.Save();
        for (int i = scaleSliders.Count - 1; i >= 0; i--)
        {
            var slider = scaleSliders[i];
            if (slider == null) { scaleSliders.RemoveAt(i); continue; }
            slider.SetValueWithoutNotify(Mathf.RoundToInt(InterfaceScale * 100));
            slider.transform.parent.Find("Value/Text").GetComponent<TMP_Text>().text = Mathf.RoundToInt(InterfaceScale * 100) + "%";
        }
        RefreshViewports();
        if (hudViewport != null) FitHud(hudViewport, MobileControls.IsTouchDevice);
    }

    public static void Install(GameObject settings)
    {
        if (settings == null || settings.transform.Find("RuStoreSettingsSafeContent") != null) return;
        var host = RuStoreAds.Instance.gameObject;
        if (host.GetComponent<RuStoreMenu>() == null) host.AddComponent<RuStoreMenu>();
        ConfigureSettings(settings.transform);
        GameLocalization.Register(settings);
    }
    private void Update()
    {
        if (deferredClose != null && Time.frameCount > deferredCloseFrame)
        {
            var expected = deferredClose; deferredClose = null;
            if (overlay == expected) Close();
        }
        RefreshViewports(false);
    }
    private static void RefreshViewports(bool force = true)
    {
        var layout = (Screen.safeArea, Screen.width, Screen.height, bannerInset, InterfaceScale);
        if (!force && viewportLayout.HasValue && viewportLayout.Value == layout) return;
        viewportLayout = layout;
        FitSafeArea(menuViewport, bannerInset);
        FitSafeArea(pauseViewport, 0);
        FitSafeArea(controlsViewport, 0);
        FitSafeArea(mainSettingsViewport, bannerInset);
        FitSafeArea(pauseSettingsViewport, 0);
        if (overlay != null)
        {
            var safe = Screen.safeArea;
            FitScaledArea(overlay.transform.Find("Panel") as RectTransform,
                new Vector2(Mathf.Max(.02f, safe.xMin / Screen.width), Mathf.Max(.03f, safe.yMin / Screen.height)),
                new Vector2(Mathf.Min(.98f, safe.xMax / Screen.width), Mathf.Min(.97f, safe.yMax / Screen.height)));
        }
        for (int i = panelViewports.Count - 1; i >= 0; i--)
            if (panelViewports[i] == null) panelViewports.RemoveAt(i);
            else FitSafeArea(panelViewports[i], 0);
    }

    public static void FitHud(RectTransform viewport, bool touch)
    {
        hudViewport = viewport;
        if (touch) FitSafeArea(viewport, 0);
        else FitScaledArea(viewport, Vector2.zero, Vector2.one);
    }

    public static void ConfigurePanel(GameObject panel, string contentName)
    {
        if (panel == null || panel.transform.Find(contentName) != null) return;
        var children = new Transform[panel.transform.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = panel.transform.GetChild(i);
        var content = Node(panel.transform, contentName, 0, 0, 1, 1);
        foreach (var child in children) child.SetParent(content, false);
        panelViewports.Add(content); FitSafeArea(content, 0);
    }
    public static void ConfigureDeathMenu(GameObject panel, UnityEngine.UI.Button returnButton)
    {
        ConfigurePanel(panel, "RuStoreDeathSafeContent");
        if (returnButton != null) Place(returnButton.transform, .07f, .15f, .93f, .31f);
    }
    public static void ConfigurePurchaseDialog(GameObject panel)
    {
        var dialog = panel != null ? panel.transform.Find("QuantityDialog") : null;
        if (dialog == null) return;
        Font(dialog, 28);
        Place(dialog.Find("PurchaseIcon"), .06f, .86f, .145f, .98f);
        Place(dialog.Find("PurchaseTitle"), .18f, .86f, .94f, .98f); Font(dialog.Find("PurchaseTitle"), 40);
        Place(dialog.Find("PurchaseInfo"), .06f, .70f, .94f, .85f); Font(dialog.Find("PurchaseInfo"), 26);
        Place(dialog.Find("PurchaseValue"), .06f, .56f, .65f, .68f);
        Place(dialog.Find("QuantityInput"), .70f, .55f, .94f, .69f);
        // The selected-pack summary already labels the adjacent numeric field.
        dialog.Find("QuantityCaption").gameObject.SetActive(false);
        Place(dialog.Find("QuantityMinus"), .06f, .41f, .16f, .55f);
        Place(dialog.Find("QuantitySlider"), .20f, .41f, .80f, .55f);
        Place(dialog.Find("QuantityPlus"), .84f, .41f, .94f, .55f);
        ConfigureSliderTouchArea(dialog.Find("QuantitySlider").GetComponent<Slider>());
        Place(dialog.Find("PurchaseLimit"), .06f, .33f, .64f, .40f); Font(dialog.Find("PurchaseLimit"), 22);
        Place(dialog.Find("QuantityMaximum"), .70f, .26f, .94f, .40f); Font(dialog.Find("QuantityMaximum"), 24);
        Place(dialog.Find("PurchaseTotal"), .06f, .25f, .64f, .33f);
        Place(dialog.Find("PurchaseBalance"), .06f, .19f, .94f, .25f); Font(dialog.Find("PurchaseBalance"), 22);
        Place(dialog.Find("PurchaseConfirm"), .06f, .03f, .58f, .17f);
        Place(dialog.Find("PurchaseCancel"), .62f, .03f, .94f, .17f);
        Place(dialog.Find("TutorialQuantityControls"), .04f, .40f, .96f, .71f);
    }
    public static bool HandleBack()
    {
        if (consumedBackFrame == Time.frameCount) return true;
        if (!IsOverlayOpen || !Input.GetKeyDown(KeyCode.Escape)) return false;
        if (controlsPreview != null)
        {
            if (controlsPreview.IsEditingLayout) controlsPreview.EndLayoutEdit(false);
            else controlsPreview.CloseControlsSettings();
            consumedBackFrame = Time.frameCount; return true;
        }
        Close(); consumedBackFrame = Time.frameCount; return true;
    }
    public static void OpenMainControls()
    {
        if (!GameProgress.IsReady || IsOverlayOpen || YG.YG2.nowAdsShow) return;
        controlsPreview = MobileControls.OpenMenuSettings(() => controlsPreview = null);
    }
    public static void OpenNickname()
    {
        var panel = Open("Имя игрока"); if (panel == null) return;
        Text(Node(panel, "Hint", .07f, .65f, .93f, .82f), "Text", "Имя хранится только на этом устройстве", 32);
        var fieldRect = Node(panel, "Nickname", .10f, .41f, .90f, .60f);
        fieldRect.gameObject.AddComponent<Image>().color = new Color32(12, 32, 27, 255);
        var field = fieldRect.gameObject.AddComponent<TMP_InputField>();
        var viewport = Node(fieldRect, "Viewport", .03f, .08f, .97f, .92f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var value = Text(viewport, "Value", "", 42);
        value.alignment = TextAlignmentOptions.MidlineLeft; value.enableAutoSizing = false;
        var placeholder = Text(viewport, "Placeholder", "Введите ник", 40);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.color = new Color32(170, 179, 161, 255);
        field.textViewport = viewport; field.textComponent = value; field.placeholder = placeholder;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = GameProgress.LocalNicknameLimit;
        field.text = GameProgress.LocalNickname;
        GameLocalization.Protect(value);
        Text(Node(panel, "Limit", .10f, .29f, .90f, .38f), "Text", "До 20 символов", 28);
        Button(panel, "Cancel", "Отмена", .10f, .06f, .47f, .22f, Close);
        UnityEngine.UI.Button save = null;
        void Submit(bool fromKeyboard)
        {
            if (!GameProgress.SetLocalNickname(field.text)) return;
            if (fromKeyboard)
            {
                // TMP still reads its native keyboard after onSubmit returns.
                // Keep the field alive until the next frame finishes that callback.
                deferredClose = overlay; deferredCloseFrame = Time.frameCount;
            }
            else Close();
        }
        save = Button(panel, "SaveNickname", "Сохранить", .53f, .06f, .90f, .22f, () => Submit(false));
        save.interactable = GameProgress.NormalizeLocalNickname(field.text).Length > 0;
        field.onValueChanged.AddListener(text => save.interactable = GameProgress.NormalizeLocalNickname(text).Length > 0);
        field.onSubmit.AddListener(_ => Submit(true));
        GameLocalization.Register(panel.gameObject);
        field.ActivateInputField();
    }
    public static void BindMedal(Image medal, Stats statistics)
    {
        if (medal == null) return;
        medal.raycastTarget = true; medal.preserveAspect = true;
        var button = medal.GetComponent<UnityEngine.UI.Button>() ?? medal.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = medal; button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => OpenMedals(statistics));
    }
    public static void OpenMedals(Stats statistics)
    {
        if (statistics == null) return;
        var panel = Open("Медали"); if (panel == null) return;
        int mmr = statistics.Mmr, current = Stats.MedalIndexForMmr(mmr);
        Text(Node(panel, "Summary", .04f, .79f, .96f, .86f), "Text",
            "Ваш MMR: " + mmr.ToString("N0"), 30);
        var grid = Node(panel, "Medals", .04f, .22f, .96f, .77f);
        for (int i = 0; i < Stats.MedalCount; i++)
        {
            int column = i % 4, row = i / 4;
            var card = Node(grid, "Medal_" + i, column * .25f + .007f, .51f - row * .50f,
                (column + 1) * .25f - .007f, .99f - row * .50f);
            card.gameObject.AddComponent<Image>().color = i == current ? new Color32(70, 101, 67, 255) : new Color32(18, 40, 33, 255);
            var icon = Node(card, "Icon", .29f, .42f, .71f, .96f).gameObject.AddComponent<Image>();
            icon.sprite = statistics.MedalForMmr(Stats.MedalMinimumMmr(i)); icon.preserveAspect = true; icon.raycastTarget = false;
            Text(Node(card, "Name", .03f, .20f, .97f, .42f), "Text", Stats.MedalName(i), 32);
            Text(Node(card, "Threshold", .03f, .02f, .97f, .20f), "Text", "от " + Stats.MedalMinimumMmr(i).ToString("N0") + " MMR", 26);
        }
        Text(Node(panel, "Current", .04f, .045f, .63f, .18f), "Text", "Текущая медаль: " + Stats.MedalName(current), 28);
        Button(panel, "CloseMedals", "Закрыть", .67f, .04f, .96f, .18f, Close);
        GameLocalization.Register(panel.gameObject);
    }
    public static void OpenAbout()
    {
        var panel = Open("Об игре");
        if (panel == null) return;
        string suffix = GameLocalization.Language == "en" ? "-en" : "";
        var policy = Resources.Load<TextAsset>("RuStorePrivacy" + suffix) ?? Resources.Load<TextAsset>("RuStorePrivacy");
        var credits = Resources.Load<TextAsset>("AssetCredits");
        var scroll = Node(panel, "Scroll", .04f, .23f, .96f, .84f);
        scroll.gameObject.AddComponent<Image>().color = new Color32(20, 42, 36, 255);
        scroll.gameObject.AddComponent<RectMask2D>();
        string version = GameLocalization.Language == "en" ? "Version " : "Версия ";
        var text = Text(scroll, "Documents", version + Application.version + "\n\n"
            + (policy != null ? policy.text : "dudin1918@mail.ru") + "\n\n" + (credits != null ? credits.text : ""), 26);
        text.alignment = TextAlignmentOptions.TopLeft; text.enableAutoSizing = false;
        text.margin = new Vector4(14, 8, 14, 8);
        GameLocalization.Protect(text);
        var content = text.rectTransform;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var rect = scroll.gameObject.AddComponent<ScrollRect>();
        rect.viewport = scroll; rect.content = content; rect.horizontal = false;
        rect.movementType = ScrollRect.MovementType.Clamped; rect.scrollSensitivity = 30;
        Button(panel, "ReadOnline", "Документы", .05f, .03f, .47f, .19f,
            () => Application.OpenURL(RuStoreSettings.Load().privacyUrl));
        Button(panel, "CloseAbout", "Закрыть", .53f, .03f, .95f, .19f, Close);
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
        overlay.AddComponent<Image>().color = new Color32(8, 20, 17, 255);
        var safe = Screen.safeArea;
        var panel = Node(overlay.transform, "Panel", Mathf.Max(.02f, safe.xMin / Screen.width), Mathf.Max(.03f, safe.yMin / Screen.height),
            Mathf.Min(.98f, safe.xMax / Screen.width), Mathf.Min(.97f, safe.yMax / Screen.height));
        RefreshViewports();
        panel.gameObject.AddComponent<Image>().color = new Color32(26, 50, 43, 255);
        Text(Node(panel, "Title", .04f, .87f, .96f, .97f), "Text", title, 31);
        return panel;
    }
    public static void Close()
    {
        deferredClose = null;
        if (controlsPreview != null)
        {
            var preview = controlsPreview; controlsPreview = null;
            preview.CloseControlsSettings();
        }
        if (overlay == null) return;
        overlay.SetActive(false); Destroy(overlay); overlay = null;
        Time.timeScale = savedTimeScale; AudioListener.pause = savedAudioPause;
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
    private static UnityEngine.UI.Button Button(Transform parent, string name, string caption, float left, float bottom, float right, float top, Action action)
    {
        var rect = Node(parent, name, left, bottom, right, top); rect.gameObject.AddComponent<Image>().color = new Color32(62, 91, 69, 255);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(() => action());
        Text(rect, "Label", caption, 28);
        return button;
    }
    public static void SetBannerInset(int pixels)
    {
        int inset = SceneManager.GetActiveScene().buildIndex == 0 ? Mathf.Max(0, pixels) : 0;
        if (bannerInset == inset) return;
        bannerInset = inset;
        FitSafeArea(menuViewport, bannerInset);
        FitSafeArea(mainSettingsViewport, bannerInset);
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
        foreach (var panel in new[] { menu, title, profile })
            if (panel != null) panel.transform.SetParent(menuViewport, false);
        FitSafeArea(menuViewport, bannerInset);
        Place(menu.transform, .06f, .07f, .48f, .56f);
        Place(play.transform, 0, .70f, 1, .99f);
        Place(menu.transform.Find("ForestSettingsButton"), 0, .36f, 1, .65f);
        Button(menu.transform, "MainControlsSettings", "Настроить кнопки", 0, .02f, 1, .31f, OpenMainControls);
        Font(play.transform, 56);
        Font(menu.transform.Find("ForestSettingsButton"), 52);
        Font(menu.transform.Find("MainControlsSettings"), 42);
        var mobileCard = settings.transform.Find("RuStoreSettingsSafeContent/ForestControlsSection/MobileControlsCard");
        if (mobileCard != null)
        {
            var hint = mobileCard.Find("ControlsHint"); if (hint != null) hint.gameObject.SetActive(false);
            var description = mobileCard.Find("ControlsDescription")?.GetComponent<TMP_Text>();
            if (description != null) description.text = "Размер, прозрачность\nи расположение кнопок";
            Button(mobileCard, "MobileControlsSettings", "Настроить кнопки", .07f, .02f, .93f, .56f, OpenMainControls);
            Font(mobileCard.Find("MobileControlsSettings"), 36);
        }
        Place(title.transform, .06f, .60f, .49f, .94f);
        Font(title.transform.Find("GameTitle"), 94);
        Font(title.transform.Find("ForestWorldTitle"), 54);
        Place(profile.transform, .55f, .12f, .94f, .90f);
        ConfigureProfile(profile.transform);
        GameLocalization.Register(menu);
        GameLocalization.Register(settings);
    }

    public static void ConfigurePauseMenu(GameObject menu, GameObject settings, UnityEngine.UI.Button play)
    {
        // Leave both backdrops full-screen; only interactive content follows the safe area.
        Place(menu.transform, 0, 0, 1, 1);
        var children = new Transform[menu.transform.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = menu.transform.GetChild(i);
        pauseViewport = Node(menu.transform, "RuStorePauseSafeContent", 0, 0, 1, 1);
        foreach (var child in children)
            if (child.name != "ForestShade" && child != settings.transform) child.SetParent(pauseViewport, false);
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
        Place(profile.Find("Hero"), .40f, .79f, .60f, .97f);
        var profileName = profile.Find("ProfileName");
        Place(profileName, .06f, .60f, .94f, .76f);
        if (profileName != null)
        {
            var label = profileName.GetComponent<TextMeshProUGUI>(); label.raycastTarget = true;
            var rename = profileName.GetComponent<UnityEngine.UI.Button>() ?? profileName.gameObject.AddComponent<UnityEngine.UI.Button>();
            rename.targetGraphic = label; rename.onClick.AddListener(OpenNickname);
        }
        Place(profile.Find("ProfileSubtitle"), .06f, .52f, .94f, .60f);
        var subtitle = profile.Find("ProfileSubtitle")?.GetComponent<TMP_Text>();
        if (subtitle != null) subtitle.text = "Нажми на имя, чтобы изменить";
        Place(profile.Find("ProfileMedal"), .12f, .31f, .40f, .51f);
        Place(profile.Find("ProfileMmr"), .44f, .39f, .90f, .51f);
        Place(profile.Find("MmrCaption"), .44f, .31f, .90f, .39f);
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
        // The serialized editor lives under MobileControls.SafeArea. Its backdrop
        // must follow the full canvas; only dialog content should follow the cutout.
        var canvas = settings.transform.parent.GetComponentInParent<Canvas>().rootCanvas;
        settings.transform.SetParent(canvas.transform, false);
        Place(settings.transform, 0, 0, 1, 1);
        var backdrop = settings.transform.Find("Backdrop");
        Place(backdrop, 0, 0, 1, 1);
        var background = backdrop.GetComponent<Image>();
        var color = background.color; color.a = 1f; background.color = color;
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
        Place(dialog.Find("Hint"), .05f, .02f, .95f, .12f); Font(dialog.Find("Hint"), 32);
        dialog.Find("Hint").GetComponent<TMP_Text>().text = "Касание — автонаводка. Удержание — прицел.";
        foreach (var slider in dialog.GetComponentsInChildren<Slider>(true)) ConfigureSliderTouchArea(slider);
        Place(toolbar, .04f, .75f, .74f, .99f); Font(toolbar, 36);
        Place(toolbar.Find("Title"), .02f, .48f, .40f, .98f);
        toolbar.Find("Title").GetComponent<TMP_Text>().text = "Расположение\nкнопок";
        Place(toolbar.Find("Hint"), .02f, .02f, .98f, .33f); Font(toolbar.Find("Hint"), 30);
        Place(toolbar.Find("Save"), .42f, .38f, .60f, .98f);
        Place(toolbar.Find("Cancel"), .62f, .38f, .80f, .98f);
        Place(toolbar.Find("Reset"), .82f, .38f, .98f, .98f);
        foreach (string name in new[] { "Save", "Cancel", "Reset" }) Font(toolbar.Find(name), 40);
    }

    private static void ConfigureSettings(Transform settings)
    {
        Place(settings, 0, 0, 1, 1);
        var background = settings.GetComponent<Image>();
        if (background != null) { var color = background.color; color.a = 1f; background.color = color; }
        var children = new Transform[settings.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = settings.GetChild(i);
        var content = Node(settings, "RuStoreSettingsSafeContent", 0, 0, 1, 1);
        foreach (var child in children) child.SetParent(content, false);
        if (settings.gameObject.scene.name == "Menu") mainSettingsViewport = content;
        else pauseSettingsViewport = content;
        FitSafeArea(content, settings.gameObject.scene.name == "Menu" ? bannerInset : 0);
        settings = content;
        Place(settings.Find("BackTyMenu"), .04f, .85f, .23f, .98f);
        Place(settings.Find("ForestSettingsTitle"), .27f, .85f, .74f, .98f);
        Button(settings, "About", "Об игре", .78f, .85f, .96f, .98f, OpenAbout);
        Font(settings.Find("BackTyMenu"), 42);
        Font(settings.Find("About"), 42);
        Font(settings.Find("ForestSettingsTitle"), 58);
        var audio = settings.Find("ForestAudioSection");
        Place(audio, .04f, .27f, .48f, .81f);
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
        Place(controls, .52f, .50f, .96f, .81f);
        Place(controls.Find("Heading"), .07f, .82f, .93f, .99f);
        Font(controls, 36);
        var card = controls.Find("MobileControlsCard");
        Place(card, 0, 0, 1, .82f);
        if (card != null)
        {
            var heading = card.Find("ControlsTitle"); if (heading != null) heading.gameObject.SetActive(false);
            Place(card.Find("ControlsDescription"), .07f, .59f, .93f, .98f);
            Place(card.Find("MobileControlsSettings"), .07f, .02f, .93f, .56f);
            Place(card.Find("ControlsHint"), .07f, .02f, .93f, .56f);
        }
        var scaleRow = Node(settings, "InterfaceScale", .52f, .31f, .96f, .47f);
        scaleRow.gameObject.AddComponent<Image>().color = new Color32(20, 42, 36, 255);
        Text(Node(scaleRow, "Caption", .03f, .06f, .36f, .94f), "Text", "Масштаб\nинтерфейса", 34);
        var scaleValue = Text(Node(scaleRow, "Value", .83f, .06f, .98f, .94f), "Text", Mathf.RoundToInt(InterfaceScale * 100) + "%", 34);
        GameLocalization.Protect(scaleValue);
        var scaleSlider = Instantiate(audio.GetComponentInChildren<Slider>(true), scaleRow, false);
        scaleSlider.name = "ScaleSlider";
        scaleSlider.onValueChanged = new Slider.SliderEvent();
        Place(scaleSlider.transform, .39f, 0, .79f, 1);
        scaleSlider.minValue = 80; scaleSlider.maxValue = 120; scaleSlider.wholeNumbers = true;
        scaleSlider.SetValueWithoutNotify(Mathf.RoundToInt(InterfaceScale * 100));
        scaleSlider.onValueChanged.AddListener(percent => SetInterfaceScale(percent / 100f));
        ConfigureSliderTouchArea(scaleSlider); scaleSliders.Add(scaleSlider);
        var language = settings.Find("LanguageSelector");
        Place(language, .52f, .05f, .96f, .28f);
        Place(language.Find("LanguageTitle"), .04f, .66f, .96f, .98f);
        Place(language.Find("Choose_auto"), .04f, .06f, .33f, .60f);
        Place(language.Find("Choose_ru"), .355f, .06f, .645f, .60f);
        Place(language.Find("Choose_en"), .67f, .06f, .96f, .60f);
        Font(language, 38);
        var damage = settings.Find("DamageNumbersSetting");
        Place(damage, .04f, .05f, .48f, .23f); Font(damage, 38);
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
        FitScaledArea(viewport, new Vector2(safe.xMin / width, Mathf.Min(bottom / height, .30f)),
            new Vector2(safe.xMax / width, safe.yMax / height));
    }
    private static void FitScaledArea(RectTransform viewport, Vector2 min, Vector2 max)
    {
        if (viewport == null) return;
        // Keep backgrounds and touch controls outside this foreground transform.
        // Larger text/fixed-size HUD elements use more virtual pixels, while stretched panels still fit.
        float scale = InterfaceScale, fit = Mathf.Min(1f, 1f / scale);
        Vector2 center = (min + max) * .5f, half = (max - min) * (.5f * fit);
        min = center - half; max = center + half;
        if (viewport.anchorMin != min) viewport.anchorMin = min;
        if (viewport.anchorMax != max) viewport.anchorMax = max;
        if (viewport.offsetMin != Vector2.zero) viewport.offsetMin = Vector2.zero;
        if (viewport.offsetMax != Vector2.zero) viewport.offsetMax = Vector2.zero;
        var localScale = new Vector3(scale, scale, 1f);
        if (viewport.localScale != localScale) viewport.localScale = localScale;
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
            // At 120% the smallest fitting label remains as readable as the old 70% minimum.
            text.fontSizeMin = size * .58f;
        }
    }
}
#endif
