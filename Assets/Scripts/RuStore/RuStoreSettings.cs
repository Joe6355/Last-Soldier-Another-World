using UnityEngine;

[CreateAssetMenu(menuName = "Last Soldier/RuStore settings")]
public sealed class RuStoreSettings : ScriptableObject
{
    public string packageName = "com.joe6355.lastsoldier.rustore";
    public string versionName = "1.0.0";
    public int versionCode = 1;
    public string rewardedAdUnitId = "";
    public string interstitialAdUnitId = "";
    public string bannerAdUnitId = "";
    public string supportEmail = "dudin1918@mail.ru";
    public string privacyUrl = "https://github.com/Joe6355/Last-Soldier-Another-World/blob/codex/rustore-release/Publishing/RuStore/privacy-policy.md";
    public bool useTestAds = true;
    public bool releasePrivacyReviewed = false;
    public static RuStoreSettings Load() => Resources.Load<RuStoreSettings>("RuStoreSettings");
}
