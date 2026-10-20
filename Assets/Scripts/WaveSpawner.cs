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
    public int[] enemyTypes = System.Array.Empty<int>();

    [Header("Интервал спавна (0 — общая настройка)")]
    [Min(0)] public float spawnInterval;

    [Header("Перерыв после волны (сек)")]
    public float breakDuration = 15f;   // Отдых между волнами
}

public partial class WaveSpawner : MonoBehaviour
{
    public const int NormalWaveLimit = 25;
    private bool arenaCleared;
    public bool IsArenaCleared => !crimsonArena && arenaCleared;
    public bool IsFinalBossWave => !crimsonArena && currentWaveIndex == NormalWaveLimit - 1;
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

    [Header("Усиление врагов с 16-й волны")]
    [SerializeField, Min(16)] private int reinforcementFirstWave = 16;
    [SerializeField, Min(1)] private int reinforcementWaveStep = 5;
    [SerializeField, Min(0)] private float healthBoostPerStep = .25f;
    [SerializeField, Min(0)] private float damageBoostPerStep = .15f;
    [SerializeField, Min(0)] private float movementBoostPerStep = .1f;
    [SerializeField, Min(0)] private float projectileSpeedBoostPerStep = .1f;
    [SerializeField, Min(0)] private float spawnRateBoostPerStep = .3f;
    [SerializeField, Min(0)] private float enemyCountBoostPerStep = .35f;
    [SerializeField, Min(1)] private int maxConcurrentLateEnemies = 60;

    [Header("Боссы в общем спавне с 21-й волны")]
    [SerializeField, Min(21)] private int regularBossFirstWave = 21;
    [SerializeField, Min(2)] private int regularBossSpawnEvery = 30;
    [SerializeField, Min(1)] private int maxConcurrentRegularBosses = 2;
    [SerializeField] private GameObject regularBossLoot;
    private readonly List<GameObject> regularBosses = new List<GameObject>();
    private readonly List<GameObject> enemyProjectiles = new List<GameObject>();
    private int regularBossesSpawned;
    private GameObject regularSlimeTemplate, regularAssassinTemplate;

    [Header("UI для таймера (необязательно)")]
    [SerializeField] private Text waveTimerText;
    [SerializeField] private Image waveTimerImage;
    [SerializeField] private Text waveNumberText;
    [SerializeField] private GameObject waveHudPanel;

    [Header("Объект, который появляется во время перерыва")]
    [SerializeField] private GameObject breakIndicator;

    [Header("Укрепления лесного гарнизона")]
    [SerializeField] private ArenaFeature[] arenaFeatures = System.Array.Empty<ArenaFeature>();
    [SerializeField] private TMPro.TextMeshProUGUI arenaHint;
    private float hintTime;
    private SpriteRenderer arenaPlayerSprite;
    private int previousPlayerSort;

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
    private readonly List<int> spawnBag = new List<int>();
    private bool bossActivated;
    private bool tutorialWave;
    private int tutorialEnemiesLeft;
    public int CurrentWaveNumber => tutorialWave ? 0 : currentWaveIndex + 1 + (crimsonArena ? crimsonCycle * waves.Count : 0);
    public bool IsTutorialWave => tutorialWave;
    public Transform TutorialExit => breakIndicator != null ? breakIndicator.transform : null;
    public RectTransform TutorialHud => waveHudPanel != null ? waveHudPanel.GetComponent<RectTransform>()
        : waveNumberText != null ? waveNumberText.rectTransform : null;
    public bool IsRunning => playerInsideZone && (isWaveActive || isBreakActive);
    public bool IsCombatActive => playerInsideZone && isWaveActive;
    public int ActiveEnemyCount => activeEnemies.Count;
    public float RemainingWaveTime => waveTimeLeft;
    public int ReinforcementLevel => CurrentWaveNumber < reinforcementFirstWave ? 0
        : Mathf.Min(10, 1 + (CurrentWaveNumber - reinforcementFirstWave) / Mathf.Max(1, reinforcementWaveStep));
    public float ProjectileSpeedMultiplier => crimsonArena ? 1f + Mathf.Min(.6f, .04f * (CurrentWaveNumber - 1))
        : 1f + projectileSpeedBoostPerStep * ReinforcementLevel;
    public float BarrelDamageMultiplier => crimsonArena ? HealthMultiplier : 1f + healthBoostPerStep * ReinforcementLevel;
    public bool CanSpawnRegularBossMinion => IsCombatActive && !tutorialWave && GameProgress.IsReady && !YG.YG2.isPauseGame
        && Time.timeScale > 0 && activeEnemies.Count < maxConcurrentLateEnemies;
    public float EffectiveSpawnInterval
    {
        get
        {
            float interval = waves.Count > currentWaveIndex && currentWaveIndex >= 0 && waves[currentWaveIndex].spawnInterval > 0
                ? waves[currentWaveIndex].spawnInterval : spawnInterval;
            return crimsonArena ? Mathf.Max(.25f, .65f - .012f * (CurrentWaveNumber - 1))
                : Mathf.Max(.01f, interval) / (1f + spawnRateBoostPerStep * ReinforcementLevel);
        }
    }
    public int EffectiveEnemyLimit => crimsonArena ? Mathf.Min(180, 32 + 6 * (CurrentWaveNumber - 1))
        : waves.Count > currentWaveIndex && currentWaveIndex >= 0
        ? Mathf.CeilToInt(waves[currentWaveIndex].maxEnemies * (1f + enemyCountBoostPerStep * ReinforcementLevel)) : 0;

