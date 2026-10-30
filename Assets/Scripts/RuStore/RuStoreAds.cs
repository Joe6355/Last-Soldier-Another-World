#if LAST_SOLDIER_RUSTORE
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using YandexMobileAds;
using YandexMobileAds.Base;
using YG.Insides;

// Adapt real Mobile Ads callbacks to the existing, guarded game rewards.
public sealed class RuStoreAds : MonoBehaviour
{
    private static RuStoreAds instance;
    private RewardedAdLoader rewardedLoader;
    private InterstitialAdLoader interstitialLoader;
    private RewardedAd rewarded, activeRewarded;
    private Interstitial interstitial, activeInterstitial;
    private Banner banner;
    private BannerAdSize bannerSize;
    private bool initialized, rewardLoading, interLoading, rewardedGranted, adShown, bannerLoaded, bannerWanted, bannerVisible;
    private bool applicationPaused, applicationFocused = true;
    private float nextInitialize, nextRewardLoad, nextInterLoad, showDeadline;
    private string rewardId;
    private const float ShowTimeoutSeconds = 15f;
    public const string PrivacyNoticeKey = "LastSoldier.RuStore.PrivacyNotice.v1";
    private static RuStoreSettings Settings => RuStoreSettings.Load();
    public static RuStoreAds Instance
    {
        get
        {
            if (instance != null) return instance;
            var host = new GameObject("RuStoreAds"); DontDestroyOnLoad(host);
            return instance = host.AddComponent<RuStoreAds>();
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { var ads = Instance; }
    private static string Unit(string production, string demo) => Settings != null && Settings.useTestAds ? demo : production;
    private void Update()
    {
        if (!initialized && Time.unscaledTime >= nextInitialize && PlayerPrefs.GetInt(PrivacyNoticeKey, 0) == 1) Initialize();
        if (!initialized) return;
        if (rewarded == null && activeRewarded == null && !rewardLoading && Time.unscaledTime >= nextRewardLoad) LoadRewarded();
        if (interstitial == null && activeInterstitial == null && !interLoading && Time.unscaledTime >= nextInterLoad) LoadInterstitial();
        bool visible = bannerLoaded && bannerWanted && SceneManager.GetActiveScene().buildIndex == 0
            && !RuStoreMenu.IsOverlayOpen && !YG.YG2.nowAdsShow;
        if (banner != null && visible != bannerVisible)
        {
            bannerVisible = visible;
            if (visible) banner.Show(); else banner.Hide();
        }
        RuStoreMenu.SetBannerInset(visible && bannerSize != null ? bannerSize.Height : 0);
    }
    private void LateUpdate()
    {
        // The SDK dispatches native callbacks in Update. Let it deliver queued
        // reward/dismissal events before deciding that Show received no response.
        if (applicationPaused || !applicationFocused || adShown || showDeadline <= 0f || Time.unscaledTime <= showDeadline) return;
        if (activeRewarded != null) CloseRewarded(activeRewarded, true);
        if (activeInterstitial != null) CloseInterstitial(activeInterstitial, true);
    }
    private void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        if (!paused) RearmShowTimeout();
    }
    private void OnApplicationFocus(bool focused)
    {
        applicationFocused = focused;
        if (focused) RearmShowTimeout();
    }
    private void RearmShowTimeout()
    {
        // Time spent inside the native AdActivity is not a failed Show request.
        if (!adShown && (activeRewarded != null || activeInterstitial != null))
            showDeadline = Time.unscaledTime + ShowTimeoutSeconds;
    }
    private void Initialize()
    {
        if (Settings == null) return;
        try
        {
            // Conservative first release: contextual ads, no location tracking.
            YandexAds.SetUserConsent(false);
            YandexAds.SetAgeRestricted(true);
            YandexAds.SetLocationTracking(false);
            rewardedLoader = new RewardedAdLoader(); interstitialLoader = new InterstitialAdLoader();
            initialized = true;
            LoadRewarded(); LoadInterstitial();
            string id = Unit(Settings.bannerAdUnitId, "demo-banner-yandex");
            if (!string.IsNullOrWhiteSpace(id))
            {
                bannerSize = BannerAdSize.Sticky(Screen.width);
                banner = new Banner(bannerSize, AdPosition.BottomCenter);
                banner.OnAdLoaded += (_, __) => { bannerLoaded = true; banner.Hide(); };
                banner.OnAdFailedToLoad += (_, __) => { bannerLoaded = false; banner.Hide(); };
                banner.Hide(); banner.LoadAd(new AdRequest(id));
            }
        }
        catch (Exception) { initialized = false; nextInitialize = Time.unscaledTime + 30f; }
    }
    private void LoadRewarded()
    {
        string id = Unit(Settings.rewardedAdUnitId, "demo-rewarded-yandex");
        if (string.IsNullOrWhiteSpace(id)) { nextRewardLoad = Time.unscaledTime + 60f; return; }
        rewardLoading = true;
        try
        {
            rewardedLoader.LoadAd(new AdRequest(id), ad =>
            {
                rewardLoading = false; rewarded = ad;
                ad.OnAdShown += (_, __) =>
                {
                    if (activeRewarded != ad) return;
                    adShown = true; showDeadline = 0f;
                    Debug.Log("[RuStoreAds] Rewarded shown");
                };
                ad.OnRewarded += (_, __) =>
                {
                    if (activeRewarded != ad || rewardedGranted) return;
                    rewardedGranted = true;
                    Debug.Log("[RuStoreAds] Rewarded earned");
                    YGInsides.RewardAdv(rewardId);
                };
                ad.OnAdDismissed += (_, __) => CloseRewarded(ad, false);
                ad.OnAdFailedToShow += (_, __) => CloseRewarded(ad, true);
            }, error => { rewardLoading = false; nextRewardLoad = Time.unscaledTime + 30f; });
        }
        catch (Exception) { rewardLoading = false; nextRewardLoad = Time.unscaledTime + 30f; }
    }
    private void LoadInterstitial()
    {
        string id = Unit(Settings.interstitialAdUnitId, "demo-interstitial-yandex");
        if (string.IsNullOrWhiteSpace(id)) { nextInterLoad = Time.unscaledTime + 60f; return; }
        interLoading = true;
        try
        {
            interstitialLoader.LoadAd(new AdRequest(id), ad =>
            {
                interLoading = false; interstitial = ad;
                ad.OnAdShown += (_, __) => { if (activeInterstitial == ad) { adShown = true; showDeadline = 0f; } };
                ad.OnAdDismissed += (_, __) => CloseInterstitial(ad, false);
                ad.OnAdFailedToShow += (_, __) => CloseInterstitial(ad, true);
            }, error => { interLoading = false; nextInterLoad = Time.unscaledTime + 30f; });
        }
        catch (Exception) { interLoading = false; nextInterLoad = Time.unscaledTime + 30f; }
    }
    public void ShowRewarded(string id)
    {
        if (activeRewarded != null || activeInterstitial != null) return;
        YGInsides.OpenRewardedAdv();
        if (rewarded == null)
        {
            YGInsides.ErrorRewardedAdv(); YGInsides.CloseRewardedAdv();
            return;
        }
        rewardId = id; rewardedGranted = adShown = false;
        activeRewarded = rewarded; rewarded = null; showDeadline = Time.unscaledTime + ShowTimeoutSeconds;
        Debug.Log("[RuStoreAds] Rewarded requested");
        try { activeRewarded.Show(); } catch (Exception) { CloseRewarded(activeRewarded, true); }
    }
    private void CloseRewarded(RewardedAd ad, bool failed)
    {
        if (activeRewarded != ad || ad == null) return;
        activeRewarded = null; showDeadline = 0f;
        Debug.Log("[RuStoreAds] Rewarded closed: " + (rewardedGranted ? "granted" : failed ? "failed" : "cancelled"));
        if (failed && !rewardedGranted) YGInsides.ErrorRewardedAdv();
        YGInsides.CloseRewardedAdv();
        ad.Destroy(); nextRewardLoad = Time.unscaledTime + 1f;
    }
    public void ShowInterstitial()
    {
        if (activeRewarded != null || activeInterstitial != null) return;
        YGInsides.OpenInterAdv();
        if (interstitial == null) { YGInsides.ErrorInterAdv(); YGInsides.CloseInterAdv(false); return; }
        activeInterstitial = interstitial; interstitial = null; adShown = false; showDeadline = Time.unscaledTime + ShowTimeoutSeconds;
        try { activeInterstitial.Show(); } catch (Exception) { CloseInterstitial(activeInterstitial, true); }
    }
    private void CloseInterstitial(Interstitial ad, bool failed)
    {
        if (activeInterstitial != ad || ad == null) return;
        bool shown = adShown && !failed;
        activeInterstitial = null; showDeadline = 0f;
        if (failed) YGInsides.ErrorInterAdv();
        YGInsides.CloseInterAdv(shown);
        ad.Destroy(); nextInterLoad = Time.unscaledTime + 1f;
    }
    public void SetBannerVisible(bool value) => bannerWanted = value;
    private void OnDestroy()
    {
        rewardedLoader?.CancelLoading(); interstitialLoader?.CancelLoading();
        rewarded?.Destroy(); interstitial?.Destroy(); banner?.Destroy();
        if (instance == this) instance = null;
    }
}
#endif
