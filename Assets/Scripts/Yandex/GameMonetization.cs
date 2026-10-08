using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG;

public sealed class GameMonetization : MonoBehaviour
{
    public const string ContinueRewardId = "continue_wave_full_heal";
    public const string MerchantCoinsRewardId = "merchant_bonus_coins";
    public enum CoinsRewardResult { Granted, Cancelled, Unavailable }
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
    private PlayerController coinsRecipient;
    private int coinsAmount, coinsGranted;
    private bool coinsPending, coinsRewarded, coinsError;
    private System.Action<CoinsRewardResult, int> coinsCompleted;
    public bool IsAwaitingContinue => deadPlayer != null;
    public bool IsRewardedAdPending => rewardPending || coinsPending;

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
        if (deadPlayer == null || IsRewardedAdPending || rewardGranted || !GameProgress.IsReady || YG2.nowAdsShow || YG2.isPauseGame) return;
        rewardPending = true;
        rewardError = false;
        SetButtons(false);
        deathStatus.text = "Загрузка видео…";
        YG2.RewardedAdvShow(ContinueRewardId);
    }

    public bool RequestCoinsForVideo(PlayerController recipient, int amount, System.Action<CoinsRewardResult, int> completed)
    {
        if (recipient == null || recipient.hp <= 0 || recipient.IsAwaitingRevive || deadPlayer != null
            || amount <= 0 || recipient.totalCoins > int.MaxValue - amount || IsRewardedAdPending
            || !GameProgress.IsReady || !YG2.isSDKEnabled || YG2.nowAdsShow || YG2.isPauseGame) return false;
        coinsRecipient = recipient;
        coinsAmount = amount;
        coinsGranted = 0;
        coinsRewarded = coinsError = false;
        coinsCompleted = completed;
        coinsPending = true;
        YG2.RewardedAdvShow(MerchantCoinsRewardId);
        return true;
    }

    private void OnReward(string id)
    {
        if (id == MerchantCoinsRewardId)
        {
            if (!coinsPending || coinsRewarded || coinsRecipient == null) return;
            coinsRewarded = true;
            int before = coinsRecipient.totalCoins;
            coinsRecipient.AddCoin(coinsAmount);
            coinsGranted = coinsRecipient.totalCoins - before;
            GameProgress.SaveNow();
            return;
        }
        if (id != ContinueRewardId || !rewardPending || rewardGranted || deadPlayer == null) return;
        rewardGranted = true;
        deadPlayer.ReviveAfterVideo();
        deathStatus.text = "Здоровье восстановлено. Продолжаем…";
        GameProgress.SaveNow();
    }

    private void OnRewardClosed()
    {
        if (coinsPending)
        {
            var result = coinsRewarded ? CoinsRewardResult.Granted : coinsError ? CoinsRewardResult.Unavailable : CoinsRewardResult.Cancelled;
            var completed = coinsCompleted;
            int granted = coinsGranted;
            coinsPending = false;
            coinsRecipient = null;
            coinsCompleted = null;
            completed?.Invoke(result, granted);
        }
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
        if (coinsPending && !coinsRewarded) coinsError = true;
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
        gameUi.ContinueAfterDeath();
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
