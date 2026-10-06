using PlayerPrefs = RedefineYG.PlayerPrefs;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-500)]
public class PlayerController : Sounds
{
    [Header("Щит и всё, что за него отвечает")]
    [SerializeField] private float shieldValue = 100f;
    [SerializeField] private float shieldMaxValue = 100f;
    [SerializeField] private float shieldDrainRate = 10f;
    [SerializeField] private float shieldRegenRate = 3f;
    [SerializeField] private bool shieldActive = false;

    [SerializeField] private Image shieldImage;
    [SerializeField] private GameObject shieldVisual;

    [Header("Настройки скорости")]
    public float originalMoveSpeed = 2f;      // начальная скорость
    [HideInInspector] public float shieldMultiplier = 1f;
    [HideInInspector] public float mirrorMultiplier = 1f;
    [HideInInspector] public float cameraMultiplier = 1f;

    private float finalSpeed;

    [Header("Ссылки на объекты")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Camera cam;
    [SerializeField] private Animator anim;

    [Header("Монеты и UI")]
    [SerializeField] private Text coinValueText;
    public int totalCoins = 0;

    public CrossbowController crossbowController;

    [Header("Зеркало (телепорт)")]
    [SerializeField] private Transform mirrorHome;
    private KeyCode keyToHold = KeyCode.E;
    public float holdDuration = 3f;
    private float holdTimer = 0f;
    private bool isHolding = false;
    [SerializeField] private int mirrorRemainder = 1;
    [SerializeField] private Text mirrorCountText;

    [SerializeField] private Image progressBarImage;
    [SerializeField] private GameObject progressBarContainer;

    [Header("Здоровье и хп бар")]
    [SerializeField] private Image hpBar;
    public float hp = 100;
    public float maxHp = 100;                // изначальное макс. хп

    // Зелья
    private KeyCode keyHeal = KeyCode.H;
    [SerializeField] private int potionCount = 4;
    [SerializeField] private float heal = 55f;
    [SerializeField] private Image imagePotionHeal;
    [SerializeField] private Text textCountPotionHeal;

    private SpriteRenderer sprite;

    // Параметры яда
    private float poisDamage = 1;
    private float poisTicks = 5;
    private float poisTickInterval = 1;

    // =======================
    //    DASH / STAMINA
    // =======================
    [Header("Dash / Stamina Settings")]
    [SerializeField] private float dashDistance = 2f;
    [SerializeField] private float dashCost = 25f;
    [SerializeField] public float maxStamina = 100f;      // изначальная стамина
    [SerializeField] private float staminaRegenRate = 5f;
    [SerializeField] private Image staminaBar;

    // Текущее значение стамины
    [HideInInspector] public float stamina;

    // ====== Дополнительные поля для сохранения базовых значений ======
    // (чтобы ResetAllUpgrades мог вернуть к исходным)
    private float baseMaxHp;
    private float baseShieldMaxValue;
    private float baseMaxStamina;
    private float baseMoveSpeed;
    // --------------------------------------------------

    private Stats stats;
    public bool IsAwaitingRevive { get; private set; }
    private float invulnerableUntil;

    private void Start()
    {
        stats = FindObjectOfType<Stats>();

        // Сохраняем базовые нач. значения (для сброса апгрейдов)
        baseMaxHp = maxHp;
        baseShieldMaxValue = shieldMaxValue;
        baseMaxStamina = maxStamina;
        baseMoveSpeed = originalMoveSpeed;

        crossbowController = FindObjectOfType<CrossbowController>();
        LoadPlayerData();
        hpBar.fillAmount = hp / maxHp;
        mirrorCountText.text = mirrorRemainder.ToString();

        totalCoins = PlayerPrefs.GetInt("Coins", 0);
        Debug.Log("Монеты игрока: " + totalCoins);
        UpdateCoinText();

        crossbowController = FindObjectOfType<CrossbowController>();
        if (crossbowController == null)
        {
            Debug.LogError("CrossbowController не привязан!");
        }

        progressBarContainer.SetActive(false);
        textCountPotionHeal.text = potionCount.ToString();

        sprite = GetComponent<SpriteRenderer>();

        // Инициализируем стамину
        if (PlayerPrefs.HasKey("PlayerStamina"))
        {
            // загружаем сохранённую стамину
            stamina = PlayerPrefs.GetFloat("PlayerStamina");
            // clamp, если вдруг больше max
            if (stamina > maxStamina) stamina = maxStamina;
        }
        else
        {
            stamina = maxStamina;
        }
        if (staminaBar != null)
            staminaBar.fillAmount = stamina / maxStamina;
        var upgradeShop = FindObjectOfType<Beka>(true);
        if (upgradeShop != null)
        {
            upgradeShop.LoadUpgrades();
            RestoreUpgrades(upgradeShop.upgradeItems);
        }
        IsAwaitingRevive = PlayerPrefs.GetInt("PlayerDeathPending", 0) == 1 && hp <= 0;
        FindObjectOfType<WaveSpawner>()?.RestoreCheckpointPosition(this);
    }

    private void Update()
    {
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || Time.timeScale == 0f) return;
        CrossBowController();
        MirrorHome();
        HealPoition();
        HandleShield();
        HandleDash();  // логика дэша со стаминой
    }

