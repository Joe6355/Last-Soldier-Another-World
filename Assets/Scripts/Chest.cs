using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chest : Sounds
{
    public GameObject coinPrefab; // Префаб монетки
    public Transform spawnPoint; // Точка спавна монеток
    public int maxCoins = 10; // Максимальное количество монеток
    public int coinsSpawned; // Текущее количество заспавненных монеток
    private Animator anim;
    private bool isChestOpened = false; // Флаг, указывающий, открыт ли сундук

    private void Start()
    {
        // Загружаем количество монеток из PlayerPrefs
        coinsSpawned = PlayerPrefs.GetInt("CoinsSpawned", 0);
        anim = GetComponent<Animator>();
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // Проверяем, что игрок взаимодействует с сундуком
        if (collision.CompareTag("Player") && Input.GetKeyDown(KeyCode.F))
        {
            // Если сундук уже открыт, не выполняем повторное открытие
            if (!isChestOpened)
            {
                PlaySound(sounds[1], volume: 1, destroyed: false); // Проигрываем звук открытия
                anim.SetTrigger("Open"); // Запускаем анимацию открытия
                isChestOpened = true; // Отмечаем, что сундук открыт
            }

            // Если общее количество монеток меньше максимума
            if (coinsSpawned < maxCoins)
            {
                // Спавним монетку
                Instantiate(coinPrefab, spawnPoint.position, Quaternion.identity);
                PlaySound(sounds[0], volume: 1, destroyed: false); // Звук монетки

                // Увеличиваем счетчик заспавненных монеток
                coinsSpawned++;

                // Сохраняем в PlayerPrefs
                PlayerPrefs.SetInt("CoinsSpawned", coinsSpawned);

                // Отладочная информация
                Debug.Log($"Coins spawned: {coinsSpawned}/{maxCoins}");
            }
            else
            {
                // Если достигнут лимит монеток, сундук закрывается
                anim.SetTrigger("Close"); // Запускаем анимацию закрытия
                PlaySound(sounds[2], volume: 1, destroyed: false); // Звук закрытия
                Debug.Log("Maximum number of coins spawned!");
            }
        }
    }
}