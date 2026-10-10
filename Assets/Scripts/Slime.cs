using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

public class Slime : Sounds
{
    private bool deathHandled;
    private float waveDamageMultiplier = 1f;
    public void ApplyWaveScaling(float healthMultiplier, float damageMultiplier, float speedMultiplier)
    {
        health = Mathf.CeilToInt(health * healthMultiplier);
        defSpeed *= speedMultiplier;
        collisionDamage *= damageMultiplier;
        waveDamageMultiplier = damageMultiplier;
    }
    [SerializeField] private int health;
    [SerializeField] private float agrDist;
    [SerializeField] private float defSpeed;
    [SerializeField] private float attackCooldown = 3.0f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform[] firePositions;
    [SerializeField] private GameObject poisonPrefab; // Префаб для объекта после смерти
    [SerializeField] private GameObject[] lootPrefabs;
    [SerializeField] private float dropChanceMin = 0.25f;
    [SerializeField] private float dropChanceMax = 0.50f;
    [SerializeField] private int lootDropCount = 3;
    [SerializeField] private float collisionDamage = 3f; // Урон при столкновении

    private Transform player;
    private Animator anim;
    private Rigidbody2D rb;
    private SpriteRenderer sprite;

    private float lastAttackTime = 0;
    private bool isAttacking = false;
    private float currentSpeed;
    private int walkTrigger, attackTrigger, idleTrigger;

    private Stats stats;
    private void Start()
    {
        stats = FindObjectOfType<Stats>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        player = FindObjectOfType<PlayerController>()?.transform;

        if (player == null)
        {
            Debug.LogError("PlayerController не найден!");
        }

        currentSpeed = defSpeed;
        lastAttackTime = Time.time;
        if (anim != null)
            foreach (var parameter in anim.parameters)
            {
                if (parameter.type != AnimatorControllerParameterType.Trigger) continue;
                if (parameter.name == "Walk" || parameter.name == "GoRun") walkTrigger = parameter.nameHash;
                if (parameter.name == "Attack" || parameter.name == "GoAt") attackTrigger = parameter.nameHash;
                if (parameter.name == "NoRun") idleTrigger = parameter.nameHash;
            }
    }

    private void FixedUpdate()
    {
        if (deathHandled || player == null)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        if (isAttacking) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer > agrDist)
        {
            // Игрок далеко, слизень стоит
            currentSpeed = 0;
            rb.velocity = Vector2.zero;
            if (idleTrigger != 0) anim.SetTrigger(idleTrigger);
        }
        else
        {
            // Игрок в радиусе агра, слизень движется к нему
            currentSpeed = defSpeed;
            MoveTowardsPlayer();
            
            if (walkTrigger != 0) anim.SetTrigger(walkTrigger);

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(Attack());
                PlaySound(sounds[0], volume: 1, destroyed: true);
            }
        }

        FacePlayer();
    }

    private void MoveTowardsPlayer()
    {
        Vector2 direction = (player.position - transform.position).normalized;
        rb.velocity = direction * currentSpeed;
    }

    private IEnumerator Attack()
    {
        isAttacking = true;
        rb.velocity = Vector2.zero; // Остановить движение
        currentSpeed = 0;

        if (attackTrigger != 0) anim.SetTrigger(attackTrigger);

        yield return new WaitForSeconds(0.5f); // Небольшая задержка перед атакой

        // Стреляем
        foreach (var firePosition in firePositions)
        {
            WaveSpawner.ScaleEnemyProjectile(Instantiate(projectilePrefab, firePosition.position, Quaternion.identity), waveDamageMultiplier);
        }

        yield return new WaitForSeconds(2.0f); // Ожидание после выстрела
        lastAttackTime = Time.time;
        isAttacking = false;
        currentSpeed = defSpeed; // Возвращаем скорость
    }

    private void FacePlayer()
    {
        if (player.position.x < transform.position.x && transform.localScale.x > 0)
        {
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
        }
        else if (player.position.x > transform.position.x && transform.localScale.x < 0)
        {
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            var playerController = collision.gameObject.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamage(collisionDamage);
            }

            Die();
        }
    }

    public void TakeDamage(int amount, bool isHolyArrow)
    {
        double previousHealth = health;
        if (deathHandled) return;
        // Получаем ссылку на стрелу
        ArrowDef arrowDef = FindObjectOfType<ArrowDef>();



        // Если стрела святая
        if (isHolyArrow)
        {
            // Если у врага тег из списка Holy, наносим урон
            if (arrowDef.HolyEnemyTags.Contains(gameObject.tag))
            {
                health -= amount;
                PlaySound(sounds[1], volume: 1, destroyed: true);
                //Debug.Log($"Враг получил урон от святой стрелы: {amount}. Текущее здоровье: {health}");
            }
            // Если враг не святой, восстанавливаем здоровье
            else if (arrowDef.EnemyTags.Contains(gameObject.tag))
            {
                Heal(amount);
                //Debug.Log($"Враг был вылечен святой стрелой на {amount}. Текущее здоровье: {health}");
            }
        }
        else
        {
            // Если стрела не святая, наносим обычный урон
            health -= amount;
            PlaySound(sounds[1], volume: 1, destroyed: true);
            //Debug.Log($"Враг получил обычный урон: {amount}. Текущее здоровье: {health}");
        }

        DamageNumbers.Show(transform, previousHealth, health);
        SetTransparence(0.5f);

        if (health <= 0)
        {
            Die();
        }

        Invoke("ResetTransparency", 0.1f);
    }

    // Метод для лечения
    public void Heal(int amount)
    {
        health += amount;
        //Debug.Log($"Враг был вылечен на {amount}. Текущие здоровье: {health}");

    }

    private void Die()
    {
        if (deathHandled) return;
        deathHandled = true;
        PlaySound(sounds[2], volume: 1, destroyed: true);
        stats.AddEnemyKill(Stats.EnemyKind.Slime);

        // Спавн ядовитого пятна
        Instantiate(poisonPrefab, transform.position, Quaternion.identity);

        // Спавн лута
        DropLoot();

        // Уничтожение слизня
        Destroy(gameObject);
    }

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

    private void SetTransparence(float alpha)
    {
        Color originalColor = sprite.color;
        sprite.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
    }

    private void ResetTransparency()
    {
        Color originalColor = sprite.color;
        sprite.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f); // возвращаем прозрачность к 1 (полностью видимый)
    }
}
