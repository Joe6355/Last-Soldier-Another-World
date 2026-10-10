using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YG;

[DefaultExecutionOrder(-600)]
public class MobileControls : MonoBehaviour
{
    [Header("Существующие игровые системы")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Ui gameUi;
    [SerializeField] private Shop shop;
    [SerializeField] private Beka trainer;
    [SerializeField] private Chest[] chests = System.Array.Empty<Chest>();
    [Header("Сенсорное управление")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform safeArea;
    [SerializeField] private RectTransform hudSafeArea, wallet, mirrorProgress;
    [SerializeField] private RectTransform combatStatus;
    [SerializeField] private GameObject combatEquipment;
    [SerializeField] private GameObject gameplayControls;
    [SerializeField] private GameObject rotateHint;
    [SerializeField] private TouchControl movement, aim, shield, mirror, view;
    [SerializeField] private Button dashButton, healButton, modeButton, interactButton, pauseButton, utilityButton;
    [SerializeField] private GameObject utilitiesPanel;
    [SerializeField] private Button[] directArrowButtons = System.Array.Empty<Button>();
    [SerializeField] private TextMeshProUGUI[] directArrowCounts = System.Array.Empty<TextMeshProUGUI>();
    [SerializeField] private Image[] directArrowSelection = System.Array.Empty<Image>();
    [SerializeField] private Image shieldIndicator;
    [SerializeField] private TextMeshProUGUI modeLabel, interactLabel, healCountLabel, mirrorCountLabel;
    [SerializeField] private GameObject[] desktopHints = System.Array.Empty<GameObject>();
    [Header("Настройка управления на этом устройстве")]
    [SerializeField] private Button controlsSettingsButton, closeControlsSettingsButton, editLayoutButton, resetControlsButton;
    [SerializeField] private Button pauseControlsSettingsButton;
    [SerializeField] private GameObject desktopControlBindings, mobileControlsCard;
    [SerializeField] private Button finishLayoutButton, cancelLayoutButton, resetLayoutButton;
    [SerializeField] private GameObject gameMenuPanel, gameSettingsPanel, controlsSettingsPanel, controlsSettingsDialog, settingsBackdrop, layoutToolbar;
    [SerializeField] private Slider sizeSlider, opacitySlider;
    [SerializeField] private TextMeshProUGUI sizeValueText, opacityValueText, layoutHint;
    [SerializeField] private RectTransform[] editableControls = System.Array.Empty<RectTransform>();
    [SerializeField] private Vector2[] defaultPositions = System.Array.Empty<Vector2>();
    [Header("Автоприцел")]
    [SerializeField, Min(1f)] private float autoAimRange = 9f;
    private Collider2D[] aimCandidates = new Collider2D[128];
    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[32];
    private readonly List<TouchControl> layoutTouches = new List<TouchControl>();
    private Camera gameCamera;
    private Transform autoAimTarget;
    private Vector2 autoAimPoint;
    private float nextAimSearch, controlSize = 1f, controlOpacity = .82f;
    private bool shieldLatched, editingLayout, initialized;
    private Vector3 statusScale;
    private Vector2 statusPosition;
    private Vector2 layoutDimensions, dragStartPoint, dragStartPosition;
    private RectTransform draggedControl;
    private Vector2[] editSnapshot;
    private const string PreferencePrefix = "MobileControls.v2.";
    private static readonly Color PanelColor = new Color32(35, 68, 59, 235);
    private static readonly Color GoldColor = new Color32(227, 186, 101, 255);
    private bool focused = true, applicationPaused;
    private bool touchWasEnabled;
    private Vector2 walletMin, walletMax, walletPosition, progressPosition;

    private void Awake()
    {
        gameCamera = Camera.main;
        if (combatStatus != null) { statusScale = combatStatus.localScale; statusPosition = combatStatus.anchoredPosition; }
        foreach (var rect in editableControls)
            if (rect != null) layoutTouches.AddRange(rect.GetComponentsInChildren<TouchControl>(true));
        if (wallet == null || mirrorProgress == null) return;
        walletMin = wallet.anchorMin; walletMax = wallet.anchorMax; walletPosition = wallet.anchoredPosition;
        progressPosition = mirrorProgress.anchoredPosition;
    }

    public static bool IsTouchDevice => Application.isMobilePlatform || YG2.envir.isMobile || YG2.envir.isTablet;
    public bool UseTouch => IsTouchDevice;
    public bool CanControl => UseTouch && GameProgress.IsReady && focused && !applicationPaused
        && !YG2.isPauseGame && !YG2.nowAdsShow && Time.timeScale > 0f
        && player != null && player.crossbowController != null && player.hp > 0f && !player.IsAwaitingRevive && !gameUi.IsMobileOrientationPaused
        && !gameUi.IsMenuOpen && !gameUi.IsTradeOpen && !editingLayout;
    public Vector2 Movement => CanControl ? movement.Value : Vector2.zero;
    public bool IsEditingLayout => editingLayout;
    public bool IsControlsSettingsOpen => controlsSettingsPanel != null && controlsSettingsPanel.activeInHierarchy;
    public bool AutomaticSelected => player != null && player.crossbowController != null
        && player.crossbowController.ShootingMode == 3 && player.crossbowController.IsModeUnlocked(3);
    public Vector2 AimDirection
    {
        get
        {
            if (aim.HasQueuedPress && aim.QueuedManualAim) return aim.QueuedDirection;
            if (aim.IsHeld && aim.IsManualAim) return aim.LastDirection;
            if (aim.IsHeld || aim.HasQueuedPress) return AutoAimDirection();
            return Movement.sqrMagnitude > .001f ? Movement.normalized : aim.LastDirection;
        }
    }
    public bool FireHeld => CanControl && aim.IsRepeating;
    public bool ShieldHeld => CanControl && shieldLatched;
    public bool MirrorHeld => CanControl && mirror.IsHeld;
    public bool ViewHeld => CanControl && view.IsHeld;
    public bool ConsumeFirePress() => aim.ConsumePress() && CanControl;

    private void Start()
    {
        dashButton.onClick.AddListener(Dash);
        healButton.onClick.AddListener(Heal);
        modeButton.onClick.AddListener(SwitchMode);
        interactButton.onClick.AddListener(Interact);
        pauseButton.onClick.AddListener(OpenPause);
        utilityButton.onClick.AddListener(ToggleUtilities);
        for (int i = 0; i < directArrowButtons.Length; i++)
        {
            int index = i;
            directArrowButtons[i].onClick.AddListener(() => SelectArrows(index));
        }
        controlsSettingsButton.onClick.AddListener(OpenControlsSettings);
        if (pauseControlsSettingsButton != null) pauseControlsSettingsButton.onClick.AddListener(OpenControlsFromPause);
        closeControlsSettingsButton.onClick.AddListener(CloseControlsSettings);
        editLayoutButton.onClick.AddListener(BeginLayoutEdit);
        resetControlsButton.onClick.AddListener(ResetControls);
        finishLayoutButton.onClick.AddListener(() => EndLayoutEdit(true));
        cancelLayoutButton.onClick.AddListener(() => EndLayoutEdit(false));
        resetLayoutButton.onClick.AddListener(ResetLayout);
        controlSize = Mathf.Clamp(UnityEngine.PlayerPrefs.GetFloat(PreferencePrefix + "Size", 1f), .8f, 1.25f);
        controlOpacity = Mathf.Clamp(UnityEngine.PlayerPrefs.GetFloat(PreferencePrefix + "Opacity", .82f), .45f, 1f);
        sizeSlider.SetValueWithoutNotify(controlSize); opacitySlider.SetValueWithoutNotify(controlOpacity);
        sizeSlider.onValueChanged.AddListener(SetControlSize);
        opacitySlider.onValueChanged.AddListener(SetControlOpacity);
        initialized = true;
        RefreshLayout();
        ApplySavedLayout(); ApplyAppearance();
    }
    private void OnEnable() => YG2.onPauseGame += OnSdkPause;
    private void OnDisable()
    {
        YG2.onPauseGame -= OnSdkPause;
        ResetInput();
    }
    private void Update()
    {
        RefreshLayout();
        if (!CanControl) return;
        if (shield.ConsumePress()) shieldLatched = !shieldLatched && player.ShieldValue > 0f;
        if (shieldLatched && player.ShieldValue <= 0f) shieldLatched = false;
        shieldIndicator.color = shieldLatched ? GoldColor : PanelColor;
        shieldIndicator.GetComponentInChildren<TextMeshProUGUI>().color = shieldLatched
            ? new Color32(20, 43, 38, 255) : new Color32(244, 240, 223, 255);
    }
    public void RefreshLayout()
    {
        bool touch = UseTouch;
        if (touchWasEnabled != touch)
        {
            ResetInput();
            foreach (var hint in desktopHints) if (hint != null) hint.SetActive(!touch);
            if (combatEquipment != null) combatEquipment.SetActive(!touch);
            if (combatStatus != null)
            {
                combatStatus.localScale = touch ? statusScale * 1.35f : statusScale;
                combatStatus.anchoredPosition = touch
                    ? new Vector2(combatStatus.rect.width * combatStatus.localScale.x * .5f + 12,
                        -combatStatus.rect.height * combatStatus.localScale.y * .5f - 12)
                    : statusPosition;
            }
            wallet.anchorMin = touch ? new Vector2(.78f, 1) : walletMin;
            wallet.anchorMax = touch ? new Vector2(.78f, 1) : walletMax;
            wallet.anchoredPosition = touch ? new Vector2(0, -50) : walletPosition;
            mirrorProgress.anchoredPosition = touch ? new Vector2(0, 300) : progressPosition;
            touchWasEnabled = touch;
        }
        var pixels = canvas.pixelRect;
        bool portrait = touch && pixels.height > pixels.width;
        gameUi.SetMobileOrientationPause(portrait);
        rotateHint.SetActive(portrait);
        var area = Screen.safeArea;
        var min = new Vector2(area.xMin / Mathf.Max(1, Screen.width), area.yMin / Mathf.Max(1, Screen.height));
        var max = new Vector2(area.xMax / Mathf.Max(1, Screen.width), area.yMax / Mathf.Max(1, Screen.height));
        safeArea.anchorMin = Vector2.Max(Vector2.zero, min); safeArea.anchorMax = Vector2.Min(Vector2.one, max);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        hudSafeArea.anchorMin = touch ? safeArea.anchorMin : Vector2.zero;
        hudSafeArea.anchorMax = touch ? safeArea.anchorMax : Vector2.one;
        hudSafeArea.offsetMin = hudSafeArea.offsetMax = Vector2.zero;
        if (initialized && safeArea.rect.size != layoutDimensions)
        {
            ApplySavedLayout();
            layoutDimensions = safeArea.rect.size;
        }
        if (controlsSettingsButton != null) controlsSettingsButton.gameObject.SetActive(touch);
        if (pauseControlsSettingsButton != null) pauseControlsSettingsButton.gameObject.SetActive(touch);
        if (desktopControlBindings != null) desktopControlBindings.SetActive(!touch);
        if (mobileControlsCard != null) mobileControlsCard.SetActive(touch);
        if (controlsSettingsPanel != null && controlsSettingsPanel.activeSelf
            && (!touch || portrait || !gameUi.IsMenuOpen)) CloseControlsSettings();
        bool active = CanControl && !portrait;
        if (!active && !editingLayout) ResetInput();
        gameplayControls.SetActive(active || editingLayout && touch && !portrait);
        if (!active) return;
        dashButton.interactable = player.CanDash;
        healButton.interactable = player.PotionCount > 0 && player.hp < player.maxHp;
        mirror.GetComponent<Button>().interactable = player.MirrorCount > 0;
        var bow = player.crossbowController;
        for (int i = 0; i < directArrowCounts.Length; i++)
        {
            directArrowCounts[i].text = CrossbowController.FormatArrowCount(bow.GetArrowCount(i));
            directArrowSelection[i].color = i == bow.SelectedArrowIndex ? GoldColor : PanelColor;
            directArrowCounts[i].color = i == bow.SelectedArrowIndex
                ? new Color32(20, 43, 38, 255) : new Color32(244, 240, 223, 255);
        }
        healCountLabel.text = player.PotionCount.ToString("N0");
        mirrorCountLabel.text = player.MirrorCount.ToString("N0");
        modeLabel.text = bow.ShootingMode == 1 ? "Одиночный" : bow.ShootingMode == 2 ? "Дробовик" : "Автоогонь";
        bool merchant = shop.IsPlayerInRange || trainer.IsPlayerInRange;
        var chest = AvailableChest();
        interactButton.gameObject.SetActive(merchant || chest != null);
        interactLabel.text = merchant ? "Торговец" : "Открыть сундук";
    }
    private Chest AvailableChest()
    {
        foreach (var chest in chests) if (chest != null && chest.CanInteract) return chest;
        return null;
    }
    public void ResetInput()
    {
        movement?.ResetControl(); aim?.ResetControl(); shield?.ResetControl(); mirror?.ResetControl(); view?.ResetControl();
        foreach (var control in layoutTouches) if (control != null) control.ResetControl();
        shieldLatched = false; autoAimTarget = null; nextAimSearch = 0f;
        if (utilitiesPanel != null) utilitiesPanel.SetActive(false);
        if ((UseTouch || touchWasEnabled) && player != null) player.CancelTouchActions();
    }
    private void Dash() { if (CanControl) player.TryDash(); }
    private void Heal() { if (CanControl) player.UseHealingPotion(); }
    private void SelectArrows(int index) { if (CanControl) player.crossbowController.TrySelectArrowType(index); }
    private void SwitchMode()
    {
        if (!CanControl) return;
        var bow = player.crossbowController;
        aim.ResetControl();
        for (int i = 1; i <= 3; i++) if (bow.TrySelectMode((bow.ShootingMode - 1 + i) % 3 + 1)) break;
    }
    private void Interact()
    {
        if (!CanControl) return;
        ResetInput();
        if (shop.IsPlayerInRange) shop.OpenShop();
        else if (trainer.IsPlayerInRange) trainer.OpenShop();
        else AvailableChest()?.Interact();
    }
    private void OpenPause()
    {
        if (!CanControl) return;
        ResetInput(); gameUi.ToggleMenu();
    }
    private void ToggleUtilities()
    {
        if (!CanControl) return;
        utilitiesPanel.SetActive(!utilitiesPanel.activeSelf);
        mirror.ResetControl(); view.ResetControl();
    }

    private Vector2 AutoAimDirection()
    {
        Vector2 origin = player.transform.position;
        if (Time.unscaledTime >= nextAimSearch)
        {
            nextAimSearch = Time.unscaledTime + .12f;
            autoAimTarget = null;
            int count;
            while ((count = Physics2D.OverlapCircleNonAlloc(origin, autoAimRange, aimCandidates)) == aimCandidates.Length
                && aimCandidates.Length < 4096) System.Array.Resize(ref aimCandidates, aimCandidates.Length * 2);
            float nearest = autoAimRange * autoAimRange;
            for (int i = 0; i < count; i++)
            {
                var collider = aimCandidates[i];
                var enemy = ArrowDef.FindEnemy(collider);
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                if (enemy is SlimeBoss boss && !boss.IsAlive) continue;
                Vector2 point = collider.bounds.center;
                float distance = (point - origin).sqrMagnitude;
                if (distance >= nearest) continue;
                if (gameCamera != null)
                {
                    var screen = gameCamera.WorldToViewportPoint(point);
                    if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
                }
                if (!ClearShot(origin, point, enemy)) continue;
                nearest = distance; autoAimTarget = enemy.transform; autoAimPoint = point - (Vector2)enemy.transform.position;
            }
        }
        Vector2 direction = autoAimTarget != null ? (Vector2)autoAimTarget.position + autoAimPoint - origin : aim.LastDirection;
        return direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.down;
    }

    private bool ClearShot(Vector2 origin, Vector2 point, MonoBehaviour target)
    {
        int count = Physics2D.LinecastNonAlloc(origin, point, sightHits);
        for (int i = 0; i < count; i++)
        {
            var collider = sightHits[i].collider;
            if (collider == null || collider.isTrigger || collider.GetComponentInParent<PlayerController>() == player
                || collider.GetComponentInParent<ArrowDef>() != null || collider.CompareTag("Projectile")) continue;
            var enemy = ArrowDef.FindEnemy(collider);
            if (enemy == null && collider.transform != target.transform) return false;
        }
        return true;
    }

    private void OpenControlsFromPause()
    {
        if (!UseTouch || !gameUi.IsMenuOpen) return;
        gameUi.ShowSettings();
        OpenControlsSettings();
    }

    public void OpenControlsSettings()
    {
        if (!UseTouch || !gameUi.IsMenuOpen || !gameSettingsPanel.activeInHierarchy) return;
        ResetInput(); controlsSettingsPanel.SetActive(true);
        controlsSettingsDialog.SetActive(true); settingsBackdrop.SetActive(true); layoutToolbar.SetActive(false);
        gameSettingsPanel.SetActive(false); gameMenuPanel.SetActive(false);
        ApplyAppearance();
    }
    public void CloseControlsSettings()
    {
        bool wasOpen = controlsSettingsPanel.activeSelf;
        if (editingLayout) EndLayoutEdit(false);
        controlsSettingsPanel.SetActive(false); SaveAppearance(); ResetInput();
        if (wasOpen && gameUi.IsMenuOpen)
        { gameMenuPanel.SetActive(true); gameSettingsPanel.SetActive(true); }
    }
    public void BeginLayoutEdit()
    {
        if (!UseTouch || !controlsSettingsPanel.activeSelf) return;
        ResetInput();
        editSnapshot = new Vector2[editableControls.Length];
        for (int i = 0; i < editableControls.Length; i++) editSnapshot[i] = editableControls[i].anchoredPosition;
        editingLayout = true; draggedControl = null;
        controlsSettingsDialog.SetActive(false); settingsBackdrop.SetActive(false); layoutToolbar.SetActive(true);
        layoutHint.text = "Перетаскивай кнопки. Бой на паузе.";
        gameplayControls.SetActive(true); interactButton.gameObject.SetActive(true);
        ApplyAppearance();
    }
    public void EndLayoutEdit(bool save)
    {
        if (!editingLayout) return;
        if (draggedControl != null) EndControlDrag();
        if (save) SaveLayout();
        else if (editSnapshot != null)
            for (int i = 0; i < editableControls.Length; i++) editableControls[i].anchoredPosition = editSnapshot[i];
        editingLayout = false; draggedControl = null;
        ResetInput(); gameplayControls.SetActive(false);
        controlsSettingsDialog.SetActive(true); settingsBackdrop.SetActive(true); layoutToolbar.SetActive(false);
        ApplyAppearance();
    }
    public bool BeginControlDrag(Transform source, Vector2 screenPoint)
    {
        if (!editingLayout || draggedControl != null) return false;
        foreach (var rect in editableControls)
        {
            if (source != rect && !source.IsChildOf(rect)) continue;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea, screenPoint, null, out dragStartPoint)) return false;
            draggedControl = rect; dragStartPosition = rect.anchoredPosition; return true;
        }
        return false;
    }
    public void DragControl(Vector2 screenPoint)
    {
        if (!editingLayout || draggedControl == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea, screenPoint, null, out var point)) return;
        draggedControl.anchoredPosition = dragStartPosition + point - dragStartPoint;
        ClampControl(draggedControl);
    }
    public void EndControlDrag()
    {
        if (draggedControl == null) return;
        foreach (var rect in editableControls)
        {
            if (rect == draggedControl || !rect.gameObject.activeInHierarchy) continue;
            if (!ScreenBounds(rect).Overlaps(ScreenBounds(draggedControl))) continue;
            draggedControl.anchoredPosition = dragStartPosition;
            layoutHint.text = "Кнопки пересекаются. Выбери свободное место.";
            draggedControl = null; return;
        }
        layoutHint.text = "Перетаскивай кнопки. Бой на паузе.";
        draggedControl = null;
    }
    private static Rect ScreenBounds(RectTransform rect)
    {
        var points = new Vector3[4]; rect.GetWorldCorners(points);
        return Rect.MinMaxRect(points[0].x, points[0].y, points[2].x, points[2].y);
    }
    private void ClampControl(RectTransform rect)
    {
        var bounds = ScreenBounds(rect); var area = ScreenBounds(safeArea);
        Vector2 offset = Vector2.zero;
        if (bounds.xMin < area.xMin + 6) offset.x += area.xMin + 6 - bounds.xMin;
        if (bounds.xMax > area.xMax - 6) offset.x -= bounds.xMax - area.xMax + 6;
        if (bounds.yMin < area.yMin + 6) offset.y += area.yMin + 6 - bounds.yMin;
        if (bounds.yMax > area.yMax - 6) offset.y -= bounds.yMax - area.yMax + 6;
        rect.anchoredPosition += offset / Mathf.Max(.01f, canvas.scaleFactor);
    }
    private void ApplySavedLayout()
    {
        if (!initialized || safeArea.rect.width <= 0 || safeArea.rect.height <= 0 || editingLayout) return;
        for (int i = 0; i < editableControls.Length; i++)
        {
            var rect = editableControls[i]; string key = PreferencePrefix + rect.name;
            rect.anchoredPosition = UnityEngine.PlayerPrefs.HasKey(key + ".X")
                ? new Vector2(UnityEngine.PlayerPrefs.GetFloat(key + ".X") * safeArea.rect.width,
                    UnityEngine.PlayerPrefs.GetFloat(key + ".Y") * safeArea.rect.height)
                : defaultPositions[i];
            rect.localScale = Vector3.one * controlSize; ClampControl(rect);
        }
    }
    private void SaveLayout()
    {
        foreach (var rect in editableControls)
        {
            string key = PreferencePrefix + rect.name;
            UnityEngine.PlayerPrefs.SetFloat(key + ".X", rect.anchoredPosition.x / Mathf.Max(1f, safeArea.rect.width));
            UnityEngine.PlayerPrefs.SetFloat(key + ".Y", rect.anchoredPosition.y / Mathf.Max(1f, safeArea.rect.height));
        }
        UnityEngine.PlayerPrefs.Save();
    }
    public void ResetLayout()
    {
        for (int i = 0; i < editableControls.Length; i++)
        {
            editableControls[i].anchoredPosition = defaultPositions[i]; ClampControl(editableControls[i]);
            if (!editingLayout)
            {
                string key = PreferencePrefix + editableControls[i].name;
                UnityEngine.PlayerPrefs.DeleteKey(key + ".X"); UnityEngine.PlayerPrefs.DeleteKey(key + ".Y");
            }
        }
        if (!editingLayout) UnityEngine.PlayerPrefs.Save();
    }
    public void ResetControls()
    {
        controlSize = 1f; controlOpacity = .82f;
        sizeSlider.SetValueWithoutNotify(controlSize); opacitySlider.SetValueWithoutNotify(controlOpacity);
        ApplyAppearance(); ResetLayout(); SaveAppearance();
    }
    private void SetControlSize(float value) { controlSize = value; ApplyAppearance(); SaveAppearance(); }
    private void SetControlOpacity(float value) { controlOpacity = value; ApplyAppearance(); SaveAppearance(); }
    private void ApplyAppearance()
    {
        foreach (var rect in editableControls)
        {
            rect.localScale = Vector3.one * controlSize;
            var group = rect.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = editingLayout ? 1f : controlOpacity;
            if (initialized) ClampControl(rect);
        }
        sizeValueText.text = Mathf.RoundToInt(controlSize * 100) + "%";
        opacityValueText.text = Mathf.RoundToInt(controlOpacity * 100) + "%";
    }
    private void SaveAppearance()
    {
        UnityEngine.PlayerPrefs.SetFloat(PreferencePrefix + "Size", controlSize);
        UnityEngine.PlayerPrefs.SetFloat(PreferencePrefix + "Opacity", controlOpacity);
        UnityEngine.PlayerPrefs.Save();
    }
    private void InterruptControls()
    {
        if (controlsSettingsPanel != null && controlsSettingsPanel.activeSelf) CloseControlsSettings();
        ResetInput();
    }
    private void OnSdkPause(bool paused) { if (paused) InterruptControls(); }
    private void OnApplicationFocus(bool value) { focused = value; if (!value) InterruptControls(); }
    private void OnApplicationPause(bool value) { applicationPaused = value; if (value) InterruptControls(); }
}
