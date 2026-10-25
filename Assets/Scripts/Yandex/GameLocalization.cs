using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YG;

// One localization boundary for the existing serialized and dynamically updated UI.
// Original text stays in the game systems; only its presentation is translated.
[DefaultExecutionOrder(32000)]
public sealed class GameLocalization : MonoBehaviour
{
    private const string PreferenceKey = "LastSoldier.Language";
    private static GameLocalization instance;
    private static readonly Dictionary<string, string> translations = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
    private static readonly List<Template> templates = new List<Template>();
    private static readonly HashSet<int> protectedTexts = new HashSet<int>();
    private static readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();
    private static readonly List<int> deadBindings = new List<int>();
    private static string selection = "auto", language = "ru";
    private float nextCleanup;
    public static string Selection => selection;
    public static string Language => language;
    public static event Action Changed;

    [Serializable] private sealed class Catalog { public Entry[] entries; }
    [Serializable] private sealed class Entry { public string ru, en; }
    private sealed class Template
    {
        public Regex Pattern;
        public string English;
        public int Specificity;
    }
    private sealed class Binding
    {
        public TMP_Text Tmp;
        public Text Legacy;
        public string Source, Rendered;
        public bool Alive => Tmp != null || Legacy != null;
        public string Value { get => Tmp != null ? Tmp.text : Legacy.text; set { if (Tmp != null) Tmp.text = value; else Legacy.text = value; } }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string GameLocalePreference_js();
    [DllImport("__Internal")] private static extern void GameLocaleSelection_js(string choice, string resolved);
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        translations.Clear(); cache.Clear(); templates.Clear(); protectedTexts.Clear(); bindings.Clear(); deadBindings.Clear();
        selection = "auto"; language = "ru"; Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        var host = new GameObject("GameLocalization");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<GameLocalization>();
    }

    private void Awake()
    {
        instance = this;
        LoadCatalog();
        selection = NormalizeSelection(UnityEngine.PlayerPrefs.GetString(PreferenceKey, "auto"));
#if UNITY_WEBGL && !UNITY_EDITOR
        string browserChoice = GameLocalePreference_js();
        if (!string.IsNullOrEmpty(browserChoice)) selection = NormalizeSelection(browserChoice);
#endif
        ResolveLanguage();
        YG2.onGetSDKData += OnSdkData;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        Canvas.preWillRenderCanvases += RefreshAll;
    }

