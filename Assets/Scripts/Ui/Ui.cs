using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class Ui : MonoBehaviour
{
    [Header("Основные элементы")]
    [SerializeField] private GameObject menuPanel; // Главная панель меню
    [SerializeField] private GameObject settingsPanel; // Панель настроек
    [SerializeField] private GameObject ratingPanel;

    [Header("Музыка")]
    [SerializeField] private AudioSource menuMusic; // Музыка в меню
    [SerializeField] private AudioSource[] gameMusicSources; // Массив источников музыки для игры

    [Header("Эффекты")]
    [SerializeField] private AudioSource[] sfxSources; // Массив источников эффектов

    [Header("Слайдеры")]
    [SerializeField] private Slider musicSlider; // Слайдер громкости музыки
    [SerializeField] private Slider sfxSlider; // Слайдер громкости эффектов

    [Header("Кнопки")]
    [SerializeField] private Button playButton;
    [FormerlySerializedAs("exitButton")]
    [SerializeField] private Button ratingButton;
    [SerializeField] private Button openMenuButton; // Кнопка для открытия меню вне его

    private bool isMenuOpen = false; // Состояние меню (открыто/закрыто)
    private bool isGameMusicPlaying = false; // Проверка, играет ли музыка игры

    private PlayerController playerController;

    [SerializeField] private Button killPlayer;

    private CrossbowController crossbowController;

    public static float sfxVolume = 1f;
    private void Start()
    {
        //crossbowController.SetShootingState(false);
        crossbowController  = FindObjectOfType<CrossbowController>();
        playerController = FindObjectOfType<PlayerController>();
        // Останавливаем игру при запуске
        PauseGame();

        // Включаем музыку меню
        if (menuMusic != null)
        {
            menuMusic.Play();
        }

        // Привязываем кнопки
        playButton.onClick.AddListener(StartGame);
        ratingButton.onClick.AddListener(ShowRating);

        // Привязываем слайдеры
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);

        // Привязываем кнопку для открытия меню
        if (openMenuButton != null)
        {
            openMenuButton.onClick.AddListener(ToggleMenu);
        }

        // Устанавливаем начальные значения слайдеров
        musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.5f);

        // Устанавливаем начальные громкости
        SetMusicVolume(musicSlider.value);
        SetSFXVolume(sfxSlider.value);

        // Убедиться, что музыка игры не играет при запуске
        PauseGameMusic();

        if (isGameMusicPlaying)
        {
            Debug.Log("Музыка игры играет");
        }
    }

    private void Update()
    {
        // Открытие/закрытие меню по нажатию клавиши Esc
        if (!YG.YG2.isPauseGame && !playerController.IsAwaitingRevive && Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();

        }
    }

    private void PauseGame()
    {
        crossbowController.SetShootingState(false);
        YG.YG2.GameplayStop();
        GameProgress.SaveNow();
        // Останавливаем время
        Time.timeScale = 0f;

        // Ставим музыку игры на паузу
        PauseGameMusic();

        // Останавливаем эффекты
        foreach (var sfx in sfxSources)
        {
            if (sfx != null)
                sfx.Pause();
        }

        // Включаем музыку меню
        if (menuMusic != null && !menuMusic.isPlaying)
        {
            menuMusic.Play();
        }
    }

    private void ResumeGame()
    {
        YG.YG2.GameplayStart();
        // Возобновляем время
        Time.timeScale = 1f;

        // Возобновляем эффекты
        foreach (var sfx in sfxSources)
        {
            if (sfx != null)
                sfx.UnPause();
        }

        // Останавливаем музыку меню
        if (menuMusic != null)
        {
            menuMusic.Stop();
        }

        // Возобновляем музыку игры
        ResumeGameMusic();
    }

    public void StartGame()
    {
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || playerController.IsAwaitingRevive) return;
        AudioListener.pause = false;
        // Выключаем меню
        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (ratingPanel != null) ratingPanel.SetActive(false);
        isMenuOpen = false;
        crossbowController.SetShootingState(true);

        // Останавливаем музыку меню
        if (menuMusic != null)
        {
            menuMusic.Stop();
        }

        // Включаем игровую музыку
        PlayGameMusic();

        // Возобновляем игру
        ResumeGame();
    }

    public void ShowRating()
    {
        if (ratingPanel == null || YG.YG2.isPauseGame || playerController.IsAwaitingRevive) return;
        isMenuOpen = true;
        menuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        PauseGame();
        FindObjectOfType<Stats>()?.UpdateUI();
        ratingPanel.SetActive(true);
    }

    public void CloseRating()
    {
        if (ratingPanel != null) ratingPanel.SetActive(false);
    }

    public void ToggleMenu()
    {
        if (YG.YG2.isPauseGame || playerController.IsAwaitingRevive) return;
        if (ratingPanel != null && ratingPanel.activeSelf)
        {
            CloseRating();
            return;
        }
        isMenuOpen = !isMenuOpen;

        if (isMenuOpen)
        {
            // Открываем меню
            menuPanel.SetActive(true);
            PauseGame();
            crossbowController.SetShootingState(false);
        }
        else
        {
            // Закрываем меню
            menuPanel.SetActive(false);
            ResumeGame();
            crossbowController.SetShootingState(true);
        }
    }

    public void PauseForDeath()
    {
        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (ratingPanel != null) ratingPanel.SetActive(false);
        isMenuOpen = false;
        PauseGame();
        AudioListener.pause = true;
    }

    public void ContinueAfterDeath() => StartGame();

    public void ShowMenuAfterDeath()
    {
        AudioListener.pause = false;
        menuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        if (ratingPanel != null) ratingPanel.SetActive(false);
        isMenuOpen = true;
        PauseGame();
    }

    private void PlayGameMusic()
    {
        // Проверяем, если массив пустой
        if (gameMusicSources.Length == 0) return;

        // Включаем все треки в массиве
        foreach (var musicSource in gameMusicSources)
        {
            if (musicSource != null && !musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        isGameMusicPlaying = true;
    }

    private void PauseGameMusic()
    {
        // Ставим на паузу все треки в массиве
        foreach (var musicSource in gameMusicSources)
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Pause();
            }
        }

        isGameMusicPlaying = false;
    }

    private void ResumeGameMusic()
    {
        // Возобновляем все треки в массиве
        foreach (var musicSource in gameMusicSources)
        {
            if (musicSource != null && !musicSource.isPlaying)
            {
                musicSource.UnPause();
            }
        }

        isGameMusicPlaying = true;
    }

    public void SetMusicVolume(float volume)
    {
        // Устанавливаем громкость для музыки в меню
        if (menuMusic != null)
            menuMusic.volume = volume;

        // Устанавливаем громкость для музыки игры
        foreach (var musicSource in gameMusicSources)
        {
            if (musicSource != null)
                musicSource.volume = volume;
        }

        // Сохраняем значение
        PlayerPrefs.SetFloat("MusicVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = volume; // Обновляем глобальную громкость
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save(); // Сохраняем в PlayerPrefs

        // Теперь передаем громкость во все источники звука
        foreach (var sfx in sfxSources)
        {
            if (sfx != null)
                sfx.volume = volume;
        }
    }

    public void KillPlayer()
    {

        playerController.TeleportPlayerHome();
        playerController.hp += 25;
        playerController.totalCoins /= 2;


        // Убедимся, что количество монет не может быть меньше 0
        if (playerController.totalCoins <= 0)
        {
            playerController.totalCoins = 0;
        }
    }
}
