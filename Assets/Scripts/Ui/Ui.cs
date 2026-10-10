using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private TextMeshProUGUI musicValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;
    [SerializeField] private Toggle damageNumbersToggle;

    [Header("Кнопки")]
    [SerializeField] private Button playButton;
    [FormerlySerializedAs("exitButton")]
    [SerializeField] private Button ratingButton;
    [SerializeField] private Button openMenuButton; // Кнопка для открытия меню вне его

    private bool isMenuOpen = false; // Состояние меню (открыто/закрыто)

    private PlayerController playerController;

    [SerializeField] private Button killPlayer;

    private CrossbowController crossbowController;

    public static float sfxVolume = 1f;
    private bool startWhenReady;
    private Shop activeShop;
    public bool IsMenuOpen => isMenuOpen;
    public bool IsTradeOpen => activeShop != null;
    private bool mobileOrientationPaused, orientationResumePending;
    public bool IsMobileOrientationPaused => mobileOrientationPaused;
    private void Start()
    {
        if (damageNumbersToggle != null)
        {
            damageNumbersToggle.SetIsOnWithoutNotify(DamageNumbers.IsEnabled);
            damageNumbersToggle.onValueChanged.AddListener(DamageNumbers.SetEnabled);
        }
        //crossbowController.SetShootingState(false);
        crossbowController  = FindObjectOfType<CrossbowController>();
        playerController = FindObjectOfType<PlayerController>();
        // Главное меню использует те же настройки звука без запуска игрового уровня.
        if (playerController == null)
        {
            BindAudioSettings();
            if (menuMusic != null) menuMusic.Play();
            return;
        }
        // Привязываем кнопки
        playButton.onClick.AddListener(StartGame);
        ratingButton.onClick.AddListener(ShowRating);

        BindAudioSettings();

        // Привязываем кнопку для открытия меню
        if (openMenuButton != null)
        {
            openMenuButton.onClick.AddListener(ToggleMenu);
        }

        // Игровая сцена открывается сразу; Esc остаётся обычной паузой.
        PauseGameMusic();
        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (ratingPanel != null) ratingPanel.SetActive(false);
        startWhenReady = true;
        if (GameProgress.IsReady) BeginLoadedGame();
    }

    private void Update()
    {
        if (mobileOrientationPaused && playerController != null && GameProgress.IsReady && !YG.YG2.isPauseGame && Time.timeScale > 0f) PauseGame();
        if (orientationResumePending && playerController != null && GameProgress.IsReady && !YG.YG2.isPauseGame)
        {
            orientationResumePending = false;
            if (!isMenuOpen && activeShop == null && !playerController.IsAwaitingRevive)
            {
                crossbowController.SetShootingState(true); ResumeGame();
            }
        }
        if (startWhenReady && GameProgress.IsReady) BeginLoadedGame();
        // Открытие/закрытие меню по нажатию клавиши Esc
        if (playerController != null && !YG.YG2.isPauseGame && !playerController.IsAwaitingRevive && Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();

        }
    }

    private void BeginLoadedGame()
    {
        if (YG.YG2.isPauseGame) return;
        startWhenReady = false;
        if (playerController.IsAwaitingRevive) PauseForDeath();
        else StartGame();
    }

    public bool BeginTrade(Shop shop)
    {
        if (activeShop != null || isMenuOpen || !GameProgress.IsReady || YG.YG2.isPauseGame || playerController.IsAwaitingRevive) return false;
        activeShop = shop;
        PauseGame();
        return true;
    }

    public void EndTrade(Shop shop)
    {
        if (activeShop != shop) return;
        activeShop = null;
        if (isMenuOpen || playerController.IsAwaitingRevive || YG.YG2.isPauseGame) return;
        crossbowController.SetShootingState(true);
        ResumeGame();
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

    public void SetMobileOrientationPause(bool paused)
    {
        if (mobileOrientationPaused == paused) return;
        mobileOrientationPaused = paused;
        orientationResumePending = !paused;
        if (paused && playerController != null && !YG.YG2.isPauseGame) PauseGame();
    }

    private void ResumeGame()
    {
        if (mobileOrientationPaused) { PauseGame(); return; }
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

    public void ShowSettings()
    {
        if (playerController == null || YG.YG2.isPauseGame || playerController.IsAwaitingRevive) return;
        isMenuOpen = true;
        menuPanel.SetActive(true);
        if (ratingPanel != null) ratingPanel.SetActive(false);
        settingsPanel.SetActive(true);
        PauseGame();
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void ToggleMenu()
    {
        if (YG.YG2.isPauseGame || playerController.IsAwaitingRevive) return;
        var mobile = FindObjectOfType<MobileControls>();
        if (mobile != null && mobile.IsControlsSettingsOpen)
        { mobile.CloseControlsSettings(); return; }
        if (activeShop != null)
        {
            activeShop.CloseTopPanel();
            return;
        }
        if (ratingPanel != null && ratingPanel.activeSelf)
        {
            CloseRating();
            return;
        }
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            CloseSettings();
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
        if (activeShop != null) activeShop.CloseShop();
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

    }

    public void SetMusicVolume(float volume)
    {
        // Устанавливаем громкость для музыки в меню
        if (menuMusic != null)
            menuMusic.volume = volume;

        // Устанавливаем громкость для музыки игры
        foreach (var musicSource in gameMusicSources ?? System.Array.Empty<AudioSource>())
        {
            if (musicSource != null)
                musicSource.volume = volume;
        }

        // Сохраняем значение
        PlayerPrefs.SetFloat("MusicVolume", volume);
        PlayerPrefs.Save();
        if (musicValueText != null) musicValueText.text = Mathf.RoundToInt(volume * 100f) + "%";
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = volume; // Обновляем глобальную громкость
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save(); // Сохраняем в PlayerPrefs
        if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(volume * 100f) + "%";

        // Теперь передаем громкость во все источники звука
        foreach (var sfx in sfxSources ?? System.Array.Empty<AudioSource>())
        {
            if (sfx != null)
                sfx.volume = volume;
        }
    }

    private void BindAudioSettings()
    {
        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MusicVolume", 0.5f));
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            SetMusicVolume(musicSlider.value);
        }
        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 0.5f));
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            SetSFXVolume(sfxSlider.value);
        }
    }

    private void OnDestroy()
    {
        if (damageNumbersToggle != null) damageNumbersToggle.onValueChanged.RemoveListener(DamageNumbers.SetEnabled);
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(SetSFXVolume);
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