    private void OnDestroy()
    {
        YG2.onGetSDKData -= OnSdkData;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Canvas.preWillRenderCanvases -= RefreshAll;
        if (instance == this) instance = null;
    }
    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextCleanup) { nextCleanup = Time.unscaledTime + 2f; RemoveDeadBindings(); }
        RefreshAll();
    }
    private void OnSdkData() { if (selection == "auto") ResolveLanguage(); }
    private void OnSceneUnloaded(Scene scene) => RemoveDeadBindings();
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RemoveDeadBindings();
        foreach (var root in scene.GetRootGameObjects()) Register(root);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var menu in root.GetComponentsInChildren<MainMenuController>(true))
            {
                LanguageSelector.Install(menu.SettingsPanel);
#if LAST_SOLDIER_RUSTORE
                RuStoreMenu.Install(menu.SettingsPanel);
#endif
            }
            foreach (var ui in root.GetComponentsInChildren<Ui>(true))
            {
                LanguageSelector.Install(ui.SettingsPanel);
#if LAST_SOLDIER_RUSTORE
                RuStoreMenu.Install(ui.SettingsPanel);
#endif
            }
        }
        RefreshAll();
    }

    public static string NormalizeSelection(string value) => value == "ru" || value == "en" ? value : "auto";
    public static string Resolve(string choice, string sdkLanguage, SystemLanguage systemLanguage)
    {
        if (choice == "ru" || choice == "en") return choice;
        if (!string.IsNullOrEmpty(sdkLanguage))
        {
            string code = sdkLanguage.ToLowerInvariant().Split('-', '_')[0];
            return code == "ru" || code == "be" || code == "kk" || code == "uk" || code == "uz" ? "ru" : "en";
        }
        return systemLanguage == SystemLanguage.Russian ? "ru" : "en";
    }
    public static void SetSelection(string choice)
    {
        selection = NormalizeSelection(choice);
        UnityEngine.PlayerPrefs.SetString(PreferenceKey, selection);
        UnityEngine.PlayerPrefs.Save();
        ResolveLanguage();
    }
    private static void ResolveLanguage()
    {
        language = Resolve(selection, YG2.isSDKEnabled ? YG2.envir.language : null, Application.systemLanguage);
        cache.Clear();
#if UNITY_WEBGL && !UNITY_EDITOR
        GameLocaleSelection_js(selection, language);
#endif
        Changed?.Invoke();
        RefreshAll();
    }

    public static void Protect(Component text)
    {
        if (text == null) return;
        int id = text.GetInstanceID();
        protectedTexts.Add(id); bindings.Remove(id);
    }
    public static void Register(GameObject root)
    {
        if (root == null) return;
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) Register(text);
        foreach (var text in root.GetComponentsInChildren<Text>(true)) Register(text);
    }
    public static void Register(Component text)
    {
        if (text == null || protectedTexts.Contains(text.GetInstanceID()) || bindings.ContainsKey(text.GetInstanceID())) return;
        var tmp = text as TMP_Text;
        var legacy = text as Text;
        if (tmp == null && legacy == null) return;
        var input = text.GetComponentInParent<TMP_InputField>(true);
        if (input != null && input.textComponent == tmp) return;
        var legacyInput = text.GetComponentInParent<InputField>(true);
        if (legacyInput != null && legacyInput.textComponent == legacy) return;
        bindings.Add(text.GetInstanceID(), new Binding { Tmp = tmp, Legacy = legacy, Source = tmp != null ? tmp.text : legacy.text });
    }
    private static void RemoveDeadBindings()
    {
        deadBindings.Clear();
        foreach (var pair in bindings) if (!pair.Value.Alive) deadBindings.Add(pair.Key);
        foreach (int id in deadBindings) { bindings.Remove(id); protectedTexts.Remove(id); }
    }
    public static void RefreshAll()
    {
        foreach (var pair in bindings)
        {
            var binding = pair.Value;
            if (!binding.Alive) continue;
            string current = binding.Value;
            if (current != binding.Rendered) binding.Source = current;
            string translated = Text(binding.Source);
            if (current != translated) binding.Value = translated;
            binding.Rendered = translated;
        }
    }

    public static string Text(string source) => language == "en" ? TranslateEnglish(source, 0) : source;
    // Also used by validation without changing the player's language preference.
    public static string English(string source) => TranslateEnglish(source, 0);
    private static bool HasRussian(string value)
    {
        foreach (char character in value) if (character >= '\u0400' && character <= '\u04ff') return true;
        return false;
    }
    private static string TranslateEnglish(string source, int depth)
    {
        if (string.IsNullOrEmpty(source) || depth > 6) return source;
        if (translations.TryGetValue(source, out string exact)) return exact;
        if (!HasRussian(source)) return source;
        if (cache.TryGetValue(source, out string known)) return known;
        string result = source;
        foreach (var template in templates)
        {
            Match match = template.Pattern.Match(source);
            if (!match.Success) continue;
            result = Regex.Replace(template.English, @"\{(\d+)\}", placeholder =>
                TranslateEnglish(match.Groups["p" + placeholder.Groups[1].Value].Value, depth + 1));
            break;
        }
        if (result == source && source.IndexOf('\n') >= 0)
        {
            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = TranslateEnglish(lines[i], depth + 1);
            result = string.Join("\n", lines);
        }
        if (depth == 0)
        {
            if (cache.Count >= 2048) cache.Clear();
            cache[source] = result;
        }
        return result;
    }
    private static void LoadCatalog()
    {
        translations.Clear(); templates.Clear(); cache.Clear();
        var asset = Resources.Load<TextAsset>("Localization/English");
        if (asset == null) { Debug.LogError("Missing English localization catalog"); return; }
        var catalog = JsonUtility.FromJson<Catalog>(asset.text);
        foreach (var entry in catalog.entries)
        {
            if (string.IsNullOrEmpty(entry.ru) || entry.en == null) continue;
            var placeholders = Regex.Matches(entry.ru, @"\{(\d+)\}");
            if (placeholders.Count == 0) { translations[entry.ru] = entry.en; continue; }
            var pattern = new StringBuilder("\\A");
            var seen = new HashSet<string>();
            int cursor = 0, literalLength = 0;
            foreach (Match placeholder in placeholders)
            {
                string literal = entry.ru.Substring(cursor, placeholder.Index - cursor);
                pattern.Append(Regex.Escape(literal)); literalLength += literal.Length;
                string group = "p" + placeholder.Groups[1].Value;
                pattern.Append(seen.Add(group) ? "(?<" + group + ">.*?)" : "\\k<" + group + ">");
                cursor = placeholder.Index + placeholder.Length;
            }
            pattern.Append(Regex.Escape(entry.ru.Substring(cursor))).Append("\\z");
            literalLength += entry.ru.Length - cursor;
            templates.Add(new Template { Pattern = new Regex(pattern.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant),
                English = entry.en, Specificity = literalLength });
        }
        templates.Sort((left, right) => right.Specificity.CompareTo(left.Specificity));
    }
}
