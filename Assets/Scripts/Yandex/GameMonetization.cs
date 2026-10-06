using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG;

public sealed class GameMonetization : MonoBehaviour
{
    public const string ContinueRewardId = "continue_wave_full_heal";
    private static GameMonetization instance;

    [Header("Смерть игрока — продолжение за видео")]
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button returnButton;
    [SerializeField] private TextMeshProUGUI deathStatus;
    [SerializeField] private Ui gameUi;

    [Header("Sticky-баннер — только в меню")]
    [SerializeField] private GameObject[] bannerMenuPanels;
    [SerializeField] private GameObject editorBannerPreview;
    [Header("Метки рекламы — видны только в редакторе")]
    [SerializeField] private GameObject editorInterstitialMarker;

    private PlayerController player, deadPlayer;
    private bool rewardPending, rewardGranted, rewardError, bannerVisible;
    public bool IsAwaitingContinue => deadPlayer != null;

    private void Awake() => instance = this;

    private void Start()
    {
        player = FindObjectOfType<PlayerController>();
        if (continueButton != null && continueButton.onClick.GetPersistentEventCount() == 0) continueButton.onClick.AddListener(ContinueForVideo);
        if (returnButton != null && returnButton.onClick.GetPersistentEventCount() == 0) returnButton.onClick.AddListener(ReturnToCamp);
        YG2.onRewardAdv += OnReward;
        YG2.onCloseRewardedAdv += OnRewardClosed;
        YG2.onErrorRewardedAdv += OnRewardError;
#if !UNITY_EDITOR
        if (editorBannerPreview != null) editorBannerPreview.SetActive(false);
        if (editorInterstitialMarker != null) editorInterstitialMarker.SetActive(false);
#endif
    }

    private void OnDestroy()
    {
        YG2.onRewardAdv -= OnReward;
        YG2.onCloseRewardedAdv -= OnRewardClosed;
        YG2.onErrorRewardedAdv -= OnRewardError;
        if (instance == this) instance = null;
        if (bannerVisible && YG2.isSDKEnabled) YG2.StickyAdActivity(false);
    }

    private void Update()
    {
        if (deadPlayer == null && deathPanel != null && GameProgress.IsReady)
        {
            if (player != null && player.IsAwaitingRevive) BeginDeath(player);
        }
        if (deadPlayer != null && rewardGranted && !rewardPending && !YG2.isPauseGame)
        {
            deadPlayer = null;
            deathPanel.SetActive(false);
            gameUi.ContinueAfterDeath();
        }
        bool showBanner = false;
        if (bannerMenuPanels != null)
            foreach (var panel in bannerMenuPanels)
                if (panel != null && panel.activeInHierarchy) showBanner = true;
        showBanner &= !YG2.nowAdsShow && deadPlayer == null;
#if UNITY_EDITOR
        if (editorBannerPreview != null) editorBannerPreview.SetActive(showBanner);
#endif
        if (YG2.isSDKEnabled && showBanner != bannerVisible)
        {
            bannerVisible = showBanner;
            YG2.StickyAdActivity(showBanner);
        }
    }

    public static bool HandleDeath(PlayerController player) => instance != null && instance.BeginDeath(player);

    private bool BeginDeath(PlayerController player)
    {
        if (deathPanel == null || gameUi == null) return false;
        if (deadPlayer != null) return true;
        deadPlayer = player;
        rewardGranted = rewardPending = rewardError = false;
        gameUi.PauseForDeath();
        deathPanel.SetActive(true);
        var waves = FindObjectOfType<WaveSpawner>();
        string wave = waves != null && waves.IsRunning ? "Волна " + waves.CurrentWaveNumber + " сохранена. " : "";
        deathStatus.text = wave + "Посмотрите видео, чтобы продолжить с полным здоровьем";
        SetButtons(true);
        GameProgress.SaveNow();
        return true;
    }

    public void ContinueForVideo()
    {
        if (deadPlayer == null || rewardPending || rewardGranted || !GameProgress.IsReady || YG2.nowAdsShow || YG2.isPauseGame) return;
        rewardPending = true;
        rewardError = false;
        SetButtons(false);
        deathStatus.text = "Загрузка видео…";
        YG2.RewardedAdvShow(ContinueRewardId);
    }

    private void OnReward(string id)
    {
        if (id != ContinueRewardId || !rewardPending || rewardGranted || deadPlayer == null) return;
        rewardGranted = true;
        deadPlayer.ReviveAfterVideo();
        deathStatus.text = "Здоровье восстановлено. Продолжаем…";
        GameProgress.SaveNow();
    }

    private void OnRewardClosed()
    {
        if (!rewardPending) return;
        rewardPending = false;
        if (!rewardGranted)
        {
            deathStatus.text = rewardError ? "Видео сейчас недоступно. Попробуйте позже или вернитесь в лагерь"
                : "Просмотр не завершён. Можно попробовать ещё раз или вернуться в лагерь";
            SetButtons(true);
        }
    }

    private void OnRewardError()
    {
        if (deadPlayer == null || !rewardPending || rewardGranted) return;
        rewardError = true;
        deathStatus.text = "Видео сейчас недоступно. Попробуйте позже или вернитесь в лагерь";
        // Close callback unlocks the buttons after SDK pause has been restored.
    }

    public void ReturnToCamp()
    {
        if (deadPlayer == null || rewardPending || YG2.nowAdsShow || YG2.isPauseGame) return;
        deadPlayer.ReturnToCampAfterDeath();
        deadPlayer = null;
        deathPanel.SetActive(false);
        gameUi.ShowMenuAfterDeath();
        GameProgress.SaveNow();
        // Natural break after death; the SDK controls display frequency and availability.
        YG2.InterstitialAdvShow();
    }

    private void SetButtons(bool enabled)
    {
        continueButton.interactable = enabled;
        returnButton.interactable = enabled;
    }
}
