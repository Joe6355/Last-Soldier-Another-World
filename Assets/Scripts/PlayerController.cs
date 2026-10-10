using PlayerPrefs = RedefineYG.PlayerPrefs;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(-500)]
public class PlayerController : Sounds
{
    public float LootAttractionRadius { get; private set; }

    public void RefreshLootAttraction(Beka.UpgradeItem[] items)
    {
        LootAttractionRadius = ArenaFeature.Level(Beka.UpgradeItemType.LootAttraction, items) > 0
            ? ArenaFeature.LootRadius(ArenaFeature.Level(Beka.UpgradeItemType.LootAttractionRange, items)) : 0f;
    }

    public bool TryAttractLoot(Rigidbody2D loot, bool wasAttracted = false)
    {
        Vector2 target = transform.position;
        if (loot == null || LootAttractionRadius <= 0f || !GameProgress.IsReady || YG.YG2.isPauseGame
            || Time.timeScale <= 0f || IsAwaitingRevive || hp <= 0f
            || (loot.position - target).sqrMagnitude > LootAttractionRadius * LootAttractionRadius)
        {
            if (loot != null && wasAttracted) loot.velocity = Vector2.zero;
            return false;
        }
        loot.velocity = Vector2.zero;
        loot.angularVelocity = 0f;
        loot.MovePosition(Vector2.MoveTowards(loot.position, target, 8f * Time.fixedDeltaTime));
        return true;
    }
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
    [SerializeField] private TextMeshProUGUI[] additionalCoinTexts;
    public int totalCoins = 0;

    public CrossbowController crossbowController;
    [SerializeField] private MobileControls mobileControls;
    public bool CanDash => stamina >= dashCost;

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
    [SerializeField] private TextMeshProUGUI hpValueText;
    [SerializeField] private TextMeshProUGUI shieldValueText;
    [SerializeField] private TextMeshProUGUI staminaValueText;
    public float ShieldMaxValue => shieldMaxValue;
    public float ShieldValue => shieldValue;
    public int MirrorCount => mirrorRemainder;
    public int PotionCount => potionCount;
    public float PotionHealAmount => heal;

