using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YG;
using YG.Utils.LB;

public class TopPlayersPanel : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private TextMeshProUGUI playerEntryPrefab;
    private readonly List<TextMeshProUGUI> entries = new List<TextMeshProUGUI>();
    private float nextRefresh, deadline;
    private bool waiting;

    private void OnEnable()
    {
        YG2.onGetLeaderboard += OnLeaderboard;
        GameProgress.Changed += LoadTopPlayers;
    }

    private void OnDisable()
    {
        YG2.onGetLeaderboard -= OnLeaderboard;
        GameProgress.Changed -= LoadTopPlayers;
    }

    private void Start() => LoadTopPlayers();

    private void Update()
    {
        if (waiting && Time.unscaledTime >= deadline)
        {
            waiting = false;
            ShowStatus("Рейтинг временно недоступен");
        }
        if (!waiting && contentParent.gameObject.activeInHierarchy && Time.unscaledTime >= nextRefresh)
            LoadTopPlayers();
    }

    public void LoadTopPlayers()
    {
        if (!GameProgress.IsReady || waiting) return;
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
        if (data.players == null || data.players.Length == 0)
        {
            AddEntry("В рейтинге пока нет игроков");
            return;
        }
        foreach (LBPlayerData player in data.players)
        {
            if (player == null || player.name == InfoYG.NO_DATA || player.rank > 15) continue;
            string name = string.IsNullOrEmpty(player.name) || player.name == InfoYG.ANONYMOUS
                ? "Игрок" : player.name;
            // The console leaderboard must use decimalOffset = 2.
            AddEntry($"{player.rank}. {name} — Рейтинг: {player.score / 100d:F2}");
        }
        if (entries.Count == 0) AddEntry("Рейтинг временно недоступен");
    }

    private void ShowStatus(string text) { Clear(); AddEntry(text); }

    private void Clear()
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);
        entries.Clear();
    }

    private void AddEntry(string text)
    {
        var entry = Instantiate(playerEntryPrefab, contentParent);
        entry.richText = false;
        entry.text = text;
        entries.Add(entry);
    }
}
