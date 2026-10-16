using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SlimeBoss : Sounds
{
    [Header("=== ПАРАМЕТРЫ ЗДОРОВЬЯ ===")]
    [SerializeField] private float maxHealth = 50f;
    private float currentHealth;
    public bool IsAlive => currentHealth > 0f && !isDead;

    [Header("=== ССЫЛКА НА HP-БАР (ОТДЕЛЬНЫЙ ОБЪЕКТ) ===")]
    [Tooltip("Здесь мы получаем ссылку на 'HpSlimeBoss' (объект в сцене), обычно выключенный.")]
    public GameObject bossHpBarObject; // Сам GameObject, который содержит Image
    [SerializeField] private Image bossHpBar;         // Само Image (Fill) внутри этого объекта

    [Header("=== ПАРАМЕТРЫ АТАКИ ===")]
    [SerializeField] private GameObject projectile1;
    [SerializeField] private GameObject projectile2;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float attackRange = 10f;
    [SerializeField] private float timeBetweenAttacks = 2f;
    private float attackTimer;

    [Header("=== ПАРАМЕТРЫ ДВИЖЕНИЯ И АГРА ===")]
    [SerializeField] private float moveSpeed = 2f;
    private bool isAggroed = false;

    [Header("=== ANIMATOR И СПРАЙТ ===")]
    [SerializeField] private Animator anim;
    [SerializeField] private SpriteRenderer sprite;

    [Header("=== DROP ЛУТ ===")]
    [SerializeField] private GameObject[] lootPrefabs;
    [SerializeField] private float dropChanceMin = 0.25f;
    [SerializeField] private float dropChanceMax = 0.50f;
    [SerializeField] private int lootDropCount = 3;

    [Header("=== ПРОЧЕЕ ===")]
    [SerializeField] private float deathAnimationTime = 1.5f;
    [SerializeField] private int damageTouch = 3;

    private Transform player;
    private PlayerController playerController;
    private bool isDead = false;

    private Stats stats;
    private bool regularEnemy;
    private WaveSpawner waveOwner;
    private float waveDamageMultiplier = 1f;
    private float waveProjectileSpeedMultiplier = 1f;
    private readonly List<GameObject> ownedProjectiles = new List<GameObject>();
    public bool IsRegularEnemy => regularEnemy;
    private bool CanAct => !isDead && (!regularEnemy || (waveOwner != null && waveOwner.IsCombatActive && Time.timeScale > 0f && !YG.YG2.isPauseGame));

    // Configure an inactive clone before Start; the wave-10 boss stays unchanged.
    public void ConfigureAsRegularEnemy(WaveSpawner owner, float healthMultiplier, float damageMultiplier,
        float movementMultiplier, float projectileSpeedMultiplier)
    {
        if (regularEnemy) return;
        regularEnemy = true;
        waveOwner = owner;
        maxHealth *= Mathf.Max(1f, healthMultiplier);
        damageTouch = Mathf.CeilToInt(damageTouch * Mathf.Max(1f, damageMultiplier));
        moveSpeed *= Mathf.Max(1f, movementMultiplier);
        waveDamageMultiplier = Mathf.Max(1f, damageMultiplier);
        waveProjectileSpeedMultiplier = Mathf.Max(1f, projectileSpeedMultiplier);
        // A regular clone must never toggle shared scene HP UI.
        bossHpBarObject = null;
        bossHpBar = null;
        if (anim != null && !anim.transform.IsChildOf(transform)) anim = GetComponent<Animator>();
        if (sprite != null && !sprite.transform.IsChildOf(transform)) sprite = GetComponent<SpriteRenderer>();
        if (firePoint == null || !firePoint.IsChildOf(transform))
        {
            var point = new GameObject("RegularSlimeFirePoint").transform;
            point.SetParent(transform, false);
            firePoint = point;
        }
    }

    private void Start()
    {
        stats = FindObjectOfType<Stats>();

        // 1) Инициализируем здоровье
        currentHealth = maxHealth;

        // 2) Если ссылку на HP-бар передали извне, включаем
        if (bossHpBarObject != null)
        {
            bossHpBarObject.SetActive(true);

            if (bossHpBar == null)
            {
                bossHpBar = bossHpBarObject.GetComponentInChildren<Image>();
            }
        }
        else if (!regularEnemy)
        {
            Debug.LogWarning("BossHpBarObject не привязан к SlimeBoss!");
        }

        // 3) Отображаем актуальный HP
        UpdateBossHpBar();

        // 4) Находим игрока
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
        {
            player = found.transform;
            playerController = found.GetComponent<PlayerController>();
        }
        else
        {
            Debug.LogWarning("Игрок не найден! Тег 'Player' отсутствует.");
        }

        // 5) Проверяем Animator, Sprite
        if (anim == null) anim = GetComponent<Animator>();
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        if (regularEnemy && player != null) isAggroed = true;
    }

    private void Update()
    {
        if (!CanAct) return;

        if (isAggroed && player != null)
        {
            float dist = Vector2.Distance(transform.position, player.position);
            FacePlayerWithFlip();

            if (dist > attackRange)
            {
                anim.SetTrigger("Walking");
                MoveTowardsPlayer();
            }
            else
            {
                if (attackTimer <= 0f)
                {
                    anim.SetTrigger("Atck");
                    Attack();
                    attackTimer = timeBetweenAttacks;
                }
                else
                {
                    attackTimer -= Time.deltaTime;
                }
            }
        }
        else
        {
            anim.SetTrigger("Ide");
        }
    }

    private void MoveTowardsPlayer()
    {
        Vector3 dir = (player.position - transform.position).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;
    }

    private void Attack()
    {
        if (!CanAct || player == null) return;
        if (firePoint == null)
        {
            Debug.LogWarning("firePoint не задан у босса!");
            return;
        }

        SpawnProjectile(projectile1);
        SpawnProjectile(projectile2);
        PlaySound(sounds[2], volume: 1, destroyed: true);
    }

    private void SpawnProjectile(GameObject prefab)
    {
        if (prefab == null) return;
        GameObject shot = Instantiate(prefab, firePoint.position, firePoint.rotation);
        if (regularEnemy) WaveSpawner.ScaleEnemyProjectile(shot, waveDamageMultiplier, waveProjectileSpeedMultiplier);
        ownedProjectiles.RemoveAll(projectile => projectile == null);
        ownedProjectiles.Add(shot);
        waveOwner?.TrackEnemyProjectile(shot);
    }

    private void ClearOwnedProjectiles()
    {
        foreach (var shot in ownedProjectiles) if (shot != null) Destroy(shot);
        ownedProjectiles.Clear();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        CancelInvoke();
        ClearOwnedProjectiles();
    }

    private void OnDestroy()
    {
        ClearOwnedProjectiles();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isAggroed = true;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!CanAct) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            if (playerController != null)
                playerController.TakeDamage(damageTouch);
        }
        else if (collision.gameObject.CompareTag("Projectile"))
        {
            int damageAmount = 10;
            bool isHolyArrow = false;

            TakeDamage(damageAmount, isHolyArrow);
            Destroy(collision.gameObject);
        }
    }

    public void TakeDamage(int amount, bool isHolyArrow)
    {
        double previousHealth = currentHealth;
        if (isDead) return;

        isAggroed = true;
        ArrowDef arrowDef = FindObjectOfType<ArrowDef>();
        if (arrowDef == null)
        {
            currentHealth -= amount;
            PlaySound(sounds[1], volume: 1, destroyed: true);
        }
        else
        {
            if (isHolyArrow)
            {
                if (arrowDef.HolyEnemyTags.Contains(gameObject.tag))
                {
                    currentHealth -= amount;
                    PlaySound(sounds[1], volume: 1, destroyed: true);
                }
                else if (arrowDef.EnemyTags.Contains(gameObject.tag))
                {
                    Heal(amount);
                }
            }
            else
            {
                currentHealth -= amount;
                PlaySound(sounds[1], volume: 1, destroyed: true);
            }
        }

        DamageNumbers.Show(transform, previousHealth, currentHealth);
        SetTransparence(0.5f);
        Invoke(nameof(ResetTransparency), 0.1f);

        UpdateBossHpBar();

        if (currentHealth <= 0)
        {
            StartCoroutine(DeathSequence());
        }
    }

    private IEnumerator DeathSequence()
    {
        isDead = true;
        ClearOwnedProjectiles();
        anim.SetTrigger("Dead");

        yield return new WaitForSeconds(deathAnimationTime);

        // Перед уничтожением выключаем HP-бар
        if (bossHpBarObject != null)
            bossHpBarObject.SetActive(false);

        if (stats != null)
        {
            if (regularEnemy) stats.AddEnemyKill(Stats.EnemyKind.Slime);
            else stats.AddBossDead(Stats.EnemyKind.SlimeBoss);
        }

        DropLoot();
        Destroy(gameObject);
        PlaySound(sounds[0], volume: 1, destroyed: true);
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        currentHealth += amount;
        if (currentHealth > maxHealth)
            currentHealth = maxHealth;

        UpdateBossHpBar();
    }

    private void DropLoot()
    {
        for (int i = 0; i < lootDropCount; i++)
        {
            float dropChance = Random.Range(dropChanceMin, dropChanceMax);
            if (Random.value <= dropChance && lootPrefabs != null && lootPrefabs.Length > 0)
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

    private void FacePlayerWithFlip()
    {
        if (player == null) return;
        Vector3 directionToPlayer = player.position - transform.position;

        if (directionToPlayer.x > 0f)
        {
            transform.rotation = Quaternion.Euler(0, 180f, 0);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0, 0f, 0);
        }
    }

    private void SetTransparence(float alpha)
    {
        if (sprite == null) return;
        Color c = sprite.color;
        sprite.color = new Color(c.r, c.g, c.b, alpha);
    }

    private void ResetTransparency()
    {
        if (sprite == null) return;
        Color c = sprite.color;
        sprite.color = new Color(c.r, c.g, c.b, 1f);
    }

    private void UpdateBossHpBar()
    {
        if (bossHpBar != null)
        {
            bossHpBar.fillAmount = currentHealth / maxHealth;
        }
    }

    // ЯД
    public void ApplyPoison(int poisonDamagePerTick, int poisonTicks, float tickInterval)
    {
        StartCoroutine(ApplyPoisonDamage(poisonDamagePerTick, poisonTicks, tickInterval));
    }

    private IEnumerator ApplyPoisonDamage(int poisonDamagePerTick, int poisonTicks, float tickInterval)
    {
        for (int i = 0; i < poisonTicks; i++)
        {
            if (!isDead && currentHealth > 0)
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
}
