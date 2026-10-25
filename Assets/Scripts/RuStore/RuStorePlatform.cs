#if LAST_SOLDIER_RUSTORE
using UnityEngine;

namespace YG
{
    public partial class PlatformYG2
    {
        public void InitAwake()
        {
            YG2.infoYG.Basic.editEventSystem = false;
            Application.targetFrameRate = 60;
        }
        public void InitAuth() { YG2.player.auth = false; YG2.player.id = "guest"; YG2.player.name = ""; }
        public void GetAuth() => InitAuth();
        public void OpenAuthDialog() => GameProgress.AuthorizationClosed();
        public void GetLeaderboard(string name, int top, int around, string photoSize)
            => GameProgress.ReportLeaderboardFailure(name);
        public void SetLeaderboard(string name, int score, string extraData)
            => GameProgress.RatingFailed();
        public void RewardedAdvShow(string id) => RuStoreAds.Instance.ShowRewarded(id);
        public void InterstitialAdvShow() => RuStoreAds.Instance.ShowInterstitial();
        public void StickyAdActivity(bool show) => RuStoreAds.Instance.SetBannerVisible(show);
    }
}
#endif
