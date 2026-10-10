using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Each control owns one finger; dragging another control cannot release it.
public class TouchControl : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum Kind { Move, Aim, Hold, Toggle, Layout }
    [SerializeField] private Kind kind;
    [SerializeField] private RectTransform thumb;
    [SerializeField] private RectTransform baseVisual;
    [SerializeField] private MobileControls layoutOwner;
    [SerializeField] private bool floating;
    [SerializeField, Min(1f)] private float joystickRadius = 64f;
    [SerializeField, Range(0f, .5f)] private float deadZone = .15f;
    private int pointerId = int.MinValue;
    private bool pressQueued, dragged, layoutDrag, queuedManualAim;
    private Vector2 origin, queuedDirection, basePosition, thumbPosition;
    private float pressedAt;
    public bool IsHeld => pointerId != int.MinValue;
    public Vector2 Value { get; private set; }
    public Vector2 LastDirection { get; private set; } = Vector2.down;
    public bool IsManualAim => kind == Kind.Aim && Value.sqrMagnitude > 0f;
    public bool HasQueuedPress => pressQueued;
    public bool QueuedManualAim => queuedManualAim;
    public Vector2 QueuedDirection => queuedDirection;
    public bool IsRepeating => kind == Kind.Aim && IsHeld && !layoutDrag && layoutOwner != null
        && layoutOwner.AutomaticSelected && Time.unscaledTime - pressedAt >= .18f;

    private void Awake()
    {
        if (baseVisual != null) basePosition = baseVisual.anchoredPosition;
        if (thumb != null) thumbPosition = thumb.anchoredPosition;
        if (kind == Kind.Aim && thumb != null) thumb.gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (!isActiveAndEnabled || IsHeld || data.button != PointerEventData.InputButton.Left) return;
        if (layoutOwner != null && layoutOwner.IsEditingLayout)
        {
            if (!layoutOwner.BeginControlDrag(transform, data.position)) return;
            pointerId = data.pointerId; layoutDrag = true;
            return;
        }
        var selectable = GetComponent<Selectable>();
        if (selectable != null && !selectable.IsInteractable()) return;
        if (kind == Kind.Layout || layoutOwner != null && !layoutOwner.CanControl) return;
        pointerId = data.pointerId; dragged = false; pressedAt = Time.unscaledTime;
        if (kind == Kind.Aim && thumb != null) thumb.gameObject.SetActive(true);
        if (kind == Kind.Move || kind == Kind.Aim)
        {
            if (!LocalPoint(data, out var point)) { ResetControl(); return; }
            origin = floating || kind == Kind.Aim ? point : ((RectTransform)transform).rect.center;
            if (floating && baseVisual != null) baseVisual.anchoredPosition = origin;
            UpdateValue(data);
        }
    }
    public void OnDrag(PointerEventData data)
    {
        if (data.pointerId != pointerId || !isActiveAndEnabled) return;
        if (layoutDrag) layoutOwner.DragControl(data.position);
        else UpdateValue(data);
    }
    public void OnPointerUp(PointerEventData data)
    {
        if (data.pointerId != pointerId) return;
        if (layoutDrag)
        {
            layoutOwner.EndControlDrag(); Release(true); return;
        }
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).fingerId == pointerId && Input.GetTouch(i).phase == TouchPhase.Canceled) { ResetControl(); return; }
        if (kind == Kind.Aim)
        {
            UpdateValue(data);
            // Returning a dragged aim to its origin cancels the shot.
            if (!IsRepeating && (!dragged || IsManualAim))
            {
                queuedManualAim = IsManualAim;
                queuedDirection = LastDirection;
                pressQueued = true;
            }
        }
        else if (kind == Kind.Toggle) pressQueued = true;
        Release(false);
    }
    private bool LocalPoint(PointerEventData data, out Vector2 point) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
            data.position, data.pressEventCamera, out point);

    private void UpdateValue(PointerEventData data)
    {
        if (kind != Kind.Move && kind != Kind.Aim || !LocalPoint(data, out var local)) return;
        var raw = Vector2.ClampMagnitude((local - origin) / joystickRadius, 1f);
        Value = raw.magnitude <= deadZone ? Vector2.zero : raw;
        if (Value != Vector2.zero)
        {
            LastDirection = Value.normalized;
            dragged = true;
        }
        if (thumb != null) thumb.anchoredPosition = (floating ? origin : thumbPosition) + raw * joystickRadius * .58f;
    }
    public bool ConsumePress()
    {
        bool result = pressQueued; pressQueued = false; return result;
    }
    public void ResetControl() => Release(true);
    private void Release(bool cancelPress)
    {
        pointerId = int.MinValue; Value = Vector2.zero; layoutDrag = false;
        if (cancelPress) pressQueued = false;
        if (thumb != null) thumb.anchoredPosition = thumbPosition;
        if (kind == Kind.Aim && thumb != null) thumb.gameObject.SetActive(false);
        if (baseVisual != null) baseVisual.anchoredPosition = basePosition;
    }
    private void OnDisable() => ResetControl();
}
