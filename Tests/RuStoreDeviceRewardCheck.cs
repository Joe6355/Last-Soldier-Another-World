#if LAST_SOLDIER_RUSTORE_QA && UNITY_ANDROID && !UNITY_EDITOR
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

// Copy into a temporary runtime Assets folder for an explicit QA build only.
// Uses the real merchant request; never emits SDK events or adds coins itself.
[DefaultExecutionOrder(1000)]
public sealed class RuStoreDeviceRewardCheck : MonoBehaviour
{
    private int phase, coinsBefore, granted;
    private float nextPoll, deadline;
    private bool completed;
    private PlayerController player;
    private GameMonetization.CoinsRewardResult result;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        var host = new GameObject("RuStoreDeviceRewardCheck");
        DontDestroyOnLoad(host);
        host.AddComponent<RuStoreDeviceRewardCheck>();
    }
    private void Start()
    {
        deadline = Time.realtimeSinceStartup + 300f;
        Debug.Log("[RuStoreDeviceCheck] Started: real native merchant reward, no simulated callbacks");
    }
    private void Update()
    {
        if (phase >= 4 || Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + .5f;
        if (!GameProgress.IsReady || RuStoreMenu.IsOverlayOpen) return;
        if (phase == 0)
        {
            var menu = FindObjectOfType<MainMenuController>();
            if (menu == null) return;
            phase = 1;
            menu.Play();
            return;
        }
        if (SceneManager.GetActiveScene().buildIndex != 1) return;
        if (phase == 1)
        {
            player = FindObjectOfType<PlayerController>();
            var ui = FindObjectOfType<Ui>();
            if (player == null || ui == null) return;
            ui.StartGame();
            coinsBefore = player.totalCoins;
            Debug.Log("[RuStoreDeviceCheck] BEFORE coins=" + coinsBefore);
            phase = 2;
        }
        if (phase != 2 || YG2.nowAdsShow || YG2.isPauseGame) return;
        var readiness = typeof(RuStoreAds).GetField("rewarded", BindingFlags.Instance | BindingFlags.NonPublic);
        if (readiness == null) { Fail("Reward readiness field missing"); return; }
        if (readiness.GetValue(RuStoreAds.Instance) == null) return;
        var monetization = FindObjectOfType<GameMonetization>();
        if (monetization == null) return;
        phase = 3;
        bool accepted = monetization.RequestCoinsForVideo(player, 500, (value, amount) =>
        {
            result = value; granted = amount; completed = true;
            Debug.Log("[RuStoreDeviceCheck] CALLBACK result=" + value + " granted=" + amount);
        });
        Debug.Log("[RuStoreDeviceCheck] REQUEST accepted=" + accepted);
        if (!accepted) Fail("Production merchant request rejected");
    }
    private void LateUpdate()
    {
        if (phase >= 4) return;
        if (completed && !YG2.nowAdsShow && !YG2.isPauseGame)
        {
            phase = 4;
            bool passed = result == GameMonetization.CoinsRewardResult.Granted && granted == 500
                && player != null && player.totalCoins == coinsBefore + 500;
            Debug.Log("[RuStoreDeviceCheck] " + (passed ? "PASS" : "FAIL") + " before=" + coinsBefore
                + " after=" + (player != null ? player.totalCoins.ToString() : "missing")
                + " granted=" + granted + " result=" + result);
            return;
        }
        if (Time.realtimeSinceStartup > deadline) Fail("Timeout phase=" + phase + " callback=" + completed);
    }
    private void Fail(string reason)
    {
        phase = 4;
        Debug.LogError("[RuStoreDeviceCheck] FAIL " + reason);
    }
}
#endif
