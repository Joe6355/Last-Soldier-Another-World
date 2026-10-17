using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Portal : Sounds
{
    [Tooltip("Ссылка на другой портал, куда переносим игрока.")]
    public Transform linkedPortal;

    [Header("Настройки кулдауна")]
    [Tooltip("Задержка (в секундах) перед повторным телепортом.")]
    public float teleportCooldown = 1f;

    private float lastTeleportTime = float.NegativeInfinity; // Когда последний раз телепортировались

    [Header("Смещение после телепорта")]
    [Tooltip("На сколько сместить игрока относительно центра linkedPortal.")]
    public Vector2 offset = new Vector2(5f, 0f);

    [SerializeField] private string destinationLabel;
    [SerializeField] private TMP_FontAsset labelFont;

    [SerializeField, Min(0)] private int requiredMmr;
    [SerializeField] private WaveSpawner destinationArena;
    [SerializeField] private WaveSpawner originArena;
    [SerializeField] private Color labelColor = new Color32(255, 228, 153, 255);
    private TextMeshPro portalLabel;
    private bool lastUnlocked;
    public bool IsUnlocked => requiredMmr == 0 || (GameProgress.IsReady && WaveSpawner.CurrentMmr >= requiredMmr);
    private void Update()
    {
        if (portalLabel == null || requiredMmr == 0 || lastUnlocked == IsUnlocked) return;
        RefreshLabel();
    }
    private void RefreshLabel()
    {
        lastUnlocked = IsUnlocked;
        if (portalLabel == null) return;
        portalLabel.text = GameLocalization.Text(destinationLabel)
            + (lastUnlocked ? "" : "\n" + GameLocalization.Text("Требуется {0} MMR").Replace("{0}", requiredMmr.ToString("N0")));
        portalLabel.color = lastUnlocked ? labelColor : new Color32(220, 135, 135, 255);
    }
    private void OnEnable() => GameLocalization.Changed += RefreshLabel;
    private void OnDisable() => GameLocalization.Changed -= RefreshLabel;

    private void Start()
    {
        if (string.IsNullOrEmpty(destinationLabel)) return;
        var labelObject = new GameObject("DestinationLabel", typeof(TextMeshPro));
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0, .8f, -.1f);
        var label = labelObject.GetComponent<TextMeshPro>();
        if (labelFont != null) label.font = labelFont;
        label.text = destinationLabel; label.fontSize = 3;
        label.alignment = TextAlignmentOptions.Center;
        label.color = labelColor;
        label.outlineWidth = .15f;
        label.rectTransform.sizeDelta = new Vector2(3.6f, .7f);
        label.GetComponent<MeshRenderer>().sortingOrder = 20;
        portalLabel = label;
        GameLocalization.Protect(label);
        RefreshLabel();
    }

    private void OnTriggerEnter2D(Collider2D other) => TryTeleport(other);
    private void OnTriggerStay2D(Collider2D other)
    {
        // The player may already be in the exit when the return exercise unlocks.
        if (TipsManager.IsGuideActive || requiredMmr > 0) TryTeleport(other);
    }
    private void TryTeleport(Collider2D other)
    {
        // Если мы недавно телепортировались, ждём кулдаун
        if (linkedPortal == null || !IsUnlocked || !TipsManager.AllowsPortal(this)
            || YG.YG2.isPauseGame || Time.timeScale == 0f || Time.time - lastTeleportTime < teleportCooldown)
            return;

        // Если вошёл игрок (с тегом "Player")...
        if (other.CompareTag("Player"))
        {
            // Телепортируем в позицию "linkedPortal" + офсет
            if (originArena != null) originArena.AbandonRun();
            if (destinationArena != null && WaveSpawner.ActiveArena != null && WaveSpawner.ActiveArena != destinationArena)
                WaveSpawner.ActiveArena.AbandonRun();
            Vector3 newPosition = linkedPortal.position + (Vector3)offset;
            Vector3 delta = newPosition - other.transform.position;
            other.transform.position = newPosition;
            var body = other.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = newPosition; body.velocity = Vector2.zero; }
            var camera = FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();
            if (camera != null)
            {
                camera.OnTargetObjectWarped(other.transform, delta);
                camera.PreviousStateIsValid = false;
            }
            TipsManager.ReportPortal(this);
            // Play on the traveller after the camera warp.
            if (sounds != null && sounds.Length > 1)
                other.GetComponent<PlayerController>()?.PlaySound(sounds[1], .8f);

            // Запоминаем время, когда телепорт произошёл
            lastTeleportTime = Time.time;
        }
    }
}