using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

public class Slime : Sounds
{
    private bool deathHandled;
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
    }

    private void FixedUpdate()
    {
        if (isAttacking) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer > agrDist)
        {
            // Игрок далеко, слизень стоит
            currentSpeed = 0;
            rb.velocity = Vector2.zero;
        }
        else
        {
            // Игрок в радиусе агра, слизень движется к нему
            MoveTowardsPlayer();
            
            anim.SetTrigger("Walk");

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

        anim.SetTrigger("Attack");

        yield return new WaitForSeconds(0.5f); // Небольшая задержка перед атакой

        // Стреляем
        foreach (var firePosition in firePositions)
        {
            Instantiate(projectilePrefab, firePosition.position, Quaternion.identity);
        }

        lastAttackTime = Time.time;

        yield return new WaitForSeconds(2.0f); // Ожидание после выстрела
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
        stats.countEnemyDead++;
        stats.SaveInfo();
        stats.UpdateUI();

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
