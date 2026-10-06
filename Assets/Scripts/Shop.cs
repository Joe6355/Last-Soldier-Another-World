using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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

    private bool isBuying = false; // Флаг: идёт ли автопокупка

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
        originalMoveSpeed
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
    }

    private void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            OpenShop();
        }

        //if (Input.GetKeyUp(KeyCode.F) && panelShop.activeSelf) // Закрытие магазина
        //{
        //    CloseShop();
        //}
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
            CloseShop(); // Закрыть магазин, если игрок вышел из зоны
            anim.SetTrigger("IdeVar");
        }
    }

    public void OpenShop()
    {
        if (isPlayerInRange)
        {
            panelShop.SetActive(true);
            crossbowController.SetShootingState(false); // Запрещаем стрельбу
            Debug.Log("Магазин открыт");
        }
    }

    public void CloseShop()
    {
        panelShop.SetActive(false);
        crossbowController.SetShootingState(true); // Разрешаем стрельбу
        Debug.Log("Магазин закрыт");
    }

    private void UpdateButtonPrices()
    {
        for (int i = 0; i < shopItems.Length && i < buyButtons.Length; i++)
        {
            Text buttonText = buyButtons[i].GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = $"{shopItems[i].itemPrice:N0} монет";
            }
        }
    }

    public void BuyItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= shopItems.Length) return;

        // Если зажат Ctrl — автопокупка
        if (Input.GetKey(KeyCode.LeftControl))
        {
            // Запускаем автопокупку, если она ещё не идёт
            if (!isBuying)
            {
                StartCoroutine(AutoBuyCoroutine(itemIndex));
            }
        }
        else
        {
            // Обычная покупка
            AttemptSinglePurchase(itemIndex);
        }
    }

    private void AttemptSinglePurchase(int itemIndex)
    {
        ShopItem item = shopItems[itemIndex];

        if (player.totalCoins >= item.itemPrice)
        {
            // Покупаем
            player.AddCoin(-item.itemPrice);
            Debug.Log($"Куплен {item.itemName} за {item.itemPrice} монет!");

            // Применяем покупку
            ApplyItemEffect(item);

            // Проигрываем звук покупки
            PlaySound(sounds[0], volume: 1, destroyed: false);
        }
        else
        {
            // Недостаточно монет
            PlaySound(sounds[1], volume: 1, destroyed: false);
            Debug.Log("Недостаточно монет для покупки " + item.itemName);
            anim.SetTrigger("Event");
        }
    }

    private IEnumerator AutoBuyCoroutine(int itemIndex)
    {
        isBuying = true; // Флаг: покупка началась

        ShopItem item = shopItems[itemIndex];
        bool wasSuccessful = false; // Флаг: была ли хотя бы одна успешная покупка

        while (Input.GetKey(KeyCode.LeftControl) && player.totalCoins >= item.itemPrice)
        {
            // Покупаем предмет
            player.AddCoin(-item.itemPrice);
            Debug.Log($"Куплен {item.itemName} за {item.itemPrice} монет (автопокупка)");

            // Применяем покупку
            ApplyItemEffect(item);

            wasSuccessful = true; // Фиксируем, что покупка произошла

            yield return new WaitForSeconds(0.1f); // Небольшая задержка между покупками
        }

        // Если хотя бы одна покупка была успешной, проигрываем звук покупки
        if (wasSuccessful)
        {
            PlaySound(sounds[0], volume: 1, destroyed: false);
        }

        // Если цикл прервался из-за нехватки монет
        if (player.totalCoins < item.itemPrice)
        {
            PlaySound(sounds[1], volume: 1, destroyed: false);
            Debug.Log("Недостаточно монет для покупки " + item.itemName);
            anim.SetTrigger("Event");
        }

        isBuying = false; // Покупка завершена
    }

    private void ApplyItemEffect(ShopItem item)
    {
        switch (item.itemType)
        {
            case ItemType.Arrow:
                crossbowController.AddArrows(0, item.itemValue);
                break;
            case ItemType.ArrowPoison:
                crossbowController.AddArrows(1, item.itemValue);
                break;
            case ItemType.ArrowHoly:
                crossbowController.AddArrows(2, item.itemValue);
                break;
            case ItemType.Mirorr:
                player.AddMirorr(item.itemValue);
                break;
            case ItemType.PoitionHeal:
                player.AddPoitonHeal(item.itemValue);
                break;
            default:
                Debug.LogWarning("Неизвестный тип предмета");
                break;
        }
    }
}
