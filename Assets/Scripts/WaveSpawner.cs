using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PlayerPrefs = RedefineYG.PlayerPrefs;

[System.Serializable]
public class WaveConfig
{
    [Header("Длительность волны (сек)")]
    public float waveDuration = 30f;    // Сколько идёт волна

    [Header("Диапазон уровней врагов (для Prefabs)")]
    public int minDifficulty = 0;       // Индекс минимального врага
    public int maxDifficulty = 1;       // Индекс максимального врага

    [Header("Максимум врагов в волне")]
    public int maxEnemies = 10;

    [Header("Перерыв после волны (сек)")]
    public float breakDuration = 15f;   // Отдых между волнами
}

public class WaveSpawner : MonoBehaviour
{
    #region Спавн врагов и UI

    [Header("Точки спавна врагов")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Header("Префабы врагов (индекс = сложность)")]
    [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();

    [Header("Список волн")]
    [SerializeField] private List<WaveConfig> waves = new List<WaveConfig>();

    [Header("Интервал спавна во время волны")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private Vector2 spawnOffsetRange = new Vector2(2f, 2f);

    [Header("UI для таймера (необязательно)")]
    [SerializeField] private Text waveTimerText;
    [SerializeField] private Image waveTimerImage;
    [SerializeField] private Text waveNumberText;
    [SerializeField] private GameObject waveHudPanel;

    [Header("Объект, который появляется во время перерыва")]
    [SerializeField] private GameObject breakIndicator;

    // Список для хранения всех заспавненных врагов
    private List<GameObject> activeEnemies = new List<GameObject>();

    #endregion

    #region Босс на 10-й и 15-й волнах

    [Header("Босс на 10-й волне (Слизь)")]
    [Tooltip("Босс-слизь, который появляется на 10-й волне")]
    [SerializeField] private GameObject bossWave10;
    [Tooltip("HP-бар босса-слизи (панель) на 10-й волне")]
    [SerializeField] private GameObject bossHPBar10;
    [Tooltip("Заглушка (например, UI-элемент) для босса-слизи на 10-й волне")]
    [SerializeField] private GameObject bossPlaceholder10;

    [Header("Босс на 15-й волне (Ассассин)")]
    [Tooltip("Босс-ассассин, который появляется на 15-й волне")]
    [SerializeField] private GameObject bossWave15;
    [Tooltip("HP-бар босса-ассассина (панель) на 15-й волне")]
    [SerializeField] private GameObject bossHPBar15;
    [Tooltip("Заглушка (например, UI-элемент) для босса-ассассина на 15-й волне")]
    [SerializeField] private GameObject bossPlaceholder15;

    #endregion

    #region Внутренние переменные волны

    private int currentWaveIndex = 0;
    private float waveTimeLeft = 0f;
    private float breakTimeLeft = 0f;
    private bool isWaveActive = false;
    private bool isBreakActive = false;
    private bool playerInsideZone = false;
    private int enemiesSpawnedInWave = 0;
    private bool bossActivated;
    public int CurrentWaveNumber => currentWaveIndex + 1;
    public bool IsRunning => playerInsideZone && (isWaveActive || isBreakActive);
    public int ActiveEnemyCount => activeEnemies.Count;
    public float RemainingWaveTime => waveTimeLeft;

    #endregion

    #region Старт и Update

    private void Start()
    {
        // Скрываем UI, пока игрок не зайдёт
        HideSpawnerUI();

        if (breakIndicator != null)
            breakIndicator.SetActive(false);

        // Скрываем объекты босса для 10-й и 15-й волн
        if (bossWave10 != null) bossWave10.SetActive(false);
        if (bossHPBar10 != null) bossHPBar10.SetActive(false);
        if (bossPlaceholder10 != null) bossPlaceholder10.SetActive(false);

        if (bossWave15 != null) bossWave15.SetActive(false);
        if (bossHPBar15 != null) bossHPBar15.SetActive(false);
        if (bossPlaceholder15 != null) bossPlaceholder15.SetActive(false);
    }

    private void Update()
    {
        if (!playerInsideZone || Time.timeScale == 0f || YG.YG2.isPauseGame) return;  // Игрок ещё не в зоне — ничего не делаем

        if (isWaveActive)
        {
            // Удаляем из списка уничтожённых врагов (null)
            activeEnemies.RemoveAll(enemy => enemy == null);
            WaveConfig currentWave = waves[currentWaveIndex];

            // Если это боссовая волна, не учитываем обычные условия окончания волны
            if (currentWaveIndex == 9)
            {
                // Для волны с боссом-слизью
                if (bossActivated && (bossWave10 == null || !bossWave10.activeInHierarchy))
                {
                    CompleteWave(true);
                    return;
                }
                // Можно обновлять UI по-другому или просто выводить номер волны
                UpdateWaveUI(0, currentWave.waveDuration, true);
            }
            else if (currentWaveIndex == 14)
            {
                // Для волны с боссом-ассассином
                if (bossActivated && (bossWave15 == null || !bossWave15.activeInHierarchy))
                {
                    CompleteWave(true);
                    return;
                }
                UpdateWaveUI(0, currentWave.waveDuration, true);
            }
            else
            {
                // Обычная логика для обычных волн:
                if (enemiesSpawnedInWave >= currentWave.maxEnemies && activeEnemies.Count == 0)
                {
                    CompleteWave(true);
                    return;
                }

                waveTimeLeft -= Time.deltaTime;
                if (waveTimeLeft <= 0f)
                {
                    CompleteWave(false);
                }
                else
                {
                    UpdateWaveUI(waveTimeLeft, currentWave.waveDuration, true);
                }
            }
        }
        else if (isBreakActive)
        {
            breakTimeLeft -= Time.deltaTime;
            if (breakTimeLeft <= 0f)
            {
                isBreakActive = false;
                if (breakIndicator != null)
                    breakIndicator.SetActive(false);
                NextWave();
            }
            else
            {
                UpdateWaveUI(breakTimeLeft, waves[currentWaveIndex].breakDuration, false);
            }
        }
    }



    #endregion

    #region Волновая логика

    private void StartWave(int waveIndex)
    {
        StopAllCoroutines();
        bossActivated = false;
        if (waveIndex < 0 || waveIndex >= waves.Count)
        {
            Debug.LogWarning("Нет такой волны: " + waveIndex);
            return;
        }

        if (breakIndicator != null)
            breakIndicator.SetActive(false);

        isWaveActive = true;
        isBreakActive = false;

        WaveConfig wave = waves[waveIndex];
        waveTimeLeft = wave.waveDuration;
        enemiesSpawnedInWave = 0;
        SaveCheckpoint();

        // Очистка заспавненных врагов (если игрок ранее покидал арену)
        ClearActiveEnemies();

        // Логика выбора:
        // Если волна 10 (индекс 9) — активируется босс-слизь,
        // если волна 15 (индекс 14) — активируется босс-ассассин,
        // иначе спавнятся обычные враги.
        if (waveIndex == 9)
        {
            StartCoroutine(ActivateBossWave10());
        }
        else if (waveIndex == 14)
        {
            StartCoroutine(ActivateBossWave15());
        }
        else
        {
            StartCoroutine(SpawnEnemiesDuringWave(waveIndex));
        }
    }

    private IEnumerator SpawnEnemiesDuringWave(int waveIndex)
    {
        WaveConfig wave = waves[waveIndex];

        while (isWaveActive && currentWaveIndex == waveIndex)
        {
            if (enemiesSpawnedInWave < wave.maxEnemies)
            {
                SpawnEnemy(wave);
                enemiesSpawnedInWave++;
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnEnemy(WaveConfig wave)
    {
        if (spawnPoints.Count == 0 || enemyPrefabs.Count == 0) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
        float offsetX = Random.Range(-spawnOffsetRange.x, spawnOffsetRange.x);
        float offsetY = Random.Range(-spawnOffsetRange.y, spawnOffsetRange.y);

        Vector3 spawnPos = new Vector3(
            spawnPoint.position.x + offsetX,
            spawnPoint.position.y + offsetY,
            spawnPoint.position.z
        );

        int enemyLevel = Random.Range(wave.minDifficulty, wave.maxDifficulty + 1);
        enemyLevel = Mathf.Clamp(enemyLevel, 0, enemyPrefabs.Count - 1);

        GameObject enemy = Instantiate(enemyPrefabs[enemyLevel], spawnPos, Quaternion.identity);
        activeEnemies.Add(enemy);
    }

    private void CompleteWave(bool cleared)
    {
        if (!isWaveActive) return;
        isWaveActive = false;
        StopAllCoroutines();
        if (cleared) FindObjectOfType<Stats>()?.AddCompletedWave();
        StartBreak();
        SaveCheckpoint();
    }

    private void StartBreak()
    {
        isBreakActive = true;
        breakTimeLeft = waves[currentWaveIndex].breakDuration;

        if (breakIndicator != null)
            breakIndicator.SetActive(true);
    }

    private void NextWave()
    {
        currentWaveIndex++;
        if (currentWaveIndex >= waves.Count)
        {
            Debug.Log("Все волны пройдены!");
            // Можно зациклить или остановить спавн.
            currentWaveIndex = 0;
        }
        StartWave(currentWaveIndex);
    }

    #endregion

    #region Triggerы и сброс

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !playerInsideZone)
        {
            playerInsideZone = true;
            ShowSpawnerUI();
            ResetWavesAndStart();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            playerInsideZone = false;
            HideSpawnerUI();
            ResetSpawnerCompletely();
        }
    }

    private void ResetWavesAndStart()
    {
        currentWaveIndex = PlayerPrefs.GetInt("ArenaRunActive", 0) == 1
            ? Mathf.Clamp(PlayerPrefs.GetInt("ArenaWaveIndex", 0), 0, waves.Count - 1) : 0;
        isWaveActive = false;
        isBreakActive = false;
        StartWave(currentWaveIndex);
    }

    private void ResetSpawnerCompletely()
    {
        PlayerPrefs.SetInt("ArenaRunActive", 0);
        GameProgress.RequestSave();
        StopAllCoroutines();
        currentWaveIndex = 0;
        isWaveActive = false;
        isBreakActive = false;

        if (breakIndicator != null)
            breakIndicator.SetActive(false);

        ClearActiveEnemies();

        // Скрываем объекты босса для 10-й волны
        if (bossWave10 != null) bossWave10.SetActive(false);
        if (bossHPBar10 != null) bossHPBar10.SetActive(false);
        if (bossPlaceholder10 != null) bossPlaceholder10.SetActive(false);

        // Скрываем босса для 15-й волны и его UI
        if (bossWave15 != null) bossWave15.SetActive(false);
        if (bossHPBar15 != null) bossHPBar15.SetActive(false);
        if (bossPlaceholder15 != null) bossPlaceholder15.SetActive(false);
    }

    public void AbandonRun()
    {
        playerInsideZone = false;
        ResetSpawnerCompletely();
        HideSpawnerUI();
    }

    public void SaveCheckpoint()
    {
        if (!IsRunning || waves.Count == 0) return;
        PlayerPrefs.SetInt("ArenaRunActive", 1);
        PlayerPrefs.SetInt("ArenaWaveIndex", isBreakActive ? (currentWaveIndex + 1) % waves.Count : currentWaveIndex);
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            PlayerPrefs.SetFloat("ArenaPlayerX", player.transform.position.x);
            PlayerPrefs.SetFloat("ArenaPlayerY", player.transform.position.y);
        }
        GameProgress.RequestSave();
    }

    public void RestoreCheckpointPosition(PlayerController player)
    {
        if (PlayerPrefs.GetInt("ArenaRunActive", 0) != 1 || !PlayerPrefs.HasKey("ArenaPlayerX")) return;
        var zone = GetComponent<Collider2D>();
        var point = new Vector3(PlayerPrefs.GetFloat("ArenaPlayerX"), PlayerPrefs.GetFloat("ArenaPlayerY"), player.transform.position.z);
        if (zone != null && zone.bounds.Contains(new Vector3(point.x, point.y, zone.bounds.center.z)))
            player.transform.position = point;
        else PlayerPrefs.SetInt("ArenaRunActive", 0);
    }

    private void ClearActiveEnemies()
    {
        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy != null)
                Destroy(enemy);
        }
        activeEnemies.Clear();
    }

