using System.Linq;
using UnityEngine;
using System.Collections;

public class Enemy : Sounds
{
    private bool deathHandled;
    public enum AttackStyle { ContactExplosion, Club, SpiritOrb }
    [SerializeField] private AttackStyle attackStyle;
    [SerializeField, Min(.2f)] private float attackRange = .95f;
    [SerializeField, Min(.2f)] private float attackCooldown = 1.8f;
    [SerializeField, Min(.1f)] private float attackWindup = .4f;
    [SerializeField, Min(.1f)] private float defeatDuration = .5f;
    [SerializeField] private GameObject spiritProjectile;
    private bool attacking;
    private float nextAttackTime;
    private float waveDamageMultiplier = 1f, waveProjectileSpeedMultiplier = 1f;
    private readonly System.Collections.Generic.List<GameObject> ownedProjectiles = new System.Collections.Generic.List<GameObject>();
    public AttackStyle Style => attackStyle;
    public bool IsAlive => !deathHandled && health > 0;
    private bool CanAct => IsAlive && GameProgress.IsReady && !YG.YG2.isPauseGame && Time.timeScale > 0
        && playerController != null && playerController.hp > 0 && !playerController.IsAwaitingRevive;
    public void ApplyWaveScaling(float healthMultiplier, float damageMultiplier, float speedMultiplier, float projectileSpeedMultiplier = 1f)
    {
        health = Mathf.CeilToInt(health * healthMultiplier);
        defSpeed *= speedMultiplier;
        damageTouch = Mathf.CeilToInt(damageTouch * damageMultiplier);
        waveDamageMultiplier = damageMultiplier;
        waveProjectileSpeedMultiplier = projectileSpeedMultiplier;
    }
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
        nextAttackTime = Time.time + attackCooldown * .5f;
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
        if (attackStyle != AttackStyle.ContactExplosion)
        {
            if (CanAct && player != null && !attacking)
            {
                FacePlayerWithFlip();
                UpdateArchetype();
            }
            return;
        }
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
    private void UpdateArchetype()
    {
        float distance = Vector2.Distance(transform.position, player.position);
        if (distance > agrDistantion) { anim.SetTrigger("NoRun"); return; }
        if (distance > attackRange)
        {
            Vector2 next = Vector2.MoveTowards(transform.position, player.position, defSpeed * Time.fixedDeltaTime);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            anim.SetTrigger("GoRun");
        }
        else
        {
            anim.SetTrigger("NoRun");
            if (Time.time >= nextAttackTime) StartCoroutine(ArchetypeAttack());
        }
    }

    private IEnumerator ArchetypeAttack()
    {
        attacking = true;
        nextAttackTime = Time.time + attackCooldown;
        Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
        anim.SetTrigger("Attack");
        yield return new WaitForSeconds(attackWindup);
        if (!CanAct) { attacking = false; yield break; }
        PlaySound(sounds != null && sounds.Length > 1 ? sounds[1] : null, .65f);
        if (attackStyle == AttackStyle.Club)
        {
            Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
            if (toPlayer.magnitude <= attackRange + .25f && Vector2.Dot(direction, toPlayer.normalized) > .35f)
                playerController.TakeDamage(damageTouch);
        }
        else if (spiritProjectile != null)
        {
            var projectile = Instantiate(spiritProjectile, transform.position + (Vector3)direction * .35f, Quaternion.identity);
            WaveSpawner.ScaleEnemyProjectile(projectile, waveDamageMultiplier, waveProjectileSpeedMultiplier);
            ownedProjectiles.RemoveAll(shot => shot == null);
            ownedProjectiles.Add(projectile);
        }
        yield return new WaitForSeconds(.2f);
        attacking = false;
    }

    private void ClearProjectiles()
    {
        foreach (var projectile in ownedProjectiles) if (projectile != null) Destroy(projectile);
        ownedProjectiles.Clear();
    }

    private void OnDisable()
    {
        if (attackStyle == AttackStyle.ContactExplosion) return;
        StopAllCoroutines(); CancelInvoke(); attacking = false; ClearProjectiles();
    }

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
        if (deathHandled || amount <= 0) return;
        if (isHolyArrow && !CompareTag("HolyEnemy")) { Heal(amount); return; }
        double previousHealth = health;
        health -= amount;

        DamageNumbers.Show(transform, previousHealth, health);
        SetTransparence(.5f);
        Invoke(nameof(ResetTransparency), .1f);
        if (health <= 0) Die();
    }

    /// <summary>
    /// Лечение врага.
    /// </summary>
    public void Heal(int amount)
    {
        if (!deathHandled && amount > 0) health = (int)System.Math.Min(int.MaxValue, (long)health + amount);
    }

    /// <summary>
    /// Смерть врага (дроп лута и уничтожение объекта).
    /// </summary>
    private void Die()
    {
        if (deathHandled) return;
        deathHandled = true;
        if (attackStyle != AttackStyle.ContactExplosion)
        {
            StopAllCoroutines(); CancelInvoke(); ClearProjectiles();
            foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.simulated = false;
            anim.ResetTrigger("GoRun"); anim.ResetTrigger("NoRun"); anim.ResetTrigger("Attack");
            anim.SetTrigger("Defeat");
            PlaySound(sounds != null && sounds.Length > 0 ? sounds[0] : null, .65f, destroyed: true, spatial: false);
            stats?.AddEnemyKill(); DropLoot(); Destroy(gameObject, defeatDuration);
            return;
        }
        if (sounds != null && sounds.Length > 0)
            PlaySound(sounds[0], .85f, destroyed: true, spatial: false);
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
        if (attackStyle != AttackStyle.ContactExplosion) return;
        if (coll.gameObject.CompareTag("Player"))
        {

            //playerController.TakeDamage(damageTouch);
            // Если есть PlayerController, нанесём урон
            if (playerController != null)
            {
                playerController.TakeDamage(damageTouch);

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
