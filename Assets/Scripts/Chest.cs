using PlayerPrefs = RedefineYG.PlayerPrefs;
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
    private bool isPlayerInRange;
    public bool CanInteract => isPlayerInRange && coinsSpawned < maxCoins;
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
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = true;
            if (Input.GetKeyDown(KeyCode.F)) Interact();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player")) isPlayerInRange = false;
    }

    public void Interact()
    {
        if (!isPlayerInRange || !GameProgress.IsReady || YG.YG2.isPauseGame || Time.timeScale <= 0f) return;
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
            GameProgress.RequestSave();

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
