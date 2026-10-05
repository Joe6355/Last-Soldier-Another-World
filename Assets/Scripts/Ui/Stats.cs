using PlayerPrefs = RedefineYG.PlayerPrefs;
using UnityEngine;
using UnityEngine.UI;

public class Stats : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Text[] texts;         // [0] = враги, [1] = игрок, [2] = боссы, [3] = элитные
    [SerializeField] private Image LvlMedalki;       // UI-Image, куда ставим медаль

    [Header("Medal Sprites")]
    [SerializeField] private Sprite bronzeMedal;
    [SerializeField] private Sprite silverMedal;
    [SerializeField] private Sprite goldMedal;
    [SerializeField] private Sprite diamondMedal;

    [Header("Stats Counters")]
    public int countEnemyDead;       // Убито обычных врагов
    public int countPlayerDead;      // Смертей игрока
    public int countBossDead;        // Убито боссов
    public int countElitEnemyDead;   // Убито элитных врагов

    private void Start()
    {
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
        texts[0].text = countEnemyDead.ToString();
        texts[1].text = countPlayerDead.ToString();
        texts[2].text = countBossDead.ToString();
        texts[3].text = countElitEnemyDead.ToString();

        CheckMedal();
    }

    /// <summary>
    /// Логика выбора медали. 
    /// Изменяет LvlMedalki.sprite в зависимости от countEnemyDead.
    /// </summary>
    private void CheckMedal()
    {
        if (countEnemyDead >= 1000)
        {
            LvlMedalki.sprite = diamondMedal;
        }
        else if (countEnemyDead >= 500)
        {
            LvlMedalki.sprite = goldMedal;
        }
        else if (countEnemyDead >= 100)
        {
            LvlMedalki.sprite = silverMedal;
        }
        else
        {
            LvlMedalki.sprite = bronzeMedal;
        }
    }

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
