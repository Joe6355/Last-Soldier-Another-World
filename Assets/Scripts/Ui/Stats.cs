using PlayerPrefs = RedefineYG.PlayerPrefs;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

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

    public enum EnemyKind { Other, Slime, Skeleton, SlimeBoss, Assassin }
    public enum AchievementMetric
    {
        Kills, Slimes, Skeletons, Elite, Bosses, SlimeBosses, Assassins, Waves,
        ShopPacks, Upgrades, Shotgun, Automatic, Potions, Outposts, Altar, Barrels
    }

    public sealed class Achievement
    {
        public readonly string Title, Description;
        public readonly AchievementMetric Metric;
        public readonly int Target, Reward;
        public Achievement(string title, string description, AchievementMetric metric, int target, int reward)
        {
            Title = title; Description = description; Metric = metric; Target = target; Reward = reward;
        }
    }

    // Порядок сохраняет ID достижений: новые цели можно добавлять только в конец.
    public static IReadOnlyList<Achievement> Achievements { get; } = Array.AsReadOnly(new[]
    {
        new Achievement("Первые победы", "Убить 25 врагов любого типа", AchievementMetric.Kills, 25, 50),
        new Achievement("Защитник лагеря", "Убить 100 врагов любого типа", AchievementMetric.Kills, 100, 150),
        new Achievement("Опытный боец", "Убить 250 врагов любого типа", AchievementMetric.Kills, 250, 300),
        new Achievement("Гроза арены", "Убить 500 врагов любого типа", AchievementMetric.Kills, 500, 600),
        new Achievement("Тысяча побед", "Убить 1 000 врагов любого типа", AchievementMetric.Kills, 1000, 1000),
        new Achievement("Неудержимый", "Убить 2 500 врагов любого типа", AchievementMetric.Kills, 2500, 2500),
        new Achievement("Легенда гарнизона", "Убить 5 000 врагов любого типа", AchievementMetric.Kills, 5000, 5000),
        new Achievement("Первые слаймы", "Убить 25 слаймов", AchievementMetric.Slimes, 25, 100),
        new Achievement("Чистые сапоги", "Убить 100 слаймов", AchievementMetric.Slimes, 100, 250),
        new Achievement("Охотник на слизь", "Убить 250 слаймов", AchievementMetric.Slimes, 250, 500),
        new Achievement("Гроза слаймов", "Убить 500 слаймов", AchievementMetric.Slimes, 500, 1000),
        new Achievement("Без следа слизи", "Убить 1 000 слаймов", AchievementMetric.Slimes, 1000, 2000),
        new Achievement("Кости врозь", "Убить 25 скелетов, включая магов", AchievementMetric.Skeletons, 25, 100),
        new Achievement("Костолом", "Убить 100 скелетов, включая магов", AchievementMetric.Skeletons, 100, 250),
        new Achievement("Враг нежити", "Убить 250 скелетов, включая магов", AchievementMetric.Skeletons, 250, 500),
        new Achievement("Покой для нежити", "Убить 500 скелетов, включая магов", AchievementMetric.Skeletons, 500, 1000),
        new Achievement("Опасные противники", "Убить 10 элитных врагов", AchievementMetric.Elite, 10, 150),
        new Achievement("Охотник на элиту", "Убить 25 элитных врагов", AchievementMetric.Elite, 25, 350),
        new Achievement("Лучше лучших", "Убить 100 элитных врагов", AchievementMetric.Elite, 100, 1200),
        new Achievement("Первая большая победа", "Убить любого босса", AchievementMetric.Bosses, 1, 250),
        new Achievement("Пять трофеев", "Убить 5 боссов", AchievementMetric.Bosses, 5, 1000),
        new Achievement("Победитель великанов", "Убить 10 боссов", AchievementMetric.Bosses, 10, 2000),
        new Achievement("Большая слизь", "Победить босса-слайма", AchievementMetric.SlimeBosses, 1, 350),
        new Achievement("Повелитель слизи", "Победить босса-слайма 5 раз", AchievementMetric.SlimeBosses, 5, 1250),
        new Achievement("Вижу тебя", "Победить босса-ассасина", AchievementMetric.Assassins, 1, 500),
        new Achievement("Охотник на тени", "Победить босса-ассасина 5 раз", AchievementMetric.Assassins, 5, 1500),
        new Achievement("Первая зачистка", "Полностью зачистить 1 волну", AchievementMetric.Waves, 1, 75),
        new Achievement("На страже", "Полностью зачистить 5 волн", AchievementMetric.Waves, 5, 200),
        new Achievement("Десять рубежей", "Полностью зачистить 10 волн", AchievementMetric.Waves, 10, 400),
        new Achievement("Долгая служба", "Полностью зачистить 25 волн", AchievementMetric.Waves, 25, 1000),
        new Achievement("Стойкий гарнизон", "Полностью зачистить 50 волн", AchievementMetric.Waves, 50, 2000),
        new Achievement("Ветеран другого мира", "Полностью зачистить 100 волн", AchievementMetric.Waves, 100, 4000),
        new Achievement("Запасливый", "Купить 5 наборов товаров у торговца", AchievementMetric.ShopPacks, 5, 25),
        new Achievement("Постоянный покупатель", "Купить 25 наборов товаров у торговца", AchievementMetric.ShopPacks, 25, 100),
        new Achievement("Снабженец", "Купить 100 наборов товаров у торговца", AchievementMetric.ShopPacks, 100, 300),
        new Achievement("Первое улучшение", "Купить 1 улучшение героя, оружия или арены", AchievementMetric.Upgrades, 1, 100),
        new Achievement("Работа над собой", "Купить 5 улучшений героя, оружия или арены", AchievementMetric.Upgrades, 5, 250),
        new Achievement("Новая сила", "Купить 10 улучшений героя, оружия или арены", AchievementMetric.Upgrades, 10, 500),
        new Achievement("Хорошо подготовлен", "Купить 25 улучшений героя, оружия или арены", AchievementMetric.Upgrades, 25, 1250),
        new Achievement("Мастер развития", "Купить 50 улучшений героя, оружия или арены", AchievementMetric.Upgrades, 50, 2500),
        new Achievement("Тройной залп", "Разблокировать дробовик у торговца", AchievementMetric.Shotgun, 1, 250),
        new Achievement("Без остановки", "Разблокировать автоогонь у торговца", AchievementMetric.Automatic, 1, 500),
        new Achievement("Второе дыхание", "Использовать зелье лечения", AchievementMetric.Potions, 1, 25),
        new Achievement("Полевая медицина", "Использовать 10 зелий лечения", AchievementMetric.Potions, 10, 150),
        new Achievement("Всегда в строю", "Использовать 50 зелий лечения", AchievementMetric.Potions, 50, 500),
        new Achievement("Новый рубеж", "Захватить аванпост", AchievementMetric.Outposts, 1, 100),
        new Achievement("Расширение гарнизона", "Захватить 5 аванпостов", AchievementMetric.Outposts, 5, 350),
        new Achievement("Хозяин арены", "Захватить 25 аванпостов", AchievementMetric.Outposts, 25, 1000),
        new Achievement("Благословение", "Восстановить здоровье у алтаря", AchievementMetric.Altar, 1, 150),
        new Achievement("Взрывной характер", "Взорвать 10 бочек", AchievementMetric.Barrels, 10, 400)
    });

    public event Action AchievementsChanged;

    public int AchievementProgress(AchievementMetric metric)
    {
        switch (metric)
        {
            case AchievementMetric.Kills: return TotalKills;
            case AchievementMetric.Elite: return countElitEnemyDead;
            case AchievementMetric.Bosses: return countBossDead;
            case AchievementMetric.Waves: return completedWaves;
            case AchievementMetric.Shotgun: return (PlayerPrefs.GetInt("Achievement.Weapons", 0) & 1) != 0 ? 1 : 0;
            case AchievementMetric.Automatic: return (PlayerPrefs.GetInt("Achievement.Weapons", 0) & 2) != 0 ? 1 : 0;
            default: return Mathf.Max(0, PlayerPrefs.GetInt("Achievement." + metric, 0));
        }
    }

    public bool IsAchievementClaimed(int index)
    {
        return index >= 0 && index < Achievements.Count
            && (PlayerPrefs.GetInt("Achievement.Claims" + index / 31, 0) & (1 << (index % 31))) != 0;
    }

    public bool CanClaimAchievement(int index, PlayerController player)
    {
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || player == null || player.IsAwaitingRevive
            || index < 0 || index >= Achievements.Count || IsAchievementClaimed(index)) return false;
        var goal = Achievements[index];
        return AchievementProgress(goal.Metric) >= goal.Target && player.totalCoins <= int.MaxValue - goal.Reward;
    }

    public bool TryClaimAchievement(int index, PlayerController player)
    {
        if (!CanClaimAchievement(index, player)) return false;
        string key = "Achievement.Claims" + index / 31;
        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) | (1 << (index % 31)));
        player.AddCoin(Achievements[index].Reward);
        GameProgress.SaveNow();
        AchievementsChanged?.Invoke();
        return true;
    }

    public void RecordAchievementEvent(AchievementMetric metric, int amount = 1)
    {
        if (!GameProgress.IsReady || amount <= 0) return;
        switch (metric)
        {
            case AchievementMetric.Slimes: case AchievementMetric.Skeletons:
            case AchievementMetric.SlimeBosses: case AchievementMetric.Assassins:
            case AchievementMetric.ShopPacks: case AchievementMetric.Upgrades:
            case AchievementMetric.Potions: case AchievementMetric.Outposts:
            case AchievementMetric.Altar: case AchievementMetric.Barrels:
                PlayerPrefs.SetInt("Achievement." + metric,
                    (int)Math.Min(int.MaxValue, (long)AchievementProgress(metric) + amount));
                GameProgress.RequestSave();
                AchievementsChanged?.Invoke();
                break;
        }
    }

    public void RecordUpgradePurchase(Beka.UpgradeItemType type)
    {
        if (!GameProgress.IsReady) return;
        int weapons = PlayerPrefs.GetInt("Achievement.Weapons", 0);
        if (type == Beka.UpgradeItemType.Shotgun) weapons |= 1;
        if (type == Beka.UpgradeItemType.Automatic) weapons |= 2;
        PlayerPrefs.SetInt("Achievement.Weapons", weapons);
        RecordAchievementEvent(AchievementMetric.Upgrades);
    }

    private void InitializeAchievementBaseline()
    {
        if (!PlayerPrefs.HasKey("Achievement.Upgrades"))
        {
            long upgrades = 0;
            for (int i = 0; i < 30; i++) upgrades += Mathf.Max(0, PlayerPrefs.GetInt("UpgradeShop_Item" + i + "_Count", 0));
            PlayerPrefs.SetInt("Achievement.Upgrades", (int)Math.Min(int.MaxValue, upgrades));
            GameProgress.RequestSave();
        }
        int weapons = PlayerPrefs.GetInt("Achievement.Weapons", 0);
        if (PlayerPrefs.GetInt("UpgradeShop_Item5_Count", 0) > 0) weapons |= 1;
        if (PlayerPrefs.GetInt("UpgradeShop_Item6_Count", 0) > 0) weapons |= 2;
        if (weapons != PlayerPrefs.GetInt("Achievement.Weapons", 0))
        {
            PlayerPrefs.SetInt("Achievement.Weapons", weapons);
            GameProgress.RequestSave();
        }
    }

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
        AchievementsChanged?.Invoke();
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
        InitializeAchievementBaseline();
        AchievementsChanged?.Invoke();
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

    public void AddEnemyKill(EnemyKind kind = EnemyKind.Other)
    {
        if (countEnemyDead < int.MaxValue) countEnemyDead++;
        RecordEnemyKind(kind);
        UpdateUI();
        SaveInfo();
    }

    public void AddPlayerDead()
    {
        countPlayerDead++;
        UpdateUI();
        SaveInfo();
    }

    public void AddBossDead(EnemyKind kind = EnemyKind.Other)
    {
        if (countBossDead < int.MaxValue) countBossDead++;
        RecordEnemyKind(kind);
        UpdateUI();
        SaveInfo();
    }

    public void AddElitEnemyDead(EnemyKind kind = EnemyKind.Other)
    {
        if (countElitEnemyDead < int.MaxValue) countElitEnemyDead++;
        RecordEnemyKind(kind);
        UpdateUI();
        SaveInfo();
    }

    private void RecordEnemyKind(EnemyKind kind)
    {
        if (kind == EnemyKind.Slime) RecordAchievementEvent(AchievementMetric.Slimes);
        else if (kind == EnemyKind.Skeleton) RecordAchievementEvent(AchievementMetric.Skeletons);
        else if (kind == EnemyKind.SlimeBoss) RecordAchievementEvent(AchievementMetric.SlimeBosses);
        else if (kind == EnemyKind.Assassin) RecordAchievementEvent(AchievementMetric.Assassins);
    }
}