    #endregion

    #region Старт и Update

    private void Start()
    {
        // Скрываем UI, пока игрок не зайдёт
        if (ActiveArena == null || ActiveArena == this) HideSpawnerUI();

        if (breakIndicator != null)
            breakIndicator.SetActive(false);

        HideDedicatedBosses();
        LayoutPairedBossHud();

        // The dedicated bosses destroy themselves on death. Preserve inactive
        // templates before waves 10/15 so their later ordinary copies remain available.
        if (bossWave10 != null)
        {
            regularSlimeTemplate = Instantiate(bossWave10, transform, true);
            regularSlimeTemplate.name = "LateWaveSlimeTemplate";
            regularSlimeTemplate.SetActive(false);
        }
        if (bossWave15 != null)
        {
            regularAssassinTemplate = Instantiate(bossWave15, transform, true);
            regularAssassinTemplate.name = "LateWaveAssassinTemplate";
            regularAssassinTemplate.SetActive(false);
        }
    }

    private void Update()
    {
        LayoutPairedBossHud();
        UpdateDemoControls();
        if (!playerInsideZone || Time.timeScale == 0f || YG.YG2.isPauseGame) return;  // Игрок ещё не в зоне — ничего не делаем
        if (tutorialWave) { UpdateTutorialUI(); return; }
        if (IsArenaCleared) { UpdateClearedArenaUI(); return; }
        hintTime = Mathf.Max(0, hintTime - Time.deltaTime);
        if (arenaHint != null) arenaHint.gameObject.SetActive(overtime && hintTime > 0 && isWaveActive);

        if (isWaveActive)
        {
            // Удаляем из списка уничтожённых врагов (null)
            activeEnemies.RemoveAll(enemy => enemy == null);
            WaveConfig currentWave = waves[currentWaveIndex];

            // Если это боссовая волна, не учитываем обычные условия окончания волны
            if (!crimsonArena && currentWaveIndex == 9)
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
            else if (!crimsonArena && currentWaveIndex == 14)
            {
                // Для волны с боссом-ассассином
                if (bossActivated && (bossWave15 == null || !bossWave15.activeInHierarchy))
                {
                    CompleteWave(true);
                    return;
                }
                UpdateWaveUI(0, currentWave.waveDuration, true);
            }
            else if (!crimsonArena && currentWaveIndex == 19)
            {
                if (bossActivated && shaulaDefeated) { CompleteWave(true); return; }
                UpdateWaveUI(0, currentWave.waveDuration, true);
                if (waveTimerText != null) waveTimerText.text = GameLocalization.Text("Босс");
                if (waveTimerImage != null) waveTimerImage.fillAmount = 0;
            }
            else if (IsFinalBossWave)
            {
                // Wait for the death/reward sequences, not only for zero HP.
                if (bossActivated && shaulaDefeated && bossWave10 == null && bossWave15 == null)
                {
                    CompleteWave(true);
                    return;
                }
                UpdateWaveUI(0, 1, true);
                if (waveTimerText != null) waveTimerText.text = GameLocalization.Text("Финал");
                if (waveTimerImage != null) waveTimerImage.fillAmount = 0;
            }
            else
            {
                // Обычная логика для обычных волн:
                if (enemiesSpawnedInWave >= EffectiveEnemyLimit && activeEnemies.Count == 0 && (!IsPairedBossWave || bossActivated))
                {
                    CompleteWave(true);
                    return;
                }

                if (!overtime) waveTimeLeft -= Time.deltaTime;
                if (waveTimeLeft <= 0f)
                {
                    EnterOvertime();
                }
                else
                {
                    UpdateWaveUI(waveTimeLeft, EffectiveWaveDuration, true);
                }
            }
        }
        else if (isBreakActive)
        {
            if (GameProgress.IsArenaDemo)
            {
                UpdateWaveUI(0, 1, false);
                if (waveTimerText != null) waveTimerText.text = "9";
                return;
            }
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

    private void StartWave(int waveIndex, bool restoreArena = false)
    {
        StopAllCoroutines();
        bossActivated = false;
        overtime = false;
        arenaCleared = false;
        if (waveIndex < 0 || waveIndex >= waves.Count)
        {
            Debug.LogWarning("Нет такой волны: " + waveIndex);
            return;
        }

        if (breakIndicator != null)
            breakIndicator.SetActive(false);

        HideDedicatedBosses();

        isWaveActive = true;
        isBreakActive = false;
        regularBossesSpawned = 0;

        WaveConfig wave = waves[waveIndex];
        waveTimeLeft = EffectiveWaveDuration;
        enemiesSpawnedInWave = 0;
        spawnBag.Clear();

        // Очистка заспавненных врагов (если игрок ранее покидал арену)
        ClearActiveEnemies();
        ShowArenaPlayer();
        int outposts = 0;
        foreach (var feature in arenaFeatures) if (feature != null && feature.Kind == ArenaFeature.FeatureKind.Outpost) outposts++;
        foreach (var feature in arenaFeatures)
            if (feature != null) feature.BeginWave(outposts > 0 && feature.SlotIndex == waveIndex % outposts, restoreArena);
        hintTime = 0;
        SaveCheckpoint();

        // Логика выбора:
        // Если волна 10 (индекс 9) — активируется босс-слизь,
        // если волна 15 (индекс 14) — активируется босс-ассассин,
        // иначе спавнятся обычные враги.
        if (crimsonArena)
        {
            for (int i = 0; i < 8; i++) if (SpawnEnemy(wave)) enemiesSpawnedInWave++;
            StartCoroutine(SpawnEnemiesDuringWave(waveIndex));
            if (IsPairedBossWave) StartCoroutine(SpawnPairedBosses());
        }
        else if (waveIndex == 9)
        {
            StartCoroutine(ActivateBossWave10());
        }
        else if (waveIndex == 14)
        {
            StartCoroutine(ActivateBossWave15());
        }
        else if (waveIndex == 19)
        {
            StartCoroutine(ActivateShaula());
        }
        else if (IsFinalBossWave)
        {
            StartCoroutine(SpawnPairedBosses());
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
            if (GameProgress.IsReady && !YG.YG2.isPauseGame && enemiesSpawnedInWave < EffectiveEnemyLimit
                && ((!crimsonArena && ReinforcementLevel == 0) || activeEnemies.Count < maxConcurrentLateEnemies))
            {
                if (SpawnEnemy(wave)) enemiesSpawnedInWave++;
            }
            yield return new WaitForSeconds(EffectiveSpawnInterval);
        }
    }

    private int SelectEnemyIndex(WaveConfig wave)
    {
        // A shuffled bag gives every configured species a turn before repeating.
        // Early lessons and dedicated bosses keep their existing wave paths.
        if ((!crimsonArena && CurrentWaveNumber < 5) || wave.enemyTypes == null || wave.enemyTypes.Length == 0)
            return Mathf.Clamp(Random.Range(wave.minDifficulty, wave.maxDifficulty + 1), 0, enemyPrefabs.Count - 1);
        if (spawnBag.Count == 0)
        {
            foreach (int index in wave.enemyTypes)
                if (index >= 0 && index < enemyPrefabs.Count && enemyPrefabs[index] != null && !spawnBag.Contains(index)) spawnBag.Add(index);
            for (int i = spawnBag.Count - 1; i > 0; i--)
            {
                int other = Random.Range(0, i + 1);
                int value = spawnBag[i]; spawnBag[i] = spawnBag[other]; spawnBag[other] = value;
            }
        }
        if (spawnBag.Count == 0) return Mathf.Clamp(wave.minDifficulty, 0, enemyPrefabs.Count - 1);
        int result = spawnBag[spawnBag.Count - 1];
        spawnBag.RemoveAt(spawnBag.Count - 1);
        return result;
    }

    private bool SpawnEnemy(WaveConfig wave)
    {
        if (spawnPoints.Count == 0 || enemyPrefabs.Count == 0) return false;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
        if (spawnPoint == null) return false;
        float offsetX = Random.Range(-spawnOffsetRange.x, spawnOffsetRange.x);
        float offsetY = Random.Range(-spawnOffsetRange.y, spawnOffsetRange.y);

        Vector3 spawnPos = new Vector3(
            spawnPoint.position.x + offsetX,
            spawnPoint.position.y + offsetY,
            spawnPoint.position.z
        );

        regularBosses.RemoveAll(enemy => enemy == null || !enemy.activeInHierarchy);
        if (!crimsonArena && CurrentWaveNumber >= regularBossFirstWave
            && (enemiesSpawnedInWave + 1) % Mathf.Max(2, regularBossSpawnEvery) == 0
            && regularBosses.Count < maxConcurrentRegularBosses && SpawnRegularBoss(spawnPos)) return true;

        if (!TrySafeSpawn(spawnPos, out spawnPos)) return false;
        int enemyLevel = SelectEnemyIndex(wave);
        if (enemyPrefabs[enemyLevel] == null) return false;

        GameObject enemy = Instantiate(enemyPrefabs[enemyLevel], spawnPos, Quaternion.identity);
        ApplyEnemyReinforcement(enemy);
        activeEnemies.Add(enemy);
        return true;
    }

    [SerializeField] private GameObject slimeBossEscortPrefab;
    [SerializeField, Range(0, 12)] private int slimeBossEscortCount = 6;

    private void SpawnSlimeBossEscort()
    {
        if (currentWaveIndex != 9 || bossWave10 == null || !CanSpawnRegularBossMinion
            || slimeBossEscortPrefab == null || spawnPoints.Count == 0) return;
        for (int i = 0; i < slimeBossEscortCount && CanSpawnRegularBossMinion; i++)
        {
            var point = spawnPoints[i % spawnPoints.Count];
            if (point == null) continue;
            Vector3 position = point.position;
            // Repeated entrances use a small offset, rather than overlapping colliders.
            position.x += i / spawnPoints.Count * .6f;
            var escort = Instantiate(slimeBossEscortPrefab, position, Quaternion.identity);
            if (RegisterSpawnedEnemy(escort)) EnemyDefeatEffect.PlaySpawn(escort);
            else Destroy(escort);
        }
    }

    private bool SpawnRegularBoss(Vector3 position)
    {
        bool slimeTurn = regularBossesSpawned % 2 == 0;
        GameObject source = slimeTurn ? regularSlimeTemplate : regularAssassinTemplate;
        if (source == null || (slimeTurn ? source.GetComponent<SlimeBoss>() == null : source.GetComponent<Roberto>() == null)) return false;
        // The original scene bosses are inactive outside their dedicated waves.
        // Configure the clone before Start can show shared UI or launch attacks.
        GameObject enemy = Instantiate(source, position, source.transform.rotation);
        enemy.SetActive(false);
        float health = HealthMultiplier;
        float damage = DamageMultiplier;
        float movement = MovementMultiplier;
        if (slimeTurn) enemy.GetComponent<SlimeBoss>().ConfigureAsRegularEnemy(this, health, damage, movement, ProjectileSpeedMultiplier);
        else
        {
            var assassin = enemy.GetComponent<Roberto>();
            assassin.ConfigureAsRegularEnemy(this, health, damage, movement, ProjectileSpeedMultiplier);
            assassin.ConfigureRegularLoot(regularBossLoot);
        }
        activeEnemies.Add(enemy);
        regularBosses.Add(enemy);
        regularBossesSpawned++;
        enemy.SetActive(true);
        return true;
    }

    public bool RegisterSpawnedEnemy(GameObject enemy)
    {
        if (enemy == null || !CanSpawnRegularBossMinion || activeEnemies.Contains(enemy)) return false;
        if (!TrySafeSpawn(enemy.transform.position, out var safePosition)) return false;
        enemy.transform.position = safePosition;
        ApplyEnemyReinforcement(enemy);
        activeEnemies.Add(enemy);
        return true;
    }

    public void TrackEnemyProjectile(GameObject projectile)
    {
        if (projectile == null || !IsCombatActive || tutorialWave) return;
        enemyProjectiles.RemoveAll(item => item == null);
        if (!enemyProjectiles.Contains(projectile)) enemyProjectiles.Add(projectile);
    }

    private void ApplyEnemyReinforcement(GameObject enemy)
    {
        int level = ReinforcementLevel;
        if (level == 0 && !crimsonArena) return;
        float health = HealthMultiplier;
        float damage = DamageMultiplier;
        float speed = MovementMultiplier;
        var target = ArrowDef.FindEnemy(enemy.transform);
        if (target is Enemy melee) melee.ApplyWaveScaling(health, damage, speed, ProjectileSpeedMultiplier);
        else if (target is Slime slime) slime.ApplyWaveScaling(health, damage, speed, ProjectileSpeedMultiplier);
        else if (target is SceletonDef skeleton) skeleton.ApplyWaveScaling(health, damage, speed);
        else if (target is SceletMag mage) mage.ApplyWaveScaling(health, damage, speed, ProjectileSpeedMultiplier);
        else if (target is HealerEnemy healer) healer.ApplyWaveScaling(health, damage, speed);
    }

    public static void ScaleEnemyProjectile(GameObject projectile, float damageMultiplier, float speedMultiplier = 1f)
    {
        ScaleProjectileValues(projectile, damageMultiplier, speedMultiplier);
        ActiveArena?.TrackEnemyProjectile(projectile);
    }

    private void CompleteWave(bool cleared)
    {
        if (!isWaveActive) return;
        isWaveActive = false;
        overtime = false;
        StopAllCoroutines();
        if (!crimsonArena && currentWaveIndex == 9) ClearActiveEnemies();
        if (cleared)
        {
            FindObjectOfType<Stats>()?.AddCompletedWave();
            AwardCrimsonWaveLoot();
        }
        StartBreak();
        if (cleared && IsFinalBossWave) FinishNormalArena();
        SaveCheckpoint();
    }

    private void FinishNormalArena()
    {
        arenaCleared = true;
        isWaveActive = false;
        isBreakActive = true;
        breakTimeLeft = 0;
        ClearActiveEnemies();
        HideDedicatedBosses();
        foreach (var feature in arenaFeatures) if (feature != null) feature.EndRun();
        if (breakIndicator != null) breakIndicator.SetActive(true);
        UpdateClearedArenaUI();
    }

    private void UpdateClearedArenaUI()
    {
        if (waveNumberText != null) waveNumberText.text = GameLocalization.Text("Арена пройдена");
        if (waveTimerText != null) waveTimerText.text = GameLocalization.Text("В лагерь");
        if (waveTimerImage != null) { waveTimerImage.fillAmount = 1; waveTimerImage.color = new Color32(227, 186, 101, 255); }
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
        if (IsArenaCleared) return;
        currentWaveIndex++;
        if (currentWaveIndex >= waves.Count)
        {
            if (crimsonArena) { currentWaveIndex = 0; crimsonCycle++; }
            else currentWaveIndex = Mathf.Min(NormalWaveLimit, waves.Count) - 1;
        }
        StartWave(currentWaveIndex);
    }

    #endregion

    #region Triggerы и сброс

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (TipsManager.IsGuideActive) return;
        if (collision.CompareTag("Player") && !playerInsideZone && CanEnter)
        {
            if (ActiveArena != null && ActiveArena != this) ActiveArena.AbandonRun();
            ActiveArena = this;
            playerInsideZone = true;
            ShowSpawnerUI();
            ResetWavesAndStart();
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // A player may already be inside the zone when the lesson is skipped.
        if (!playerInsideZone && !TipsManager.IsGuideActive) OnTriggerEnter2D(collision);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!playerInsideZone) return;
        // Выгрузка сцены удаляет коллайдер игрока: это не отказ от сохранённой волны.
        if (isActiveAndEnabled && gameObject.scene.isLoaded && collision != null && collision.enabled
            && collision.gameObject.activeInHierarchy && collision.gameObject.scene.isLoaded
            && collision.GetComponentInParent<PlayerController>() != null && collision.CompareTag("Player"))
        {
            if (tutorialWave) return;
            // Water constrains this island. Leaving its trigger must not cancel
            // the run before DeadZone can return a dash to the shore.
            if (crimsonArena) return;
            playerInsideZone = false;
            HideSpawnerUI();
            ResetSpawnerCompletely();
        }
    }

    private void ResetWavesAndStart()
    {
        bool restore = PlayerPrefs.GetInt("ArenaRunActive", 0) == 1
            && PlayerPrefs.GetInt("ArenaRunKind", 0) == (crimsonArena ? 1 : 0);
        crimsonCycle = restore ? PlayerPrefs.GetInt("CrimsonArenaCycle", 0) : 0;
        currentWaveIndex = restore
            ? Mathf.Clamp(PlayerPrefs.GetInt("ArenaWaveIndex", 0), 0, waves.Count - 1) : 0;
        isWaveActive = false;
        isBreakActive = false;
        if (!crimsonArena && restore && PlayerPrefs.GetInt("ArenaRunCompleted", 0) == 1)
        {
            currentWaveIndex = NormalWaveLimit - 1;
            ShowArenaPlayer(); FinishNormalArena();
            return;
        }
        StartWave(currentWaveIndex, restore);
    }

    private void ResetSpawnerCompletely()
    {
        if (ActiveArena == this) ActiveArena = null;
        PlayerPrefs.SetInt("ArenaRunActive", 0);
        PlayerPrefs.SetInt("ArenaRunCompleted", 0);
        arenaCleared = false;
        overtime = false; crimsonCycle = 0;
        GameProgress.RequestSave();
        StopAllCoroutines();
        currentWaveIndex = 0;
        isWaveActive = false;
        isBreakActive = false;
        foreach (var feature in arenaFeatures) if (feature != null) feature.EndRun();
        RestoreArenaPlayer();
        ClearArenaCheckpoint();
        if (arenaHint != null) arenaHint.gameObject.SetActive(false);

        if (breakIndicator != null)
            breakIndicator.SetActive(false);

        ClearActiveEnemies();

        HideDedicatedBosses();
    }

    public void AbandonRun()
    {
        if (demoRun && GameProgress.IsArenaDemo) { GameProgress.ExitArenaDemo(); return; }
        playerInsideZone = false;
        ResetSpawnerCompletely();
        HideSpawnerUI();
    }

    public void SaveCheckpoint()
    {
        if (GameProgress.IsArenaDemo) return;
        if (tutorialWave || TipsManager.IsGuideActive || !IsRunning || waves.Count == 0) return;
        PlayerPrefs.SetInt("ArenaRunActive", 1);
        PlayerPrefs.SetInt("ArenaRunKind", crimsonArena ? 1 : 0);
        PlayerPrefs.SetInt("ArenaRunCompleted", IsArenaCleared ? 1 : 0);
        PlayerPrefs.SetInt("CrimsonArenaCycle", crimsonCycle
            + (crimsonArena && isBreakActive && currentWaveIndex == waves.Count - 1 ? 1 : 0));
        PlayerPrefs.SetInt("ArenaWaveIndex", IsArenaCleared ? NormalWaveLimit - 1
            : isBreakActive ? (currentWaveIndex + 1) % waves.Count : currentWaveIndex);
        if (isBreakActive) ClearArenaCheckpoint();
        else foreach (var feature in arenaFeatures) if (feature != null) feature.SaveCheckpoint();
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            PlayerPrefs.SetFloat("ArenaPlayerX", player.transform.position.x);
            PlayerPrefs.SetFloat("ArenaPlayerY", player.transform.position.y);
        }
        GameProgress.RequestSave();
    }

