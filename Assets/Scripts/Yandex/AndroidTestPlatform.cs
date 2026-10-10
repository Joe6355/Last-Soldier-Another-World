#if LAST_SOLDIER_ANDROID_TEST && !UNITY_WEBGL
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG.Insides;

// Only the separate Android test build uses these offline platform callbacks.
namespace YG
{
    public partial class PlatformYG2
    {
        public void OpenAuthDialog() => GameProgress.AuthorizationClosed();
        public void GetLeaderboard(string nameLB, int quantityTop, int quantityAround, string photoSizeLB)
            => GameProgress.ReportLeaderboardFailure(nameLB);
        public void RewardedAdvShow(string id) => AndroidTestAd.Show(id);
        public void InterstitialAdvShow() => AndroidTestAd.Show(null);
    }
}

public sealed class AndroidTestAd : MonoBehaviour
{
    private static AndroidTestAd active;
    private string rewardId;
    private float availableAt;
    private TextMeshProUGUI status;
    private Button finishButton;
    private bool closed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
        active = null;
        // The overlay blocks gameplay raycasts and needs menu events to stay enabled.
        YG.YG2.infoYG.Basic.editEventSystem = false;
        Application.targetFrameRate = 60;
    }

    public static void Show(string id)
    {
        if (active != null) return;
        var host = new GameObject("AndroidTestAd", typeof(RectTransform));
        DontDestroyOnLoad(host);
        active = host.AddComponent<AndroidTestAd>();
        active.rewardId = id;
        active.availableAt = Time.realtimeSinceStartup + 3f;
        if (id != null) YGInsides.OpenRewardedAdv(); else YGInsides.OpenInterAdv();
        active.CreateUi();
    }

    private void CreateUi()
    {
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        gameObject.AddComponent<Image>().color = new Color32(18, 39, 34, 250);
        Text("Title", rewardId != null ? "Тестовое видео" : "Тестовая реклама", .68f, .84f, 34);
        Text("Hint", "В этом APK используется симуляция рекламы", .55f, .66f, 23);
        status = Text("Status", "", .42f, .54f, 24);
        finishButton = Button("Finish", rewardId != null ? "Получить награду" : "Закрыть", .27f, .38f);
        finishButton.onClick.AddListener(() => Close(true));
        if (rewardId != null) Button("Cancel", "Закрыть без награды", .12f, .23f).onClick.AddListener(() => Close(false));
        Update();
    }

    private void Update()
    {
        if (status == null) return;
        float remaining = Mathf.Max(0f, availableAt - Time.realtimeSinceStartup);
        finishButton.interactable = remaining <= 0f;
        status.text = remaining > 0f ? "Подождите " + Mathf.CeilToInt(remaining) + " сек." : "Проверка завершена";
    }

    public void Close(bool completed)
    {
        if (closed || completed && Time.realtimeSinceStartup < availableAt) return;
        closed = true;
        gameObject.SetActive(false);
        if (rewardId != null)
        {
            if (completed) YGInsides.RewardAdv(rewardId);
            YGInsides.CloseRewardedAdv();
        }
        else YGInsides.CloseInterAdv(completed);
        active = null;
        Destroy(gameObject);
    }

    private TextMeshProUGUI Text(string name, string caption, float bottom, float top, int size)
    {
        var node = Node(name, bottom, top); var label = node.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset; label.text = caption; label.fontSize = label.fontSizeMax = size;
        label.fontSizeMin = size * .75f; label.enableAutoSizing = true; label.alignment = TextAlignmentOptions.Center;
        label.color = new Color32(244, 240, 223, 255); label.raycastTarget = false; return label;
    }

    private Button Button(string name, string caption, float bottom, float top)
    {
        var node = Node(name, bottom, top); var image = node.AddComponent<Image>(); image.color = new Color32(50, 83, 70, 255);
        var button = node.AddComponent<Button>(); button.targetGraphic = image;
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(node.transform, false);
        var rect = (RectTransform)text.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = text.GetComponent<TextMeshProUGUI>(); label.font = TMP_Settings.defaultFontAsset; label.fontSize = 24;
        label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 24;
        label.text = caption; label.color = new Color32(244, 240, 223, 255); label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return button;
    }

    private GameObject Node(string name, float bottom, float top)
    {
        var node = new GameObject(name, typeof(RectTransform)); node.transform.SetParent(transform, false);
        var rect = (RectTransform)node.transform; rect.anchorMin = new Vector2(.2f, bottom); rect.anchorMax = new Vector2(.8f, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero; node.layer = 5; return node;
    }
}
#endif
