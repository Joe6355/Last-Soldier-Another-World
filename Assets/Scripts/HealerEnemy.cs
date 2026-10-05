using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
public class HealerEnemy : Sounds
{
    [Header("=== ПАРАМЕТРЫ ЗДОРОВЬЯ ===")]
    [SerializeField] private int health = 10;

    [Header("=== ЛОГИКА ДВИЖЕНИЯ И АГРА ===")]
    public float agrDist = 5f;           // Радиус "видит игрока"
    public float backDist = 1.5f;        // Дистанция отступления
    public float optimalDist = 3f;       // Оптимальная дистанция
    public float defSpeed = 2f;         // Скорость движения
    private float speed;

    [Header("=== ЛЕЧЕНИЕ / УРОН ===")]
    [Tooltip("Как часто (в секундах) происходит цикл хила?")]
    public float healCooldown = 8f;
    [Tooltip("Сколько восстанавливаем союзникам?")]
    public int healAmount = 3;
    [Tooltip("Сколько урона наносим игроку, если он в зоне?")]
    public int damageToPlayer = 2;

    [Header("=== ПРЕДУПРЕЖДЕНИЕ ПЕРЕД ХИЛОМ ===")]
    [Tooltip("За сколько секунд до хила появится эффект (warning)?")]
    public float warningDuration = 2f;
    [SerializeField] private GameObject healWarningObject; // Предупреждающий круг/область
    // В нём должен быть SpriteRenderer (или несколько).
    // Можно через GetComponentsInChildren<SpriteRenderer>() и менять им прозрачность.

    [Header("=== КОЛЛАЙДЕР ДЛЯ ХИЛА (CircleCollider2D) ===")]
    [SerializeField] private CircleCollider2D healArea;
    protected string[] enemyTags = { "Slime", "Skeleton" };

    [Header("=== ДРОП ЛУТ ===")]
    [SerializeField] private GameObject[] lootPrefabs;
    [SerializeField] private float dropChanceMin = 0.25f;
    [SerializeField] private float dropChanceMax = 0.50f;
    [SerializeField] private int lootDropCount = 3;

    [Header("=== ПРОЧЕЕ ===")]
    [SerializeField] private Transform frontPoint;
    private Transform player;
    private PlayerController playerController;
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sprite;

    // Вспомогательные переменные
    private bool isHealingNow = false;   // Чтобы не запускать хил несколько раз подряд
    private float timer;                 // Отсчёт до начала следующего цикла хила

    private void Start()
    {
        stats = FindObjectOfType<Stats>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();

        // Находим PlayerController
        playerController = FindObjectOfType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("PlayerController не найден!");
        }

