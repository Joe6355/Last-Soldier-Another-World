using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class WaveSpawner
{
    private static WaveSpawner pickerOwner;
    private static int demoInputFrame = -1;
    private GameObject demoPicker;
    private TMP_InputField demoWaveInput;
    private TMP_Text demoDescription, demoValidation;
    private Button demoExit;
    private bool demoRun;
    private float demoResumeScale = 1;
    public static bool BlocksDemoInput => pickerOwner != null || demoInputFrame == Time.frameCount;
    public bool IsDemoPickerOpen => demoPicker != null && demoPicker.activeSelf;
    public int DemoWaveLimit => waves.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDemoPicker() { pickerOwner = null; demoInputFrame = -1; }

    private bool CanUseDemo => GameProgress.DemoToolsAvailable && GameProgress.IsReady && !YG.YG2.isPauseGame
        && !YG.YG2.nowAdsShow && !TipsManager.IsGuideActive && !tutorialWave && ActiveArena == this && IsRunning
        && arenaPlayer != null && arenaPlayer.hp > 0 && !arenaPlayer.IsAwaitingRevive;

    private void UpdateDemoControls()
    {
        if (demoRun && GameProgress.IsArenaDemo && !playerInsideZone) { GameProgress.ExitArenaDemo(); return; }
        if (!GameProgress.DemoToolsAvailable || ActiveArena != this) return;
        if (arenaPlayer == null) arenaPlayer = FindObjectOfType<PlayerController>();
        if (IsDemoPickerOpen)
        {
            if (YG.YG2.isPauseGame || YG.YG2.nowAdsShow) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { CloseDemoPicker(); return; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartDemoFromInput();
            return;
        }
        if (Time.timeScale > 0 && (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9))) OpenDemoPicker();
    }

    public bool OpenDemoPicker()
    {
        if (arenaPlayer == null) arenaPlayer = FindObjectOfType<PlayerController>();
        if (!CanUseDemo || Time.timeScale <= 0 || pickerOwner != null) return false;
        var ui = FindObjectOfType<Ui>();
        if (ui != null && (ui.IsMenuOpen || ui.IsTradeOpen || ui.IsMobileOrientationPaused)) return false;
        GameProgress.SaveNow();
        if (demoPicker == null) CreateDemoPicker();
        demoResumeScale = Time.timeScale;
        pickerOwner = this; demoInputFrame = Time.frameCount;
        demoWaveInput.SetTextWithoutNotify(CurrentWaveNumber.ToString());
        demoDescription.text = GameLocalization.Text("Выбери волну от 1 до {0}. Тест использует копию сохранения: награды, расходники и рейтинг не сохраняются.")
            .Replace("{0}", waves.Count.ToString());
        demoValidation.text = "";
        demoExit.gameObject.SetActive(GameProgress.IsArenaDemo);
        demoPicker.SetActive(true);
        FindObjectOfType<MobileControls>()?.ResetInput();
        Time.timeScale = 0; YG.YG2.GameplayStop();
        return true;
    }

    public bool TryStartDemoWave(int number)
    {
        if (arenaPlayer == null) arenaPlayer = FindObjectOfType<PlayerController>();
        if (!CanUseDemo || number < 1 || number > waves.Count || !GameProgress.BeginArenaDemo()) return false;
        demoRun = true;
        CloseDemoPicker();
        currentWaveIndex = number - 1; crimsonCycle = 0;
        StartWave(currentWaveIndex);
        demoInputFrame = Time.frameCount;
        return true;
    }

    public void CloseDemoPicker(bool resume = true)
    {
        if (!IsDemoPickerOpen) return;
        demoPicker.SetActive(false); pickerOwner = null; demoInputFrame = Time.frameCount;
        FindObjectOfType<MobileControls>()?.ResetInput();
        if (resume && !YG.YG2.isPauseGame && !YG.YG2.nowAdsShow)
        {
            Time.timeScale = demoResumeScale > 0 ? demoResumeScale : 1;
            YG.YG2.GameplayStart();
        }
    }

    private void StartDemoFromInput()
    {
        if (!int.TryParse(demoWaveInput.text, out int number) || !TryStartDemoWave(number))
            demoValidation.text = GameLocalization.Text("Введи номер от 1 до {0}").Replace("{0}", waves.Count.ToString());
    }

    private void CreateDemoPicker()
    {
        var root = new GameObject("DemoWavePicker", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var backdrop = DemoRect("Backdrop", root.transform, Vector2.zero, Vector2.zero);
        backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.sizeDelta = Vector2.zero;
        backdrop.gameObject.AddComponent<Image>().color = new Color(0, .04f, .03f, .82f);
        var card = DemoRect("Card", root.transform, new Vector2(780, 620), Vector2.zero);
        card.gameObject.AddComponent<Image>().color = new Color32(26, 55, 45, 255);
        DemoLabel("Title", card, new Vector2(720, 54), new Vector2(0, 260), "Тест волн", 38);
        demoDescription = DemoLabel("Description", card, new Vector2(704, 110), new Vector2(0, 169), "", 27);
        demoDescription.enableWordWrapping = true;
        var inputRect = DemoRect("WaveNumber", card, new Vector2(220, 70), new Vector2(0, 63));
        inputRect.gameObject.AddComponent<Image>().color = new Color32(16, 36, 29, 255);
        demoWaveInput = inputRect.gameObject.AddComponent<TMP_InputField>();
        var viewport = DemoRect("Viewport", inputRect, new Vector2(200, 60), Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        var inputText = DemoLabel("Text", viewport, new Vector2(200, 60), Vector2.zero, "", 36);
        demoWaveInput.textViewport = viewport; demoWaveInput.textComponent = inputText;
        demoWaveInput.contentType = TMP_InputField.ContentType.IntegerNumber; demoWaveInput.characterLimit = 4;
        DemoButton("Minus", card, new Vector2(160, 70), new Vector2(-218, 63), "−", () => ChangeDemoNumber(-1));
        DemoButton("Plus", card, new Vector2(160, 70), new Vector2(218, 63), "+", () => ChangeDemoNumber(1));
        demoValidation = DemoLabel("Validation", card, new Vector2(700, 35), new Vector2(0, 8), "", 24);
        DemoButton("Shaula", card, new Vector2(700, 65), new Vector2(0, -49), "20 — Шаула", () => TryStartDemoWave(20));
        DemoButton("Start", card, new Vector2(340, 70), new Vector2(-180, -135), "Начать тест", StartDemoFromInput);
        DemoButton("Close", card, new Vector2(340, 70), new Vector2(180, -135), "Закрыть", () => CloseDemoPicker());
        demoExit = DemoButton("Exit", card, new Vector2(700, 64), new Vector2(0, -226), "Вернуться в обычную игру", () => GameProgress.ExitArenaDemo());
        demoPicker = root; root.SetActive(false);
    }

    private void ChangeDemoNumber(int delta)
    {
        int.TryParse(demoWaveInput.text, out int number);
        demoWaveInput.SetTextWithoutNotify(Mathf.Clamp(number + delta, 1, waves.Count).ToString());
    }
    private RectTransform DemoRect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }
    private TMP_Text DemoLabel(string name, Transform parent, Vector2 size, Vector2 position, string text, int fontSize)
    {
        var label = DemoRect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = shaulaTitle != null ? shaulaTitle.font : TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center;
        label.color = new Color32(241, 233, 206, 255); label.raycastTarget = false;
        label.text = text; GameLocalization.Register(label); return label;
    }
    private Button DemoButton(string name, Transform parent, Vector2 size, Vector2 position, string text, UnityEngine.Events.UnityAction action)
    {
        var rect = DemoRect(name, parent, size, position);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(220, 182, 99, 255);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        var label = DemoLabel("Label", rect, size - new Vector2(20, 8), Vector2.zero, text, 29);
        label.color = new Color32(37, 43, 30, 255); return button;
    }
}
