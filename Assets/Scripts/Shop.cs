using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Shop : Sounds
{
    [SerializeField] private GameObject wellcomeText;
    [SerializeField] private GameObject interactivButton;
    [SerializeField] private CircleCollider2D circleCollider;
    [SerializeField] private GameObject panelShop;
    [SerializeField] private PlayerController player;
    [SerializeField] private Button[] buyButtons;

    private bool isPlayerInRange = false;
    private Animator anim;

    [Header("Товары и существующая прокачка")]
    [SerializeField] private Beka upgradeShop;
    [SerializeField] private GameObject goodsContent;
    [SerializeField] private GameObject upgradesContent;
    [SerializeField] private Button goodsTab;
    [SerializeField] private Button upgradesTab;
    [SerializeField] private Button arenaTab;

    [Header("Бонус за просмотр видео")]
    [SerializeField] private GameMonetization monetization;
    [SerializeField] private GameObject bonusesContent;
    [SerializeField] private Button bonusesTab;
    [SerializeField] private Button bonusVideoButton;
    [SerializeField] private TextMeshProUGUI bonusRewardText;
    [SerializeField] private TextMeshProUGUI bonusStatusText;
    [SerializeField, Min(1)] private int videoCoinsReward = 100;
    private string bonusMessage = "Награда за полный просмотр видео";

    [Header("Покупка нескольких наборов")]
    [SerializeField] private GameObject quantityPanel;
    [SerializeField] private Image quantityIcon;
    [SerializeField] private TextMeshProUGUI quantityTitle;
    [SerializeField] private TextMeshProUGUI quantityInfo;
    [SerializeField] private TextMeshProUGUI quantityValueText;
    [SerializeField] private TextMeshProUGUI quantityTotalText;
    [SerializeField] private TextMeshProUGUI quantityBalanceText;
    [SerializeField] private TextMeshProUGUI quantityStatusText;
    [SerializeField] private Slider quantitySlider;
    [SerializeField] private TMP_InputField quantityInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;
    [SerializeField] private Button maximumButton;

    private Ui gameUi;
    private Beka currentTrainer;
    private int selectedItem = -1;
    private int selectedQuantity = 1;
    private int lastCoins = -1;
    public bool IsOpen => panelShop != null && panelShop.activeInHierarchy;
    private bool InTradingRange => currentTrainer != null ? currentTrainer.IsPlayerInRange : isPlayerInRange;
    private bool CanTrade => IsOpen && InTradingRange && GameProgress.IsReady && !YG.YG2.isPauseGame && !player.IsAwaitingRevive;
    public bool CanPurchaseUpgrades => CanTrade && upgradesContent != null && upgradesContent.activeInHierarchy && !quantityPanel.activeSelf;
    public bool IsTradingWith(Beka trainer) => IsOpen && currentTrainer == trainer;

    [System.Serializable]
    public class ShopItem
    {
        public string itemName;
        public int itemPrice;
        public ItemType itemType;
        public int itemValue; // Количество или значение предмета
    }

    public enum ItemType
    {
        Arrow,
        Mirorr,
        PoitionHeal,
        MaxHealth,
        ArrowPoison,
        ArrowHoly,
        originalMoveSpeed,
        ArrowPiercing
    }

    public ShopItem[] shopItems;
    public CrossbowController crossbowController;

    private void Start()
    {
        wellcomeText.SetActive(false);
        interactivButton.SetActive(false);
        panelShop.SetActive(false);

        crossbowController = FindObjectOfType<CrossbowController>();
        if (crossbowController == null)
        {
            Debug.LogError("CrossbowController не привязан!");
        }

        UpdateButtonPrices();

        anim = GetComponent<Animator>();
        gameUi = FindObjectOfType<Ui>();
        if (monetization == null) monetization = FindObjectOfType<GameMonetization>();
        if (bonusesContent != null) bonusesContent.SetActive(false);
        if (quantityPanel != null) quantityPanel.SetActive(false);
        if (quantitySlider != null) quantitySlider.onValueChanged.AddListener(OnQuantitySliderChanged);
        if (quantityInput != null) quantityInput.onEndEdit.AddListener(OnQuantityInputChanged);
    }

    private void Update()
    {
        if (IsOpen && bonusesContent != null && bonusesContent.activeInHierarchy) RefreshBonuses();
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || player.IsAwaitingRevive) return;
        if (IsOpen && !InTradingRange) CloseShop();
        if (isPlayerInRange && currentTrainer == null && Input.GetKeyDown(KeyCode.F))
        {
            if (IsOpen) CloseTopPanel();
            else OpenShop();
        }
        if (IsOpen && lastCoins != player.totalCoins)
        {
            lastCoins = player.totalCoins;
            upgradeShop?.RefreshOffers();
            if (quantityPanel.activeSelf) RefreshQuantity();
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
        if (coll.CompareTag("Player"))
        {
            isPlayerInRange = false;
            wellcomeText.SetActive(false);
            interactivButton.SetActive(false);
            if (currentTrainer == null) CloseShop();
            anim.SetTrigger("IdeVar");
        }
    }

    public void OpenShop()
    {
        if (!isPlayerInRange || IsOpen || !gameUi.BeginTrade(this)) return;
        currentTrainer = null;
        panelShop.SetActive(true);
        ShowGoods();
    }

    public void OpenFromTrainer(Beka trainer)
    {
        if (trainer != upgradeShop || !trainer.IsPlayerInRange || IsOpen || !gameUi.BeginTrade(this)) return;
        currentTrainer = trainer;
        panelShop.SetActive(true);
        ShowUpgrades();
    }

    public void CloseShop()
    {
        if (!IsOpen) return;
        CloseQuantity();
        panelShop.SetActive(false);
        currentTrainer = null;
        gameUi.EndTrade(this);
    }

    public void CloseTopPanel()
    {
        if (quantityPanel.activeSelf) CloseQuantity();
        else CloseShop();
    }

    public void ShowGoods()
    {
        CloseQuantity();
        if (bonusesContent != null) bonusesContent.SetActive(false);
        goodsContent.SetActive(true);
        upgradesContent.SetActive(false);
        RefreshTabs(0);
    }

    public void ShowUpgrades()
    {
        if (upgradeShop == null) return;
        CloseQuantity();
        if (bonusesContent != null) bonusesContent.SetActive(false);
        goodsContent.SetActive(false);
        upgradesContent.SetActive(true);
        upgradeShop.ShowHeroUpgrades();
        RefreshTabs(1);
    }

    public void ShowArena()
    {
        if (upgradeShop == null) return;
        CloseQuantity();
        if (bonusesContent != null) bonusesContent.SetActive(false);
        goodsContent.SetActive(false);
        upgradesContent.SetActive(true);
        upgradeShop.ShowArenaUpgrades();
        RefreshTabs(2);
    }

    public void ShowBonuses()
    {
        if (bonusesContent == null) return;
        CloseQuantity();
        goodsContent.SetActive(false);
        upgradesContent.SetActive(false);
        bonusesContent.SetActive(true);
        RefreshTabs(3);
        RefreshBonuses();
    }

    public void WatchVideoForCoins()
    {
        if (!CanTrade || bonusesContent == null || !bonusesContent.activeInHierarchy || monetization == null) return;
        bonusMessage = "Загрузка видео…";
        if (!monetization.RequestCoinsForVideo(player, videoCoinsReward, OnCoinsVideoCompleted)
            && !monetization.IsRewardedAdPending) bonusMessage = "Видео сейчас недоступно. Попробуй позже";
        RefreshBonuses();
    }

    private void OnCoinsVideoCompleted(GameMonetization.CoinsRewardResult result, int coins)
    {
        if (this == null) return;
        bonusMessage = result == GameMonetization.CoinsRewardResult.Granted ? $"Получено {coins:N0} монет!"
            : result == GameMonetization.CoinsRewardResult.Cancelled ? "Просмотр не завершён. Награда не получена"
            : "Видео сейчас недоступно. Попробуй позже";
        upgradeShop?.RefreshOffers();
        RefreshBonuses();
    }

    private void RefreshBonuses()
    {
        bool ready = GameProgress.IsReady && YG.YG2.isSDKEnabled && monetization != null;
        bool pending = monetization != null && monetization.IsRewardedAdPending;
        bool room = videoCoinsReward > 0 && player.totalCoins <= int.MaxValue - videoCoinsReward;
        if (bonusRewardText != null) bonusRewardText.text = $"+{videoCoinsReward:N0} монет";
        if (bonusVideoButton != null) bonusVideoButton.interactable = CanTrade && ready && !pending && room && !YG.YG2.nowAdsShow;
        if (bonusStatusText != null) bonusStatusText.text = pending ? "Загрузка и просмотр видео…"
            : !room ? "Бонус недоступен: достигнут предел монет"
            : !ready ? "Ждём подключения рекламы…" : bonusMessage;
    }

    private void RefreshTabs(int page)
    {
        bool upgrades = page == 1;
        goodsTab.GetComponent<Image>().color = page != 0 ? new Color32(35, 68, 59, 255) : new Color32(227, 186, 101, 255);
        upgradesTab.GetComponent<Image>().color = upgrades ? new Color32(227, 186, 101, 255) : new Color32(35, 68, 59, 255);
        goodsTab.GetComponentInChildren<TextMeshProUGUI>().color = page != 0 ? new Color32(244, 240, 223, 255) : new Color32(20, 43, 38, 255);
        upgradesTab.GetComponentInChildren<TextMeshProUGUI>().color = upgrades ? new Color32(20, 43, 38, 255) : new Color32(244, 240, 223, 255);
        if (arenaTab != null)
        {
            arenaTab.GetComponent<Image>().color = page == 2 ? new Color32(227, 186, 101, 255) : new Color32(35, 68, 59, 255);
            arenaTab.GetComponentInChildren<TextMeshProUGUI>().color = page == 2 ? new Color32(20, 43, 38, 255) : new Color32(244, 240, 223, 255);
        }
        if (bonusesTab != null)
        {
            bonusesTab.GetComponent<Image>().color = page == 3 ? new Color32(227, 186, 101, 255) : new Color32(35, 68, 59, 255);
            bonusesTab.GetComponentInChildren<TextMeshProUGUI>().color = page == 3 ? new Color32(20, 43, 38, 255) : new Color32(244, 240, 223, 255);
        }
    }

    private void UpdateButtonPrices()
    {
        for (int i = 0; i < shopItems.Length && i < buyButtons.Length; i++)
        {
            Text buttonText = buyButtons[i].GetComponentInChildren<Text>(true);
            if (buttonText != null)
            {
                buttonText.text = $"{shopItems[i].itemPrice:N0} монет";
            }
        }
    }

    public void BuyItem(int itemIndex)
    {
        if (!CanTrade || itemIndex < 0 || itemIndex >= shopItems.Length || !goodsContent.activeSelf) return;
        var item = shopItems[itemIndex];
        selectedItem = itemIndex;
        selectedQuantity = 1;
        quantityTitle.text = ItemTitle(item.itemType);
        var icon = buyButtons[itemIndex].transform.parent.Find("ItemIcon")?.GetComponent<Image>();
        if (icon != null) quantityIcon.sprite = icon.sprite;
        quantityPanel.SetActive(true);
        RefreshQuantity();
    }

    private static string ItemTitle(ItemType type)
    {
        return type == ItemType.Arrow ? "Обычные стрелы" : type == ItemType.ArrowPoison ? "Ядовитые стрелы"
            : type == ItemType.ArrowHoly ? "Святые стрелы" : type == ItemType.ArrowPiercing ? "Пробивные стрелы" : type == ItemType.Mirorr ? "Зеркало" : "Зелье лечения";
    }

    private static string PackCount(int count)
    {
        int lastTwo = count % 100, last = count % 10;
        string word = lastTwo >= 11 && lastTwo <= 14 ? "наборов" : last == 1 ? "набор" : last >= 2 && last <= 4 ? "набора" : "наборов";
        return $"{count:N0} {word}";
    }

    private int Stock(ShopItem item)
    {
        return item.itemType == ItemType.Arrow ? crossbowController.GetArrowCount(0)
            : item.itemType == ItemType.ArrowPoison ? crossbowController.GetArrowCount(1)
            : item.itemType == ItemType.ArrowHoly ? crossbowController.GetArrowCount(2)
            : item.itemType == ItemType.ArrowPiercing ? crossbowController.GetArrowCount(3)
            : item.itemType == ItemType.Mirorr ? player.MirrorCount : player.PotionCount;
    }

    public int MaximumQuantity()
    {
        if (selectedItem < 0) return 0;
        var item = shopItems[selectedItem];
        if (item.itemPrice <= 0 || item.itemValue <= 0) return 0;
        return Mathf.Min(player.totalCoins / item.itemPrice, (int)(((long)int.MaxValue - Stock(item)) / item.itemValue));
    }

    private void RefreshQuantity()
    {
        if (selectedItem < 0) return;
        var item = shopItems[selectedItem];
        int maximum = MaximumQuantity();
        selectedQuantity = Mathf.Clamp(selectedQuantity, 1, Mathf.Max(1, maximum));
        quantitySlider.wholeNumbers = true;
        quantitySlider.minValue = 1;
        quantitySlider.maxValue = Mathf.Max(2, maximum);
        quantitySlider.interactable = maximum > 1;
        quantitySlider.SetValueWithoutNotify(selectedQuantity);
        quantityInput.SetTextWithoutNotify(selectedQuantity.ToString());
        long received = (long)item.itemValue * selectedQuantity;
        long cost = (long)item.itemPrice * selectedQuantity;
        quantityInfo.text = $"В наборе: {item.itemValue:N0} шт. · {item.itemPrice:N0} монет\nУ тебя: {Stock(item):N0} шт.";
        quantityValueText.text = $"{PackCount(selectedQuantity)} · {received:N0} шт.";
        quantityTotalText.text = $"Итого: {cost:N0} монет";
        quantityBalanceText.text = $"Останется: {System.Math.Max(0L, (long)player.totalCoins - cost):N0}";
        quantityStatusText.text = maximum > 0 ? $"Можно купить: {PackCount(maximum)}"
            : item.itemPrice <= 0 || item.itemValue <= 0 ? "Товар недоступен"
            : (long)Stock(item) + item.itemValue > int.MaxValue ? "Нет места для ещё одного набора" : "Недостаточно монет";
        confirmButton.interactable = CanTrade && maximum > 0;
        decreaseButton.interactable = maximum > 0 && selectedQuantity > 1;
        increaseButton.interactable = maximum > selectedQuantity;
        maximumButton.interactable = maximum > 0;
        quantityInput.interactable = maximum > 0;
    }

    private void OnQuantitySliderChanged(float value)
    {
        selectedQuantity = Mathf.RoundToInt(value);
        RefreshQuantity();
    }

    private void OnQuantityInputChanged(string value)
    {
        if (int.TryParse(value, out int quantity)) selectedQuantity = quantity;
        RefreshQuantity();
    }

    public void DecreaseQuantity() { selectedQuantity--; RefreshQuantity(); }
    public void IncreaseQuantity() { if (selectedQuantity < int.MaxValue) selectedQuantity++; RefreshQuantity(); }
    public void SelectMaximum() { selectedQuantity = MaximumQuantity(); RefreshQuantity(); }

    public void CloseQuantity()
    {
        if (quantityPanel != null) quantityPanel.SetActive(false);
        selectedItem = -1;
    }

    public void ConfirmPurchase()
    {
        if (!CanTrade || selectedItem < 0 || !quantityPanel.activeSelf) return;
        if (!int.TryParse(quantityInput.text, out int quantity) || quantity < 1 || quantity > MaximumQuantity())
        {
            RefreshQuantity();
            return;
        }
        var item = shopItems[selectedItem];
        long totalPrice = (long)item.itemPrice * quantity;
        int received = (int)((long)item.itemValue * quantity);
        player.AddCoin(-(int)totalPrice);
        ApplyItemEffect(item, received);
        PlaySound(sounds.Length > 0 ? sounds[0] : null, volume:1, destroyed:false);
        GameProgress.SaveNow();
        CloseQuantity();
        upgradeShop?.RefreshOffers();
    }

    private void ApplyItemEffect(ShopItem item, int amount)
    {
        switch (item.itemType)
        {
            case ItemType.Arrow:
                crossbowController.AddArrows(0, amount);
                break;
            case ItemType.ArrowPoison:
                crossbowController.AddArrows(1, amount);
                break;
            case ItemType.ArrowHoly:
                crossbowController.AddArrows(2, amount);
                break;
            case ItemType.ArrowPiercing:
                crossbowController.AddArrows(3, amount);
                break;
            case ItemType.Mirorr:
                player.AddMirorr(amount);
                break;
            case ItemType.PoitionHeal:
                player.AddPoitonHeal(amount);
                break;
            default:
                Debug.LogWarning("Неизвестный тип предмета");
                break;
        }
    }

    private void OnDestroy()
    {
        if (quantitySlider != null) quantitySlider.onValueChanged.RemoveListener(OnQuantitySliderChanged);
        if (quantityInput != null) quantityInput.onEndEdit.RemoveListener(OnQuantityInputChanged);
    }
}
