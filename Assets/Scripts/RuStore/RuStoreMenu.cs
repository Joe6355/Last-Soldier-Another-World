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
    private static int previousInset = -1;
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
        GameLocalization.Register(root.gameObject);
    }
    private void Update()
    {
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
        var scroll = Node(panel, "Scroll", .04f, .20f, .96f, .84f);
        scroll.gameObject.AddComponent<Image>().color = new Color32(20, 42, 36, 255);
        scroll.gameObject.AddComponent<RectMask2D>();
        var content = Node(scroll, "Content", 0, 0, 1, 1);
        content.pivot = new Vector2(.5f, 1f);
        var text = Text(content, "Policy", policy != null ? policy.text : "Политика недоступна. Связь: dudin1918@mail.ru", 21);
        text.alignment = TextAlignmentOptions.TopLeft; text.enableAutoSizing = false;
        text.margin = new Vector4(14, 8, 14, 8);
        GameLocalization.Protect(text);
        Canvas.ForceUpdateCanvases();
        content.sizeDelta = new Vector2(0, Mathf.Max(scroll.rect.height, text.GetPreferredValues(text.text, scroll.rect.width, 0).y + 32));
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.anchoredPosition = Vector2.zero;
        var rect = scroll.gameObject.AddComponent<ScrollRect>();
        rect.viewport = scroll; rect.content = content; rect.horizontal = false;
        rect.movementType = ScrollRect.MovementType.Clamped; rect.scrollSensitivity = 30;
        Button(panel, "Support", "Поддержка", .05f, .04f, .35f, .14f, OpenSupport);
        Button(panel, "ReadOnline", "Полная политика", .37f, .04f, .67f, .14f,
            () => Application.OpenURL(RuStoreSettings.Load().privacyUrl));
        Button(panel, "Continue", firstLaunch ? "Продолжить" : "Закрыть", .69f, .04f, .95f, .14f, () =>
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
        Text(rect, "Label", caption, 23);
    }
    public static void SetBannerInset(int pixels)
    {
        if (SceneManager.GetActiveScene().buildIndex != 0) { menuViewport = null; previousInset = -1; return; }
        if (menuViewport == null)
        {
            var menu = FindObjectOfType<MainMenuController>(); if (menu == null) return;
            var canvas = menu.GetComponentInParent<Canvas>() ?? menu.GetComponentInChildren<Canvas>(true)
                ?? Array.Find(FindObjectsOfType<Canvas>(), candidate => candidate.isRootCanvas && candidate.gameObject.scene == menu.gameObject.scene);
            if (canvas == null) return;
            var children = new Transform[canvas.transform.childCount];
            for (int i = 0; i < children.Length; i++) children[i] = canvas.transform.GetChild(i);
            menuViewport = Node(canvas.transform, "RuStoreBannerSafeContent", 0, 0, 1, 1);
            foreach (var child in children) child.SetParent(menuViewport, false);
            previousInset = -1;
        }
        if (previousInset == pixels) return;
        previousInset = pixels;
        float inset = pixels > 0 ? (pixels + Screen.safeArea.yMin + 12f) / Mathf.Max(1, Screen.height) : 0;
        menuViewport.anchorMin = new Vector2(0, Mathf.Clamp(inset, 0, .30f));
    }
}
#endif
