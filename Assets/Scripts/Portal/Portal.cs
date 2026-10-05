using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Portal : Sounds
{
    [Tooltip("Ссылка на другой портал, куда переносим игрока.")]
    public Transform linkedPortal;

    [Header("Настройки кулдауна")]
    [Tooltip("Задержка (в секундах) перед повторным телепортом.")]
    public float teleportCooldown = 1f;

    private float lastTeleportTime; // Когда последний раз телепортировались

    [Header("Смещение после телепорта")]
    [Tooltip("На сколько сместить игрока относительно центра linkedPortal.")]
    public Vector2 offset = new Vector2(5f, 0f);

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Если мы недавно телепортировались, ждём кулдаун
        if (Time.time - lastTeleportTime < teleportCooldown)
            return;

        // Если вошёл игрок (с тегом "Player")...
        if (other.CompareTag("Player"))
        {
            // Телепортируем в позицию "linkedPortal" + офсет
            Vector3 newPosition = linkedPortal.position + (Vector3)offset;
            other.transform.position = newPosition;

            // Запоминаем время, когда телепорт произошёл
            lastTeleportTime = Time.time;
        }
    }
}