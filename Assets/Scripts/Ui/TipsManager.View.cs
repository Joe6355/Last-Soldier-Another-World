using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class TipsManager
{
    private Button guideActionButton, guideSkipButton;
    private RectTransform guideRoot, guideCard;
    private Canvas guideCanvas;
    private RectTransform guideFocus, guideHand;
    private readonly Image[] guideShades = new Image[4];
    private TextMeshProUGUI guideTitleText, guideBodyText, guideCounterText, guideActionText;
    private TextMeshProUGUI guideWaypointText;
    private Image guideWaypoint;
    private Image guidePortrait, guideHandImage;
    private Texture2D guideHandTexture;
    private Sprite guideHandSprite;
    private bool guideWelcome, guideHasAction;
    private Rect guideLastSafeArea;
    private Vector2 guideLastScreenSize;
    private bool guideLastTouch;
    private float guideLastCanvasScale = -1f;
    private float guideCardWidth, guideCardHeight, guideScale = 1f;
    private bool guideCardPlaced, guideSkipPlaced;
    private Rect guideCardDestination, guideSkipBounds;
    private GuideStep guidePlacedStep;
    private bool guidePlacedResult, guidePlacedShop;
    private RectTransform guidePlacedTarget;
    private Vector2 guideCardVelocity, guideSkipDestination;
    private float guideObstructedSince = -1f, guideRelocateAfter;
    private int guideHandDirection = -1;
    private float guideHandTurnAfter;
    private readonly Vector3[] guideCorners = new Vector3[4];
    private readonly Vector2[] guideCardCandidates = new Vector2[12];
    private readonly Vector2[] guideSkipCandidates = new Vector2[4];
    private static readonly Vector2[] GuideHandDirections = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };

    private static readonly Color GuideGreen = new Color32(27, 53, 46, 255);
    private static readonly Color GuideCream = new Color32(247, 240, 216, 255);
    private static readonly Color GuideGold = new Color32(227, 186, 101, 255);
    private static readonly Color GuideInk = new Color32(39, 54, 42, 255);

    private void CreateGuideView(Canvas host, Sprite portrait, TMP_FontAsset font)
    {
        if (guideRoot != null) return;
        var rootObject = new GameObject("GuidedTutorialCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        rootObject.layer = host != null ? host.gameObject.layer : gameObject.layer;
        guideRoot = rootObject.GetComponent<RectTransform>();
        guideCanvas = rootObject.GetComponent<Canvas>();
        guideCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        guideCanvas.sortingOrder = 200;
        var scaler = rootObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        if (font == null && host != null)
        {
            var existingText = host.GetComponentInChildren<TMP_Text>(true);
            if (existingText != null) font = existingText.font;
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;

        for (int i = 0; i < guideShades.Length; i++)
            guideShades[i] = GuideImage("Shade" + i, guideRoot, new Color(0f, .025f, .02f, .64f), true);

        guideFocus = GuideRect("Focus", guideRoot);
        GuideBorder(guideFocus, GuideGold, 3f);
        guideWaypoint = GuideImage("PortalWaypoint", guideFocus, GuideGreen, false);
        GuideStretch(guideWaypoint.rectTransform, 3f);
        guideWaypointText = GuideText("Destination", guideWaypoint.transform, font, GuideGold, FontStyles.Bold);
        guideWaypointText.alignment = TextAlignmentOptions.Center;
        guideWaypointText.enableAutoSizing = true;
        GuideStretch(guideWaypointText.rectTransform, 2f);
        guideWaypoint.gameObject.SetActive(false);
        guideHandImage = GuideImage("Pointer", guideRoot, Color.white, false);
        guideHand = guideHandImage.rectTransform;
        guideHand.pivot = new Vector2(.5f, .9f);
        CreateGuideHandSprite();
        guideHandImage.sprite = guideHandSprite;
        guideHandImage.preserveAspect = true;

        var cardImage = GuideImage("MentorCard", guideRoot, GuideCream, true);
        guideCard = cardImage.rectTransform;
        GuideBorder(guideCard, GuideGold, 2f);
        guidePortrait = GuideImage("MentorPortrait", guideCard, GuideGreen, false);
        var portraitImage = GuideImage("Portrait", guidePortrait.rectTransform, Color.white, false);
        portraitImage.sprite = portrait;
        portraitImage.preserveAspect = true;
        portraitImage.enabled = portrait != null;
        GuideStretch(portraitImage.rectTransform, 4f);
        guideTitleText = GuideText("Title", guideCard, font, GuideInk, FontStyles.Bold);
        guideCounterText = GuideText("Counter", guideCard, font, new Color32(114, 105, 74, 255));
        guideBodyText = GuideText("Body", guideCard, font, GuideInk);
        guideBodyText.enableWordWrapping = true;
        guideBodyText.overflowMode = TextOverflowModes.Overflow;

        guideActionButton = GuideButton("Continue", guideCard, font, GuideGold, GuideInk, out guideActionText);
        guideActionButton.onClick.AddListener(AdvanceGuide);
        guideSkipButton = GuideButton("Skip", guideRoot, font, GuideGreen, GuideCream, out var skipText);
        skipText.text = "Пропустить";
        guideSkipButton.onClick.AddListener(SkipGuide);
        GuideBorder(guideSkipButton.GetComponent<RectTransform>(), new Color32(106, 131, 106, 255), 1f);
        Canvas.ForceUpdateCanvases();
        UpdateGuideGeometry(true);
        guideRoot.gameObject.SetActive(false);
    }

    private void RenderGuide(string title, string body, string counter, string actionLabel,
        bool showAction, bool welcome = false)
    {
        if (guideRoot == null) return;
        if (guideTitleText.text == title && guideBodyText.text == body && guideCounterText.text == counter
            && guideActionText.text == actionLabel && guideHasAction == showAction && guideWelcome == welcome) return;
        guideTitleText.text = title;
        guideBodyText.text = body;
        guideCounterText.text = counter;
        guideActionText.text = actionLabel;
        guideHasAction = showAction;
        guideWelcome = welcome;
        guideActionButton.gameObject.SetActive(showAction);
        UpdateGuideGeometry(true);
    }

    private void PositionGuide(RectTransform target, Transform worldTarget, bool allowWorldInput, bool hidden)
    {
        if (guideRoot == null) return;
        guideRoot.gameObject.SetActive(!hidden);
        if (hidden) return;
        UpdateGuideGeometry(false);

        var screenBounds = new Rect(0, 0, guideRoot.rect.width, guideRoot.rect.height);
        Rect safeBounds = GuideScreenRect(Screen.safeArea);
        float padding = 12f / guideScale;
        safeBounds.xMin += padding;
        safeBounds.xMax -= padding;
        safeBounds.yMin += padding;
        safeBounds.yMax -= padding;
        Rect focus = Rect.zero;
        bool hasFocus = !guideWelcome && TryGuideTargetRect(target, worldTarget, out focus);
        if (hasFocus)
        {
            float targetPadding = 6f / guideScale;
            focus.xMin = Mathf.Clamp(focus.xMin - targetPadding, screenBounds.xMin, screenBounds.xMax);
            focus.xMax = Mathf.Clamp(focus.xMax + targetPadding, screenBounds.xMin, screenBounds.xMax);
            focus.yMin = Mathf.Clamp(focus.yMin - targetPadding, screenBounds.yMin, screenBounds.yMax);
            focus.yMax = Mathf.Clamp(focus.yMax + targetPadding, screenBounds.yMin, screenBounds.yMax);
            hasFocus = focus.width > .5f && focus.height > .5f;
        }

        guideFocus.gameObject.SetActive(hasFocus);
        guideHand.gameObject.SetActive(hasFocus);
        bool portalStep = step == GuideStep.ArenaPortal || step == GuideStep.ArenaReturn;
        Vector3 viewport = worldTarget != null && Camera.main != null ? Camera.main.WorldToViewportPoint(worldTarget.position) : new Vector3(.5f, .5f, 1);
        bool offscreenPortal = portalStep && hasFocus && (viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1);
        guideWaypoint.gameObject.SetActive(offscreenPortal);
        if (offscreenPortal)
            guideWaypointText.text = (step == GuideStep.ArenaPortal ? "АРЕНА" : "ЛАГЕРЬ")
                + $"\n{Vector2.Distance(player.transform.position, worldTarget.position):0} м";
        if (!hasFocus)
        {
            GuidePlace(guideShades[0].rectTransform, screenBounds);
            for (int i = 1; i < guideShades.Length; i++) GuidePlace(guideShades[i].rectTransform, Rect.zero);
        }
        else
        {
            GuidePlace(guideShades[0].rectTransform, new Rect(0, 0, focus.xMin, screenBounds.height));
            GuidePlace(guideShades[1].rectTransform, new Rect(focus.xMax, 0, screenBounds.xMax - focus.xMax, screenBounds.height));
            GuidePlace(guideShades[2].rectTransform, new Rect(focus.xMin, 0, focus.width, focus.yMin));
            GuidePlace(guideShades[3].rectTransform, new Rect(focus.xMin, focus.yMax, focus.width, screenBounds.yMax - focus.yMax));
            GuidePlace(guideFocus, focus);
        }
        foreach (var shade in guideShades)
        {
            shade.raycastTarget = !allowWorldInput;
            shade.color = new Color(0f, .025f, .02f, shop != null && shop.IsOpen ? .64f : .18f);
        }

        Rect heroBounds = Rect.zero, worldBounds = Rect.zero;
        if (shop == null || !shop.IsOpen)
        {
            if (player != null) TryGuideTargetRect(null, player.transform, out heroBounds);
            TryGuideTargetRect(null, worldTarget, out worldBounds);
            // Protect the action around the hero on keyboard-only and result steps too.
            float margin = 46f / guideScale;
            heroBounds = new Rect(heroBounds.xMin - margin, heroBounds.yMin - margin,
                heroBounds.width + margin * 2f, heroBounds.height + margin * 2f);
        }
        Rect movementBounds = Rect.zero;
        if (controls != null && controls.UseTouch && Allows(GuideAction.Move))
            TryGuideTargetRect(controls.GuideTarget(GuideAction.Move), null, out movementBounds);
        Rect cardBounds = PositionStableGuideCard(safeBounds, focus, hasFocus, heroBounds, worldBounds, movementBounds, target);
        GuidePlace(guideCard, cardBounds);
        PlaceGuideSkip(safeBounds, cardBounds, focus, hasFocus);
        if (hasFocus) PlaceGuideHand(safeBounds, focus, cardBounds);
    }

    public bool IsPointerOverGuide(Vector2 screenPoint)
    {
        if (guideRoot == null || !guideRoot.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(guideCard, screenPoint, null)
            || RectTransformUtility.RectangleContainsScreenPoint(guideSkipButton.GetComponent<RectTransform>(), screenPoint, null);
    }

    private void UpdateGuideGeometry(bool force)
    {
        var dimensions = new Vector2(Screen.width, Screen.height);
        bool touch = MobileControls.IsTouchDevice;
        float canvasScale = guideCanvas.scaleFactor;
        bool geometryChanged = dimensions != guideLastScreenSize || guideLastSafeArea != Screen.safeArea
            || touch != guideLastTouch || !Mathf.Approximately(canvasScale, guideLastCanvasScale);
        if (!force && !geometryChanged) return;
        if (geometryChanged)
        {
            guideCardPlaced = guideSkipPlaced = false;
            guideHandDirection = -1;
        }
        guideLastScreenSize = dimensions;
        guideLastSafeArea = Screen.safeArea;
        guideLastTouch = touch;
        guideLastCanvasScale = canvasScale;
        // Touch text keeps the same apparent size on 854x393 and 2400x1080 phones.
        float density = touch ? Mathf.Clamp(Screen.safeArea.height / 393f, 1f, 3f) : 1f;
        guideScale = Mathf.Max(.01f, canvasScale / density);
        bool compact = touch || Screen.height < 550;
        float unit = 1f / guideScale;
        Rect safe = GuideScreenRect(Screen.safeArea);
        guideCardWidth = Mathf.Max(150f * unit, Mathf.Min(safe.width - 24f * unit, (compact ? 330f : 430f) * unit));
        if ((touch || Screen.width < 800) && (shop == null || !shop.IsOpen))
            guideCardWidth = Mathf.Min(guideCardWidth, safe.width * .5f - 92f * unit);
        bool narrow = guideCardWidth < 240f * unit;
        float bodyFont = (compact ? 15f : 19f) * unit;
        float bodyWidth = guideCardWidth - 28f * unit;
        float header = ((compact ? 60f : 71f) + (narrow ? 24f : 0f)) * unit;
        float footer = (guideHasAction ? (compact ? 47f : 54f) : 14f) * unit;
        float bodySpace = Mathf.Max(0, safe.height - 24f * unit - header - footer);
        guideBodyText.enableAutoSizing = false;
        guideBodyText.fontSize = bodyFont;
        float preferredBodyHeight = guideBodyText.GetPreferredValues(guideBodyText.text, bodyWidth, Mathf.Infinity).y + 2f * unit;
        float bodyHeight = Mathf.Clamp(preferredBodyHeight, Mathf.Min(bodySpace, (compact ? 36f : 42f) * unit), bodySpace);
        guideBodyText.enableAutoSizing = preferredBodyHeight > bodySpace;
        guideBodyText.fontSizeMax = bodyFont;
        guideBodyText.fontSizeMin = (compact ? 13f : 16f) * unit;
        guideCardHeight = Mathf.Min(safe.height - 24f * unit, header + bodyHeight + footer);
        float portraitSize = (narrow ? 32f : compact ? 44f : 54f) * unit;
        GuidePlace(guidePortrait.rectTransform, new Rect(12f * unit, guideCardHeight - portraitSize - 10f * unit, portraitSize, portraitSize));
        float titleX = portraitSize + 24f * unit;
        guideTitleText.fontSize = (compact ? 18f : 22f) * unit;
        guideTitleText.enableAutoSizing = true;
        guideTitleText.enableWordWrapping = narrow;
        guideTitleText.fontSizeMax = guideTitleText.fontSize;
        guideTitleText.fontSizeMin = (compact ? 15f : 18f) * unit;
        guideTitleText.overflowMode = TextOverflowModes.Ellipsis;
        GuidePlace(guideTitleText.rectTransform, new Rect(titleX, guideCardHeight - (narrow ? 60f : 36f) * unit,
            guideCardWidth - titleX - 12f * unit, (narrow ? 50f : 26f) * unit));
        guideCounterText.fontSize = (compact ? 11f : 13f) * unit;
        GuidePlace(guideCounterText.rectTransform, new Rect(titleX, guideCardHeight - (narrow ? 77f : 53f) * unit,
            guideCardWidth - titleX - 12f * unit, 17f * unit));
        GuidePlace(guideBodyText.rectTransform, new Rect(14f * unit, footer, bodyWidth,
            Mathf.Max(0, guideCardHeight - header - footer)));
        guideBodyText.alignment = TextAlignmentOptions.TopLeft;
        guideActionText.fontSize = (compact ? 16f : 18f) * unit;
        GuidePlace(guideActionButton.GetComponent<RectTransform>(), new Rect(14f * unit, 10f * unit,
            guideCardWidth - 28f * unit, (compact ? 32f : 38f) * unit));
        guideSkipButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = (compact ? 13f : 16f) * unit;
        guideHand.sizeDelta = new Vector2(compact ? 42f : 52f, compact ? 42f : 52f) * unit;
        guideWaypointText.fontSizeMin = 11f * unit;
        guideWaypointText.fontSizeMax = 15f * unit;
        foreach (var border in guideFocus.GetComponentsInChildren<Image>())
        {
            if (border == guideWaypoint) continue;
            var rect = border.rectTransform;
            if (border.name == "Top" || border.name == "Bottom") rect.sizeDelta = new Vector2(0, 3f * unit);
            else rect.sizeDelta = new Vector2(3f * unit, 0);
        }
    }

    private Rect PositionStableGuideCard(Rect safe, Rect focus, bool hasFocus, Rect hero, Rect world,
        Rect movement, RectTransform target)
    {
        var size = new Vector2(Mathf.Min(guideCardWidth, safe.width), Mathf.Min(guideCardHeight, safe.height));
        bool shopOpen = shop != null && shop.IsOpen;
        bool changed = !guideCardPlaced || guidePlacedStep != step || guidePlacedResult != result
            || guidePlacedShop != shopOpen || guidePlacedTarget != target || guideCardDestination.size != size;
        if (changed)
        {
            var previous = new Rect(guideCardDestination.position, size);
            previous.position = new Vector2(Mathf.Clamp(previous.x, safe.xMin, safe.xMax - size.x),
                Mathf.Clamp(previous.y, safe.yMin, safe.yMax - size.y));
            // Keep the same reading position across text updates and steps whenever it remains usable.
            guideCardDestination = guideCardPlaced && GuideCardObstruction(previous, focus, hasFocus, hero, world, movement) < .01f
                ? previous : PlaceGuideCard(safe, focus, hasFocus, hero, world, movement);
            guideCardPlaced = true;
            guidePlacedStep = step; guidePlacedResult = result; guidePlacedShop = shopOpen; guidePlacedTarget = target;
            guideCardVelocity = Vector2.zero;
            guideObstructedSince = -1f;
            guideRelocateAfter = Time.unscaledTime + 1f;
            guideHandDirection = -1;
            return guideCardDestination;
        }

        var current = new Rect(guideCard.anchoredPosition, size);
        bool moving = (current.position - guideCardDestination.position).sqrMagnitude > .25f / (guideScale * guideScale);
        // Moving targets and camera noise never select a new dock on their own. A real, sustained
        // obstruction must remain for 0.4 seconds; the one-second cooldown prevents back-and-forth.
        if (!moving && GuideCardObstruction(current, focus, hasFocus, hero, world, movement) > 16f / (guideScale * guideScale))
        {
            if (guideObstructedSince < 0) guideObstructedSince = Time.unscaledTime;
            if (Time.unscaledTime - guideObstructedSince >= .4f && Time.unscaledTime >= guideRelocateAfter)
            {
                Rect alternative = PlaceGuideCard(safe, focus, hasFocus, hero, world, movement, current);
                if (GuideCardObstruction(alternative, focus, hasFocus, hero, world, movement)
                    < GuideCardObstruction(current, focus, hasFocus, hero, world, movement) * .5f)
                {
                    guideCardDestination = alternative;
                    guideCardVelocity = Vector2.zero;
                    guideRelocateAfter = Time.unscaledTime + 1f;
                }
                guideObstructedSince = -1f;
            }
        }
        else guideObstructedSince = -1f;

        current.position = Vector2.SmoothDamp(current.position, guideCardDestination.position,
            ref guideCardVelocity, .18f, 1400f / guideScale, Time.unscaledDeltaTime);
        if ((current.position - guideCardDestination.position).sqrMagnitude < .25f / (guideScale * guideScale))
        { current.position = guideCardDestination.position; guideCardVelocity = Vector2.zero; }
        return current;
    }

    private static float GuideCardObstruction(Rect card, Rect focus, bool hasFocus, Rect hero, Rect world, Rect movement)
        => (hasFocus ? GuideOverlapArea(card, focus) : 0) + GuideOverlapArea(card, hero)
            + GuideOverlapArea(card, world) + GuideOverlapArea(card, movement);

    private Rect PlaceGuideCard(Rect safe, Rect focus, bool hasFocus, Rect heroBounds, Rect worldBounds,
        Rect movementBounds, Rect? motionStart = null)
    {
        float width = Mathf.Min(guideCardWidth, safe.width), height = Mathf.Min(guideCardHeight, safe.height);
        if (!hasFocus) focus = heroBounds.width > 0 ? heroBounds : new Rect(safe.center, Vector2.zero);
        float gap = 20f / guideScale;
        guideCardCandidates[0] = new Vector2(safe.center.x - width * .5f, safe.yMin);
        guideCardCandidates[1] = new Vector2(safe.center.x - width * .5f, safe.yMax - height);
        guideCardCandidates[2] = new Vector2(safe.xMin, safe.center.y - height * .5f);
        guideCardCandidates[3] = new Vector2(safe.xMax - width, safe.center.y - height * .5f);
        guideCardCandidates[4] = new Vector2(focus.xMin - gap - width, focus.center.y - height * .5f);
        guideCardCandidates[5] = new Vector2(focus.xMax + gap, focus.center.y - height * .5f);
        guideCardCandidates[6] = new Vector2(focus.center.x - width * .5f, focus.yMax + gap);
        guideCardCandidates[7] = new Vector2(focus.center.x - width * .5f, focus.yMin - gap - height);
        guideCardCandidates[8] = new Vector2(safe.xMin, safe.yMin);
        guideCardCandidates[9] = new Vector2(safe.xMax - width, safe.yMin);
        guideCardCandidates[10] = new Vector2(safe.xMin, safe.yMax - height);
        guideCardCandidates[11] = new Vector2(safe.xMax - width, safe.yMax - height);
        Rect best = motionStart ?? Rect.zero;
        float bestScore = float.PositiveInfinity;
        var protectedFocus = focus;
        protectedFocus.xMin -= 10f / guideScale; protectedFocus.xMax += 10f / guideScale;
        protectedFocus.yMin -= 10f / guideScale; protectedFocus.yMax += 10f / guideScale;
        for (int i = 0; i < guideCardCandidates.Length; i++)
        {
            // World targets move. Use only screen-fixed docks outside the stationary shop layout.
            if (i >= 4 && i <= 7 && (shop == null || !shop.IsOpen)) continue;
            var card = new Rect(Mathf.Clamp(guideCardCandidates[i].x, safe.xMin, safe.xMax - width),
                Mathf.Clamp(guideCardCandidates[i].y, safe.yMin, safe.yMax - height), width, height);
            if (motionStart.HasValue)
            {
                Rect start = motionStart.Value, travel = GuideMotionBounds(start, card);
                // A smooth transfer must not sweep across an otherwise visible hero or control.
                if (GuideNewObstruction(start, travel, heroBounds) || GuideNewObstruction(start, travel, movementBounds)
                    || hasFocus && GuideNewObstruction(start, travel, focus)) continue;
            }
            float overlap = hasFocus ? GuideOverlapArea(card, protectedFocus) : 0;
            float distance = Vector2.Distance(card.center, focus.center);
            float worldOverlap = GuideOverlapArea(card, heroBounds) + GuideOverlapArea(card, worldBounds) + GuideOverlapArea(card, movementBounds);
            float score = overlap * 100f + worldOverlap * 100f + distance * .05f + i * 2f;
            if (score < bestScore) { best = card; bestScore = score; }
        }
        return best;
    }

    private void PlaceGuideSkip(Rect safe, Rect card, Rect focus, bool hasFocus)
    {
        float unit = 1f / guideScale;
        float width = (Screen.height < 550 ? 105f : 125f) * unit, height = 34f * unit;
        Rect travel = GuideMotionBounds(card, guideCardDestination);
        bool moving = (card.position - guideCardDestination.position).sqrMagnitude > .25f * unit * unit;
        bool protectFocus = hasFocus && guidePlacedTarget != null;
        if (guideSkipPlaced && guideSkipBounds.size == new Vector2(width, height)
            && (moving && guideSkipDestination == guideCardDestination.position
                || GuideOverlapArea(guideSkipBounds, travel) < .01f
                    && (!protectFocus || GuideOverlapArea(guideSkipBounds, focus) < .01f)))
        { GuidePlace(guideSkipButton.GetComponent<RectTransform>(), guideSkipBounds); return; }
        guideSkipCandidates[0] = new Vector2(safe.xMax - width, safe.yMax - height);
        guideSkipCandidates[1] = new Vector2(safe.xMin, safe.yMax - height);
        guideSkipCandidates[2] = new Vector2(safe.xMax - width, safe.yMin);
        guideSkipCandidates[3] = new Vector2(safe.xMin, safe.yMin);
        Rect best = Rect.zero; float bestScore = float.PositiveInfinity;
        for (int i = 0; i < guideSkipCandidates.Length; i++)
        {
            var bounds = new Rect(guideSkipCandidates[i], new Vector2(width, height));
            float score = GuideOverlapArea(bounds, travel) + (hasFocus ? GuideOverlapArea(bounds, focus) : 0) + i;
            if (score < bestScore) { best = bounds; bestScore = score; }
        }
        guideSkipBounds = best; guideSkipPlaced = true;
        guideSkipDestination = guideCardDestination.position;
        GuidePlace(guideSkipButton.GetComponent<RectTransform>(), best);
    }

    private void PlaceGuideHand(Rect safe, Rect focus, Rect card)
    {
        float handSize = guideHand.sizeDelta.x;
        Vector2 best = Vector2.down, bestPoint = focus.center;
        float bestScore = float.PositiveInfinity;
        float previousScore = float.PositiveInfinity;
        for (int i = 0; i < GuideHandDirections.Length; i++)
        {
            Vector2 direction = GuideHandDirections[i];
            Vector2 edge = focus.center + Vector2.Scale(direction, focus.size * .5f);
            Vector2 middle = edge + direction * (handSize * .48f + 8f / guideScale);
            Rect box = new Rect(middle - Vector2.one * handSize * .5f, Vector2.one * handSize);
            float outside = box.width * box.height - GuideOverlapArea(box, safe);
            float score = GuideOverlapArea(box, card) + outside * 2f + i;
            if (i == guideHandDirection) previousScore = score;
            if (score < bestScore) { best = direction; bestPoint = edge; bestScore = score; }
        }
        if (guideHandDirection >= 0 && (Time.unscaledTime < guideHandTurnAfter || previousScore <= bestScore + handSize * handSize * .2f))
        {
            best = GuideHandDirections[guideHandDirection];
            bestPoint = focus.center + Vector2.Scale(best, focus.size * .5f);
            bestScore = previousScore;
        }
        else
        {
            guideHandDirection = System.Array.IndexOf(GuideHandDirections, best);
            guideHandTurnAfter = Time.unscaledTime + .35f;
        }
        guideHand.gameObject.SetActive(bestScore < handSize * handSize * .6f);
        float pulse = Mathf.Sin(Time.unscaledTime * 4.5f);
        guideHand.anchoredPosition = bestPoint + best * (8f + pulse * 2.5f) / guideScale;
        guideHand.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(-best.y, -best.x) * Mathf.Rad2Deg - 90f);
        guideHand.localScale = Vector3.one * (1f + pulse * .045f);
    }

    private bool TryGuideTargetRect(RectTransform target, Transform worldTarget, out Rect rect)
    {
        rect = Rect.zero;
        if (target != null && target.gameObject.activeInHierarchy)
        {
            var targetCanvas = target.GetComponentInParent<Canvas>();
            Camera camera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera : null;
            target.GetWorldCorners(guideCorners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in guideCorners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                Vector2 local = GuideScreenPoint(screen);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }
        if (worldTarget == null || Camera.main == null || !worldTarget.gameObject.activeInHierarchy) return false;
        Vector3 point = Camera.main.WorldToScreenPoint(worldTarget.position);
        if (point.z < 0) return false;
        var sprite = worldTarget.GetComponent<SpriteRenderer>();
        float radius = 32f;
        if (sprite != null && worldTarget.GetComponent<Portal>() != null
            && point.x > Screen.safeArea.xMin && point.x < Screen.safeArea.xMax
            && point.y > Screen.safeArea.yMin && point.y < Screen.safeArea.yMax)
        {
            // Include the destination sign, so the card cannot hide where the portal leads.
            Bounds bounds = sprite.bounds;
            var sign = worldTarget.GetComponentInChildren<TextMeshPro>();
            if (sign != null) bounds.Encapsulate(sign.GetComponent<MeshRenderer>().bounds);
            Vector3 lower = Camera.main.WorldToScreenPoint(bounds.min);
            Vector3 upper = Camera.main.WorldToScreenPoint(bounds.max);
            rect = GuideScreenRect(Rect.MinMaxRect(Mathf.Min(lower.x, upper.x) - 8f, Mathf.Min(lower.y, upper.y) - 8f,
                Mathf.Max(lower.x, upper.x) + 8f, Mathf.Max(lower.y, upper.y) + 8f));
            return true;
        }
        if (sprite != null)
        {
            Vector3 a = Camera.main.WorldToScreenPoint(sprite.bounds.min);
            Vector3 b = Camera.main.WorldToScreenPoint(sprite.bounds.max);
            radius = Mathf.Max(radius, Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) * .5f);
        }
        // A portal outside the camera remains a visible waypoint at the screen edge.
        float inset = radius + 24f;
        point.x = Mathf.Clamp(point.x, Screen.safeArea.xMin + inset, Screen.safeArea.xMax - inset);
        point.y = Mathf.Clamp(point.y, Screen.safeArea.yMin + inset, Screen.safeArea.yMax - inset);
        rect = GuideScreenRect(new Rect(point.x - radius, point.y - radius, radius * 2f, radius * 2f));
        return true;
    }

    private Vector2 GuideScreenPoint(Vector2 screenPoint)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(guideRoot, screenPoint, null, out var local);
        return local - guideRoot.rect.min;
    }

    private Rect GuideScreenRect(Rect screenRect)
    {
        Vector2 min = GuideScreenPoint(screenRect.min), max = GuideScreenPoint(screenRect.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static float GuideOverlapArea(Rect first, Rect second) => Mathf.Max(0, Mathf.Min(first.xMax, second.xMax)
        - Mathf.Max(first.xMin, second.xMin)) * Mathf.Max(0, Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin));

    private static Rect GuideMotionBounds(Rect from, Rect to) => Rect.MinMaxRect(Mathf.Min(from.xMin, to.xMin),
        Mathf.Min(from.yMin, to.yMin), Mathf.Max(from.xMax, to.xMax), Mathf.Max(from.yMax, to.yMax));

    private static bool GuideNewObstruction(Rect from, Rect travel, Rect obstacle)
        => GuideOverlapArea(from, obstacle) < .01f && GuideOverlapArea(travel, obstacle) > .01f;

    private static RectTransform GuideRect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        return rect;
    }

    private static Image GuideImage(string name, Transform parent, Color color, bool raycast)
    {
        var image = GuideRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = raycast;
        return image;
    }

    private static TextMeshProUGUI GuideText(string name, Transform parent, TMP_FontAsset font, Color color,
        FontStyles style = FontStyles.Normal)
    {
        var text = GuideRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.color = color; text.fontStyle = style; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Left;
        return text;
    }

    private static Button GuideButton(string name, Transform parent, TMP_FontAsset font, Color background,
        Color ink, out TextMeshProUGUI label)
    {
        var image = GuideImage(name, parent, background, true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
        colors.pressedColor = new Color(.88f, .88f, .88f); button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        label = GuideText("Label", image.rectTransform, font, ink, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        GuideStretch(label.rectTransform, 2f);
        return button;
    }

    private static void GuidePlace(RectTransform rect, Rect bounds)
    {
        rect.anchoredPosition = bounds.position;
        rect.sizeDelta = bounds.size;
    }

    private static void GuideStretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
    }

    private static void GuideBorder(RectTransform parent, Color color, float thickness)
    {
        string[] names = { "Top", "Bottom", "Left", "Right" };
        for (int i = 0; i < names.Length; i++)
        {
            var rect = GuideImage(names[i], parent, color, false).rectTransform;
            if (i < 2)
            {
                rect.anchorMin = new Vector2(0, i == 0 ? 1 : 0);
                rect.anchorMax = new Vector2(1, i == 0 ? 1 : 0);
                rect.pivot = new Vector2(.5f, i == 0 ? 1 : 0);
                rect.sizeDelta = new Vector2(0, thickness);
            }
            else
            {
                rect.anchorMin = new Vector2(i == 2 ? 0 : 1, 0);
                rect.anchorMax = new Vector2(i == 2 ? 0 : 1, 1);
                rect.pivot = new Vector2(i == 2 ? 0 : 1, .5f);
                rect.sizeDelta = new Vector2(thickness, 0);
            }
            rect.anchoredPosition = Vector2.zero;
        }
    }

    private void CreateGuideHandSprite()
    {
        const int size = 32;
        var filled = new bool[size, size];
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                filled[x, y] = (x >= 12 && x <= 16 && y >= 17 && y <= 30)
                    || (x >= 8 && x <= 24 && y >= 7 && y <= 17)
                    || (x >= 18 && x <= 25 && y >= 13 && y <= 20)
                    || (x >= 4 && x <= 10 && y >= 12 && y <= 17)
                    || (x >= 10 && x <= 21 && y >= 1 && y <= 7);
        var pixels = new Color32[size * size];
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (!filled[x, y]) continue;
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1
                    || !filled[x - 1, y] || !filled[x + 1, y] || !filled[x, y - 1] || !filled[x, y + 1];
                pixels[y * size + x] = edge ? new Color32(34, 46, 40, 255)
                    : y < 8 ? new Color32(227, 186, 101, 255) : new Color32(255, 253, 242, 255);
            }
        guideHandTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        guideHandTexture.name = "TutorialPointer";
        guideHandTexture.SetPixels32(pixels); guideHandTexture.Apply(false, true);
        guideHandSprite = Sprite.Create(guideHandTexture, new Rect(0, 0, size, size), new Vector2(.5f, .9f), 32);
    }

    private void DestroyGuideView()
    {
        if (guideRoot != null) Destroy(guideRoot.gameObject);
        if (guideHandSprite != null) Destroy(guideHandSprite);
        if (guideHandTexture != null) Destroy(guideHandTexture);
        guideRoot = guideCard = guideFocus = guideHand = null;
        guideCanvas = null; guideHandSprite = null; guideHandTexture = null;
    }
}
