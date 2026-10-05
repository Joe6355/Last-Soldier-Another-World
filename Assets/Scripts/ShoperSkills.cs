using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShoperSkills : MonoBehaviour
{
    [SerializeField] private PlayerController player;

    [System.Serializable]
    public class Skill
    {
        public string skillName;
        public int skillPrice;
        public bool isUnlocked; // Исправлено: "isUnloked" на "isUnlocked"
    }

    public Skill[] skills; // массив наших навыков

    private bool isPlayerInRange = false; // Флаг, чтобы проверить, находится ли игрок в зоне действия
    private bool isShopOpen = false; // Флаг для отслеживания состояния магазина

    [SerializeField] private GameObject buttonOnSkills; // Кнопка для открытия магазина
    [SerializeField] private GameObject text; // Текст для подсказки
    [SerializeField] private GameObject panelSkills; // Панель магазина


    
    private void Start()
    {
        buttonOnSkills.SetActive(false); // Скрываем кнопку в начале
        text.SetActive(false); // Скрываем текст в начале
        panelSkills.SetActive(false); // Скрываем панель магазина в начале
    }

    private void Update()
    {
        Debug.Log("Update вызывается."); // Проверка вызова Update

        // Проверяем, находится ли игрок в зоне действия и нажата ли клавиша F
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            if (isShopOpen)
            {
                CloseSkillTree(); // Закрываем магазин, если он открыт
            }
            else
            {
                OpenSkillTree(); // Открываем магазин, если он закрыт
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D coll)
    {
        if (coll.CompareTag("Player"))
        {
            Debug.Log("Игрок вошел в зону триггера."); // Отладочное сообщение
            buttonOnSkills.SetActive(true); // Показываем кнопку
            text.SetActive(true); // Показываем текст подсказки
            isPlayerInRange = true; // Устанавливаем флаг, что игрок в зоне действия
        }
    }

    private void OnTriggerExit2D(Collider2D coll)
    {
        if (coll.CompareTag("Player"))
        {
            Debug.Log("Игрок покинул зону триггера."); // Отладочное сообщение
            buttonOnSkills.SetActive(false); // Скрываем кнопку
            text.SetActive(false); // Скрываем текст подсказки
            CloseSkillTree(); // Закрываем магазин при выходе игрока из триггера
            isPlayerInRange = false; // Устанавливаем флаг, что игрок вышел из зоны действия
        }
    }

    private void OpenSkillTree()
    {
        Debug.Log("Открытие магазина"); // Отладочное сообщение
        panelSkills.SetActive(true); // Показываем панель навыков
        isShopOpen = true; // Устанавливаем флаг, что магазин открыт
    }

    private void CloseSkillTree()
    {
        Debug.Log("Закрытие магазина"); // Отладочное сообщение
        panelSkills.SetActive(false); // Скрываем панель навыков
        isShopOpen = false; // Устанавливаем флаг, что магазин закрыт
    }
}