using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    private void OnEnable()
    {
        YG2.onGetLeaderboard += OnLeaderboard;
        GameProgress.Changed += OnProfileChanged;
        GameProgress.LeaderboardLoadFailed += OnFailure;
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

    private void OnProfileChanged()
    {
        waiting = false;
        nextRefresh = 0f;
        Clear();
        LoadTopPlayers();
    }

    public void LoadTopPlayers()
    {
        if (!GameProgress.IsReady || waiting || contentParent == null || !contentParent.gameObject.activeInHierarchy || Time.unscaledTime < nextRefresh) return;
        waiting = true;
        deadline = Time.unscaledTime + 12f;
        nextRefresh = Time.unscaledTime + 60f;
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
        ShowStatus(entries.Count == 0 ? "В рейтинге пока нет игроков" : YG2.player.auth
            ? "Больше зачищенных волн и побед — выше MMR" : "Войдите через Яндекс ID в главном меню, чтобы попасть в рейтинг");
    }

    private void OnFailure(string name)
    {
        if (name != GameProgress.LeaderboardName) return;
        waiting = false;
        ShowStatus("Рейтинг временно недоступен. Ваш прогресс сохранён");
    }

    private void ShowStatus(string text) { if (statusText != null) statusText.text = text; }

    private void Clear()
    {
        foreach (var row in entries) if (row != null) Destroy(row.gameObject);
        entries.Clear();
    }
}
