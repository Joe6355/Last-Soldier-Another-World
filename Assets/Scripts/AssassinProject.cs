using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AssassinProject : MonoBehaviour
{
    public float speed = 5f;
    public float lifeTime = 5f;
    private Vector3 targetPosition;
    private bool isLaunched = false;
    public float damage = 5;
    public float waitforsec = 2f;
    private PlayerController playerController;
    private float reachDistance = 0.1f; // Пороговое значение для определения, что снаряд достиг цели

    // Новое поле: точка, которая поворачивается лицом к игроку при старте
    [Header("Rotation Settings")]
    [Tooltip("Точка (Transform), которая будет поворачиваться лицом к игроку при старте. Обычно дочерний пустой объект.")]
    [SerializeField] private Transform facePoint;

    private void Start()
    {
        // Находим игрока сразу, чтобы можно было использовать его для поворота
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            // Если задана точка для поворота, поворачиваем её лицом к игроку
            if (facePoint != null)
            {
                Vector3 direction = (playerObject.transform.position - facePoint.position).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                facePoint.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
        else
        {
            Debug.LogWarning("Игрок не найден! Проверьте тег 'Player'.");
        }

        // Запускаем подготовку и запуск снаряда
        StartCoroutine(PrepareAndLaunch());
        playerController = FindObjectOfType<PlayerController>();
    }

    private IEnumerator PrepareAndLaunch()
    {
        // Задержка перед запуском
        yield return new WaitForSeconds(waitforsec);

        // Запоминаем позицию игрока
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            targetPosition = playerObject.transform.position;
        }
        else
        {
            Destroy(gameObject); // Уничтожаем снаряд, если игрок не найден
            yield break;
        }

        isLaunched = true; // Отмечаем, что снаряд запущен
        Destroy(gameObject, lifeTime); // Уничтожаем снаряд через заданное время жизни
    }

    private void Update()
    {
        if (isLaunched)
        {
            // Двигаем снаряд к запомненной позиции игрока
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

            // Проверяем, достиг ли снаряд запомненной позиции игрока
            if (Vector3.Distance(transform.position, targetPosition) <= reachDistance)
            {
                Destroy(gameObject); // Уничтожаем снаряд при достижении цели
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D coll)
    {
        if (coll.gameObject.CompareTag("Player"))
        {
            // Если найден контроллер игрока, наносим урон
            if (playerController != null)
            {
                playerController.TakeDamage(damage);
            }
            Destroy(gameObject);
        }
    }
}
