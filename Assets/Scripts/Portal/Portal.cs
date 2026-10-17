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
        label.color = new Color32(255, 228, 153, 255);
        label.outlineWidth = .15f;
        label.rectTransform.sizeDelta = new Vector2(3.6f, .7f);
        label.GetComponent<MeshRenderer>().sortingOrder = 20;
        GameLocalization.Register(label);
    }

    private void OnTriggerEnter2D(Collider2D other) => TryTeleport(other);
    private void OnTriggerStay2D(Collider2D other)
    {
        // The player may already be in the exit when the return exercise unlocks.
        if (TipsManager.IsGuideActive) TryTeleport(other);
    }
    private void TryTeleport(Collider2D other)
    {
        // Если мы недавно телепортировались, ждём кулдаун
        if (linkedPortal == null || !TipsManager.AllowsPortal(this)
            || YG.YG2.isPauseGame || Time.timeScale == 0f || Time.time - lastTeleportTime < teleportCooldown)
            return;

        // Если вошёл игрок (с тегом "Player")...
        if (other.CompareTag("Player"))
        {
            // Телепортируем в позицию "linkedPortal" + офсет
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