        // Находим игрока по тегу
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogWarning("Игрок не найден (нет объекта с тегом 'Player')");
        }

        // Сбрасываем таймер на полный кулдаун
        timer = healCooldown;

        // Скрываем объект предупреждения, если он задан
        if (healWarningObject != null)
        {
            healWarningObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Поворот к игроку
        if (player != null)
        {
            FacePlayerWithFlip();
        }

        // Логика кулдауна хила
        if (!isHealingNow)
        {
            timer -= Time.deltaTime;
            // Когда таймер истёк — запускаем корутину подготовки к хилу
            if (timer <= 0f)
            {
                StartCoroutine(HealRoutine());
                // Снова выставим таймер на healCooldown (но фактический хил произойдёт в корутине)
                timer = healCooldown;
            }
        }
    }

    private void FixedUpdate()
    {
        // Логика движения
        if (player != null)
        {
            MoveLogic();
        }
    }

    /// <summary>
    /// Основная корутина: за 2 секунды до самого хила (warningDuration) появляется healWarningObject, 
    /// плавно меняет прозрачность, а в конце запускается анимация "Heal" и DoMassHealAndDamage().
    /// </summary>
    private IEnumerator HealRoutine()
    {
        isHealingNow = true;

        // Если нет предупреждающего объекта, просто ждём и хилим
        if (healWarningObject == null)
        {
            yield return new WaitForSeconds(warningDuration);
            anim.SetTrigger("Heal");
            DoMassHealAndDamage();
            isHealingNow = false;
            yield break;
        }

        // 1) Включаем круг, ставим альфу 0.3
        healWarningObject.SetActive(true);
        SetWarningAlpha(0.3f);

        float elapsed = 0f;
        float startAlpha = 0.3f;
        float endAlpha = 0.8f;
        bool healAnimStarted = false; // Флаг: запущена ли анимация «Heal» чуть раньше

        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / warningDuration; // от 0 до 1

            // Плавно повышаем альфу (0.3 → 0.8)
            float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, t);
            SetWarningAlpha(currentAlpha);

            // 2) Запускаем анимацию хила чуть заранее, 
            //    например при 80% (или 90%) завершения зарядки круга
            if (!healAnimStarted && t >= 0.7f)
            {
                anim.SetTrigger("Heal");
                healAnimStarted = true;
            }

            yield return null;
        }

        // 3) По окончании зарядки круга делаем сам хил/урон
        DoMassHealAndDamage();
        PlaySound(sounds[1], volume: 1, destroyed: true);
        // 4) Скрываем круг
        healWarningObject.SetActive(false);

        isHealingNow = false;
    }

    /// <summary>
    /// Меняем прозрачность (alpha) у всех SpriteRenderer в healWarningObject.
    /// </summary>
    private void SetWarningAlpha(float alpha)
    {
        // Можно взять все спрайты в самом объекте или его детях
        SpriteRenderer[] srs = healWarningObject.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in srs)
        {
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    /// <summary>
    /// Непосредственно массовое исцеление/урон.
    /// </summary>
    private void DoMassHealAndDamage()
    {
        if (healArea == null) return;

        // С помощью OverlapCircleAll ищем все коллайдеры в области
        float radius = healArea.radius;
        Vector2 center = healArea.transform.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (Collider2D col in hits)
        {
            if (col == null) continue;

            string colTag = col.gameObject.tag;
            // Если это враг из списка — лечим
            if (enemyTags.Contains(colTag))
            {
                // У него может быть компонент Enemy, SceletonDef и т.д.
                var sdef = col.gameObject.GetComponent<SceletonDef>();
                if (sdef != null) sdef.Heal(healAmount);

                var enemy = col.gameObject.GetComponent<Enemy>();
                if (enemy != null) enemy.Heal(healAmount);

                var mag = col.gameObject.GetComponent<SceletMag>();
                if (mag != null) mag.Heal(healAmount);

                var slime = col.gameObject.GetComponent<Slime>();
                if (slime != null) slime.Heal(healAmount);

                var healer = col.gameObject.GetComponent<HealerEnemy>();
                if (healer != null && healer != this) // Чтобы не хилить самого себя? Или можно и себя
                {
                    healer.Heal(healAmount);
                }
            }
            // Если это игрок — наносим урон
            else if (colTag == "Player")
            {
                if (playerController != null)
                {
                    playerController.TakeDamage(damageToPlayer);
                }
            }
        }
    }

    // ======================================
    //        ЛОГИКА ДВИЖЕНИЯ (пример)
    // ======================================
    private void MoveLogic()
    {
        float distance = Vector2.Distance(player.position, transform.position);

        if (distance > agrDist)
        {
            // Игрок далеко
            speed = 0;
            anim.ResetTrigger("Walk");
        }
        else if (distance <= agrDist && distance > optimalDist)
        {
            // Идём к игроку
            speed = defSpeed;
            MoveTowards(player.position);
            anim.SetTrigger("Walk");
        }
        else if (distance <= optimalDist && distance >= backDist)
        {
            // Стоим
            speed = 0;
            anim.ResetTrigger("Walk");
        }
        else if (distance < backDist)
        {
            // Отступаем
            speed = defSpeed;
            Vector3 dirAway = (transform.position - player.position).normalized;
            Vector3 targetPos = transform.position + dirAway;
            MoveTowards(targetPos);
            anim.SetTrigger("Walk");
        }
    }

    private void MoveTowards(Vector3 target)
    {
        Vector3 currentPos = transform.position;
        Vector2 newPos2D = Vector2.MoveTowards(
            new Vector2(currentPos.x, currentPos.y),
            new Vector2(target.x, target.y),
            speed * Time.fixedDeltaTime
        );
        transform.position = new Vector3(newPos2D.x, newPos2D.y, currentPos.z);
    }

    private void FacePlayerWithFlip()
    {
        if (player == null || frontPoint == null) return;
        float deadZone = 0.3f;
        Vector3 directionToPlayer = player.position - frontPoint.position;

        if (Mathf.Abs(directionToPlayer.x) > deadZone)
        {
            if (directionToPlayer.x < 0 && transform.localScale.x > 0)
            {
                transform.localScale = new Vector3(
                    -Mathf.Abs(transform.localScale.x),
                    transform.localScale.y,
                    transform.localScale.z
                );
            }
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

    // ======================================
    //        ПОЛУЧЕНИЕ УРОНА
    // ======================================
    public void TakeDamage(int amount, bool isHolyArrow)
    {
        ArrowDef arrowDef = FindObjectOfType<ArrowDef>();

        if (isHolyArrow && arrowDef != null)
        {
            if (arrowDef.HolyEnemyTags.Contains(gameObject.tag))
            {
                health -= amount;
            }
            else if (arrowDef.EnemyTags.Contains(gameObject.tag))
            {
                Heal(amount);
            }
        }
        else
        {
            health -= amount;
            PlaySound(sounds[0], volume: 1, destroyed: true);
        }

        SetTransparency(0.5f);
        Invoke(nameof(ResetTransparency), 0.1f);

        if (health <= 0)
        {
            Die();
        }
    }

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

    public void Heal(int amount)
    {
        health += amount;
    }

    private Stats stats;
    private void Die()
    {
        stats.countElitEnemyDead++;
        stats.UpdateUI();
        DropLoot();
        Destroy(gameObject);
    }

    private void DropLoot()
    {
        for (int i = 0; i < lootDropCount; i++)
        {
            float dropChance = Random.Range(dropChanceMin, dropChanceMax);
            if (Random.value <= dropChance && lootPrefabs.Length > 0)
            {
                GameObject loot = lootPrefabs[Random.Range(0, lootPrefabs.Length)];
                Instantiate(loot, transform.position, Quaternion.identity);
            }
        }
    }

    private void SetTransparency(float alpha)
    {
        if (!sprite) return;
        Color c = sprite.color;
        sprite.color = new Color(c.r, c.g, c.b, alpha);
    }

    private void ResetTransparency()
    {
        if (!sprite) return;
        Color c = sprite.color;
        sprite.color = new Color(c.r, c.g, c.b, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        // Чтобы было видно радиус хила в сцене
        if (healArea != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(healArea.transform.position, healArea.radius);
        }
    }
}