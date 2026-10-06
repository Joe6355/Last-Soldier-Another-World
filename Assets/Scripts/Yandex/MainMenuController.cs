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
        authLabel.text = YG2.player.auth ? "Яндекс ID подключён" : "Войти через Яндекс ID";
        statusText.richText = false;
        statusText.text = GameProgress.AuthPending ? "Вход через Яндекс ID…"
            : !GameProgress.IsReady ? "Загрузка сохранения…"
            : YG2.player.auth ? "Игрок: " + YG2.player.name
            : "Можно играть без регистрации.\nЯндекс ID — прогресс на других устройствах и участие в рейтинге.";
        if (GameProgress.IsReady && !GameProgress.CloudAvailable)
            statusText.text += "\nОблако недоступно. Прогресс сохраняется на этом устройстве.";
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
        ratingPanel.SetActive(true);
    }

    public void CloseRating()
    {
        if (ratingPanel != null) ratingPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
        if (titlePanel != null) titlePanel.SetActive(true);
    }

    private void Update()
    {
        if (!YG2.isPauseGame && ratingPanel != null && ratingPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseRating();
    }
}
