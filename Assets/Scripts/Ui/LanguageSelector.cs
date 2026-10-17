using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LanguageSelector : MonoBehaviour
{
    private readonly Button[] options = new Button[3];
    private GameObject creditsPanel;
    private static readonly string[] Choices = { "auto", "ru", "en" };
    private static readonly string[] Labels = { "Авто", "Русский", "English" };

    public static void Install(GameObject settings)
    {
        if (settings == null || settings.GetComponentInChildren<LanguageSelector>(true) != null) return;
        var host = new GameObject("LanguageSelector", typeof(RectTransform), typeof(Image), typeof(LanguageSelector));
        host.transform.SetParent(settings.transform, false);
        var rect = host.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.815f, .13f); rect.anchorMax = new Vector2(.975f, .78f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        host.GetComponent<Image>().color = new Color32(27, 53, 46, 245);
        var selector = host.GetComponent<LanguageSelector>();
        TMP_Text sample = settings.transform.Find("ForestAudioSection")?.GetComponentInChildren<TMP_Text>(true)
            ?? settings.GetComponentInChildren<TMP_Text>(true);
        var title = MakeText(host.transform, "LanguageTitle", "Язык", sample != null ? sample.font : TMP_Settings.defaultFontAsset);
        Stretch(title.rectTransform, .04f, .72f, .96f, .94f);
        title.fontSize = 24; title.fontStyle = FontStyles.Bold;
        for (int i = 0; i < Choices.Length; i++)
        {
            int index = i;
            var buttonObject = new GameObject("Choose_" + Choices[i], typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(host.transform, false);
            Stretch(buttonObject.GetComponent<RectTransform>(), .08f, .48f - i * .19f, .92f, .64f - i * .19f);
            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => GameLocalization.SetSelection(Choices[index]));
            selector.options[i] = button;
            var label = MakeText(buttonObject.transform, "Label", Labels[i], title.font);
            Stretch(label.rectTransform, .03f, .04f, .97f, .96f);
            label.fontSize = 22;
        }
        var credits = selector.MakeButton(host.transform, "AssetCredits", "Авторы ассетов", title.font,
            () => selector.creditsPanel.SetActive(true));
        Stretch(credits.GetComponent<RectTransform>(), .08f, -.10f, .92f, .015f);
        selector.MakeCreditsPanel(settings.transform, title.font);
        selector.Refresh();
        GameLocalization.Register(host);
    }
    private static TextMeshProUGUI MakeText(Transform parent, string name, string text, TMP_FontAsset font)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        node.transform.SetParent(parent, false);
        var label = node.GetComponent<TextMeshProUGUI>();
        label.text = text; label.font = font; label.color = new Color32(247, 240, 216, 255);
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.enableAutoSizing = true; label.fontSizeMin = 13; label.fontSizeMax = 24;
        return label;
    }
    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom); rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private Button MakeButton(Transform parent, string name, string text, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        node.transform.SetParent(parent, false);
        node.GetComponent<Image>().color = new Color32(35, 68, 59, 255);
        var button = node.GetComponent<Button>(); button.onClick.AddListener(action);
        var label = MakeText(node.transform, "Label", text, font);
        Stretch(label.rectTransform, .04f, .04f, .96f, .96f);
        return button;
    }
    private void MakeCreditsPanel(Transform settings, TMP_FontAsset font)
    {
        creditsPanel = new GameObject("AssetCreditsPanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        creditsPanel.transform.SetParent(settings, false);
        // Keep the modal above the settings sub-canvases and the editor banner placeholder.
        var modalCanvas = creditsPanel.GetComponent<Canvas>();
        modalCanvas.overrideSorting = true; modalCanvas.sortingOrder = 300;
        // The main menu reserves its right strip for a banner, including in SDK simulation.
        Stretch(creditsPanel.GetComponent<RectTransform>(), .04f, .04f, settings.gameObject.scene.name == "Menu" ? .80f : .96f, .96f);
        creditsPanel.GetComponent<Image>().color = new Color32(20, 43, 38, 255);
        var heading = MakeText(creditsPanel.transform, "Title", "Лицензии графики и звука", font);
        Stretch(heading.rectTransform, .03f, .89f, .75f, .97f);
        heading.fontStyle = FontStyles.Bold; heading.fontSize = 26;
        var close = MakeButton(creditsPanel.transform, "CloseCredits", "Закрыть", font, () => creditsPanel.SetActive(false));
        Stretch(close.GetComponent<RectTransform>(), .79f, .89f, .97f, .97f);
        var viewport = new GameObject("CreditsScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(creditsPanel.transform, false);
        Stretch(viewport.GetComponent<RectTransform>(), .03f, .035f, .97f, .86f);
        viewport.GetComponent<Image>().color = new Color32(27, 53, 46, 255);
        var body = MakeText(viewport.transform, "CreditsText", Resources.Load<TextAsset>("AssetCredits")?.text ?? "", font);
        var rect = body.rectTransform;
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.offsetMin = new Vector2(16, 0); rect.offsetMax = new Vector2(-16, 0);
        body.alignment = TextAlignmentOptions.TopLeft; body.enableAutoSizing = false; body.fontSize = 20;
        body.richText = false; body.enableWordWrapping = true;
        var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = rect;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
        GameLocalization.Protect(body);
        GameLocalization.Register(creditsPanel);
        creditsPanel.SetActive(false);
    }
    private void OnEnable() { GameLocalization.Changed += Refresh; Refresh(); }
    private void OnDisable()
    {
        GameLocalization.Changed -= Refresh;
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }
    private void Refresh()
    {
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == null) continue;
            bool selected = GameLocalization.Selection == Choices[i];
            options[i].GetComponent<Image>().color = selected ? new Color32(227, 186, 101, 255) : new Color32(35, 68, 59, 255);
            options[i].GetComponentInChildren<TMP_Text>().color = selected ? new Color32(20, 43, 38, 255) : new Color32(247, 240, 216, 255);
        }
    }
}
