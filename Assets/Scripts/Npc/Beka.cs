using PlayerPrefs = RedefineYG.PlayerPrefs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class Beka : MonoBehaviour
{
    [SerializeField] private GameObject wellcomeText;
    [SerializeField] private GameObject interactivButton;
    [SerializeField] private CircleCollider2D circleCollider;
    [SerializeField] private GameObject panelShop;
    [SerializeField] private PlayerController player;
    [SerializeField] private Button[] buyButtons;

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

        public int CurrentPrice
        {
            get { return basePrice + purchaseCount * priceIncrement; }
        }
    }

    public enum UpgradeItemType
    {
        MaxHP,
        ShieldMax,
        StaminaMax,
        MoveSpeed,
        ArrowDamage
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
            crossbowController.canShoot = false;
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
            crossbowController.canShoot = true;
            isPlayerInRange = false;
            wellcomeText.SetActive(false);
            interactivButton.SetActive(false);
            CloseShop();
            anim.SetTrigger("IdeVar");
        }
    }

    public void OpenShop()
    {
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
            Text buttonText = buyButtons[i].GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                int price = upgradeItems[i].CurrentPrice;
                float val = upgradeItems[i].itemValue;
                buttonText.text = $"{upgradeItems[i].itemName}\n{price} монет (+{val})";
            }
        }
    }

    // Покупка по нажатию на кнопку
    public void BuyItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= upgradeItems.Length) return;

        if (Input.GetKey(KeyCode.LeftControl))
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
        UpgradeItem item = upgradeItems[itemIndex];
        int price = item.CurrentPrice;

        if (player.totalCoins >= price)
        {
            player.AddCoin(-price);
            Debug.Log($"Куплено улучшение: {item.itemName} за {price} монет.");

            ApplyUpgrade(item);
            item.purchaseCount++;

            PlaySoundPurchase();
            UpdateButtonPrices();

            // Сохраняем апгрейды
            SaveUpgrades();
            // Сохраняем статы игрока
            player.SavePlayerData();
        }
        else
        {
            PlaySoundFail();
            Debug.Log($"Недостаточно монет для {item.itemName}");
            anim.SetTrigger("Event");
        }
    }

    private IEnumerator AutoBuyCoroutine(int itemIndex)
    {
        isBuying = true;
        UpgradeItem item = upgradeItems[itemIndex];
        bool purchasedSomething = false;

        while (Input.GetKey(KeyCode.LeftControl))
        {
            int price = item.CurrentPrice;
            if (player.totalCoins >= price)
            {
                player.AddCoin(-price);
                Debug.Log($"(Авто) Куплено улучшение: {item.itemName} за {price} монет!");

                ApplyUpgrade(item);
                item.purchaseCount++;

                purchasedSomething = true;
                UpdateButtonPrices();
                SaveUpgrades();
                player.SavePlayerData();
            }
            else
            {
                PlaySoundFail();
                anim.SetTrigger("Event");
                Debug.Log($"Недостаточно монет для {item.itemName}");
                break;
            }
            yield return new WaitForSeconds(0.1f);
        }

        if (purchasedSomething) PlaySoundPurchase();
        isBuying = false;
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
            case UpgradeItemType.ArrowDamage:
                int dmgAdd = Mathf.RoundToInt(item.itemValue * (item.purchaseCount + 1))
                    - Mathf.RoundToInt(item.itemValue * item.purchaseCount);
                player.IncreaseArrowDamage(dmgAdd);
                break;
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
                upgradeItems[i].purchaseCount = PlayerPrefs.GetInt(key);
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

