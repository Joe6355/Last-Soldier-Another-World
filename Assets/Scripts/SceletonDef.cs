using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SceletonDef : Sounds
{
    [SerializeField] private int health = 10;
    private Transform player;            // Ссылка на Transform игрока (назначается в Start)
    private PlayerController playerController;

    [Header("Параметры агро и дистанций")]
    public float agrDist = 5f;           // Радиус, в котором враг «видит» игрока
    public float backDist = 1.5f;        // Дистанция, при которой враг начинает отступать
    public float optimalDist = 3f;       // Оптимальная дистанция (если ближе – враг стоит, если слишком близко – отступает)

    [Header("Скорость")]
    public float defSpeed = 2f;         // Скорость движения по умолчанию
    private float speed;

    [Header("Лут")]
    [SerializeField] private GameObject[] lootPrefabs;
    [SerializeField] private float dropChanceMin = 0.25f;
    [SerializeField] private float dropChanceMax = 0.50f;
    [SerializeField] private int lootDropCount = 3;

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sprite;

    public Transform frontPoint;         // Точка «перед лицом» для проверки направления

    private Stats stats;

    private void Start()
    {
        stats = FindObjectOfType<Stats>();

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();

        // Пробуем найти PlayerController (если есть на сцене)
        playerController = FindObjectOfType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("PlayerController не найден! " +
                             "Убедитесь, что объект игрока содержит скрипт PlayerController.");
        }

        // Пробуем найти игрока по тегу «Player»
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogWarning("Игрок не найден! Убедитесь, что объект игрока имеет тег 'Player'.");
        }
    }

    private void Update()
    {
        // Если игрок существует, поворачиваемся к нему и можно делать любую логику, не связанную с физикой
        if (player != null)
        {
            FacePlayerWithFlip();
        }
    }

    private void FixedUpdate()
    {
        // Основная логика перемещения в FixedUpdate (для физики)
        if (player != null)
        {
            MoveLogic();
        }
    }

    /// <summary>
    /// Считает дистанцию, определяет текущее состояние (бег, стоп, отступление) и двигает врага.
    /// </summary>
    private void MoveLogic()
    {
        float distance = Vector2.Distance(player.position, transform.position);

        if (distance > agrDist)
        {
            speed = 0;
            anim.SetTrigger("NoRun");
        }
        else if (distance <= agrDist && distance > optimalDist)
        {
            speed = defSpeed;
            // Было так: transform.position = Vector2.MoveTowards(transform.position, player.position, speed * Time.fixedDeltaTime);
            Vector3 currentPos = transform.position;
            Vector2 newPos2D = Vector2.MoveTowards(
                new Vector2(currentPos.x, currentPos.y),
                new Vector2(player.position.x, player.position.y),
                speed * Time.fixedDeltaTime
            );
            transform.position = new Vector3(newPos2D.x, newPos2D.y, currentPos.z);

            anim.SetTrigger("GoRun");
        }
        else if (distance <= optimalDist && distance >= backDist)
        {
            speed = 0;
            anim.SetTrigger("NoRun");
        }
        else if (distance < backDist)
        {
            speed = defSpeed;
            // Отступаем: значит считаем направление Away
            Vector3 currentPos = transform.position;
            Vector3 directionAway = (currentPos - player.position).normalized;

            // Используем MoveTowards только в 2D
            Vector2 newPos2D = Vector2.MoveTowards(
                new Vector2(currentPos.x, currentPos.y),
                new Vector2(currentPos.x + directionAway.x, currentPos.y + directionAway.y),
                speed * Time.fixedDeltaTime
            );

            // Возвращаем z
            transform.position = new Vector3(newPos2D.x, newPos2D.y, currentPos.z);

            anim.SetTrigger("GoRun");
        }
    }

    /// <summary>
    /// Поворот к игроку с учётом «мёртвой зоны».
    /// </summary>
    private void FacePlayerWithFlip()
    {
        float deadZone = 0.3f;
        Vector3 directionToPlayer = player.position - frontPoint.position;

        // Проверяем, что игрок не в «мёртвой зоне» по оси X
        if (Mathf.Abs(directionToPlayer.x) > deadZone)
        {
            // Если игрок слева, а враг смотрит вправо, разворачиваемся
            if (directionToPlayer.x < 0 && transform.localScale.x > 0)
            {
                transform.localScale = new Vector3(
                    -Mathf.Abs(transform.localScale.x),
                    transform.localScale.y,
                    transform.localScale.z
                );
            }
            // Если игрок справа, а враг смотрит влево, разворачиваемся
            else if (directionToPlayer.x > 0 && transform.localScale.x < 0)
            {
                transform.localScale = new Vector3(
                    Mathf.Abs(transform.localScale.x),
                    transform.localScale.y,
                    transform.localScale.z
                );
            }
        }
    }

    /// <summary>
    /// Метод для получения урона.
    /// Если стрела святая (isHolyArrow = true) и враг относится к «святым» врагам – враг получает урон. 
    /// Если враг не «святой» – наоборот, лечится. Иначе обычный урон.
    /// </summary>
    public void TakeDamage(int amount, bool isHolyArrow)
    {
        // Ссылка на скрипт ArrowDef (если он есть на сцене)
        ArrowDef arrowDef = FindObjectOfType<ArrowDef>();

        if (isHolyArrow && arrowDef != null)
        {
            // Если у врага тег из списка "Holy", наносим урон
            if (arrowDef.HolyEnemyTags.Contains(gameObject.tag))
            {
                health -= amount;
            }
            // Если тег из обычных врагов, лечим
            else if (arrowDef.EnemyTags.Contains(gameObject.tag))
            {
                Heal(amount);
                
            }
        }
        else
        {
            // Обычный урон
            health -= amount;
            PlaySound(sounds[0], volume: 1, destroyed: false);
        }

        // Эффект «полупрозрачности» на короткое время
        SetTransparency(0.5f);
        Invoke(nameof(ResetTransparency), 0.1f);

        // Если здоровье упало до 0 или ниже, умираем
        if (health <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Применение яда (корутин с периодическим уроном).
    /// </summary>
    public void ApplyPoison(int poisonDamagePerTick, int poisonTicks, float tickInterval)
    {
        StartCoroutine(ApplyPoisonDamage(poisonDamagePerTick, poisonTicks, tickInterval));
    }

    private IEnumerator ApplyPoisonDamage(int poisonDamagePerTick, int poisonTicks, float tickInterval)
    {
        for (int i = 0; i < poisonTicks; i++)
        {
            if (health > 0)
            {
                TakeDamage(poisonDamagePerTick, false);
                yield return new WaitForSeconds(tickInterval);
            }
            else
            {
                // Если уже умер, прерываем корутину
                break;
            }
        }
    }

    /// <summary>
    /// Лечение.
    /// </summary>
    public void Heal(int amount)
    {
        health += amount;
    }

    /// <summary>
    /// Смерть врага: дроп лута и уничтожение объекта.
    /// </summary>
    private void Die()
    {
        stats.countEnemyDead++;
        stats.UpdateUI();

        DropLoot();
        Destroy(gameObject);
    }

    /// <summary>
    /// Логика дропа лута.
    /// </summary>
    private void DropLoot()
    {
        for (int i = 0; i < lootDropCount; i++)
        {
            float dropChance = Random.Range(dropChanceMin, dropChanceMax);
            if (Random.value <= dropChance)
            {
                GameObject loot = lootPrefabs[Random.Range(0, lootPrefabs.Length)];
                Instantiate(loot, transform.position, Quaternion.identity);
            }
        }
    }

    /// <summary>
    /// Установить прозрачность спрайта.
    /// </summary>
    private void SetTransparency(float alpha)
    {
        Color originalColor = sprite.color;
        sprite.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
    }

    /// <summary>
    /// Восстановить нормальную прозрачность спрайта.
    /// </summary>
    private void ResetTransparency()
    {
        Color originalColor = sprite.color;
        sprite.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
    }
}