    private static void ClearArenaCheckpoint()
    {
        PlayerPrefs.SetInt("ArenaOutpostClaimed", 0);
        PlayerPrefs.SetFloat("ArenaOutpostCapture", 0);
        PlayerPrefs.SetInt("ArenaRuneSpent", 0);
        PlayerPrefs.SetInt("ArenaRunePlaced", 0);
    }

    public bool ContainsArenaPoint(Vector2 point, float inset = 0)
    {
        var zone = GetComponent<Collider2D>();
        if (zone == null || !zone.OverlapPoint(point)) return false;
        var bounds = zone.bounds;
        return point.x >= bounds.min.x + inset && point.x <= bounds.max.x - inset
            && point.y >= bounds.min.y + inset && point.y <= bounds.max.y - inset;
    }

    public void FillArenaTargets(List<MonoBehaviour> result, Vector2 point, float radius)
    {
        result.Clear();
        foreach (var enemy in activeEnemies) AddArenaTarget(result, enemy, point, radius);
        if (!crimsonArena && currentWaveIndex == 9) AddArenaTarget(result, bossWave10, point, radius);
        if (!crimsonArena && currentWaveIndex == 14) AddArenaTarget(result, bossWave15, point, radius);
        if (IsShaulaWave && shaula != null) AddArenaTarget(result, shaula.gameObject, point, radius);
    }

