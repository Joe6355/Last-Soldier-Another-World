using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG;
using YG.Utils.LB;

public sealed class LeaderboardRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nickname;
    [SerializeField] private TextMeshProUGUI statistics;
    [SerializeField] private Image medal;
    [SerializeField] private TextMeshProUGUI mmr;
    [SerializeField] private Image background;

    public void Bind(LBPlayerData player, Stats localStats)
    {
        string name = string.IsNullOrWhiteSpace(player.name) || player.name == InfoYG.ANONYMOUS ? "Игрок" : player.name;
        nickname.richText = false;
        nickname.text = player.rank + ". " + name;
        mmr.text = "MMR " + Math.Max(0, player.score).ToString("N0");
        Stats.LeaderboardSummary summary = null;
        if (!string.IsNullOrEmpty(player.extraData) && player.extraData.Length <= 1024)
            try { summary = JsonUtility.FromJson<Stats.LeaderboardSummary>(player.extraData); }
            catch (ArgumentException) { }
        if (summary != null && summary.IsValid)
        {
            statistics.text = Stats.FormatSummary(summary.waves, summary.kills, summary.elite, summary.bosses, summary.deaths);
            medal.sprite = localStats.MedalForKills(summary.kills);
        }
        else
        {
            statistics.text = "Статистика не предоставлена";
            medal.sprite = localStats.MedalForKills(0);
        }
        bool isCurrent = YG2.player.auth && player.uniqueID == YG2.player.id;
        background.color = isCurrent ? new Color(0.1f, 0.36f, 0.49f, 1f) : new Color(0.055f, 0.12f, 0.18f, 0.97f);
    }
}
