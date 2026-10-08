using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SceletMag : Sounds
{
    private bool deathHandled;
    [SerializeField] private int health;
    [SerializeField] private float agrDist = 6f;
    [SerializeField] private float backDist = 1.5f;
    [SerializeField] private float defSpeed = 2f;
    [SerializeField] private float optimalDist = 3.0f;

    // Позиции для атаки (масса точек, куда спавнить снаряды)
    [SerializeField] private Transform[] firePositions;

    // Два разных префаба снарядов
    [SerializeField] private GameObject projectilePrefab1;
    [SerializeField] private GameObject projectilePrefab2;

    // Параметры атаки
    public float attackCooldown = 3.0f;
    public float lastAttackTime = 0;

    // Лут
    [SerializeField] private GameObject[] lootPrefabs;
    [SerializeField] private float dropChanceMin = 0.25f;
    [SerializeField] private float dropChanceMax = 0.50f;
    [SerializeField] private int lootDropCount = 3;

    private Transform player;            // Ссылка на игрока
    private PlayerController playerController;
    private float speed;
    private float distantion;            // Дистанция до игрока

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sprite;

    // Точка «перед лицом» для определения направления взгляда
    public Transform frontPoint;

    private Stats stats;
    private void Start()
    {
        stats = FindObjectOfType<Stats>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();

        // Сначала пробуем найти объект с тегом Player
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
        {
            player = found.transform;
            // Попробуем получить PlayerController (если вдруг нужно)
            playerController = found.GetComponent<PlayerController>();
            if (playerController == null)
            {
                Debug.LogWarning("Player найден, но PlayerController на нём отсутствует!");
            }
        }
        else
        {
            Debug.LogWarning("Игрок не найден! Убедитесь, что объект игрока имеет тег 'Player'.");
        }

        speed = defSpeed;
    }

    private void FixedUpdate()
    {
        // Работать с дистанцией, движением и атакой только если player != null
        if (player != null)
        {
            Distantion();
            FacePlayerWithFlip();

            // Проверяем возможность атаки
            if (distantion <= agrDist && Time.time >= lastAttackTime + attackCooldown)
            {
                Attack();
                lastAttackTime = Time.time;
            }
        }
    }

    /// <summary>
    /// Определяет дистанцию до игрока и решает, что делать (стоять, бежать, отступать).
    /// </summary>
    private void Distantion()
    {
        // Считаем расстояние между врагом и игроком
        distantion = Vector2.Distance(player.position, transform.position);

        // Логика перемещения
        if (distantion > agrDist)
        {
            // Игрок вне агро-дистанции
            speed = 0;
            anim.SetTrigger("Ide");
        }
        else if (distantion <= agrDist && distantion > optimalDist)
        {
            // Двигаемся к игроку
            MoveTowardsPlayer();
            speed = defSpeed;
            anim.SetTrigger("GoRun");
        }
        else if (distantion <= optimalDist && distantion >= backDist)
        {
            // Стоим на месте в «оптимальной» зоне
            speed = 0;
            anim.SetTrigger("Ide");
        }
        else if (distantion < backDist)
        {
            // Отступаем, если слишком близко
            RetreatFromPlayer();
            speed = defSpeed;
            anim.SetTrigger("GoRun");
        }
    }

    /// <summary>
    /// Движение к игроку с сохранением Z-составляющей.
    /// </summary>
    private void MoveTowardsPlayer()
    {
        Vector3 currentPos = transform.position; // Текущая 3D позиция
        Vector2 newPos2D = Vector2.MoveTowards(
            new Vector2(currentPos.x, currentPos.y),
            new Vector2(player.position.x, player.position.y),
            speed * Time.deltaTime
        );
        transform.position = new Vector3(newPos2D.x, newPos2D.y, currentPos.z);
    }

    /// <summary>
    /// Отступление от игрока (если враг слишком близко).
    /// </summary>
    private void RetreatFromPlayer()
    {
        Vector3 currentPos = transform.position;
        Vector3 directionAway = (currentPos - player.position).normalized;
        // directionAway.x / .y / .z

        // Целевая точка – текущая позиция + направление от игрока
        Vector3 targetPos = currentPos + directionAway;

        // Двигаемся только по X/Y, Z не трогаем
        Vector2 newPos2D = Vector2.MoveTowards(
            new Vector2(currentPos.x, currentPos.y),
            new Vector2(targetPos.x, targetPos.y),
            speed * Time.deltaTime
        );
        transform.position = new Vector3(newPos2D.x, newPos2D.y, currentPos.z);
    }

    /// <summary>
    /// Атакуем (стреляем двумя типами снарядов из двух случайных позиций).
    /// </summary>
    private void Attack()
    {
        // Выбираем две случайные позиции
        List<Transform> selectedPositions = firePositions
            .OrderBy(x => Random.value)
            .Take(2)
            .ToList();

        // Спавним первый снаряд на первой позиции
        Instantiate(projectilePrefab1, selectedPositions[0].position, Quaternion.identity);

        // В момент атаки можем сбрасывать скорость, чтобы враг стоял
        speed = 0;
        anim.SetTrigger("GoAt");

        // Спавним второй снаряд на второй позиции
        Instantiate(projectilePrefab2, selectedPositions[1].position, Quaternion.identity);
        PlaySound(sounds[1], volume: 1, destroyed: true);
    }

    /// <summary>
    /// Разворачиваемся лицом к игроку (флип по X) с учётом «мёртвой зоны» по оси X.
    /// </summary>
    private void FacePlayerWithFlip()
    {
        float deadZone = 0.3f;
        Vector3 directionToPlayer = player.position - frontPoint.position;

        // Если игрок существенно левее/правее (больше deadZone)
        if (Mathf.Abs(directionToPlayer.x) > deadZone)
        {
            // Игрок слева, а враг смотрит вправо => флип
            if (directionToPlayer.x < 0 && transform.localScale.x > 0)
            {
                transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
            // Игрок справа, а враг смотрит влево => флип
            else if (directionToPlayer.x > 0 && transform.localScale.x < 0)
            {
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
        }
    }

    /// <summary>
    /// Становимся полупрозрачными на короткое время (например при попадании).
    /// </summary>
    private void SetTransparence(float alpha)
    {
        Color originalColor = sprite.color;
        sprite.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
    }

    /// <summary>
    /// Восстанавливаем непрозрачность.
    /// </summary>
    private void ResetTransparency()
    {
        Color originalColor = sprite.color;
        sprite.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
    }

    /// <summary>
    /// Запуск корутины ядовитого урона.
    /// </summary>
    public void ApplyPoison(int poisonDamagePerTick, int poisonTicks, float tickInterval)
    {
        StartCoroutine(ApplyPoisonDamage(poisonDamagePerTick, poisonTicks, tickInterval));
    }

    /// <summary>
    /// Периодический урон от яда.
    /// </summary>
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
                // Если враг уже умер - выход
                break;
            }
        }
    }

    /// <summary>
    /// Получение урона (с учётом святой стрелы).
    /// </summary>
    public void TakeDamage(int amount, bool isHolyArrow)
    {
        double previousHealth = health;
        if (deathHandled) return;
        ArrowDef arrowDef = FindObjectOfType<ArrowDef>();

        if (isHolyArrow && arrowDef != null)
        {
            // Если у врага тег из списка "Holy", наносим урон
            if (arrowDef.HolyEnemyTags.Contains(gameObject.tag))
            {
                health -= amount;
            }
            // Если враг обычный (Enemy), лечим
            else if (arrowDef.EnemyTags.Contains(gameObject.tag))
            {
                Heal(amount);
            }
        }
        else
        {
            // Обычный урон
            health -= amount;
            PlaySound(sounds[0], volume: 1, destroyed: true);
        }

        // Визуальный эффект попадания
        DamageNumbers.Show(transform, previousHealth, health);
        SetTransparence(0.5f);
        Invoke(nameof(ResetTransparency), 0.1f);

        // Проверяем смерть
        if (health <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Лечение врага (если выстрел не по тому типу).
    /// </summary>
    public void Heal(int amount)
    {
        health += amount;
    }

    /// <summary>
    /// Смерть: дроп лута и уничтожение объекта.
    /// </summary>
    private void Die()
    {
        if (deathHandled) return;
        deathHandled = true;
        stats.countElitEnemyDead++;
        stats.SaveInfo();
        stats.UpdateUI();
        DropLoot();
        Destroy(gameObject);
    }

    /// <summary>
    /// Дроп лута (случайно, в заданном количестве).
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
}
