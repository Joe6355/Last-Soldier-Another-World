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
    [Header("Автонаводка на видимых врагов")]
    [Tooltip("Резервный радиус, если игровая камера недоступна. С камерой поиск охватывает весь экран.")]
    [SerializeField, Min(.1f)] private float autoAimRange = 9f;
    private Collider2D[] aimCandidates = new Collider2D[128];
    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[32];
    private readonly List<TouchControl> layoutTouches = new List<TouchControl>();
    private Camera gameCamera;
    private Transform autoAimTarget;
    private Collider2D autoAimCollider;
    private Vector2 lastMovementDirection = Vector2.down;
    private Vector2 lastFacingDirection = Vector2.down;
    private float shotFacingUntil;
    private float nextAimSearch, controlSize = 1f, controlOpacity = .82f;
    private bool shieldLatched, editingLayout, initialized;
#if LAST_SOLDIER_RUSTORE
    private Canvas statusCanvas;
    private RectTransform statusHome, statusViewport;
    private CanvasGroup statusInput;
    private int statusSiblingIndex;
    private Vector2 statusPivot;
    private (Transform window, Vector3 originalScale)[] hudWindows;
    private float appliedPreviewWidth;
#endif
    private Vector3 statusScale;
    private Vector2 statusPosition;
    private Vector2 layoutDimensions, dragStartPoint, dragStartPosition;
    private RectTransform draggedControl;
    private Vector2[] editSnapshot;
    private const string PreferencePrefix = "MobileControls.v2.";
#if LAST_SOLDIER_RUSTORE
    private const string LayoutInstructions = "Перетащи кнопки и плашку.";
    private const string LayoutOverlap = "Выбери свободное место.";
#else
    private const string LayoutInstructions = "Перетаскивай кнопки. Бой на паузе.";
    private const string LayoutOverlap = "Кнопки пересекаются. Выбери свободное место.";
#endif
    private static readonly Color PanelColor = new Color32(35, 68, 59, 235);
    private static readonly Color GoldColor = new Color32(227, 186, 101, 255);
    private bool focused = true, applicationPaused;
    private bool touchWasEnabled;
    private Vector2 walletMin, walletMax, walletPosition, progressPosition;
    private int[] displayedArrowCounts;
    private int displayedArrow = -1, displayedPotions = -1, displayedMirrors = -1, displayedMode = -1;
    private bool displayedMerchant, interactionLabelReady;
    private Button mirrorButton;
    private TextMeshProUGUI shieldLabel;
    private bool? displayedShieldLatched;
    private Rect appliedSafeArea;
    private int appliedScreenWidth = -1, appliedScreenHeight = -1;
    private bool appliedHudTouch;
    private float appliedHudScale = float.NaN;
#if LAST_SOLDIER_RUSTORE
    [SerializeField] private bool menuPreview;
    [SerializeField] private float previewStatusReferenceWidth;
    private System.Action previewClosed;
    private bool IsMenuPreview => menuPreview;
    public static MobileControls OpenMenuSettings(System.Action closed)
    {
        var source = Resources.Load<MobileControls>("RuStoreControlsPreview");
        if (source == null) throw new System.InvalidOperationException("Prepare the RuStore controls preview before building");
        var preview = Instantiate(source);
        preview.previewClosed = closed;
        preview.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        preview.InitializeSettings();
        preview.OpenControlsSettings();
        GameLocalization.Register(preview.gameObject);
        return preview;
    }
#else
    private bool IsMenuPreview => false;
