using PlayerPrefs = RedefineYG.PlayerPrefs;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class Stats : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Text[] texts;         // [0] = враги, [1] = игрок, [2] = боссы, [3] = элитные
    [SerializeField] private Image LvlMedalki;       // UI-Image, куда ставим медаль

    [Header("Medal Sprites")]
    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private TextMeshProUGUI mmrText;
    [Header("Карточка в меню")]
    [SerializeField] private TextMeshProUGUI profileWavesText;
    [SerializeField] private TextMeshProUGUI profileKillsText;
    [SerializeField] private TextMeshProUGUI profileMmrText;
    [SerializeField] private Image profileMedal;
    [SerializeField] private Sprite bronzeMedal;
    [SerializeField] private Sprite silverMedal;
    [SerializeField] private Sprite goldMedal;
    [SerializeField] private Sprite diamondMedal;

    [Header("Stats Counters")]
    public int countEnemyDead;       // Убито обычных врагов
    public int countPlayerDead;      // Смертей игрока
    public int countBossDead;        // Убито боссов
    public int countElitEnemyDead;   // Убито элитных врагов

    public int completedWaves;
    public int TotalKills => (int)Math.Min(int.MaxValue, (long)countEnemyDead + countElitEnemyDead + countBossDead);
    public int Mmr => GameProgress.MmrScore(completedWaves, TotalKills);

    private void OnEnable() => GameProgress.Changed += RefreshFromSave;
    private void OnDisable() => GameProgress.Changed -= RefreshFromSave;
    private void Start() => RefreshFromSave();

    private void RefreshFromSave()
    {
        if (!GameProgress.IsReady) return;
        LoadInfo();
        UpdateUI();
    }

    private void FixedUpdate()
    {
        UpdateUI();
    }

    public void SaveInfo()
    {
        PlayerPrefs.SetInt("countEnemyDead", countEnemyDead);
        PlayerPrefs.SetInt("countPlayerDead", countPlayerDead);
        PlayerPrefs.SetInt("countBossDead", countBossDead);
        PlayerPrefs.SetInt("countElitEnemyDead", countElitEnemyDead);
        PlayerPrefs.SetInt("CompletedWaves", completedWaves);
        GameProgress.RequestSave();
    }

    /// <summary>
    /// Загружаем счётчики из PlayerPrefs (если ключей нет, вернутся 0).
    /// </summary>
    public void LoadInfo()
    {
        countEnemyDead = PlayerPrefs.GetInt("countEnemyDead", 0);
        countPlayerDead = PlayerPrefs.GetInt("countPlayerDead", 0);
        countBossDead = PlayerPrefs.GetInt("countBossDead", 0);
        countElitEnemyDead = PlayerPrefs.GetInt("countElitEnemyDead", 0);
        completedWaves = PlayerPrefs.GetInt("CompletedWaves", 0);
    }

    /// <summary>
    /// Вызывается автоматически при закрытии игры/сцены.
    /// Гарантирует, что данные сохранятся.
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveInfo();
    }

    /// <summary>
    /// Обновляем UI-текст и картинку медали.
    /// Вызывайте этот метод после изменения статистики.
    /// </summary>
    public void UpdateUI()
    {
        if (texts != null && texts.Length >= 4)
        {
            texts[0].text = countEnemyDead.ToString();
            texts[1].text = countPlayerDead.ToString();
            texts[2].text = countBossDead.ToString();
            texts[3].text = countElitEnemyDead.ToString();
        }
        if (summaryText != null) summaryText.text = FormatSummary(completedWaves, TotalKills, countElitEnemyDead, countBossDead, countPlayerDead);
        if (mmrText != null) mmrText.text = "MMR " + Mmr.ToString("N0");
        if (profileWavesText != null) profileWavesText.text = completedWaves.ToString("N0");
        if (profileKillsText != null) profileKillsText.text = TotalKills.ToString("N0");
        if (profileMmrText != null) profileMmrText.text = Mmr.ToString("N0");
        if (profileMedal != null) profileMedal.sprite = MedalForKills(TotalKills);

        CheckMedal();
    }

    /// <summary>
    /// Логика выбора медали.
    /// Изменяет LvlMedalki.sprite в зависимости от countEnemyDead.
    /// </summary>
    private void CheckMedal()
    {
        if (LvlMedalki != null) LvlMedalki.sprite = MedalForKills(TotalKills);
    }

    public Sprite MedalForKills(int kills)
    {
        if (kills >= 1000)
        {
            return diamondMedal;
        }
        else if (kills >= 500)
        {
            return goldMedal;
        }
        else if (kills >= 100)
        {
            return silverMedal;
        }
        else
        {
            return bronzeMedal;
        }
    }

    public void AddCompletedWave()
    {
        if (completedWaves < int.MaxValue) completedWaves++;
        UpdateUI();
        SaveInfo();
    }

    public static string FormatSummary(int waves, int kills, int elite, int bosses, int deaths) =>
        $"Волны: {waves:N0}    Враги: {kills:N0}\nЭлита: {elite:N0}    Боссы: {bosses:N0}    Смерти: {deaths:N0}";

    [Serializable]
    public sealed class LeaderboardSummary
    {
        public int version = 1;
        public int waves, kills, elite, bosses, deaths;
        public bool IsValid => version == 1 && waves >= 0 && kills >= 0 && elite >= 0 && bosses >= 0 && deaths >= 0;
    }

    public static LeaderboardSummary ReadLeaderboardSummary() => new LeaderboardSummary
    {
        waves = PlayerPrefs.GetInt("CompletedWaves", 0),
        kills = (int)Math.Min(int.MaxValue, (long)PlayerPrefs.GetInt("countEnemyDead", 0)
            + PlayerPrefs.GetInt("countElitEnemyDead", 0) + PlayerPrefs.GetInt("countBossDead", 0)),
        elite = PlayerPrefs.GetInt("countElitEnemyDead", 0),
        bosses = PlayerPrefs.GetInt("countBossDead", 0),
        deaths = PlayerPrefs.GetInt("countPlayerDead", 0)
    };

    public void AddEnemyKill()
    {
        countEnemyDead++;
        UpdateUI();
        SaveInfo();
    }

    public void AddPlayerDead()
    {
        countPlayerDead++;
        UpdateUI();
        SaveInfo();
    }

    public void AddBossDead()
    {
        countBossDead++;
        UpdateUI();
        SaveInfo();
    }

    public void AddElitEnemyDead()
    {
        countElitEnemyDead++;
        UpdateUI();
        SaveInfo();
    }
}
