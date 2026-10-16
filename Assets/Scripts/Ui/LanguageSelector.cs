using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LanguageSelector : MonoBehaviour
{
    private readonly Button[] options = new Button[3];
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
    private void OnEnable() { GameLocalization.Changed += Refresh; Refresh(); }
    private void OnDisable() => GameLocalization.Changed -= Refresh;
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
