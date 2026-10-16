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
    private string playerName;
    private int playerRank;
    private bool placeholder, anonymous;

    private void Awake() { GameLocalization.Protect(nickname); GameLocalization.Register(gameObject); }
    private void OnEnable() { GameLocalization.Changed += RefreshNickname; RefreshNickname(); }
    private void OnDisable() => GameLocalization.Changed -= RefreshNickname;
    private void RefreshNickname()
    {
        if (nickname == null) return;
        nickname.text = placeholder ? "—  " + GameLocalization.Text("Игрок")
            : playerRank + ". " + (anonymous ? GameLocalization.Text("Игрок") : playerName);
    }

    public void Bind(LBPlayerData player, Stats localStats)
    {
        placeholder = false;
        anonymous = string.IsNullOrWhiteSpace(player.name) || player.name == InfoYG.ANONYMOUS;
        playerName = player.name; playerRank = player.rank;
        nickname.richText = false;
        RefreshNickname();
        mmr.text = "MMR " + Math.Max(0, player.score).ToString("N0");
        medal.color = Color.white;
        medal.sprite = localStats != null ? localStats.MedalForMmr(Math.Max(0, player.score)) : null;
        Stats.LeaderboardSummary summary = null;
        if (!string.IsNullOrEmpty(player.extraData) && player.extraData.Length <= 1024)
            try { summary = JsonUtility.FromJson<Stats.LeaderboardSummary>(player.extraData); }
            catch (ArgumentException) { }
        if (summary != null && summary.IsValid)
        {
            statistics.text = Stats.FormatSummary(summary.waves, summary.kills, summary.elite, summary.bosses, summary.deaths);
        }
        else
        {
            statistics.text = "Статистика не предоставлена";
        }
        bool isCurrent = YG2.player.auth && player.uniqueID == YG2.player.id;
        background.color = isCurrent ? currentPlayerColor : normalColor;
    }

    public void BindPlaceholder(Stats localStats)
    {
        nickname.richText = false;
        placeholder = true;
        RefreshNickname();
        statistics.text = "Волны: —   Враги: —\nЭлита: —   Боссы: —";
        mmr.text = "MMR —";
        medal.sprite = localStats != null ? localStats.MedalForMmr(0) : null;
        medal.color = new Color(1f, 1f, 1f, .25f);
        background.color = normalColor;
    }
}