#endif

    private void Awake()
    {
        displayedArrowCounts = new int[directArrowCounts.Length];
        for (int i = 0; i < displayedArrowCounts.Length; i++) displayedArrowCounts[i] = int.MinValue;
        if (mirror != null) mirrorButton = mirror.GetComponent<Button>();
        if (shieldIndicator != null) shieldLabel = shieldIndicator.GetComponentInChildren<TextMeshProUGUI>();
        gameCamera = Camera.main;
        if (player != null) lastMovementDirection = -(Vector2)player.transform.up;
        lastFacingDirection = lastMovementDirection;
        if (combatStatus != null)
        {
            statusPosition = combatStatus.anchoredPosition;
            statusScale = combatStatus.localScale;
#if LAST_SOLDIER_RUSTORE
            statusPivot = combatStatus.pivot;
            statusHome = (RectTransform)combatStatus.parent;
            statusSiblingIndex = combatStatus.GetSiblingIndex();
            statusCanvas = combatStatus.GetComponentInParent<Canvas>().rootCanvas;
            statusViewport = (RectTransform)statusCanvas.transform;
            statusInput = combatStatus.gameObject.AddComponent<CanvasGroup>();
            statusInput.blocksRaycasts = false;
            combatStatus.GetComponent<Image>().raycastTarget = true;
            combatStatus.gameObject.AddComponent<TouchControl>().BindLayout(this);
            System.Array.Resize(ref editableControls, editableControls.Length + 1);
            System.Array.Resize(ref defaultPositions, defaultPositions.Length + 1);
            editableControls[editableControls.Length - 1] = combatStatus;
#endif
        }
#if LAST_SOLDIER_RUSTORE
        if (hudSafeArea != null)
        {
            hudWindows = new (Transform, Vector3)[hudSafeArea.childCount];
            for (int i = 0; i < hudWindows.Length; i++)
            {
                var window = hudSafeArea.GetChild(i);
                hudWindows[i] = (window, window.localScale);
            }
        }
#endif
        foreach (var rect in editableControls)
            if (rect != null) layoutTouches.AddRange(rect.GetComponentsInChildren<TouchControl>(true));
        if (wallet == null || mirrorProgress == null) return;
        walletMin = wallet.anchorMin; walletMax = wallet.anchorMax; walletPosition = wallet.anchoredPosition;
        progressPosition = mirrorProgress.anchoredPosition;
    }

    public static bool IsTouchDevice => Application.isMobilePlatform || YG2.envir.isMobile || YG2.envir.isTablet;
    public bool UseTouch => IsTouchDevice;
    private bool CanUseGameplayUi => !IsMenuPreview && GameProgress.IsReady && focused && !applicationPaused
        && !YG2.isPauseGame && !YG2.nowAdsShow && Time.timeScale > 0f
        && player != null && player.crossbowController != null && player.hp > 0f && !player.IsAwaitingRevive && !gameUi.IsMobileOrientationPaused
        && !gameUi.IsMenuOpen && !gameUi.IsTradeOpen && !editingLayout;
    public bool CanControl => UseTouch && CanUseGameplayUi;
    public bool IsPointerOverInteraction(Vector2 screenPoint)
    {
        if (interactButton == null || !interactButton.gameObject.activeInHierarchy) return false;
        var rootCanvas = interactButton.GetComponentInParent<Canvas>().rootCanvas;
        return RectTransformUtility.RectangleContainsScreenPoint(interactButton.GetComponent<RectTransform>(),
            screenPoint, rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera);
    }
    public Vector2 Movement => CanControl && TipsManager.Allows(TipsManager.GuideAction.Move) ? movement.Value : Vector2.zero;
    public bool IsEditingLayout => editingLayout;
    public bool IsControlsSettingsOpen => controlsSettingsPanel != null && controlsSettingsPanel.activeInHierarchy;
    public bool AutomaticSelected => player != null && player.crossbowController != null
        && player.crossbowController.ShootingMode == 3 && player.crossbowController.IsModeUnlocked(3);
    public Vector2 AimDirection
    {
        get
        {
            Vector2 direction = Movement;
            if (direction.sqrMagnitude > .001f) lastMovementDirection = direction.normalized;
            return lastMovementDirection;
        }
    }
    public Vector2 FacingDirection
    {
        get
        {
            Vector2 movementDirection = AimDirection;
            if (!CanControl) return lastFacingDirection;
            if (aim.IsManualAim) return aim.LastDirection;
            if (aim.QueuedManualAim) return aim.QueuedDirection;
            if (aim.IsHeld && TipsManager.Allows(TipsManager.GuideAction.Fire))
                return AutoAimDirection(Movement.sqrMagnitude > .001f ? movementDirection : lastFacingDirection);
            if (Movement.sqrMagnitude > .001f && Time.unscaledTime >= shotFacingUntil)
                lastFacingDirection = movementDirection;
            return lastFacingDirection;
        }
    }
    public Vector2 ResolveShotDirection()
    {
        if (aim.QueuedManualAim) return aim.QueuedDirection;
        if (aim.IsManualAim) return aim.LastDirection;
        return AutoAimDirection(Movement.sqrMagnitude > .001f ? AimDirection : lastFacingDirection);
    }
    public void RecordShotDirection(Vector2 direction)
    {
        lastFacingDirection = direction.normalized;
        shotFacingUntil = Time.unscaledTime + .25f;
    }
    public bool HasFirePress => CanControl && aim.HasQueuedPress;
    public void CancelFireInput() => aim.ResetControl();
    public bool FireHeld => CanControl && aim.IsRepeating;
    public bool ShieldHeld => CanControl && shieldLatched;
    public bool MirrorHeld => CanControl && mirror.IsHeld;
    public bool ViewHeld => CanControl && view.IsHeld;
    public bool ConsumeFirePress() => aim.ConsumePress() && CanControl;

    private void Start()
    {
        if (!IsMenuPreview)
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
            if (controlsSettingsButton != null) controlsSettingsButton.onClick.AddListener(OpenControlsSettings);
            if (pauseControlsSettingsButton != null) pauseControlsSettingsButton.onClick.AddListener(OpenControlsFromPause);
        }
        InitializeSettings();
    }
    private void InitializeSettings()
    {
        if (initialized) return;
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
#if LAST_SOLDIER_RUSTORE
        RuStoreMenu.ConfigureControlsSettings(controlsSettingsPanel);
#endif
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
        if (displayedShieldLatched != shieldLatched)
        {
            displayedShieldLatched = shieldLatched;
            shieldIndicator.color = shieldLatched ? GoldColor : PanelColor;
            if (shieldLabel != null) shieldLabel.color = shieldLatched
                ? new Color32(20, 43, 38, 255) : new Color32(244, 240, 223, 255);
        }
    }
    public void RefreshLayout()
    {
        bool touch = UseTouch;
        bool safeChanged = RefreshSafeArea();
        RefreshHud(touch, safeChanged);
        if (initialized && safeArea.rect.size != layoutDimensions)
        {
            ApplySavedLayout();
            layoutDimensions = safeArea.rect.size;
        }
        if (IsMenuPreview)
        {
            gameplayControls.SetActive(editingLayout);
            interactButton.gameObject.SetActive(editingLayout);
            rotateHint.SetActive(false);
            return;
        }
        if (touchWasEnabled != touch)
        {
            ResetInput();
            foreach (var hint in desktopHints) if (hint != null) hint.SetActive(!touch);
            if (combatEquipment != null) combatEquipment.SetActive(!touch);
#if !LAST_SOLDIER_RUSTORE
            if (combatStatus != null)
            {
                combatStatus.localScale = touch ? statusScale * 1.35f : statusScale;
                combatStatus.anchoredPosition = touch
                    ? new Vector2(combatStatus.rect.width * combatStatus.localScale.x * .5f + 12,
                        -combatStatus.rect.height * combatStatus.localScale.y * .5f - 12)
                    : statusPosition;
            }
#endif
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
        if (controlsSettingsButton != null) controlsSettingsButton.gameObject.SetActive(touch);
        if (pauseControlsSettingsButton != null) pauseControlsSettingsButton.gameObject.SetActive(touch);
        if (desktopControlBindings != null) desktopControlBindings.SetActive(!touch);
        if (mobileControlsCard != null) mobileControlsCard.SetActive(touch);
        if (controlsSettingsPanel != null && controlsSettingsPanel.activeSelf
            && (!touch || portrait || !gameUi.IsMenuOpen)) CloseControlsSettings();
        bool active = CanControl && !portrait;
        if (!active && !editingLayout) ResetInput();
        bool editing = editingLayout && touch && !portrait;
        gameplayControls.SetActive(active || editing);
        bool merchant = shop.IsPlayerInRange || trainer.IsPlayerInRange;
        var chest = touch ? AvailableChest() : null;
        interactButton.gameObject.SetActive(editing || CanUseGameplayUi && !portrait && (merchant || chest != null));
        if (!interactionLabelReady || displayedMerchant != merchant)
        {
            interactionLabelReady = true; displayedMerchant = merchant;
            interactLabel.text = merchant ? "Торговец" : "Открыть сундук";
        }
        if (!active) return;
        dashButton.interactable = player.CanDash;
        healButton.interactable = player.PotionCount > 0 && player.hp < player.maxHp;
        if (mirrorButton != null) mirrorButton.interactable = player.MirrorCount > 0;
        var bow = player.crossbowController;
        for (int i = 0; i < directArrowCounts.Length; i++)
        {
            int count = bow.GetArrowCount(i);
            if (displayedArrowCounts[i] != count)
            {
                displayedArrowCounts[i] = count;
                directArrowCounts[i].text = CrossbowController.FormatArrowCount(count);
            }
            if (displayedArrow != bow.SelectedArrowIndex)
            {
                directArrowSelection[i].color = i == bow.SelectedArrowIndex ? GoldColor : PanelColor;
                directArrowCounts[i].color = i == bow.SelectedArrowIndex
                    ? new Color32(20, 43, 38, 255) : new Color32(244, 240, 223, 255);
            }
        }
        displayedArrow = bow.SelectedArrowIndex;
        if (displayedPotions != player.PotionCount)
        { displayedPotions = player.PotionCount; healCountLabel.text = displayedPotions.ToString("N0"); }
        if (displayedMirrors != player.MirrorCount)
        { displayedMirrors = player.MirrorCount; mirrorCountLabel.text = displayedMirrors.ToString("N0"); }
        if (displayedMode != bow.ShootingMode)
        {
            displayedMode = bow.ShootingMode;
            modeLabel.text = displayedMode == 1 ? "Одиночный" : displayedMode == 2 ? "Дробовик" : "Автоогонь";
        }
    }
    private void RefreshHud(bool touch, bool safeChanged)
    {
#if LAST_SOLDIER_RUSTORE
        float hudScale = RuStoreMenu.InterfaceScale;
        float previewWidth = IsMenuPreview && statusViewport != null ? statusViewport.rect.width : 0f;
        safeChanged |= previewWidth != appliedPreviewWidth;
        appliedPreviewWidth = previewWidth;
#else
        const float hudScale = 1f;
#endif
        if (!safeChanged && appliedHudTouch == touch && appliedHudScale == hudScale) return;
        appliedHudTouch = touch; appliedHudScale = hudScale;
        if (hudSafeArea != null)
        {
            hudSafeArea.anchorMin = touch ? safeArea.anchorMin : Vector2.zero;
            hudSafeArea.anchorMax = touch ? safeArea.anchorMax : Vector2.one;
            hudSafeArea.offsetMin = hudSafeArea.offsetMax = Vector2.zero;
        }
#if LAST_SOLDIER_RUSTORE
        if (hudWindows != null)
            foreach (var (window, originalScale) in hudWindows)
                if (window != combatStatus)
                    window.localScale = new Vector3(originalScale.x * hudScale, originalScale.y * hudScale, originalScale.z);

        if (combatStatus == null) return;
        // The passive status HUD may use the display edge. Buttons still avoid the cutout.
        var parent = touch ? statusViewport : statusHome;
        if (combatStatus.parent != parent)
        {
            combatStatus.SetParent(parent, false);
            combatStatus.SetSiblingIndex(touch ? statusHome.GetSiblingIndex() : statusSiblingIndex);
        }
        combatStatus.pivot = touch ? new Vector2(0, 1) : statusPivot;
        float scale = touch ? hudScale * 1.35f : hudScale;
        if (previewStatusReferenceWidth > 0f && previewWidth > 0f) scale *= previewWidth / previewStatusReferenceWidth;
        combatStatus.localScale = new Vector3(statusScale.x * scale, statusScale.y * scale, statusScale.z);
        if (!touch) combatStatus.anchoredPosition = statusPosition;
        else if (statusViewport.rect.width > 0f && statusViewport.rect.height > 0f)
        {
            if (!editingLayout) LoadPosition(combatStatus, Vector2.zero);
            ClampControl(combatStatus);
        }
#endif
    }
    private bool RefreshSafeArea()
    {
        var area = Screen.safeArea;
        int width = Screen.width, height = Screen.height;
        if (appliedSafeArea == area && appliedScreenWidth == width && appliedScreenHeight == height) return false;
        appliedSafeArea = area; appliedScreenWidth = width; appliedScreenHeight = height;
        safeArea.anchorMin = Vector2.Max(Vector2.zero, new Vector2(area.xMin / Mathf.Max(1, width), area.yMin / Mathf.Max(1, height)));
        safeArea.anchorMax = Vector2.Min(Vector2.one, new Vector2(area.xMax / Mathf.Max(1, width), area.yMax / Mathf.Max(1, height)));
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        return true;
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
        shieldLatched = false; autoAimTarget = null; autoAimCollider = null; nextAimSearch = 0f;
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
        if (!CanUseGameplayUi || !TipsManager.Allows(TipsManager.GuideAction.Interact)) return;
        ResetInput();
        if (shop.IsPlayerInRange) shop.OpenShop();
        else if (trainer.IsPlayerInRange) trainer.OpenShop();
        else if (UseTouch) AvailableChest()?.Interact();
    }
    private void OpenPause()
    {
        if (!CanControl) return;
        ResetInput(); gameUi.ToggleMenu();
    }
    private void ToggleUtilities()
    {
        if (!CanControl || !TipsManager.Allows(TipsManager.GuideAction.Utilities)) return;
        utilitiesPanel.SetActive(!utilitiesPanel.activeSelf);
        mirror.ResetControl(); view.ResetControl();
        if (utilitiesPanel.activeSelf) TipsManager.Report(TipsManager.GuideAction.Utilities);
    }

    public void ShowTutorialUtilities() => utilitiesPanel.SetActive(true);
    public RectTransform GuideTarget(TipsManager.GuideAction action)
    {
        Transform target = action switch
        {
            TipsManager.GuideAction.Move => movement.transform,
            TipsManager.GuideAction.Fire => aim.transform,
            TipsManager.GuideAction.Arrow => directArrowButtons.Length > 1 ? directArrowButtons[1].transform : null,
            TipsManager.GuideAction.Dash => dashButton.transform,
            TipsManager.GuideAction.Shield => shield.transform,
            TipsManager.GuideAction.Heal => healButton.transform,
            TipsManager.GuideAction.Utilities => utilityButton.transform,
            TipsManager.GuideAction.Mirror => mirror.transform,
            TipsManager.GuideAction.Interact => interactButton.transform,
            _ => null
        };
        return target != null && target.gameObject.activeInHierarchy ? target.GetComponent<RectTransform>() : null;
    }

    private Vector2 AutoAimDirection(Vector2 facing)
    {
        Vector2 origin = player.transform.position;
        float searchRadius = autoAimRange;
        if (gameCamera != null && gameCamera.orthographic)
            searchRadius = Vector2.Distance(origin, gameCamera.transform.position)
                + gameCamera.orthographicSize * Mathf.Sqrt(1f + gameCamera.aspect * gameCamera.aspect);
        bool hadTarget = !object.ReferenceEquals(autoAimTarget, null);
        if (autoAimTarget != null && IsAimCandidate(autoAimCollider, origin, searchRadius, out var lockedEnemy, out var lockedPoint)
            && lockedEnemy.transform == autoAimTarget)
        {
            // A missed sting is a deliberate opening, also usable without precise touch aiming.
            var shaula = lockedEnemy is ShaulaHitZone zone ? zone.boss : lockedEnemy as ShaulaBoss;
            if (shaula != null && shaula.TailExposed)
                foreach (var weak in shaula.GetComponentsInChildren<ShaulaHitZone>())
                    if (weak.zone == ShaulaHitZone.Zone.Tail)
                    {
                        var weakCollider = weak.GetComponent<Collider2D>();
                        if (IsAimCandidate(weakCollider, origin, searchRadius, out var weakEnemy, out var weakPoint))
                        { autoAimCollider = weakCollider; autoAimTarget = weakEnemy.transform; return (weakPoint - origin).normalized; }
                    }
            return (lockedPoint - origin).normalized;
        }
        if (hadTarget) nextAimSearch = 0f;
        autoAimTarget = null; autoAimCollider = null;
        if (Time.unscaledTime >= nextAimSearch)
        {
            nextAimSearch = Time.unscaledTime + .12f;
            autoAimTarget = null;
            int count;
            while ((count = Physics2D.OverlapCircleNonAlloc(origin, searchRadius, aimCandidates)) == aimCandidates.Length
                && aimCandidates.Length < 4096) System.Array.Resize(ref aimCandidates, aimCandidates.Length * 2);
            float nearest = searchRadius * searchRadius;
            for (int i = 0; i < count; i++)
            {
                var collider = aimCandidates[i];
                if (!IsAimCandidate(collider, origin, searchRadius, out var enemy, out var point)) continue;
                float distance = (point - origin).sqrMagnitude;
                if (enemy is ShaulaHitZone zone) distance *= zone.AimWeight;
                if (distance >= nearest) continue;
                nearest = distance; autoAimTarget = enemy.transform; autoAimCollider = collider;
            }
        }
        Vector2 direction = autoAimCollider != null ? (Vector2)autoAimCollider.bounds.center - origin : facing;
        return direction.sqrMagnitude > .0001f ? direction.normalized : facing;
    }

    private bool IsAimCandidate(Collider2D collider, Vector2 origin, float searchRadius, out MonoBehaviour enemy, out Vector2 point)
    {
        enemy = null; point = Vector2.zero;
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) return false;
        enemy = ArrowDef.FindEnemy(collider);
        if (enemy == null || !enemy.isActiveAndEnabled || enemy is SlimeBoss boss && !boss.IsAlive) return false;
        if (enemy is ShaulaBoss shaula && !shaula.IsAlive || enemy is ShaulaHitZone zone && !zone.IsAlive) return false;
        var bow = player != null ? player.crossbowController : null;
        if (bow != null && !ArrowDef.CanDamageEnemy(enemy, bow.arrowPrefabs[bow.SelectedArrowIndex].GetComponent<ArrowDef>().IsHoly)) return false;
        point = collider.bounds.center;
        float distance = (point - origin).sqrMagnitude;
        if (distance < .0001f || distance > searchRadius * searchRadius) return false;
        if (gameCamera != null)
        {
            var screen = gameCamera.WorldToViewportPoint(point);
            if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) return false;
        }
        return ClearShot(origin, point, enemy);
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
        if (!UseTouch || !IsMenuPreview && (!gameUi.IsMenuOpen || !gameSettingsPanel.activeInHierarchy)) return;
        ResetInput(); controlsSettingsPanel.SetActive(true);
        controlsSettingsDialog.SetActive(true); settingsBackdrop.SetActive(true); layoutToolbar.SetActive(false);
        if (!IsMenuPreview) { gameSettingsPanel.SetActive(false); gameMenuPanel.SetActive(false); }
        ApplyAppearance();
    }
    public void CloseControlsSettings()
    {
        bool wasOpen = controlsSettingsPanel.activeSelf;
        if (editingLayout) EndLayoutEdit(false);
        controlsSettingsPanel.SetActive(false); SaveAppearance(); ResetInput();
#if LAST_SOLDIER_RUSTORE
        if (IsMenuPreview)
        {
            var closed = previewClosed; previewClosed = null;
            gameObject.SetActive(false); Destroy(gameObject); closed?.Invoke(); return;
        }
#endif
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
        layoutHint.text = LayoutInstructions;
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
        ResetInput(); gameplayControls.SetActive(false); interactButton.gameObject.SetActive(false);
        controlsSettingsDialog.SetActive(true); settingsBackdrop.SetActive(true); layoutToolbar.SetActive(false);
        ApplyAppearance();
    }
    public bool BeginControlDrag(Transform source, Vector2 screenPoint)
    {
        if (!editingLayout || draggedControl != null) return false;
        foreach (var rect in editableControls)
        {
            if (source != rect && !source.IsChildOf(rect)) continue;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent,
                screenPoint, LayoutCamera(rect), out dragStartPoint)) return false;
            draggedControl = rect; dragStartPosition = rect.anchoredPosition; return true;
        }
        return false;
    }
    public void DragControl(Vector2 screenPoint)
    {
        if (!editingLayout || draggedControl == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)draggedControl.parent,
            screenPoint, LayoutCamera(draggedControl), out var point)) return;
        draggedControl.anchoredPosition = dragStartPosition + point - dragStartPoint;
        ClampControl(draggedControl);
    }
    public void EndControlDrag()
    {
        if (draggedControl == null) return;
        var bounds = ScreenBounds(draggedControl, LayoutCamera(draggedControl));
        bool blocked = UsesInterfaceScale(draggedControl) && ScreenBounds((RectTransform)layoutToolbar.transform,
            LayoutCamera(null)).Overlaps(bounds);
        for (int i = 0; !blocked && i < editableControls.Length; i++)
        {
            var rect = editableControls[i];
            if (rect == draggedControl || !rect.gameObject.activeInHierarchy) continue;
            blocked = ScreenBounds(rect, LayoutCamera(rect)).Overlaps(bounds);
        }
        if (blocked) draggedControl.anchoredPosition = dragStartPosition;
        layoutHint.text = blocked ? LayoutOverlap : LayoutInstructions;
        draggedControl = null;
    }
    private bool UsesInterfaceScale(RectTransform rect)
    {
#if LAST_SOLDIER_RUSTORE
        return rect != null && rect == combatStatus;
#else
        return false;
#endif
    }
    private RectTransform LayoutArea(RectTransform rect)
    {
#if LAST_SOLDIER_RUSTORE
        if (UsesInterfaceScale(rect)) return statusViewport;
#endif
        return safeArea;
    }
    private Canvas LayoutCanvas(RectTransform rect)
    {
#if LAST_SOLDIER_RUSTORE
        if (UsesInterfaceScale(rect)) return statusCanvas;
#endif
        return canvas;
    }
    private Camera LayoutCamera(RectTransform rect)
    {
        var root = LayoutCanvas(rect);
        return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
    }
    private static Rect ScreenBounds(RectTransform rect, Camera camera)
    {
        var min = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.min));
        var max = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.max));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    private void ClampControl(RectTransform rect)
    {
        var camera = LayoutCamera(rect);
        var bounds = ScreenBounds(rect, camera); var area = ScreenBounds(LayoutArea(rect), camera);
        float margin = UsesInterfaceScale(rect) ? 0f : 6f;
        Vector2 offset = Vector2.zero;
        if (bounds.xMin < area.xMin + margin) offset.x += area.xMin + margin - bounds.xMin;
        if (bounds.xMax > area.xMax - margin) offset.x -= bounds.xMax - area.xMax + margin;
        if (bounds.yMin < area.yMin + margin) offset.y += area.yMin + margin - bounds.yMin;
        if (bounds.yMax > area.yMax - margin) offset.y -= bounds.yMax - area.yMax + margin;
        if (offset != Vector2.zero) rect.anchoredPosition += offset / Mathf.Max(.01f, LayoutCanvas(rect).scaleFactor);
    }
    private void LoadPosition(RectTransform rect, Vector2 defaultPosition)
    {
        string key = PreferencePrefix + rect.name;
        var area = LayoutArea(rect).rect;
        rect.anchoredPosition = UnityEngine.PlayerPrefs.HasKey(key + ".X")
            ? new Vector2(UnityEngine.PlayerPrefs.GetFloat(key + ".X") * area.width,
                UnityEngine.PlayerPrefs.GetFloat(key + ".Y") * area.height)
            : defaultPosition;
    }
    private void ApplySavedLayout()
    {
        if (!initialized || safeArea.rect.width <= 0 || safeArea.rect.height <= 0 || editingLayout) return;
        for (int i = 0; i < editableControls.Length; i++)
        {
            var rect = editableControls[i];
            if (UsesInterfaceScale(rect) && !UseTouch) continue;
            LoadPosition(rect, defaultPositions[i]);
            if (!UsesInterfaceScale(rect)) rect.localScale = Vector3.one * controlSize;
            ClampControl(rect);
        }
    }
    private void SaveLayout()
    {
        for (int i = 0; i < editableControls.Length; i++)
        {
            var rect = editableControls[i];
            // A temporary clamp at a larger interface scale should not overwrite the saved anchor.
            if (UsesInterfaceScale(rect) && editSnapshot != null && rect.anchoredPosition == editSnapshot[i]) continue;
            string key = PreferencePrefix + rect.name;
            var area = LayoutArea(rect).rect;
            UnityEngine.PlayerPrefs.SetFloat(key + ".X", rect.anchoredPosition.x / Mathf.Max(1f, area.width));
            UnityEngine.PlayerPrefs.SetFloat(key + ".Y", rect.anchoredPosition.y / Mathf.Max(1f, area.height));
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
#if LAST_SOLDIER_RUSTORE
        if (statusInput != null)
        {
            statusInput.blocksRaycasts = editingLayout;
            if (IsMenuPreview) combatStatus.gameObject.SetActive(editingLayout);
        }
#endif
        foreach (var rect in editableControls)
        {
            if (UsesInterfaceScale(rect)) continue;
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
