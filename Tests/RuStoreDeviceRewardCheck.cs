#if LAST_SOLDIER_RUSTORE_QA && UNITY_ANDROID && !UNITY_EDITOR
using System.Reflection;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YG;

// Copy into a temporary runtime Assets folder for an explicit QA build only.
// Default mode uses the real merchant request; an explicit intent extra checks menus instead.
// Neither mode emits SDK events or adds coins itself.
[DefaultExecutionOrder(1000)]
public sealed class RuStoreDeviceRewardCheck : MonoBehaviour
{
    private int phase, coinsBefore, granted;
    private float nextPoll, deadline;
    private bool completed;
    private bool menuMode;
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
        using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            menuMode = intent.Call<bool>("getBooleanExtra", "lastsoldier_ui_check", false);
        deadline = Time.realtimeSinceStartup + 300f;
        Debug.Log(menuMode ? "[RuStoreMenuCheck] Started: menu geometry and existing handlers; no ad request or coin mutation"
            : "[RuStoreDeviceCheck] Started: real native merchant reward, no simulated callbacks");
    }
    private void Update()
    {
        if (menuMode) { UpdateMenus(); return; }
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
        if (menuMode) return;
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
        phase = menuMode ? 100 : 4;
        Debug.LogError((menuMode ? "[RuStoreMenuCheck] FAIL " : "[RuStoreDeviceCheck] FAIL ") + reason);
    }

    private void UpdateMenus()
    {
        if (phase >= 100 || Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + 1f;
        if (Time.realtimeSinceStartup > deadline) { Fail("Menu timeout phase=" + phase); return; }
        if (!GameProgress.IsReady) return;
        try
        {
            var menu = FindObjectOfType<MainMenuController>();
            var ui = FindObjectOfType<Ui>();
            switch (phase)
            {
                case 0:
                    if (menu == null) return;
                    Debug.Log("[RuStoreMenuCheck] screen=" + Screen.width + "x" + Screen.height + " safe=" + Screen.safeArea
                        + " privacyAck=" + UnityEngine.PlayerPrefs.GetInt(RuStoreAds.PrivacyNoticeKey, 0));
                    // Dismiss only the test overlay; do not write privacy acknowledgement.
                    if (RuStoreMenu.IsOverlayOpen) RuStoreMenu.Close();
                    ValidateMenu(GameObject.Find("RuStoreBannerSafeContent").transform, "main");
                    menu.ShowSettings(); phase++; break;
                case 1:
                    ValidateMenu(menu.SettingsPanel.transform, "main settings");
                    menu.CloseSettings(); menu.Play(); phase++; break;
                case 2:
                    if (SceneManager.GetActiveScene().buildIndex != 1 || ui == null) return;
                    player = FindObjectOfType<PlayerController>();
                    coinsBefore = player.totalCoins;
                    Debug.Log("[RuStoreMenuCheck] BEFORE coins=" + coinsBefore);
                    ui.StartGame(); ui.ToggleMenu(); phase++; break;
                case 3:
                    ValidateMenu(GameObject.Find("RuStorePauseSafeContent").transform, "pause");
                    ui.ShowSettings(); phase++; break;
                case 4:
                    ValidateMenu(ui.SettingsPanel.transform, "pause settings");
                    FindObjectOfType<MobileControls>().OpenControlsSettings(); phase++; break;
                case 5:
                    var mobile = FindObjectOfType<MobileControls>();
                    if (!mobile.IsControlsSettingsOpen) throw new Exception("Control settings did not open");
                    ValidateMenu(GameObject.Find("RuStoreControlsSafeContent").transform, "controls");
                    mobile.CloseControlsSettings(); ui.CloseSettings(); ui.StartGame();
                    if (ui.IsMenuOpen || Time.timeScale <= 0 || player.totalCoins != coinsBefore)
                        throw new Exception("Resume or unchanged coin balance check failed");
                    ui.ToggleMenu();
                    Debug.Log("[RuStoreMenuCheck] PASS before=" + coinsBefore + " after=" + player.totalCoins
                        + " paused=" + ui.IsMenuOpen + "; typed UI handlers, no screen capture or ad request");
                    phase = 100; break;
            }
        }
        catch (Exception error) { Fail("phase=" + phase + " " + error); }
    }
    private static void ValidateMenu(Transform root, string label)
    {
        Canvas.ForceUpdateCanvases();
        int count = 0;
        foreach (var control in root.GetComponentsInChildren<Selectable>().Where(value => value.isActiveAndEnabled))
        {
            var corners = new Vector3[4]; ((RectTransform)control.transform).GetWorldCorners(corners);
            var canvas = control.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(value => RectTransformUtility.WorldToScreenPoint(camera, value)).ToArray();
            var rect = Rect.MinMaxRect(points.Min(value => value.x), points.Min(value => value.y),
                points.Max(value => value.x), points.Max(value => value.y));
            var safe = Screen.safeArea;
            if (rect.xMin < safe.xMin - 1 || rect.yMin < safe.yMin - 1 || rect.xMax > safe.xMax + 1 || rect.yMax > safe.yMax + 1
                || rect.height < Screen.height * .085f) throw new Exception(label + "/" + control.name + " invalid touch area " + rect);
            count++;
            Debug.Log("[RuStoreMenuCheck] TARGET " + label + "/" + control.name + " " + rect);
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>().Where(value => value.isActiveAndEnabled))
        {
            text.ForceMeshUpdate();
            if (text.isTextOverflowing) throw new Exception(label + "/" + text.name + " clipped text");
        }
        if (count == 0) throw new Exception(label + " has no visible controls");
        Debug.Log("[RuStoreMenuCheck] OK " + label + " controls=" + count);
    }
}
#endif
