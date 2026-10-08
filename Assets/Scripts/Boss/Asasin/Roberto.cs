using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Roberto : Sounds
{
    ///////////////////////////////
    // HP и лечение босса
    ///////////////////////////////
    [Header("HP Settings")]
    [Tooltip("Максимальное здоровье босса")]
    [SerializeField] private float maxHP = 100f;
    private float currentHP;

    [Header("Heal Settings")]
    [Tooltip("Максимальное количество зелий лечения")]
    [SerializeField] private int maxHealPotions = 3;
    private int currentHealPotions;
    [Tooltip("Процент восстановления HP при лечении (например, 0.2 = 20%)")]
    [SerializeField] private float healPercentage = 0.2f;
    [Tooltip("Длительность анимации лечения (сек)")]
    [SerializeField] private float healAnimationDuration = 1f;
    
    ///////////////////////////////
    // Настройки коллайдеров
    ///////////////////////////////
    [Header("Collider Settings")]
    [Tooltip("Коллайдер обнаружения игрока (CircleCollider2D) – используется только для первоначального агро")]
    [SerializeField] private CircleCollider2D detectionCollider;
    [Tooltip("Коллайдер ближней атаки (BoxCollider2D)")]
    [SerializeField] private BoxCollider2D attackCollider;

    ///////////////////////////////
    // Настройки ближней атаки
    ///////////////////////////////
    [Header("Melee Attack Settings")]
    [Tooltip("Урон ближней атаки")]
    [SerializeField] private float attackDamage = 10f;
    [Tooltip("Кулдаун между ближними атаками (сек)")]
    [SerializeField] private float attackCooldown = 1f;

    ///////////////////////////////
    // Настройки обычной (дальней) атаки (с прозрачностью)
    ///////////////////////////////
    [Header("Remote (Circle) Attack Settings")]
    [Tooltip("Время наполнения прозрачности обычных атак (сек)")]
    [SerializeField] private float circleCastTime = 2f;
    [Tooltip("Минимальное число повторов обычной атаки")]
    [SerializeField] private int minCasts = 1;
    [Tooltip("Максимальное число повторов обычной атаки (не более 5)")]
    [SerializeField] private int maxCasts = 5;
    [Tooltip("Урон обычной атакой (при попадании игрока в объект)")]
    [SerializeField] private float circleDamage = 5f;
    [Tooltip("Лечение босса при попадании обычной атаки (если игрок находится в объекте)")]
    [SerializeField] private float circleHealAmount = 10f;
    [Tooltip("Расстояние, при котором запускается обычная атака")]
    [SerializeField] private float aggroDistanceThreshold = 5f;

    ///////////////////////////////
    // Настройки спец атаки (без анимации прозрачности)
    ///////////////////////////////
    [Header("Special Attack Settings")]
    [Tooltip("Минимальное число повторов спец атаки")]
    [SerializeField] private int minSpecialCasts = 1;
    [Tooltip("Максимальное число повторов спец атаки (не более 3)")]
    [SerializeField] private int maxSpecialCasts = 3;
    [Tooltip("Задержка между волнами спец атаки (сек)")]
    [SerializeField] private float specialAttackDelay = 0.5f;

    ///////////////////////////////
    // Настройки кулдаунов для атак
    ///////////////////////////////
    [Header("Cooldown Settings")]
    [Tooltip("Кулдаун после обычной атаки (сек) – 15 секунд")]
    [SerializeField] private float remoteAttackExtraCooldown = 15f;
    [Tooltip("Кулдаун после спец атаки (сек) – 15 секунд")]
    [SerializeField] private float specialAttackCooldown = 15f;
    private float remoteAttackCooldown = 0f;
    private float specialAttackTimer = 0f;

    ///////////////////////////////
    // Настройки HP-бара
    ///////////////////////////////
    [Header("HP Bar Settings")]
    [Tooltip("Контейнер UI полоски HP (GameObject, содержащий UI элементы)")]
    [SerializeField] private GameObject hpBarContainer;
    [Tooltip("UI элемент для отображения HP босса (Image)")]
    [SerializeField] private Image hpBar;

    ///////////////////////////////
    // Настройки спавна атак
    ///////////////////////////////
    [Header("Remote Attack Spawn Settings")]
    [Tooltip("Точки (Transform), где будут появляться объекты для обычной (дальней) атаки")]
    [SerializeField] private Transform[] remoteAttackPoints;
    [Tooltip("Префаб для обычной (дальней) атаки")]
    [SerializeField] private GameObject remoteAttackPrefab;

    [Header("Special Attack Spawn Settings")]
    [Tooltip("Точки (Transform), где будут появляться объекты для спец атаки")]
    [SerializeField] private Transform[] specialAttackPoints;
    [Tooltip("Список префабов для спец атаки (призыв врагов)")]
    [SerializeField] private GameObject[] specialSummonPrefabs;

    ///////////////////////////////
    // Прочее
    ///////////////////////////////
    private Animator animator;
    private GameObject player;
    private SpriteRenderer sprite; // для эффекта мигания

    // Перечисление состояний босса
    private enum BossState { Idle, Aggro, Casting, Attack, Heal, SpecialAttack, Dead }
    private BossState currentState = BossState.Idle;

    // Таймер для ближней атаки
    private float attackTimer = 0f;

    ///////////////////////////////
    // Аниматор для дальней атаки и её логика
    ///////////////////////////////
    [Header("Аниматор дальних атак и их логика")]
    public Animator animBoom;

    [Header("Boom Animation Settings")]
    [Tooltip("Задержка (сек) после начала анимации Boom, после которой наносится урон")]
    [SerializeField] private float boomDamageDelay = 0.1f;
    [Tooltip("Длительность анимации Boom, после которой объект исчезает")]
    [SerializeField] private float boomAnimationDuration = 0.5f;

    // Список для хранения всех призванных мобов (специальная атака)
    private List<GameObject> summonedEnemies = new List<GameObject>();


    // Добавьте эти поля (например, сразу после блока "Аниматор дальних атак и их логика")
    [Header("Marker Attack Settings")]
    [Tooltip("Аниматор, на котором проигрывается анимация Marker")]
    public Animator markerAnimator;
    [Tooltip("Длительность анимации Marker (сек), после которой происходит телепортация к игроку")]
    public float markerAnimationDuration = 0.5f;

    private void Start()
    {
        currentHP = maxHP;
        currentHealPotions = maxHealPotions;
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        player = GameObject.FindGameObjectWithTag("Player");

        // Устанавливаем позицию по оси Z, чтобы босс был виден (z = -4)
        Vector3 pos = transform.position;
        pos.z = -4f;
        transform.position = pos;

        if (hpBarContainer != null)
            hpBarContainer.SetActive(true);
        if (hpBar != null)
            hpBar.fillAmount = currentHP / maxHP;

        animator.SetTrigger("Idle");
    }

    private void Update()
    {
        if (currentState == BossState.Dead) return;

        if (hpBar != null)
            hpBar.fillAmount = currentHP / maxHP;

        if (currentHP <= 0)
        {
            Die();
            return;
        }

        FacePlayer();

        if (currentState == BossState.Idle)
        {
            if (IsPlayerInDetectionRange())
            {
                currentState = BossState.Aggro;
                animator.SetTrigger("Run");
            }
        }

        if (currentState != BossState.Heal && currentHP < maxHP / 2f && currentHealPotions > 0)
        {
            StartCoroutine(Heal());
            return;
        }

        if (currentState == BossState.Aggro && currentHP < maxHP * 0.4f && specialAttackTimer <= 0f)
        {
            StartCoroutine(SpecialAttackRoutine());
            specialAttackTimer = specialAttackCooldown;
            return;
        }

        if (currentState == BossState.Aggro)
        {
            if (IsPlayerInAttackRange())
            {
                Attack();
            }
            else
            {
                float distance = Vector2.Distance(transform.position, player.transform.position);
                if (distance > aggroDistanceThreshold)
                {
                    if (remoteAttackCooldown <= 0f)
                    {
                        StartCoroutine(CircleCastRoutine());
                        remoteAttackCooldown = remoteAttackExtraCooldown;
                    }
                    ChasePlayer();
                }
                else
                {
                    ChasePlayer();
                }
            }
        }

        if (attackTimer > 0)
            attackTimer -= Time.deltaTime;
        if (remoteAttackCooldown > 0)
            remoteAttackCooldown -= Time.deltaTime;
        if (specialAttackTimer > 0)
            specialAttackTimer -= Time.deltaTime;
    }

    private void FacePlayer()
    {
        if (player == null) return;
        if (player.transform.position.x < transform.position.x)
            transform.rotation = Quaternion.Euler(0, 180, 0);
        else
            transform.rotation = Quaternion.Euler(0, 0, 0);
    }

    private bool IsPlayerInDetectionRange()
    {
        if (detectionCollider == null || player == null) return false;
        Vector2 center = detectionCollider.transform.position;
        float radius = detectionCollider.radius * detectionCollider.transform.lossyScale.x;
        return Vector2.Distance(player.transform.position, center) <= radius;
    }

    private bool IsPlayerInAttackRange()
    {
        if (attackCollider == null || player == null) return false;
        return attackCollider.OverlapPoint(player.transform.position);
    }

    public void Attack()
    {
        // Если атака ещё на кулдауне, выходим
        if (attackTimer > 0) return;

        currentState = BossState.Attack;
        // Запускаем стандартную анимацию атаки (если нужна)
        animator.SetTrigger("Attack");
        PlaySound(sounds[3], volume: 1, destroyed: true);
        // Вместо мгновенной телепортации запускаем корутину, которая сначала проигрывает Marker-анимацию,
        // а затем, по её завершении, телепортирует босса к игроку и наносит урон.
        StartCoroutine(MarkerAttack());
    }

    private IEnumerator MarkerAttack()
    {
        // Если указан markerAnimator – запускаем анимацию Marker по триггеру "Marker"
        if (markerAnimator != null)
        {
            markerAnimator.SetTrigger("Marker");
            // Ждём длительность анимации Marker
            yield return new WaitForSeconds(markerAnimationDuration);
        }
        else
        {
            Debug.LogWarning("MarkerAnimator не назначен!");
        }

        // После завершения анимации телепортируем босса к игроку
        Vector3 targetPos = player.transform.position;
        targetPos.z = -4f;
        transform.position = targetPos;


        // Наносим урон игроку (предполагается, что у игрока есть компонент PlayerController с методом TakeDamage)
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.TakeDamage(attackDamage);
            
        }

        // Устанавливаем кулдаун атаки и сбрасываем состояние
        attackTimer = attackCooldown;
        StartCoroutine(ResetAttackState());
    }

    private IEnumerator ResetAttackState()
    {
        yield return new WaitForSeconds(attackCooldown);
        currentState = BossState.Aggro;
        animator.SetTrigger("Run");
    }
    private void ChasePlayer()
    {
        animator.SetTrigger("Run");
        float baseSpeed = 3f;
        if (currentHP < maxHP * 0.4f)
            baseSpeed += 2f;
        Vector3 newPos = Vector2.MoveTowards(transform.position, player.transform.position, baseSpeed * Time.deltaTime);
        newPos.z = -4f;
        transform.position = newPos;
    }

    private IEnumerator Heal()
    {
        currentState = BossState.Heal;
        animator.SetTrigger("Heal");
        PlaySound(sounds[0], volume: 1, destroyed: true);
        yield return new WaitForSeconds(healAnimationDuration);

        float healAmount = maxHP * healPercentage;
        currentHP = Mathf.Min(currentHP + healAmount, maxHP);
        currentHealPotions--;

        currentState = BossState.Aggro;
        animator.SetTrigger("Run");
    }

    /// <summary>
    /// Обычная дальняя атака. На каждой из точек из remoteAttackPoints спавнится префаб,
    /// который анимируется (заполнение альфа до 0.8, запуск анимации Boom, нанесение урона, и уничтожение объекта).
    /// </summary>
    private IEnumerator CircleCastRoutine()
    {
        currentState = BossState.Casting;
        animator.SetTrigger("Idle");

        int casts = Random.Range(minCasts, Mathf.Min(maxCasts, 5) + 1);
        for (int i = 0; i < casts; i++)
        {
            foreach (Transform spawnPoint in remoteAttackPoints)
            {
                GameObject instance = Instantiate(remoteAttackPrefab, spawnPoint.position, spawnPoint.rotation);
                StartCoroutine(AnimateRemoteAttack(instance));
                
            }
            yield return new WaitForSeconds(circleCastTime + boomAnimationDuration + 0.5f);
        }

        currentState = BossState.Aggro;
        animator.SetTrigger("Run");
    }

    private IEnumerator AnimateRemoteAttack(GameObject remoteObj)
    {
        SpriteRenderer sr = remoteObj.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        float elapsed = 0f;
        Color color = sr.color;
        color.a = 0f;
        sr.color = color;

        while (elapsed < circleCastTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 0.8f, elapsed / circleCastTime);
            color.a = alpha;
            sr.color = color;
            yield return null;
        }
        color.a = 0.8f;
        sr.color = color;

        // Воспроизводим звук взрыва для каждого спавна
        //PlaySound(sounds[4], volume: 1, destroyed: true);

        // Запускаем анимацию взрыва
        Animator instanceAnimator = remoteObj.GetComponent<Animator>();
        if (instanceAnimator != null)
        {
            instanceAnimator.SetTrigger("Boom");
        }
        else
        {
            Debug.LogWarning("Animator не найден на удалённом объекте!");
        }

        yield return new WaitForSeconds(boomDamageDelay);

        Collider2D col = remoteObj.GetComponent<Collider2D>();
        if (col != null && player != null && col.OverlapPoint(player.transform.position))
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
                pc.TakeDamage(circleDamage);
            currentHP = Mathf.Min(currentHP + circleHealAmount, maxHP);
        }
        yield return new WaitForSeconds(boomAnimationDuration);
        Destroy(remoteObj);
    }

    /// <summary>
    /// Спец атака: на каждой из точек из specialAttackPoints спавнится случайно выбранный префаб из specialSummonPrefabs.
    /// Все заспавненные объекты добавляются в список summonedEnemies, чтобы потом их можно было удалить.
    /// После спец атаки босса возвращается в режим Aggro.
    /// </summary>
    private IEnumerator SpecialAttackRoutine()
    {
        currentState = BossState.SpecialAttack;
        if (sprite != null)
        {
            Color col = sprite.color;
            col.a = 0.1f;
            sprite.color = col;
        }

        int specialCasts = Random.Range(minSpecialCasts, Mathf.Min(maxSpecialCasts, 3) + 1);
        for (int i = 0; i < specialCasts; i++)
        {
            foreach (Transform spawnPoint in specialAttackPoints)
            {
                if (specialSummonPrefabs != null && specialSummonPrefabs.Length > 0)
                {
                    int index = Random.Range(0, specialSummonPrefabs.Length);
                    GameObject instance = Instantiate(specialSummonPrefabs[index], spawnPoint.position, spawnPoint.rotation);
                    summonedEnemies.Add(instance);
                    //PlaySound(sounds[4], volume: 1, destroyed: true);
                }
            }
            yield return new WaitForSeconds(specialAttackDelay);
        }

        if (sprite != null)
        {
            Color col = sprite.color;
            col.a = 1f;
            sprite.color = col;
        }
        currentState = BossState.Aggro;
        animator.SetTrigger("Run");
    }

    public void TakeDamage(float damage)
    {
        double previousHealth = currentHP;
        if (sprite != null && sprite.color.a <= 0.1f)
            damage *= 0.5f;

        if (currentState == BossState.Idle)
        {
            currentState = BossState.Aggro;
            animator.SetTrigger("Run");
        }
        currentHP -= damage;
        DamageNumbers.Show(transform, previousHealth, currentHP);
        PlaySound(sounds[1], volume: 1, destroyed: true);

        if (currentState != BossState.SpecialAttack && currentState != BossState.Casting)
            StartCoroutine(Blink());
    }

    private IEnumerator Blink()
    {
        SetTransparence(0.5f);
        //PlaySound(sounds[3], volume: 1, destroyed: true);
        yield return new WaitForSeconds(0.1f);
        ResetTransparency();
    }

    private void SetTransparence(float alpha)
    {
        if (sprite != null)
        {
            Color originalColor = sprite.color;
            sprite.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
        }
    }

    private void ResetTransparency()
    {
        if (sprite != null)
        {
            Color originalColor = sprite.color;
            sprite.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
        }
    }

    private void Die()
    {
        currentState = BossState.Dead;
        FindObjectOfType<Stats>()?.AddBossDead(Stats.EnemyKind.Assassin);
        animator.SetTrigger("Dead");
        if (hpBarContainer != null)
            hpBarContainer.SetActive(false);

        // Уничтожаем все призванные мобы
        foreach (GameObject enemy in summonedEnemies)
        {
            if (enemy != null)
                Destroy(enemy);
        }
        summonedEnemies.Clear();

        Destroy(gameObject, 0.8f);
        PlaySound(sounds[2], volume: 1, destroyed: true);
    }
}
