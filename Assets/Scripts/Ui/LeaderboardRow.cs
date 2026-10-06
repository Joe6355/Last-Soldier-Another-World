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
    [SerializeField] private Color normalColor = new Color(0.08f, 0.17f, 0.15f, 1f);
    [SerializeField] private Color currentPlayerColor = new Color(0.14f, 0.27f, 0.23f, 1f);

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
        background.color = isCurrent ? currentPlayerColor : normalColor;
    }
}
