using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerPrefs = RedefineYG.PlayerPrefs;

public partial class WaveSpawner
{
    [Header("Arena mode and fair spawning")]
    [SerializeField] private bool crimsonArena;
    [SerializeField, Min(1)] private float minimumSpawnDistance = 4f;
    [SerializeField] private UnityEngine.UI.Image pairedSlimeHealth;
    [SerializeField] private UnityEngine.UI.Image pairedAssassinHealth;
    private bool overtime;
    private int crimsonCycle;
    private PlayerController arenaPlayer;
    private int pairedHudWidth, pairedHudHeight;
    private Rect pairedHudSafeArea;
    private Vector2 pairedHudParentSize;
    private bool pairedHudFinalWave;
    public static WaveSpawner ActiveArena { get; private set; }
    public bool IsCrimson => crimsonArena;
    public bool IsOvertime => overtime;
    public int RewardMultiplier => crimsonArena ? 2 : 1;
    public bool CanEnter => !crimsonArena || CurrentMmr >= 2000;
    public static int CurrentMmr
    {
        get
        {
            var summary = Stats.ReadLeaderboardSummary();
            return GameProgress.MmrScore(summary.waves, summary.kills, summary.bonusMmr);
        }
    }
    private bool IsPairedBossWave => crimsonArena && CurrentWaveNumber % 5 == 0;
    private float EffectiveWaveDuration => crimsonArena ? 90f + 4f * Mathf.Min(30, CurrentWaveNumber - 1)
        : waves[currentWaveIndex].waveDuration;
    private float HealthMultiplier => crimsonArena ? 2f * (1f + .2f * (CurrentWaveNumber - 1))
        : 1f + healthBoostPerStep * ReinforcementLevel;
    private float DamageMultiplier => crimsonArena ? 2f * (1f + .14f * (CurrentWaveNumber - 1))
        : 1f + damageBoostPerStep * ReinforcementLevel;
    private float MovementMultiplier => crimsonArena ? 1f + Mathf.Min(.4f, .03f * (CurrentWaveNumber - 1))
        : 1f + movementBoostPerStep * ReinforcementLevel;

