using PlayerPrefs = RedefineYG.PlayerPrefs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class Beka : MonoBehaviour
{
    [SerializeField] private GameObject wellcomeText;
    [SerializeField] private GameObject interactivButton;
    [SerializeField] private CircleCollider2D circleCollider;
    [SerializeField] private GameObject panelShop;
    [SerializeField] private PlayerController player;
    [SerializeField] private Button[] buyButtons;
    [SerializeField] private Shop market;
    [SerializeField] private TextMeshProUGUI[] upgradeDetails = System.Array.Empty<TextMeshProUGUI>();
    [SerializeField] private GameObject[] upgradeCards = System.Array.Empty<GameObject>();
    [SerializeField] private GameObject arrowUpgradeSelector;
    [SerializeField] private Button heroUpgradeTab, weaponUpgradeTab;
    [SerializeField] private Button[] arrowUpgradeTabs = System.Array.Empty<Button>();
    [SerializeField] private GameObject arenaUpgradeSelector;
    [SerializeField] private Button[] arenaUpgradeTabs = System.Array.Empty<Button>();
    private bool weaponPage;
    private bool arenaPage;
    private int selectedArena;
    private int selectedArrow;
    public bool IsPlayerInRange => isPlayerInRange;

    private bool isPlayerInRange = false;
    private Animator anim;

    private bool isBuying = false; // флаг автопокупки

    private CrossbowController crossbowController;

    [System.Serializable]
    public class UpgradeItem
    {
        public string itemName;       // Название в UI
        public UpgradeItemType itemType;

        public int basePrice;         // Базовая цена
        public int priceIncrement;    // Насколько дорожает после каждой покупки
        public float itemValue;       // Насколько увеличивает параметр
        public int purchaseCount;     // Сколько раз куплено
        public int maxPurchases;      // 0 — без ограничения
        public int arrowType;
        public bool IsMaxed => purchaseCount >= (maxPurchases > 0 ? maxPurchases : int.MaxValue);

        public int CurrentPrice
        {
            get { return (int)System.Math.Min(int.MaxValue, ExactPrice); }
        }
        public long ExactPrice => (long)basePrice + (long)purchaseCount * priceIncrement;
    }

    public enum UpgradeItemType
    {
        MaxHP,
        ShieldMax,
        StaminaMax,
        MoveSpeed,
        ArrowDamage,
        Shotgun,
        Automatic,
        TypeDamage,
        ArrowSpeed,
        ReloadSpeed,
        // Legacy arena values keep their IDs for existing serialized data and saves.
        TowerPower,
        TowerRange,
        TowerRate,
        RunePower,
        RuneRadius,
        RuneDuration,
        OutpostReward,
        OutpostCapture,
        Roots,
        Altar,
        PotionHealing
    }

    public UpgradeItem[] upgradeItems;

    [Header("Звуки (опционально)")]
    [SerializeField] private AudioClip soundPurchase;
    [SerializeField] private AudioClip soundFail;

    private void Start()
    {
        crossbowController = FindObjectOfType<CrossbowController>();
        wellcomeText.SetActive(false);
        interactivButton.SetActive(false);
        panelShop.SetActive(false);

        anim = GetComponent<Animator>();

        // Загрузим ранее купленные улучшения
        LoadUpgrades();
        // Применим их сразу к игроку
        ReapplyUpgrades();

        UpdateButtonPrices();
    }

    private void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            OpenShop();
        }
    }

    private void OnTriggerEnter2D(Collider2D coll)
    {
        if (coll.CompareTag("Player"))
        {
            isPlayerInRange = true;
            wellcomeText.SetActive(true);
            interactivButton.SetActive(true);
            anim.SetTrigger("Ide");
        }
    }

    private void OnTriggerExit2D(Collider2D coll)
    {
        if (wellcomeText == null || interactivButton == null || panelShop == null || anim == null) return;
        if (coll.CompareTag("Player"))
        {
            isPlayerInRange = false;
            wellcomeText.SetActive(false);
            interactivButton.SetActive(false);
            CloseShop();
            anim.SetTrigger("IdeVar");
        }
    }

    public void OpenShop()
    {
        if (market != null)
        {
            market.OpenFromTrainer(this);
            return;
        }
        if (isPlayerInRange)
        {
            crossbowController.canShoot = false;
            panelShop.SetActive(true);
            if (player.crossbowController != null)
                player.crossbowController.SetShootingState(false);
            Debug.Log("Магазин улучшений открыт");

            UpdateButtonPrices();
        }
    }

    public void CloseShop()
    {
        if (market != null)
        {
            if (market.IsTradingWith(this)) market.CloseShop();
            return;
        }
        crossbowController.canShoot = true;
        panelShop.SetActive(false);
        if (player.crossbowController != null)
            player.crossbowController.SetShootingState(true);
        Debug.Log("Магазин улучшений закрыт");
    }

    private void UpdateButtonPrices()
    {
        for (int i = 0; i < upgradeItems.Length && i < buyButtons.Length; i++)
        {
            if (buyButtons[i] == null) continue;
            Text buttonText = buyButtons[i].GetComponentInChildren<Text>(true);
            if (buttonText != null)
            {
                long price = upgradeItems[i].ExactPrice;
                buttonText.text = upgradeItems[i].IsMaxed
                    ? upgradeItems[i].itemType == UpgradeItemType.Shotgun || upgradeItems[i].itemType == UpgradeItemType.Automatic ? "Изучено" : "Максимум"
                    : $"{price:N0} монет";
            }
            if (i < upgradeDetails.Length && upgradeDetails[i] != null)
                upgradeDetails[i].text = UpgradeSummary(upgradeItems[i]);
            buyButtons[i].interactable = !upgradeItems[i].IsMaxed && ArenaFeature.CanUpgrade(upgradeItems[i].itemType, upgradeItems)
                && upgradeItems[i].ExactPrice > 0 && upgradeItems[i].ExactPrice <= player.totalCoins;
        }
    }

    public void RefreshOffers()
    {
        for (int i = 0; i < upgradeCards.Length && i < upgradeItems.Length; i++)
        {
            if (upgradeCards[i] == null) continue;
            var item = upgradeItems[i];
            bool arena = IsArenaUpgrade(item.itemType);
            bool weapon = (int)item.itemType >= (int)UpgradeItemType.Shotgun && (int)item.itemType <= (int)UpgradeItemType.ReloadSpeed;
            bool perArrow = (int)item.itemType >= (int)UpgradeItemType.TypeDamage && (int)item.itemType <= (int)UpgradeItemType.ReloadSpeed;
            upgradeCards[i].SetActive(arenaPage ? arena && ArenaFeature.UpgradeGroup(item.itemType) == selectedArena
                : !arena && weapon == weaponPage && (!perArrow || item.arrowType == selectedArrow));
        }
        if (heroUpgradeTab != null) heroUpgradeTab.gameObject.SetActive(!arenaPage);
        if (weaponUpgradeTab != null) weaponUpgradeTab.gameObject.SetActive(!arenaPage);
        if (arrowUpgradeSelector != null) arrowUpgradeSelector.SetActive(!arenaPage && weaponPage);
        if (arenaUpgradeSelector != null) arenaUpgradeSelector.SetActive(arenaPage);
        Highlight(heroUpgradeTab, !weaponPage);
        Highlight(weaponUpgradeTab, weaponPage);
        for (int i = 0; i < arrowUpgradeTabs.Length; i++) Highlight(arrowUpgradeTabs[i], i == selectedArrow);
        for (int i = 0; i < arenaUpgradeTabs.Length; i++) Highlight(arenaUpgradeTabs[i], i == selectedArena);
        UpdateButtonPrices();
    }

    private static void Highlight(Button button, bool selected)
    {
        if (button == null) return;
        button.GetComponent<Image>().color = selected ? new Color32(227,186,101,255) : new Color32(35,68,59,255);
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.color = selected ? new Color32(20,43,38,255) : new Color32(244,240,223,255);
    }

    public void ShowHeroUpgrades() { arenaPage = false; weaponPage = false; RefreshOffers(); }
    public void ShowWeaponUpgrades() { arenaPage = false; weaponPage = true; RefreshOffers(); }
    public void ShowArenaUpgrades() { arenaPage = true; RefreshOffers(); }
    public void SelectArenaUpgrades(int type) { if (type < 0 || type >= arenaUpgradeTabs.Length) return; selectedArena = type; RefreshOffers(); }
    public void SelectArrowUpgrades(int type) { if (type < 0 || type >= arrowUpgradeTabs.Length) return; selectedArrow = type; RefreshOffers(); }

    private bool OfferVisible(int index) => upgradeCards.Length == 0 || index < upgradeCards.Length && upgradeCards[index] != null && upgradeCards[index].activeInHierarchy;

    private static bool IsArenaUpgrade(UpgradeItemType type) => (int)type >= (int)UpgradeItemType.TowerPower && (int)type <= (int)UpgradeItemType.Altar;

    private string UpgradeSummary(UpgradeItem item)
    {
        if (IsArenaUpgrade(item.itemType))
            return ArenaFeature.UpgradeSummary(item.itemType, item.purchaseCount, item.maxPurchases, upgradeItems);
        if (item.itemType == UpgradeItemType.PotionHealing)
            return $"Лечение {player.PotionHealAmount:0.##} → {player.PotionHealAmount + item.itemValue:0.##} HP\nУровень {item.purchaseCount:N0} · +{item.itemValue:0.##} HP";
        if (item.itemType == UpgradeItemType.Shotgun || item.itemType == UpgradeItemType.Automatic)
            return item.IsMaxed ? "Режим разблокирован"
                : item.itemType == UpgradeItemType.Shotgun ? "3 стрелы за выстрел\nКлавиша 2 после покупки" : "Огонь при удержании ЛКМ\nКлавиша 3 после покупки";
        if (item.itemType == UpgradeItemType.TypeDamage || item.itemType == UpgradeItemType.ArrowSpeed || item.itemType == UpgradeItemType.ReloadSpeed)
        {
            var bow = player.crossbowController;
            int next = item.IsMaxed ? 0 : 1;
            string values = item.itemType == UpgradeItemType.TypeDamage ? $"{bow.ArrowDamage(item.arrowType)} → {bow.ArrowDamage(item.arrowType) + next}"
                : item.itemType == UpgradeItemType.ArrowSpeed ? $"{bow.ArrowSpeed(item.arrowType):0.##} → {bow.ArrowSpeed(item.arrowType,next):0.##}"
                : $"{bow.ShotInterval(item.arrowType):0.000} → {bow.ShotInterval(item.arrowType,1,next):0.000} с";
            return $"{values}\nУровень {item.purchaseCount} / {item.maxPurchases}";
        }
        float current = item.itemType == UpgradeItemType.MaxHP ? player.maxHp
            : item.itemType == UpgradeItemType.ShieldMax ? player.ShieldMaxValue
            : item.itemType == UpgradeItemType.StaminaMax ? player.maxStamina
            : item.itemType == UpgradeItemType.MoveSpeed ? player.originalMoveSpeed
            : player.crossbowController.arrowPrefabs[0].GetComponent<ArrowDef>().damage;
        float increment = item.itemType == UpgradeItemType.ArrowDamage
            ? Mathf.RoundToInt(item.itemValue * (item.purchaseCount + 1)) - Mathf.RoundToInt(item.itemValue * item.purchaseCount)
            : item.itemValue;
        return $"{current:0.##} → {current + increment:0.##}\nУровень {item.purchaseCount:N0} · +{item.itemValue:0.##}";
    }

    // Покупка по нажатию на кнопку
    public void BuyItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= upgradeItems.Length || !CanPurchase() || !OfferVisible(itemIndex)) return;

        if (isActiveAndEnabled && Input.GetKey(KeyCode.LeftControl))
        {
            if (!isBuying) StartCoroutine(AutoBuyCoroutine(itemIndex));
        }
        else
        {
            AttemptSinglePurchase(itemIndex);
        }
    }

    private void AttemptSinglePurchase(int itemIndex)
    {
        if (!CanPurchase() || !OfferVisible(itemIndex)) return;
        UpgradeItem item = upgradeItems[itemIndex];
        long price = item.ExactPrice;

        if (price > 0 && price <= player.totalCoins && !item.IsMaxed && ArenaFeature.CanUpgrade(item.itemType, upgradeItems))
        {
            player.AddCoin(-(int)price);
            Debug.Log($"Куплено улучшение: {item.itemName} за {price} монет.");

            ApplyUpgrade(item);
            item.purchaseCount++;
            player.crossbowController.RestoreWeaponUpgrades(upgradeItems);
            FindObjectOfType<Stats>()?.RecordUpgradePurchase(item.itemType);

            PlaySoundPurchase();
            RefreshOffers();

            // Сохраняем апгрейды
            SaveUpgrades();
            // Сохраняем статы игрока
            player.SavePlayerData();
        }
        else
        {
            PlaySoundFail();
            Debug.Log($"Недостаточно монет для {item.itemName}");
            if (anim != null) anim.SetTrigger("Event");
        }
    }

    private IEnumerator AutoBuyCoroutine(int itemIndex)
    {
        isBuying = true;
        UpgradeItem item = upgradeItems[itemIndex];
        bool purchasedSomething = false;

        while (Input.GetKey(KeyCode.LeftControl) && CanPurchase() && OfferVisible(itemIndex))
        {
            long price = item.ExactPrice;
            if (price > 0 && price <= player.totalCoins && !item.IsMaxed && ArenaFeature.CanUpgrade(item.itemType, upgradeItems))
            {
                player.AddCoin(-(int)price);
                Debug.Log($"(Авто) Куплено улучшение: {item.itemName} за {price} монет!");

                ApplyUpgrade(item);
                item.purchaseCount++;
                player.crossbowController.RestoreWeaponUpgrades(upgradeItems);
                FindObjectOfType<Stats>()?.RecordUpgradePurchase(item.itemType);

                purchasedSomething = true;
                RefreshOffers();
                SaveUpgrades();
                player.SavePlayerData();
            }
            else
            {
                PlaySoundFail();
                if (anim != null) anim.SetTrigger("Event");
                Debug.Log($"Недостаточно монет для {item.itemName}");
                break;
            }
            yield return new WaitForSecondsRealtime(0.1f);
        }

        if (purchasedSomething) PlaySoundPurchase();
        isBuying = false;
    }

    private bool CanPurchase()
    {
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || player.IsAwaitingRevive) return false;
        return market != null ? market.CanPurchaseUpgrades : isPlayerInRange && panelShop.activeInHierarchy;
    }

    // Применяем улучшение к игроку
    private void ApplyUpgrade(UpgradeItem item)
    {
        switch (item.itemType)
        {
            case UpgradeItemType.MaxHP:
                player.IncreaseMaxHP(item.itemValue);
                break;
            case UpgradeItemType.ShieldMax:
                player.IncreaseShieldMax(item.itemValue);
                break;
            case UpgradeItemType.StaminaMax:
                player.IncreaseMaxStamina(item.itemValue);
                break;
            case UpgradeItemType.MoveSpeed:
                player.IncreaseMoveSpeed(item.itemValue);
                break;
            case UpgradeItemType.PotionHealing:
                player.IncreasePotionHealing(item.itemValue);
                break;
            case UpgradeItemType.ArrowDamage:
                int dmgAdd = Mathf.RoundToInt(item.itemValue * (item.purchaseCount + 1))
                    - Mathf.RoundToInt(item.itemValue * item.purchaseCount);
                player.IncreaseArrowDamage(dmgAdd);
                break;
            case UpgradeItemType.Shotgun:
            case UpgradeItemType.Automatic:
            case UpgradeItemType.TypeDamage:
            case UpgradeItemType.ArrowSpeed:
            case UpgradeItemType.ReloadSpeed:
            case UpgradeItemType.TowerPower:
            case UpgradeItemType.TowerRange:
            case UpgradeItemType.TowerRate:
            case UpgradeItemType.RunePower:
            case UpgradeItemType.RuneRadius:
            case UpgradeItemType.RuneDuration:
            case UpgradeItemType.OutpostReward:
            case UpgradeItemType.OutpostCapture:
            case UpgradeItemType.Roots:
            case UpgradeItemType.Altar:
                break; // Параметры оружия восстанавливаются по сохранённым уровням.
            default:
                Debug.LogWarning("Неизвестный апгрейд: " + item.itemType);
                break;
        }
    }

    private void ReapplyUpgrades()
    {
        // Counts are the source of truth. Restore from base, without healing twice.
        player.RestoreUpgrades(upgradeItems);
    }

    // ========================
    //   SAVE / LOAD
    // ========================
    public void SaveUpgrades()
    {
        // Сохраняем количество покупок у каждого upgradeItems[i]
        for (int i = 0; i < upgradeItems.Length; i++)
        {
            string key = "UpgradeShop_Item" + i.ToString() + "_Count";
            PlayerPrefs.SetInt(key, upgradeItems[i].purchaseCount);
        }
        GameProgress.RequestSave();

    }

    public void LoadUpgrades()
    {
        for (int i = 0; i < upgradeItems.Length; i++)
        {
            string key = "UpgradeShop_Item" + i.ToString() + "_Count";
            if (PlayerPrefs.HasKey(key))
            {
                upgradeItems[i].purchaseCount = Mathf.Clamp(PlayerPrefs.GetInt(key), 0, upgradeItems[i].maxPurchases > 0 ? upgradeItems[i].maxPurchases : int.MaxValue);
            }
        }
        Debug.Log("UpgradeShop: улучшения загружены!");
    }

    // ========================
    //   МЕТОД СБРОСА
    // ========================
    /// <summary>
    /// Сбрасывает все апгрейды (purchaseCount=0), 
    /// обнуляет статы игрока к базовым. 
    /// </summary>
    public void ResetAllUpgrades()
    {
        // 1) Обнуляем счётчики покупок
        for (int i = 0; i < upgradeItems.Length; i++)
        {
            upgradeItems[i].purchaseCount = 0;
        }

        // 2) Сохраняем (purchaseCount=0)
        SaveUpgrades();

        // 3) Сбрасываем статы игрока
        player.ResetAllStatsToBase();

        // 4) Сохраняем новые статы
        player.SavePlayerData();

        // 5) Обновляем UI (цены станут базовыми)
        UpdateButtonPrices();

        Debug.Log("Все апгрейды сброшены! Статы игрока возвращены к базовым.");
    }

    // ========================
    //   SOUNDS
    // ========================
    private void PlaySoundPurchase()
    {
        if (soundPurchase != null)
            AudioSource.PlayClipAtPoint(soundPurchase, transform.position, 1f);
    }

    private void PlaySoundFail()
    {
        if (soundFail != null)
            AudioSource.PlayClipAtPoint(soundFail, transform.position, 1f);
    }
}