    private static void AddArenaTarget(List<MonoBehaviour> result, GameObject enemy, Vector2 point, float radius)
    {
        if (enemy == null || !enemy.activeInHierarchy || ((Vector2)enemy.transform.position - point).sqrMagnitude > radius * radius) return;
        var target = ArrowDef.FindEnemy(enemy.transform);
        if (target != null && !result.Contains(target)) result.Add(target);
    }

    public void RestoreCheckpointPosition(PlayerController player)
    {
        if (PlayerPrefs.GetInt("ArenaRunActive", 0) != 1 || !CanEnter
            || PlayerPrefs.GetInt("ArenaRunKind", 0) != (crimsonArena ? 1 : 0) || !PlayerPrefs.HasKey("ArenaPlayerX")) return;
        var zone = GetComponent<Collider2D>();
        var point = new Vector3(PlayerPrefs.GetFloat("ArenaPlayerX"), PlayerPrefs.GetFloat("ArenaPlayerY"), player.transform.position.z);
        if (zone != null && zone.OverlapPoint(point))
            player.transform.position = point;
        else PlayerPrefs.SetInt("ArenaRunActive", 0);
    }

    private void ClearActiveEnemies()
    {
        foreach (GameObject projectile in enemyProjectiles) if (projectile != null) Destroy(projectile);
        enemyProjectiles.Clear();
        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy != null)
                Destroy(enemy);
        }
        activeEnemies.Clear();
        regularBosses.Clear();
    }

    #endregion

    #region UI Методы

    // Wave zero uses the existing arena HUD/exit, without normal spawning or rating credit.
    public void BeginTutorialWave(int enemiesLeft)
    {
        if (IsRunning && !tutorialWave) return;
        ActiveArena = this;
        tutorialWave = true; playerInsideZone = true;
        isWaveActive = enemiesLeft > 0; isBreakActive = false;
        tutorialEnemiesLeft = enemiesLeft;
        ShowArenaPlayer();
        if (breakIndicator != null) breakIndicator.SetActive(false);
        if (arenaHint != null) arenaHint.gameObject.SetActive(false);
        ShowSpawnerUI(); UpdateTutorialUI();
    }

    public void SetTutorialEnemiesLeft(int count)
    {
        if (!tutorialWave) return;
        tutorialEnemiesLeft = Mathf.Max(0, count); UpdateTutorialUI();
    }

    public void BeginTutorialBreak()
    {
        if (!tutorialWave) BeginTutorialWave(0);
        isWaveActive = false; isBreakActive = true; tutorialEnemiesLeft = 0;
        // The teaching break waits for the player; ordinary breaks retain their timer.
        if (breakIndicator != null) breakIndicator.SetActive(true);
        UpdateTutorialUI();
    }

    public void EndTutorialWave()
    {
        if (!tutorialWave) return;
        if (ActiveArena == this) ActiveArena = null;
        tutorialWave = false; playerInsideZone = isWaveActive = isBreakActive = false;
        RestoreArenaPlayer();
        if (breakIndicator != null) breakIndicator.SetActive(false);
        HideSpawnerUI();
    }

    private void UpdateTutorialUI()
    {
        if (waveNumberText != null) waveNumberText.text = "Волна 0";
        if (waveTimerText != null)
        {
            waveTimerText.text = isBreakActive ? "Перерыв" : isWaveActive ? $"Враги: {tutorialEnemiesLeft}" : "Готов";
            waveTimerText.color = new Color32(227, 186, 101, 255);
        }
        if (waveTimerImage != null)
        {
            waveTimerImage.fillAmount = isBreakActive ? 1 : 1 - tutorialEnemiesLeft / 3f;
            waveTimerImage.color = isBreakActive ? Color.gray : Color.red;
        }
    }

    private void ShowArenaPlayer()
    {
        if (arenaPlayerSprite == null)
        {
            arenaPlayerSprite = FindObjectOfType<PlayerController>()?.GetComponent<SpriteRenderer>();
            if (arenaPlayerSprite != null) previousPlayerSort = arenaPlayerSprite.sortingOrder;
        }
        if (arenaPlayerSprite != null) arenaPlayerSprite.sortingOrder = Mathf.Max(8, previousPlayerSort);
    }

    private void RestoreArenaPlayer()
    {
        if (arenaPlayerSprite != null) arenaPlayerSprite.sortingOrder = previousPlayerSort;
        arenaPlayerSprite = null;
    }

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
            waveTimerText.text = overtime ? GameLocalization.Text("Враги ×10") : $"{seconds / 60}:{seconds % 60:00}";
            waveTimerText.color = new Color32(227, 186, 101, 255);

            if (waveNumberText != null)
                waveNumberText.text = crimsonArena ? GameLocalization.Text("Красная · {0}").Replace("{0}", CurrentWaveNumber.ToString()) : $"Волна {CurrentWaveNumber}";
            if (GameProgress.IsArenaDemo && waveNumberText != null)
                waveNumberText.text = GameLocalization.Text("Тест · Волна {0}").Replace("{0}", CurrentWaveNumber.ToString());
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

    private void HideDedicatedBosses()
    {
        HideShaula();
        if (bossWave10 != null) bossWave10.SetActive(false);
        if (bossHPBar10 != null) bossHPBar10.SetActive(false);
        if (bossPlaceholder10 != null) bossPlaceholder10.SetActive(false);
        if (bossWave15 != null) bossWave15.SetActive(false);
        if (bossHPBar15 != null) bossHPBar15.SetActive(false);
        if (bossPlaceholder15 != null) bossPlaceholder15.SetActive(false);
    }

    private GameObject PrepareDedicatedBoss(bool slime)
    {
        GameObject template = slime ? regularSlimeTemplate : regularAssassinTemplate;
        if (template == null) return null;
        GameObject previous = slime ? bossWave10 : bossWave15;
        if (previous != null)
        {
            previous.SetActive(false);
            Destroy(previous);
        }
        // An inactive pristine copy restarts HP, timers, healing and death state.
        // The template keeps the dedicated fight's original scene UI references.
        GameObject boss = Instantiate(template, template.transform.position, template.transform.rotation);
        boss.name = slime ? "SlimeBossWave10" : "AssassinBossWave15";
        if (slime) bossWave10 = boss;
        else bossWave15 = boss;
        return boss;
    }

    // Босс на 10-й волне (Слизь)
    private IEnumerator ActivateBossWave10()
    {
        // Подождём 1 секунду для визуального эффекта
        yield return new WaitForSeconds(1f);

        if (!isWaveActive || currentWaveIndex != 9) yield break;
        PrepareDedicatedBoss(true);

        if (bossWave10 != null)
            bossWave10.SetActive(true);
        bossActivated = true;
        if (bossWave10 == null) { CompleteWave(false); yield break; }
        SpawnSlimeBossEscort();
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

        if (!isWaveActive || currentWaveIndex != 14) yield break;
        PrepareDedicatedBoss(false);

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
