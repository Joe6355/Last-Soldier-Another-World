using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG;
using YG.Utils.LB;

public class TopPlayersPanel : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private LeaderboardRow rowPrefab;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Stats statistics;
    private readonly List<LeaderboardRow> entries = new List<LeaderboardRow>();
    private float nextRefresh, deadline;
    private bool waiting;
    private bool resetScrollPosition;

    private void OnEnable()
    {
        YG2.onGetLeaderboard += OnLeaderboard;
        GameProgress.Changed += OnProfileChanged;
        GameProgress.LeaderboardLoadFailed += OnFailure;
        ShowPlaceholders();
    }

    private void OnDisable()
    {
        YG2.onGetLeaderboard -= OnLeaderboard;
        GameProgress.Changed -= OnProfileChanged;
        GameProgress.LeaderboardLoadFailed -= OnFailure;
    }

    private void Update()
    {
        if (waiting && Time.unscaledTime >= deadline) OnFailure(GameProgress.LeaderboardName);
        if (!waiting && contentParent != null && contentParent.gameObject.activeInHierarchy && Time.unscaledTime >= nextRefresh)
            LoadTopPlayers();
    }

    private void LateUpdate()
    {
        if (!resetScrollPosition || contentParent == null || !contentParent.gameObject.activeInHierarchy) return;
        var scroll = contentParent.GetComponentInParent<ScrollRect>();
        if (scroll != null && contentParent is RectTransform content)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
        resetScrollPosition = false;
    }

    private void OnProfileChanged()
    {
        waiting = false;
        nextRefresh = 0f;
        Clear();
        ShowPlaceholders();
        LoadTopPlayers();
    }

    public void LoadTopPlayers()
    {
        if (!GameProgress.IsReady || waiting || contentParent == null || !contentParent.gameObject.activeInHierarchy || Time.unscaledTime < nextRefresh) return;
        waiting = true;
        deadline = Time.unscaledTime + 12f;
        nextRefresh = Time.unscaledTime + 60f;
        ShowPlaceholders();
        ShowStatus("Загрузка рейтинга…");
        YG2.GetLeaderboard(GameProgress.LeaderboardName, 15, 1, "small");
    }

    private void OnLeaderboard(LBData data)
    {
        if (data == null || data.technoName != GameProgress.LeaderboardName) return;
        waiting = false;
        Clear();
        if (data.players != null)
            foreach (LBPlayerData player in data.players)
            {
                if (player == null || player.name == InfoYG.NO_DATA || player.rank > 15 || player.rank < 1) continue;
                var row = Instantiate(rowPrefab, contentParent);
                row.Bind(player, statistics);
                entries.Add(row);
            }
        bool hasPlayers = entries.Count > 0;
        if (!hasPlayers) ShowPlaceholders();
        resetScrollPosition = true;
        ShowStatus(hasPlayers ? "Общий рейтинг" : "Рейтинг пока пуст");
    }

    private void OnFailure(string name)
    {
        if (name != GameProgress.LeaderboardName) return;
        waiting = false;
        ShowPlaceholders();
        ShowStatus("Рейтинг недоступен");
    }

    private void ShowStatus(string text) { if (statusText != null) statusText.text = text; }

    private void ShowPlaceholders()
    {
        if (entries.Count > 0 || contentParent == null || rowPrefab == null) return;
        for (int i = 0; i < 4; i++)
        {
            var row = Instantiate(rowPrefab, contentParent);
            row.BindPlaceholder(statistics);
            entries.Add(row);
        }
        resetScrollPosition = true;
    }

    private void Clear()
    {
        foreach (var row in entries)
            if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        entries.Clear();
    }
}