    #endregion

    #region UI Методы

    private void ShowSpawnerUI()
    {
        if (waveHudPanel != null) waveHudPanel.SetActive(true);
        if (waveTimerText != null) waveTimerText.gameObject.SetActive(true);
        if (waveTimerImage != null) waveTimerImage.gameObject.SetActive(true);
        if (waveNumberText != null) waveNumberText.gameObject.SetActive(true);
    }

    private void HideSpawnerUI()
    {
        if (waveHudPanel != null) waveHudPanel.SetActive(false);
        if (waveTimerText != null) waveTimerText.gameObject.SetActive(false);
        if (waveTimerImage != null) waveTimerImage.gameObject.SetActive(false);
        if (waveNumberText != null) waveNumberText.gameObject.SetActive(false);
    }

    private void UpdateWaveUI(float timeLeft, float totalTime, bool isWave)
    {
        if (waveTimerText != null)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            waveTimerText.text = $"{seconds / 60}:{seconds % 60:00}";
            waveTimerText.color = new Color32(227, 186, 101, 255);

            if (waveNumberText != null)
                waveNumberText.text = $"Волна {currentWaveIndex + 1}";
        }

        if (waveTimerImage != null)
        {
            float fillAmount = 1f - (timeLeft / totalTime);
            waveTimerImage.fillAmount = fillAmount;
            waveTimerImage.color = isWave ? Color.red : Color.gray;
        }
    }

    #endregion

    #region Boss на 10-й и 15-й волнах

    // Босс на 10-й волне (Слизь)
    private IEnumerator ActivateBossWave10()
    {
        // Подождём 1 секунду для визуального эффекта
        yield return new WaitForSeconds(1f);

        if (bossWave10 != null)
            bossWave10.SetActive(true);
        bossActivated = true;
        if (bossWave10 == null) { CompleteWave(false); yield break; }
        if (bossHPBar10 != null)
            bossHPBar10.SetActive(true);
        if (bossPlaceholder10 != null)
            bossPlaceholder10.SetActive(true);

        // Вместо спавна врагов просто ждём окончания волны
        while (isWaveActive)
        {
            yield return null;
        }
    }

    // Босс на 15-й волне (Ассассин)
    private IEnumerator ActivateBossWave15()
    {
        // Подождём 1 секунду для визуального эффекта
        yield return new WaitForSeconds(1f);

        if (bossWave15 != null)
            bossWave15.SetActive(true);
        bossActivated = true;
        if (bossWave15 == null) { CompleteWave(false); yield break; }
        if (bossHPBar15 != null)
            bossHPBar15.SetActive(true);
        if (bossPlaceholder15 != null)
            bossPlaceholder15.SetActive(true);

        while (isWaveActive)
        {
            yield return null;
        }
    }

    #endregion
}
