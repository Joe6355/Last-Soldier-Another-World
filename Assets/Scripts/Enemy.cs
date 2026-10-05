using System.Linq;
using UnityEngine;
using System.Collections;

public class Enemy : Sounds
{
    [Header("Параметры врага")]
    public int health = 5;           // Здоровье врага
    public int damageTouch = 3;      // Урон при столкновении

    [Header("Дистанция и скорость")]
    public float agrDistantion = 10; // Дистанция агро
    public float defSpeed = 5;       // Скорость по умолчанию

    [Header("Ссылки на объекты")]
    public SpriteRenderer sprite;
    public Transform frontPoint;     // Точка «перед лицом»
    private Transform player;        // Ссылка на игрока
    private PlayerController playerController;
    private Animator anim;

    private float distantion;
    private float speed;

    [Header("Лут")]
    [SerializeField] private GameObject[] lootPrefabs;
    [SerializeField] private float dropChanceMin = 0.25f;
    [SerializeField] private float dropChanceMax = 0.50f;
    [SerializeField] private int lootDropCount = 3;

    private Stats stats;    
    private void Start()
    {
        stats = FindObjectOfType<Stats>();
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();

        // Попытка найти игрока
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
        {
            player = found.transform;
            // Если нужен скрипт PlayerController:
            playerController = found.GetComponent<PlayerController>();
            if (playerController == null)
            {
                Debug.LogWarning("Игрок найден, но PlayerController на нём отсутствует!");
            }
        }
        else
        {
            Debug.LogWarning("Игрок не найден! Убедитесь, что объект игрока имеет тег 'Player'.");
        }
    }

    private void FixedUpdate()
    {
        // Если игрок найден, выполняем логику движения и флипа
        if (player != null)
        {
            Distantion();
            FacePlayerWithFlip();
        }
    }

    /// <summary>
    /// Определяем дистанцию до игрока и двигаем врага, если в зоне агро.
    /// </summary>
    private void Distantion()
    {
        distantion = Vector2.Distance(player.position, transform.position);

        if (distantion < agrDistantion)
        {
            // Игрок в зоне агро — идём к нему
            speed = defSpeed;

            // Сохраняем координату Z
            Vector3 currentPos = transform.position;
            // Считаем новую позицию в 2D
            Vector2 newPos2D = Vector2.MoveTowards(
                new Vector2(currentPos.x, currentPos.y),
                new Vector2(player.position.x, player.position.y),
                speed * Time.deltaTime
            );
            // Присваиваем, оставляя z-координату без изменений
            transform.position = new Vector3(newPos2D.x, newPos2D.y, currentPos.z);

            anim.SetTrigger("GoRun");
        }
        else
        {
            // Игрок вне зоны агро — стоим
            speed = 0;
            anim.SetTrigger("NoRun");
        }
    }

    /// <summary>
    /// Поворот врага к игроку (flipX).
    /// </summary>
    private void FacePlayerWithFlip()
    {
        // Определяем направление к игроку
        Vector3 directionToPlayer = player.position - frontPoint.position;

        // Если игрок слева, зеркалим
        if (directionToPlayer.x < 0)
        {
            sprite.flipX = true;
        }
        else
        {
            sprite.flipX = false;
        }
    }

    /// <summary>
    /// Устанавливаем полупрозрачность (визуальный эффект при попадании).
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
    /// Применение яда (корутин).
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
                break;
            }
        }
    }

    /// <summary>
    /// Получение урона (учёт «святой стрелы»).
    /// </summary>
    public void TakeDamage(int amount, bool isHolyArrow)
    {
        ArrowDef arrowDef = FindObjectOfType<ArrowDef>();

        if (arrowDef == null)
        {
            // Если ArrowDef отсутствует, считаем обычный урон
            health -= amount;
        }
        else
        {
            // Если стрела святая
            if (isHolyArrow)
            {
                // Если у врага тег из списка Holy, наносим урон
                if (arrowDef.HolyEnemyTags.Contains(gameObject.tag))
                {
                    health -= amount;
                }
                // Если враг не святой, восстанавливаем здоровье
                else if (arrowDef.EnemyTags.Contains(gameObject.tag))
                {
                    Heal(amount);
                }
            }
            else
            {
                // Обычный урон
                health -= amount;
            }
        }

        // Эффект попадания (мигаем полупрозрачным)
        SetTransparence(0.5f);
        Invoke(nameof(ResetTransparency), 0.1f);

        // Проверяем, не умер ли
        if (health <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Лечение врага.
    /// </summary>
    public void Heal(int amount)
    {
        health += amount;
    }

    /// <summary>
    /// Смерть врага (дроп лута и уничтожение объекта).
    /// </summary>
    private void Die()
    {
        stats.countEnemyDead++;
        stats.SaveInfo();
        stats.UpdateUI();
        DropLoot();
        Destroy(gameObject);
    }

    /// <summary>
    /// При столкновении с игроком наносим урон игроку, затем враг умирает.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D coll)
    {
        if (coll.gameObject.CompareTag("Player"))
        {

            //playerController.TakeDamage(damageTouch);
            // Если есть PlayerController, нанесём урон
            if (playerController != null)
            {
                playerController.TakeDamage(damageTouch);
                PlaySound(sounds[0], volume: 1, destroyed: true);
            }
            
            // После нанесения урона убиваем врага
            Die();
        }
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
                Debug.Log("Лут выпал: " + loot.name);
            }
            else
            {
                Debug.Log("Лут не выпал.");
            }
        }
    }
}