    private void FixedUpdate()
    {
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || Time.timeScale == 0f || IsAwaitingRevive) return;
        finalSpeed = originalMoveSpeed * shieldMultiplier * mirrorMultiplier * cameraMultiplier;
        Movement();

        UpdateCoinText();
        MousePosition();
        HpBar();
        DeadPlayer();
    }

    // =========================
    //       ЛОГИКА СЧИТА
    // =========================
    private void HandleShield()
    {
        if (Input.GetKey(KeyCode.Space) && shieldValue > 0f)
        {
            shieldActive = true;
            shieldVisual.SetActive(true);

            shieldMultiplier = 0.5f; // замедление при щите
            shieldValue -= shieldDrainRate * Time.deltaTime;
            if (shieldValue < 0f) shieldValue = 0f;
           
        }
        else
        {
            shieldActive = false;
            shieldVisual.SetActive(false);
            shieldMultiplier = 1f;

            if (shieldValue < shieldMaxValue)
            {
                shieldValue += shieldRegenRate * Time.deltaTime;
                if (shieldValue > shieldMaxValue) shieldValue = shieldMaxValue;
            }
        }
        shieldImage.fillAmount = shieldValue / shieldMaxValue;
    }
    // ===========================
    //     ЗЕРКАЛО / ТЕЛЕПОРТ
    // ===========================
    private void MirrorHome()
    {
        if (mirrorRemainder <= 0) return;

        if (Input.GetKey(keyToHold))
        {
            mirrorMultiplier = 0.5f;

            if (!isHolding)
            {
                isHolding = true;
                holdTimer = 0f;
                progressBarContainer.SetActive(true);
            }

            holdTimer += Time.deltaTime;
            float fillAmount = holdTimer / holdDuration;
            progressBarImage.fillAmount = fillAmount;

            if (holdTimer >= holdDuration)
            {
                TeleportPlayerHome();
                PlaySound(sounds[0], volume: 1, destroyed: false);//звук зеркала
                mirrorRemainder--;
                mirrorCountText.text = mirrorRemainder.ToString();
                Debug.Log($"Заряд зеркала: {mirrorRemainder}");
                ResetMirrorState();
                SavePlayerData();
            }
        }
        else
        {
            ResetMirrorState();
        }
    }

    private void ResetMirrorState()
    {
        isHolding = false;
        holdTimer = 0f;
        mirrorMultiplier = 1f;

        progressBarImage.fillAmount = 0f;
        progressBarContainer.SetActive(false);
    }

    // ===========================
    //    ПОЛУЧЕНИЕ УРОНА
    // ===========================
    public void TakeDamage(float damage)
    {
        if (IsAwaitingRevive || Time.time < invulnerableUntil || Time.timeScale == 0f || YG.YG2.isPauseGame) return;
        // Если щит есть
        if (shieldActive && shieldValue > 0f)
        {
            shieldValue -= damage;
            if (shieldValue < 0f) shieldValue = 0f;
            return;
        }

        // Прерываем телепортацию, если она шла
        if (isHolding)
        {
            Debug.Log("Урон получен, телепортация прервана!");
            ResetMirrorState();
        }

        // Урон по HP
        hp -= damage;
        PlaySound(sounds[1], volume: 1, destroyed: true);
        SetTransparence(0.5f);
        Invoke(nameof(ResetTransparency), 0.1f);
    }

    // пример яда
    private IEnumerator ApplyPoisonDamage()
    {
        for (int i = 0; i < poisTicks; i++)
        {
            if (hp > 0)
            {
                TakeDamage(poisDamage);
                yield return new WaitForSeconds(poisTickInterval);
            }
            else
            {
                break;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Poison"))
        {
            StartCoroutine(ApplyPoisonDamage());
        }
    }

    // ===========================
    //     ДВИЖЕНИЕ / РОТАЦИЯ
    // ===========================
    private void Movement()
    {
        Vector2 movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 rbPos = rb.position;
        rb.MovePosition(rbPos + movement * finalSpeed * Time.fixedDeltaTime);

        Vector2 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 lookDir = mousePos - rbPos;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg + 90f;
        rb.rotation = angle;
    }

    private void MousePosition() { }

    private void CrossBowController()
    {
        if (Input.GetMouseButtonDown(0))
            crossbowController.Shoot();

    }

    // ===========================
    //         HP BAR
    // ===========================
    private void HpBar()
    {
        hpBar.fillAmount = hp / maxHp;
    }

    private void DeadPlayer()
    {
        if (hp > 0 || IsAwaitingRevive) return;
        hp = 0;
        IsAwaitingRevive = true;
        StopAllCoroutines();
        ResetMirrorState();
        stats.AddPlayerDead();
        PlaySound(sounds[4], volume: 1, destroyed: true);
        SavePlayerData();
        if (!GameMonetization.HandleDeath(this)) ReturnToCampAfterDeath();
    }

    public void ReviveAfterVideo()
    {
        if (!IsAwaitingRevive) return;
        hp = maxHp;
        IsAwaitingRevive = false;
        invulnerableUntil = Time.time + 2f;
        HpBar();
        SavePlayerData();
    }

    public void ReturnToCampAfterDeath()
    {
        if (!IsAwaitingRevive) return;
        FindObjectOfType<WaveSpawner>()?.AbandonRun();
        TeleportPlayerHome();
        hp = Mathf.Min(maxHp, 25f);
        totalCoins = Mathf.Max(0, totalCoins / 2);
        IsAwaitingRevive = false;
        HpBar();
        UpdateCoinText();
        SavePlayerData();
    }

    public void TeleportPlayerHome()
    {
        transform.position = mirrorHome.position;
        Debug.Log("Вы дома");
    }

    // ===========================
    //       МОНЕТЫ
    // ===========================
    public void AddCoin(int amount)
    {
        totalCoins += amount;
        PlayerPrefs.SetInt("Coins", totalCoins);
        GameProgress.RequestSave();
        Debug.Log("Собрано монеток: " + totalCoins);
    }

    public void ResetCoins()
    {
        totalCoins = 0;
        PlayerPrefs.SetInt("Coins", totalCoins);

        hp = maxHp;
        PlayerPrefs.SetFloat("PlayerHP", hp);

        mirrorRemainder = 3;
        PlayerPrefs.SetInt("MirrorRemainder", mirrorRemainder);

        potionCount = 1;
        PlayerPrefs.SetInt("potionCount", potionCount);

        UpdateCoinText();
    }

    private void UpdateCoinText()
    {
        coinValueText.text = totalCoins.ToString();
        textCountPotionHeal.text = potionCount.ToString();
        mirrorCountText.text = mirrorRemainder.ToString();
    }

    // ===========================
    //       SAVE / LOAD
    // ===========================
    public void SavePlayerData()
    {
        PlayerPrefs.SetInt("PlayerDeathPending", IsAwaitingRevive ? 1 : 0);
        PlayerPrefs.SetFloat("PlayerHP", hp);
        PlayerPrefs.SetFloat("PlayerMaxHP", maxHp);

        PlayerPrefs.SetFloat("ShieldValue", shieldValue);
        PlayerPrefs.SetFloat("ShieldMaxValue", shieldMaxValue);

        PlayerPrefs.SetFloat("PlayerStamina", stamina);
        PlayerPrefs.SetFloat("PlayerMaxStamina", maxStamina);

        PlayerPrefs.SetFloat("PlayerSpeed", originalMoveSpeed);

        PlayerPrefs.SetInt("MirrorRemainder", mirrorRemainder);
        PlayerPrefs.SetInt("potionCount", potionCount);

        PlayerPrefs.SetFloat("PlayerHPBar", hpBar.fillAmount);

        PlayerPrefs.SetInt("Coins", totalCoins);

        // Сохраняем фактический урон стрел.
        if (crossbowController != null && crossbowController.arrowPrefabs.Length > 0)
        {
            var arrow = crossbowController.arrowPrefabs[0].GetComponent<ArrowDef>();
            if (arrow != null) PlayerPrefs.SetInt("ArrowDamageUpgrade", arrow.damage);
        }

        GameProgress.RequestSave();

    }

    public void LoadPlayerData()
    {
        hp = PlayerPrefs.GetFloat("PlayerHP", 100);
        maxHp = PlayerPrefs.GetFloat("PlayerMaxHP", 100);

        shieldValue = PlayerPrefs.GetFloat("ShieldValue", 100);
        shieldMaxValue = PlayerPrefs.GetFloat("ShieldMaxValue", 100);

        stamina = PlayerPrefs.GetFloat("PlayerStamina", 100);
        maxStamina = PlayerPrefs.GetFloat("PlayerMaxStamina", 100);

        originalMoveSpeed = PlayerPrefs.GetFloat("PlayerSpeed", 2f);

        mirrorRemainder = PlayerPrefs.GetInt("MirrorRemainder", 1);
        potionCount = PlayerPrefs.GetInt("potionCount", 1);
        totalCoins = PlayerPrefs.GetInt("Coins", 0);

        // Загружаем урон стрел (если не было сохранения, ставим 2)
        int arrowDamage = PlayerPrefs.GetInt("ArrowDamageUpgrade", 2);
        foreach (var arrowPrefab in crossbowController.arrowPrefabs)
        {
            ArrowDef arrowDef = arrowPrefab.GetComponent<ArrowDef>();
            if (arrowDef != null)
            {
                arrowDef.damage = arrowDamage;
            }
        }

        Debug.Log("Данные игрока загружены (PlayerController).");
    }

    private void OnApplicationQuit()
    {
        SavePlayerData();
    }

    // ===========================
    //       ЗЕРКАЛО / ЗЕЛЬЯ
    // ===========================
    public void AddMirorr(int amount)
    {
        mirrorRemainder += amount;
        mirrorCountText.text = mirrorRemainder.ToString();
        SavePlayerData();
    }

    public void AddPoitonHeal(int amount)
    {
        potionCount += amount;
        textCountPotionHeal.text = potionCount.ToString();
        SavePlayerData();
    }

    // ===========================
    //        ВИЗ. ЭФФЕКТ
    // ===========================
    private void SetTransparence(float alpha)
    {
        Color originalColor = sprite.color;
        sprite.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
    }
    private void ResetTransparency()
    {
        Color originalColor = sprite.color;
        sprite.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
    }

    private void HealPoition()
    {
        if (Input.GetKeyDown(keyHeal))
        {
            if (potionCount > 0 && hp < maxHp)
            {
                potionCount--;
                hp += heal;
                PlaySound(sounds[2], volume: 1, destroyed: true);
                if (hp > maxHp) hp = maxHp;
                textCountPotionHeal.text = potionCount.ToString();
                SavePlayerData();
            }
        }
    }

    // =====================================
    //    ЛОГИКА ДЭША (STAMINA)
    // =====================================
    private void HandleDash()
    {
        // Восстанавливаем стамину
        if (stamina < maxStamina)
        {
            stamina += staminaRegenRate * Time.deltaTime;
            if (stamina > maxStamina) stamina = maxStamina;
        }

        // Проверяем нажатие Shift
        if (Input.GetKeyDown(KeyCode.LeftShift) && stamina >= dashCost)
        {
            Vector2 dashDirection = ((Vector2)cam.ScreenToWorldPoint(Input.mousePosition) - rb.position).normalized;
            rb.position += dashDirection * dashDistance;

            // Тратим стамину
            stamina -= dashCost;
            if (stamina < 0f) stamina = 0f;
            PlaySound(sounds[3], volume: 1, destroyed: true);
        }

        // Обновляем UI стамины
        if (staminaBar != null)
        {
            staminaBar.fillAmount = stamina / maxStamina;
        }
    }

    // ===========================
    //    МЕТОДЫ ДЛЯ АПГРЕЙДОВ
    // ===========================
    public void IncreaseMaxHP(float amount)
    {
        maxHp += amount;
        hp += amount; // сразу лечим до нового макс.
        if (hp > maxHp) hp = maxHp;
        hpBar.fillAmount = hp / maxHp;
    }

    public void IncreaseShieldMax(float amount)
    {
        shieldMaxValue += amount;
        shieldValue += amount;
        if (shieldValue > shieldMaxValue) shieldValue = shieldMaxValue;
        shieldImage.fillAmount = shieldValue / shieldMaxValue;
    }

    public void IncreaseMaxStamina(float amount)
    {
        maxStamina += amount;
        stamina += amount;
        if (stamina > maxStamina) stamina = maxStamina;
        if (staminaBar != null)
            staminaBar.fillAmount = stamina / maxStamina;
    }

    public void IncreaseMoveSpeed(float amount)
    {
        originalMoveSpeed += amount;
        // Или умножать: originalMoveSpeed *= (1+ amount*0.01f) ...
    }

    public void IncreaseArrowDamage(int amount)
    {
        // Увеличим damage у всех префабов в crossbowController
        foreach (var arrowPrefab in crossbowController.arrowPrefabs)
        {
            ArrowDef arrowDef = arrowPrefab.GetComponent<ArrowDef>();
            if (arrowDef != null)
            {
                arrowDef.damage += amount;
            }
        }
    }

    public void RestoreUpgrades(Beka.UpgradeItem[] items)
    {
        float savedHp = hp, savedShield = shieldValue, savedStamina = stamina;
        maxHp = baseMaxHp;
        shieldMaxValue = baseShieldMaxValue;
        maxStamina = baseMaxStamina;
        originalMoveSpeed = baseMoveSpeed;
        foreach (var arrowPrefab in crossbowController.arrowPrefabs)
        {
            var arrow = arrowPrefab.GetComponent<ArrowDef>();
            if (arrow != null) arrow.damage = 2;
        }
        foreach (var item in items)
        {
            float total = item.itemValue * item.purchaseCount;
            switch (item.itemType)
            {
                case Beka.UpgradeItemType.MaxHP: IncreaseMaxHP(total); break;
                case Beka.UpgradeItemType.ShieldMax: IncreaseShieldMax(total); break;
                case Beka.UpgradeItemType.StaminaMax: IncreaseMaxStamina(total); break;
                case Beka.UpgradeItemType.MoveSpeed: IncreaseMoveSpeed(total); break;
                case Beka.UpgradeItemType.ArrowDamage:
                    IncreaseArrowDamage(Mathf.RoundToInt(total)); break;
            }
        }
        hp = Mathf.Clamp(savedHp, 0f, maxHp);
        shieldValue = Mathf.Clamp(savedShield, 0f, shieldMaxValue);
        stamina = Mathf.Clamp(savedStamina, 0f, maxStamina);
        hpBar.fillAmount = hp / maxHp;
        shieldImage.fillAmount = shieldValue / shieldMaxValue;
        if (staminaBar != null) staminaBar.fillAmount = stamina / maxStamina;
    }

    public void ResetAllStatsToBase()
    {
        // Устанавливаем базовые значения
        maxHp = 100;
        hp = Mathf.Min(hp, maxHp);

        shieldMaxValue = 100;
        shieldValue = Mathf.Min(shieldValue, shieldMaxValue);

        maxStamina = 100;
        stamina = Mathf.Min(stamina, maxStamina);

        originalMoveSpeed = 2f;

        // Сбрасываем урон стрел (по умолчанию = 2)
        foreach (var arrowPrefab in crossbowController.arrowPrefabs)
        {
            ArrowDef arrowDef = arrowPrefab.GetComponent<ArrowDef>();
            if (arrowDef != null)
            {
                arrowDef.damage = 2; // Вернуть стандартный урон
            }
        }

        // Удаляем сохранённые апгрейды, если они были
        PlayerPrefs.DeleteKey("PlayerMaxHP");
        PlayerPrefs.DeleteKey("ShieldMaxValue");
        PlayerPrefs.DeleteKey("PlayerMaxStamina");
        PlayerPrefs.DeleteKey("PlayerSpeed");
        PlayerPrefs.DeleteKey("ArrowDamageUpgrade"); // Если было сохранение урона стрел

        // Сохраняем новые базовые параметры в PlayerPrefs
        SavePlayerData();

        // Обновляем UI
        if (hpBar != null) hpBar.fillAmount = hp / maxHp;
        if (shieldImage != null) shieldImage.fillAmount = shieldValue / shieldMaxValue;
        if (staminaBar != null) staminaBar.fillAmount = stamina / maxStamina;

        Debug.Log("Все апгрейды сброшены и сохранены.");
    }
}