    private void LayoutPairedBossHud()
    {
        if (bossHPBar10 == null || !(bossHPBar10.transform.parent is RectTransform parent)) return;
        if (pairedHudWidth == Screen.width && pairedHudHeight == Screen.height && pairedHudSafeArea == Screen.safeArea
            && pairedHudParentSize == parent.rect.size && pairedHudFinalWave == IsFinalBossWave) return;
        pairedHudWidth = Screen.width; pairedHudHeight = Screen.height;
        pairedHudSafeArea = Screen.safeArea; pairedHudParentSize = parent.rect.size;
        pairedHudFinalWave = IsFinalBossWave;
        float bottom = Screen.height > 0 ? Screen.safeArea.yMin / Screen.height * parent.rect.height : 0;
        float center = Screen.width > 0 ? (Screen.safeArea.center.x / Screen.width - .5f) * parent.rect.width : 0;
        float safeWidth = Screen.width > 0 ? Screen.safeArea.width / Screen.width * parent.rect.width : parent.rect.width;
        float scale = Mathf.Min(1f, safeWidth * .5f / 260f);
        float shaulaOffset = IsFinalBossWave && shaulaHealthPanel != null
            ? shaulaHealthPanel.GetComponent<RectTransform>().sizeDelta.y * Mathf.Min(1, parent.rect.width * .5f / 760f) + 8 : 0;
        Place(bossHPBar10, 0); Place(bossHPBar15, 1);
        void Place(GameObject panel, int row)
        {
            if (panel == null || !(panel.transform is RectTransform rect)) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(center, bottom + 12 + shaulaOffset + row * 42 * scale);
            rect.sizeDelta = new Vector2(260, 36);
            rect.localScale = new Vector3(scale, scale, 1);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetArenaStatics() => ActiveArena = null;

    private void OnDestroy()
    {
        if (pickerOwner == this) { pickerOwner = null; demoInputFrame = Time.frameCount; }
        if (ActiveArena == this) ActiveArena = null;
    }

    public static WaveSpawner SavedArena()
    {
        bool crimson = PlayerPrefs.GetInt("ArenaRunKind", 0) == 1 && PlayerPrefs.GetInt("ArenaRunActive", 0) == 1;
        return FindArena(crimson);
    }

    public static WaveSpawner FindArena(bool crimson)
    {
        foreach (var arena in FindObjectsOfType<WaveSpawner>())
            if (arena.crimsonArena == crimson) return arena;
        return null;
    }

    private bool TrySafeSpawn(Vector3 preferred, out Vector3 position)
    {
        if (arenaPlayer == null) arenaPlayer = FindObjectOfType<PlayerController>();
        position = preferred;
        if (arenaPlayer == null) return false;
        float radius = Mathf.Max(1f, minimumSpawnDistance);
        bool Safe(Vector3 point) => ContainsArenaPoint(point, .6f)
            && ((Vector2)(point - arenaPlayer.transform.position)).sqrMagnitude >= radius * radius;
        if (Safe(preferred)) return true;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            if (spawnPoints.Count == 0) break;
            var point = spawnPoints[Random.Range(0, spawnPoints.Count)];
            if (point == null) continue;
            var candidate = point.position + new Vector3(Random.Range(-spawnOffsetRange.x, spawnOffsetRange.x),
                Random.Range(-spawnOffsetRange.y, spawnOffsetRange.y), 0);
            if (Safe(candidate)) { position = candidate; return true; }
        }
        foreach (var point in spawnPoints)
            if (point != null && Safe(point.position)) { position = point.position; return true; }
        return false;
    }

    private void EnterOvertime()
    {
        if (overtime || tutorialWave || !isWaveActive) return;
        overtime = true;
        waveTimeLeft = 0;
        foreach (var enemy in activeEnemies) EnrageEnemy(enemy);
        if (!crimsonArena && currentWaveIndex == 9) EnrageEnemy(bossWave10);
        if (!crimsonArena && currentWaveIndex == 14) EnrageEnemy(bossWave15);
        foreach (var projectile in enemyProjectiles) ScaleProjectileValues(projectile, 10f, 1f);
        hintTime = 8f;
        if (arenaHint != null)
        {
            arenaHint.text = GameLocalization.Text("Время вышло! Враги: здоровье и урон ×10");
            arenaHint.gameObject.SetActive(true);
        }
        UpdateWaveUI(0, 1, true);
        GameProgress.RequestSave();
    }

    private static void EnrageEnemy(GameObject enemy)
    {
        if (enemy == null || !enemy.activeInHierarchy) return;
        var target = ArrowDef.FindEnemy(enemy.transform);
        if (target is Enemy melee && melee.IsAlive) melee.ApplyWaveScaling(10f, 10f, 1f);
        else if (target is Slime slime && slime.IsAlive) slime.ApplyWaveScaling(10f, 10f, 1f);
        else if (target is SceletonDef skeleton && skeleton.IsAlive) skeleton.ApplyWaveScaling(10f, 10f, 1f);
        else if (target is SceletMag mage && mage.IsAlive) mage.ApplyWaveScaling(10f, 10f, 1f);
        else if (target is HealerEnemy healer && healer.IsAlive) healer.ApplyWaveScaling(10f, 10f, 1f);
        else if (target is SlimeBoss slimeBoss && slimeBoss.IsAlive) slimeBoss.ApplyArenaScaling(10f, 10f);
        else if (target is Roberto assassin && assassin.IsAlive) assassin.ApplyArenaScaling(10f, 10f);
        else return;
        EnemyDefeatEffect.PlayEnrage(enemy);
    }

    private IEnumerator SpawnPairedBosses()
    {
        yield return new WaitForSeconds(1f);
        if (!IsCombatActive || (!IsPairedBossWave && !IsFinalBossWave)) yield break;
        if (IsFinalBossWave && (regularSlimeTemplate == null || regularAssassinTemplate == null || shaulaPrefab == null))
        {
            Debug.LogError("Final wave requires all three boss templates."); CompleteWave(false); yield break;
        }
        var zone = GetComponent<Collider2D>().bounds;
        foreach (bool slime in new[] { true, false })
        {
            var source = slime ? regularSlimeTemplate : regularAssassinTemplate;
            if (source == null) continue;
            var position = zone.center + new Vector3(slime ? -5f : 5f, 3f, 0);
            position.z = source.transform.position.z;
            var boss = Instantiate(source, position, source.transform.rotation);
            boss.SetActive(false);
            float bossHealth = HealthMultiplier * (crimsonArena ? 1.5f : 1f);
            if (slime)
                boss.GetComponent<SlimeBoss>().ConfigureArenaBoss(this, bossHealth, DamageMultiplier,
                    MovementMultiplier, ProjectileSpeedMultiplier, bossHPBar10, pairedSlimeHealth);
            else
                boss.GetComponent<Roberto>().ConfigureArenaBoss(this, bossHealth, DamageMultiplier,
                    MovementMultiplier, ProjectileSpeedMultiplier, bossHPBar15, pairedAssassinHealth);
            if (IsFinalBossWave)
            {
                var previous = slime ? bossWave10 : bossWave15;
                if (previous != null) { previous.SetActive(false); Destroy(previous); }
                boss.name = slime ? "FinalSlimeBoss" : "FinalAssassinBoss";
                if (slime) bossWave10 = boss; else bossWave15 = boss;
            }
            activeEnemies.Add(boss);
            boss.SetActive(true);
            EnemyDefeatEffect.PlaySpawn(boss);
        }
        if (IsFinalBossWave && !SpawnShaula()) { CompleteWave(false); yield break; }
        LayoutPairedBossHud();
        bossActivated = true;
    }

    public static GameObject SpawnEnemyLoot(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (GameProgress.IsArenaDemo) return null;
        if (prefab == null) return null;
        var loot = Instantiate(prefab, position, rotation);
        var arena = ActiveArena;
        if (arena != null && arena.IsCrimson && arena.ContainsArenaPoint(position))
        {
            if (loot.TryGetComponent<Coin>(out var coin)) coin.ScaleReward(2);
            if (loot.TryGetComponent<AddHolyArrow>(out var arrows)) arrows.ScaleReward(2);
            if (loot.TryGetComponent<NewArrow>(out var otherArrows)) otherArrows.ScaleReward(2);
        }
        return loot;
    }

    private void AwardCrimsonWaveLoot()
    {
        if (GameProgress.IsArenaDemo) return;
        if (!crimsonArena) return;
        if (arenaPlayer == null) arenaPlayer = FindObjectOfType<PlayerController>();
        if (arenaPlayer == null) return;
        // Direct supplies are guaranteed, independent of random monster drops.
        arenaPlayer.AddPoitonHeal(IsPairedBossWave ? 4 : 1);
        arenaPlayer.crossbowController.AddArrows(3, IsPairedBossWave ? 12 : 3);
        if (IsPairedBossWave)
        {
            arenaPlayer.AddCoin(1000);
            arenaPlayer.AddMirorr(1);
        }
    }

    private static void ScaleProjectileValues(GameObject projectile, float damage, float speed)
    {
        if (projectile == null) return;
        if (projectile.TryGetComponent<EnemyProgject>(out var shot)) { shot.damage *= damage; shot.speed *= speed; }
        if (projectile.TryGetComponent<EnemyProgjectVAR>(out var other)) { other.damage *= damage; other.speed *= speed; }
    }
}
