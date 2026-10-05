using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Npc : Sounds
{
    [Header("Waypoints и скорость")]
    [SerializeField] private Transform[] waypoints;     // Точки, между которыми ходит NPC
    [SerializeField] private float walkSpeed = 2f;      // Скорость ходьбы
    [SerializeField] private float stopDistance = 0.1f; // На каком расстоянии считаем, что дошли

    [Header("Анимации и паузы")]
    [SerializeField] private Animator animator;
    [Tooltip("Триггер для анимации ходьбы")]
    [SerializeField] private string walkTrigger = "Walk";
    [Tooltip("Триггер для анимации Idle")]
    [SerializeField] private string idleTrigger = "Ide";
    [Tooltip("Триггер для анимации при контакте с игроком")]
    [SerializeField] private string playerTrigger = "PlayerAnim";

    [Tooltip("Сколько секунд NPC стоит в Idle между перемещениями")]
    [SerializeField] private float idleTime = 2f;

    [Header("Спрайт для зеркалирования")]
    [SerializeField] private SpriteRenderer spriteRenderer; // Присвойте SpriteRenderer NPC

    private int currentWaypointIndex = 0;  // Текущий индекс в массиве waypoints
    private bool isPlayerInRange = false;  // Флаг: игрок в триггере NPC или нет

    // Флаг, что звук уже проиграли (чтобы не повторять при повторном входе)
 

    private void Start()
    {
        if (waypoints.Length == 0)
        {
            Debug.LogWarning("Не заданы waypoints для NPC!");
            return;
        }

        StartCoroutine(PatrolRoutine());
    }

    private IEnumerator PatrolRoutine()
    {
        while (true)
        {
            // 1. Включаем анимацию ходьбы, т.к. начинаем движение
            if (animator != null && !string.IsNullOrEmpty(walkTrigger))
            {
                animator.SetTrigger(walkTrigger);
            }

            // 2. Двигаемся к очередной точке, пока:
            //    - не достигли нужной дистанции, И
            //    - игрок не в радиусе (isPlayerInRange == false)
            while (!isPlayerInRange &&
                   Vector2.Distance(transform.position, waypoints[currentWaypointIndex].position) > stopDistance)
            {
                MoveTowards(waypoints[currentWaypointIndex].position);
                yield return null;
            }

            // В этот момент либо дошли до точки, либо обнаружили игрока

            // 3. Если игрок обнаружен, проигрываем "особую" анимацию
            if (isPlayerInRange)
            {
                if (animator != null && !string.IsNullOrEmpty(playerTrigger))
                {
                    animator.SetTrigger(playerTrigger);
                }

                // Ждём, пока игрок не выйдет из триггера
                while (isPlayerInRange)
                {
                    yield return null;
                }
                // Когда игрок вышел, цикл пойдёт дальше — перейдём к перемещению
            }
            else
            {
                // 4. Дошли до точки — запускаем Idle анимацию
                if (animator != null && !string.IsNullOrEmpty(idleTrigger))
                {
                    animator.SetTrigger(idleTrigger);
                }

                // Ждём несколько секунд на месте
                yield return new WaitForSeconds(idleTime);

                // 5. Переходим к следующему waypoint
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            }
        }
    }

    // Функция для движения на каждом кадре
    private void MoveTowards(Vector2 targetPosition)
    {
        // 1) Находим направление
        Vector2 direction = targetPosition - (Vector2)transform.position;

        // 2) Функция FlipSprite для зеркального разворота
        FlipSprite(direction.x);

        // 3) Считаем новую позицию в 2D (x, y)
        Vector2 newPos2D = Vector2.MoveTowards(
            (Vector2)transform.position, // Текущая позиция в 2D
            targetPosition,              // Цель
            walkSpeed * Time.deltaTime   // Скорость
        );

        // 4) Присваиваем новую позицию, но оставляем старый Z
        transform.position = new Vector3(
            newPos2D.x,
            newPos2D.y,
            transform.position.z
            );
    }

    /// <summary>
    /// Функция для зеркального разворота спрайта по горизонтали.
    /// </summary>
    /// <param name="xDir">Направление по X (может быть > 0, < 0, или = 0)</param>
    private void FlipSprite(float xDir)
    {
        if (xDir > 0f)
        {
            spriteRenderer.flipX = true;
        }
        else if (xDir < 0f)
        {
            spriteRenderer.flipX = false;
        }
        // Если xDir = 0, оставляем текущее значение flipX
    }

    // Когда игрок заходит в триггер (IsTrigger)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = true;
            PlaySound(sounds[0], volume: 1f, destroyed: false);

        }
    }

    // Когда игрок выходит из триггера
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = false;
        }
    }

    // --- Ниже методы, которые уже были в скрипте. ---
    public GameObject qust;
    public GameObject songs;

    public void Sooongs()
    {
        songs.SetActive(true);
        qust.SetActive(false);
    }

    public void Qustions()
    {
        //PlaySound(sounds[0], volume: 1f, destroyed: false);
        qust.SetActive(true);
        songs.SetActive(false);
    }
}