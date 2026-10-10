using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YG;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button authButton;
    [SerializeField] private Button ratingButton;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject ratingPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject desktopControlBindings;
    [SerializeField] private GameObject mobileControlsInfo;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI authLabel;
    private bool loading;

    private void OnEnable() => GameProgress.Changed += Refresh;
    private void OnDisable() => GameProgress.Changed -= Refresh;
    private void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Refresh();
        // The menu is usable only after profile and progress initialization.
        if (GameProgress.IsReady) YG2.GameReadyAPI();
    }

    private void Refresh()
    {
        playButton.interactable = GameProgress.IsReady && !loading;
        if (ratingButton != null) ratingButton.interactable = GameProgress.IsReady && !loading;
        authButton.interactable = GameProgress.IsReady && !YG2.player.auth && !loading;
        authButton.gameObject.SetActive(!YG2.player.auth);
        authLabel.text = "Войти в Яндекс";
        statusText.richText = false;
        statusText.text = GameProgress.AuthPending ? "Вход через Яндекс ID…"
            : !GameProgress.IsReady ? "Загрузка сохранения…"
            : YG2.player.auth ? "Облачное сохранение"
            : "Вход сохранит прогресс в облаке";
        if (GameProgress.IsReady && !GameProgress.CloudAvailable)
            statusText.text = "Сохранение на этом устройстве";
        if (GameProgress.IsReady) YG2.GameReadyAPI();
    }

    public void Play()
    {
        if (!GameProgress.IsReady || loading) return;
        loading = true;
        GameProgress.SaveNow();
        Refresh();
        SceneManager.LoadSceneAsync(1);
    }

    public void SignIn() => GameProgress.BeginAuthorization();

    public void ShowRating()
    {
        if (!GameProgress.IsReady || loading || ratingPanel == null) return;
        GameProgress.SaveNow();
        if (menuPanel != null) menuPanel.SetActive(false);
        if (titlePanel != null) titlePanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        ratingPanel.SetActive(true);
    }

    public void CloseRating()
    {
        if (ratingPanel != null) ratingPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
        if (titlePanel != null) titlePanel.SetActive(true);
        if (profilePanel != null) profilePanel.SetActive(true);
    }

    public void ShowSettings()
    {
        if (loading || settingsPanel == null) return;
        CloseRating();
        if (menuPanel != null) menuPanel.SetActive(false);
        if (titlePanel != null) titlePanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(false);
        if (desktopControlBindings != null) desktopControlBindings.SetActive(!MobileControls.IsTouchDevice);
        if (mobileControlsInfo != null) mobileControlsInfo.SetActive(MobileControls.IsTouchDevice);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        CloseRating();
    }

    private void Update()
    {
        if (YG2.isPauseGame || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
        else if (ratingPanel != null && ratingPanel.activeSelf) CloseRating();
    }
}