    // Зелья
    private KeyCode keyHeal = KeyCode.H;
    [SerializeField] private int potionCount = 4;
    [SerializeField] private float heal = 25f;
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
    private float basePotionHeal;
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
        basePotionHeal = heal;

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
        UpdateHudValues();
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || Time.timeScale == 0f) return;
        CrossBowController();
        MirrorHome();
        HealPoition();
        HandleShield();
        HandleDash();  // логика дэша со стаминой
    }

    private void UpdateHudValues()
    {
        if (hpValueText != null) hpValueText.text = $"{Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(maxHp)}";
        if (shieldValueText != null) shieldValueText.text = $"{Mathf.CeilToInt(shieldValue)} / {Mathf.CeilToInt(shieldMaxValue)}";
        if (staminaValueText != null) staminaValueText.text = $"{Mathf.CeilToInt(stamina)} / {Mathf.CeilToInt(maxStamina)}";
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
        if ((Input.GetKey(KeyCode.Space) || mobileControls != null && mobileControls.ShieldHeld) && shieldValue > 0f)
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

        if (Input.GetKey(keyToHold) || mobileControls != null && mobileControls.MirrorHeld)
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
        Vector2 movement = mobileControls != null && mobileControls.UseTouch ? mobileControls.Movement
            : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 rbPos = rb.position;
        rb.MovePosition(rbPos + movement * finalSpeed * Time.fixedDeltaTime);

        Vector2 lookDir = mobileControls != null && mobileControls.UseTouch ? mobileControls.AimDirection
            : (Vector2)cam.ScreenToWorldPoint(Input.mousePosition) - rbPos;
        FaceDirection(lookDir);
    }

    private void FaceDirection(Vector2 direction)
    {
        rb.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
    }

    public void CancelTouchActions()
    {
        if (progressBarImage != null && progressBarContainer != null) ResetMirrorState();
        shieldActive = false; shieldMultiplier = 1f;
        if (shieldVisual != null) shieldVisual.SetActive(false);
    }

    private void MousePosition() { }

    private void CrossBowController()
    {
        if (mobileControls != null && mobileControls.UseTouch)
        {
            if (!mobileControls.CanControl) return;
            Vector2 direction = mobileControls.AimDirection;
            FaceDirection(direction);
            if (mobileControls.ConsumeFirePress() || crossbowController.ShootingMode == 3 && mobileControls.FireHeld)
                crossbowController.Shoot(direction);
        }
        else if (Input.GetMouseButtonDown(0)) crossbowController.Shoot();

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
        totalCoins = (int)System.Math.Max(0L, System.Math.Min(int.MaxValue, (long)totalCoins + amount));
        UpdateCoinText();
        PlayerPrefs.SetInt("Coins", totalCoins);
        GameProgress.RequestSave();
        Debug.Log("Собрано монеток: " + totalCoins);
    }

    public void HealFromArena(float amount)
    {
        if (IsAwaitingRevive || hp <= 0 || amount <= 0) return;
        hp = Mathf.Min(maxHp, hp + amount);
        HpBar();
        SavePlayerData();
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
        coinValueText.text = totalCoins.ToString("N0");
        if (additionalCoinTexts != null)
            foreach (var text in additionalCoinTexts)
                if (text != null) text.text = totalCoins.ToString("N0") + " монет";
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
        mirrorRemainder = (int)System.Math.Min(int.MaxValue, System.Math.Max(0L, (long)mirrorRemainder + amount));
        mirrorCountText.text = mirrorRemainder.ToString();
        SavePlayerData();
    }

    public void AddPoitonHeal(int amount)
    {
        potionCount = (int)System.Math.Min(int.MaxValue, System.Math.Max(0L, (long)potionCount + amount));
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
        if (Input.GetKeyDown(keyHeal)) UseHealingPotion();
    }

    public bool UseHealingPotion()
    {
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || Time.timeScale <= 0 || IsAwaitingRevive
            || potionCount <= 0 || hp <= 0 || hp >= maxHp) return false;
        potionCount--;
        hp = Mathf.Min(maxHp, hp + heal);
        PlaySound(sounds[2], volume: 1, destroyed: true);
        textCountPotionHeal.text = potionCount.ToString();
        stats?.RecordAchievementEvent(Stats.AchievementMetric.Potions);
        SavePlayerData();
        return true;
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
        if (Input.GetKeyDown(KeyCode.LeftShift)) TryDash();

        if (staminaBar != null) staminaBar.fillAmount = stamina / maxStamina;
    }

    public bool TryDash()
    {
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || Time.timeScale <= 0f || IsAwaitingRevive || hp <= 0f || !CanDash) return false;
        Vector2 dashDirection = mobileControls != null && mobileControls.UseTouch
            ? (mobileControls.Movement.sqrMagnitude > .001f ? mobileControls.Movement.normalized : mobileControls.AimDirection)
            : ((Vector2)cam.ScreenToWorldPoint(Input.mousePosition) - rb.position).normalized;
        rb.position += dashDirection * dashDistance;

        // Тратим стамину
        stamina -= dashCost;
        if (stamina < 0f) stamina = 0f;
        PlaySound(sounds[3], volume: 1, destroyed: true);
        if (staminaBar != null) staminaBar.fillAmount = stamina / maxStamina;
        return true;
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

    public void IncreasePotionHealing(float amount)
    {
        heal += amount;
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
        heal = basePotionHeal;
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
                case Beka.UpgradeItemType.PotionHealing: IncreasePotionHealing(total); break;
                case Beka.UpgradeItemType.ArrowDamage:
                    IncreaseArrowDamage(Mathf.RoundToInt(total)); break;
            }
        }
        crossbowController.RestoreWeaponUpgrades(items);
        RefreshLootAttraction(items);
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
        heal = basePotionHeal;
        crossbowController.RestoreWeaponUpgrades(System.Array.Empty<Beka.UpgradeItem>());
        LootAttractionRadius = 0f;

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
