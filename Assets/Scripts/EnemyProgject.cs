using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyProgject : MonoBehaviour
{
    public float speed = 5f;
    public float lifeTime = 5f;
    private Vector3 targetPosition;
    private bool isLaunched = false;
    public float damage = 5;
    public float waitforsec = 2f;

    private PlayerController playerController;
    private float reachDistance = 0.1f; // Пороговое значение для определения, что снаряд достиг цели

    private void Start()
    {
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

            // Проверяем, достиг ли снаряд запомненной позиции игрока, но не столкнулся с ним
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
            playerController.TakeDamage(damage);
            Destroy(gameObject);

        }
    }

}
