using UnityEngine;
using System.Collections;

public class Animals : Sounds
{
    [Header("Настройки точек и скорости")]
    [SerializeField] private Transform[] waypoints; // Точки, к которым животное будет двигаться
    [SerializeField] private float moveSpeed = 2f; // Скорость движения

    [Header("Ссылки на аниматор и прочее")]
    [SerializeField] private Animator animator; // Аниматор для переключения анимаций
    [SerializeField] private Transform spriteTransform; // Трансформ спрайта для изменения масштаба

    [Header("Прочие настройки")]
    [SerializeField] private float stopDistance = 0.1f; // Расстояние остановки
    [SerializeField] private float waitAfterEat = 1f; // Задержка после еды

    private Transform currentTarget; // Текущая цель
    private Coroutine stopSoundCoroutine; // Корутину для остановки звука
    private bool isEating = false; // Флаг состояния "Еда"

    private void Start()
    {
        if (waypoints.Length > 0)
        {
            currentTarget = waypoints[Random.Range(0, waypoints.Length)];
            StartCoroutine(MoveAndEatRoutine());
        }
        else
        {
            Debug.LogWarning("Не заданы точки для перемещения!");
        }
    }

    private IEnumerator MoveAndEatRoutine()
    {
        while (true)
        {
            // Движение к точке
            while (Vector2.Distance(transform.position, currentTarget.position) > stopDistance)
            {
                if (!isEating)
                {
                    animator.SetTrigger("Walk"); // Анимация ходьбы
                }

                Vector2 direction = currentTarget.position - transform.position;

                // Зеркалим спрайт в зависимости от направления
                FlipSprite(direction.x);

                // Двигаем объект
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    new Vector3(currentTarget.position.x, currentTarget.position.y, transform.position.z),
                    moveSpeed * Time.deltaTime
                );

                yield return null; // Ждём до следующего кадра
            }

            // Останавливаемся у точки
            animator.SetTrigger("Idle"); // Анимация ожидания
            yield return new WaitForSeconds(0.5f); // Короткая пауза перед едой

            // Переходим в состояние "Еда"
            isEating = true;
            animator.SetTrigger("Eat");
            yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length + waitAfterEat);

            // Выбираем новую точку
            if (waypoints.Length > 0)
            {
                currentTarget = waypoints[Random.Range(0, waypoints.Length)];
            }

            isEating = false; // Сбрасываем состояние
        }
    }

    private void FlipSprite(float directionX)
    {
        if (directionX < 0)
        {
            spriteTransform.localScale = new Vector3(1, 1, 1); // Обычный масштаб
        }
        else if (directionX > 0)
        {
            spriteTransform.localScale = new Vector3(-1, 1, 1); // Отзеркаливание по X
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (stopSoundCoroutine != null)
            {
                StopCoroutine(stopSoundCoroutine); // Останавливаем предыдущую корутину остановки звука
                stopSoundCoroutine = null;
            }

            // Проигрываем звук
            PlaySound(sounds[0], volume: 0.5f, destroyed: false);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // Останавливаем звук с задержкой
            stopSoundCoroutine = StartCoroutine(StopSoundAfterDelay(1f));
        }
    }

    private IEnumerator StopSoundAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (audioSrc != null)
        {
            audioSrc.Stop();
        }
    }
}
