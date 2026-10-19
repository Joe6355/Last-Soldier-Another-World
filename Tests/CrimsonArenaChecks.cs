// Run in Builds/RatingValidation/Project only: separate save namespace.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Prefs = RedefineYG.PlayerPrefs;

[InitializeOnLoad]
public static class CrimsonArenaChecks
{
    const string Flag = "LastSoldier.CrimsonV19Checks";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static int phase, frame, coins, potions, mirrors, kills, bosses, waves, mmr, bossCoinReward;
    static double deadline, heartbeat;
    static float began;
    static PlayerController player;
    static WaveSpawner normal, red;
    static Stats stats;
    static Portal gate;
    static GameObject shot;
    static SlimeBoss slimeBoss;
    static Roberto assassin;
    static readonly List<MonoBehaviour> matrix = new List<MonoBehaviour>();
    static readonly List<int> initialHp = new List<int>();
    static readonly List<string> checks = new List<string>();
    static CrimsonArenaChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run()
    {
        PlayerSettings.companyName = "LastSoldierCrimsonV19Checks"; PlayerSettings.productName = "CrimsonV19Checks";
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        SessionState.SetBool(Flag, true); Attach(); EditorApplication.EnterPlaymode();
    }
    static void Attach()
    {
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        deadline = EditorApplication.timeSinceStartup + 150;
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack); }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private | BindingFlags.Public).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private | BindingFlags.Public).SetValue(obj, value);
    static object Call(object obj, string method, params object[] arguments) => obj.GetType().GetMethod(method, Private).Invoke(obj, arguments);
    static List<GameObject> Enemies(WaveSpawner owner) => Get<List<GameObject>>(owner, "activeEnemies");
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks.Add(message); }
    static void Next(int step) { phase = step; frame = Time.frameCount + 2; began = Time.time; Debug.Log("CRIMSON_PHASE: " + step); }
    static void Place(Vector3 point)
    {
        var delta = point - player.transform.position;
        player.transform.position = point; player.GetComponent<Rigidbody2D>().position = point;
        player.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        foreach (var camera in UnityEngine.Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>())
        { camera.OnTargetObjectWarped(player.transform, delta); camera.PreviousStateIsValid = false; }
        Physics2D.SyncTransforms();
    }
    static int Hp(MonoBehaviour enemy)
    {
        if (enemy is Enemy melee) return melee.health;
        if (enemy is SlimeBoss) return Mathf.RoundToInt(Get<float>(enemy, "currentHealth"));
        if (enemy is Roberto) return Mathf.RoundToInt(Get<float>(enemy, "currentHP"));
        return Get<int>(enemy, "health");
    }
    static void Freeze(MonoBehaviour actor)
    {
        actor.StopAllCoroutines(); actor.CancelInvoke(); actor.enabled = false;
        foreach (var collider in actor.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        if (actor.TryGetComponent<Rigidbody2D>(out var body)) { body.velocity = Vector2.zero; body.constraints = RigidbodyConstraints2D.FreezeAll; }
    }
    static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate(); Application.runInBackground = true;
        if (EditorApplication.timeSinceStartup >= heartbeat)
        {
            heartbeat = EditorApplication.timeSinceStartup + 15;
            Debug.Log("CRIMSON_TICK: phase=" + phase + "; frame=" + Time.frameCount + "; time=" + Time.time + "; scale=" + Time.timeScale);
        }
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timed out at phase " + phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount < frame) return;
        try
        {
            switch (phase)
            {
                case 0:
                    if (!GameProgress.IsReady) return;
                    Prefs.DeleteAll();
                    Prefs.SetInt("GuidedTutorial.v1.Done", 1); Prefs.SetInt("GuidedTutorial.v1.ArenaDone", 1);
                    UnityEngine.Object.FindObjectOfType<MainMenuController>().Play(); Next(1); break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    YG.YG2.envir.isDesktop = true; YG.YG2.envir.isMobile = YG.YG2.envir.isTablet = false;
                    YG.YG2.PauseGame(false);
                    foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                    player = UnityEngine.Object.FindObjectOfType<PlayerController>();
                    stats = UnityEngine.Object.FindObjectOfType<Stats>(); normal = WaveSpawner.FindArena(false); red = WaveSpawner.FindArena(true);
                    gate = GameObject.Find("CrimsonEntrance").GetComponent<Portal>();
                    player.hp = player.maxHp = 300; Set(player, "invulnerableUntil", float.MaxValue);
                    UnityEngine.Object.FindObjectOfType<Ui>().SetMobileOrientationPause(false);
                    UnityEngine.Object.FindObjectOfType<Ui>().StartGame(); Time.timeScale = 1f;
                    player.crossbowController.canShoot = false;
                    Check(normal != null && red != null && normal != red, "Two independent arena owners exist");
                    Check(red.GetComponent<PolygonCollider2D>()?.points.Length == 8, "Red island uses its eight-sided shoreline collider");
                    Check(gate.linkedPortal.GetComponent<Portal>().linkedPortal == gate.transform, "New portal is bidirectionally linked");
                    normal.enabled = red.enabled = false;
                    normal.AbandonRun();
                    Next(2); break;
                case 2:
                    TestSafeSpawning();
                    // A late ordinary boss deliberately bypasses the ordinary spawn exclusion.
                    Set(normal, "playerInsideZone", true); Set(normal, "isWaveActive", true); Set(normal, "currentWaveIndex", 20);
                    SetActive(normal); Set(normal, "enemiesSpawnedInWave", 29);
                    var originalPoints = Get<List<Transform>>(normal, "spawnPoints"); var originalOffset = Get<Vector2>(normal, "spawnOffsetRange");
                    var probe = new GameObject("OnlySpawnPoint").transform; probe.position = player.transform.position;
                    Set(normal, "spawnPoints", new List<Transform> { probe }); Set(normal, "spawnOffsetRange", Vector2.zero);
                    Check((bool)Call(normal, "SpawnEnemy", Get<List<WaveConfig>>(normal, "waves")[20]), "Boss still spawns with only a point at the player");
                    Check(Vector2.Distance(Enemies(normal).Single().transform.position, player.transform.position) < .01f, "Bosses are exempt from minimum spawn distance");
                    Set(normal, "spawnPoints", originalPoints); Set(normal, "spawnOffsetRange", originalOffset); UnityEngine.Object.Destroy(probe.gameObject);
                    Call(normal, "ClearActiveEnemies"); Set(normal, "currentWaveIndex", 0);
                    CreateOvertimeMatrix(); Next(3); break;
                case 3:
                    if (Time.time - began < .15f) return;
                    Debug.Log("CRIMSON_OVERTIME: freezing actors");
                    foreach (var actor in matrix) { Freeze(actor); initialHp.Add(Hp(actor)); }
                    shot = new GameObject("TrackedOvertimeProjectile"); shot.SetActive(false);
                    var projectile = shot.AddComponent<EnemyProgject>(); projectile.damage = 3; projectile.speed = 6;
                    normal.TrackEnemyProjectile(shot);
                    Set(normal, "waveTimeLeft", 0f); Call(normal, "Update");
                    Debug.Log("CRIMSON_OVERTIME: timer applied");
                    Check(normal.IsOvertime && normal.IsCombatActive && normal.CurrentWaveNumber == 1, "Timer expiry keeps this wave active, with no automatic break");
                    for (int i = 0; i < matrix.Count; i++) Check(Hp(matrix[i]) == initialHp[i] * 10, "Overtime health x10 for " + matrix[i].GetType().Name);
                    Check(Get<float>(matrix.First(a => a is Slime), "waveDamageMultiplier") == 10, "Slime future projectiles gain damage x10");
                    Check(Get<float>(matrix.First(a => a is SceletMag), "waveDamageMultiplier") == 10, "Mage future projectiles gain damage x10");
                    Check(matrix.OfType<Enemy>().First().damageTouch == 30, "Bomber contact damage x10");
                    Check(projectile.damage == 30 && projectile.speed == 6, "Already flying projectile damage x10, speed unchanged");
                    Call(normal, "EnterOvertime"); Call(normal, "Update");
                    Check(Hp(matrix[0]) == initialHp[0] * 10 && projectile.damage == 30, "Overtime applies exactly once");
                    Check(stats.completedWaves == 0, "Timeout gives no completion credit");
                    var fresh = NewEnemy("SlimeTrue", player.transform.position + Vector3.right * 10);
                    Check(Get<int>(fresh, "health") == Get<int>(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/SlimeTrue.prefab").GetComponent<Slime>(), "health"), "Later arrivals do not receive a retroactive overtime buff");
                    UnityEngine.Object.Destroy(fresh.gameObject);
                    normal.AbandonRun(); Place(gate.transform.position + new Vector3(0, -2.5f, 0));
                    stats.completedWaves = 19; stats.countEnemyDead = 99; stats.SaveInfo();
                    Vector3 before = player.transform.position; Call(gate, "TryTeleport", player.GetComponent<Collider2D>());
                    Check(!gate.IsUnlocked && player.transform.position == before && !red.CanEnter, "1999 MMR blocks red portal without moving player");
                    Capture("crimson-v19-portal-locked.png", new Vector3(15.7f,.8f,-10), 4.2f);
                    stats.countEnemyDead = 100; stats.SaveInfo();
                    Check(gate.IsUnlocked && red.CanEnter, "Exactly 2000 MMR unlocks red portal");
                    Call(gate, "TryTeleport", player.GetComponent<Collider2D>()); Physics2D.SyncTransforms();
                    Call(red, "OnTriggerEnter2D", player.GetComponent<Collider2D>()); red.StopAllCoroutines(); Next(4); break;
                case 4:
                    if (Time.time - began < .15f) return;
                    Check(WaveSpawner.ActiveArena == red && red.IsCombatActive && !normal.IsRunning, "Red portal routes combat to red owner only; player=" + player.transform.position
                        + "; destination=" + gate.linkedPortal.position + "; offset=" + gate.offset + "; active=" + WaveSpawner.ActiveArena?.name
                        + "; red=" + red.IsCombatActive + "; normal=" + normal.IsRunning + "; guide=" + TipsManager.IsGuideActive);
                    Check(Enemies(red).Count >= 8 && Enemies(red).Count <= 10, "Red wave starts with a pack immediately");
                    var sourcePrefabs = Get<List<GameObject>>(red, "enemyPrefabs");
                    foreach (var obj in Enemies(red))
                    {
                        var actor = ArrowDef.FindEnemy(obj.transform); Freeze(actor);
                        var prefab = sourcePrefabs.First(p => obj.name.StartsWith(p.name + "(", StringComparison.Ordinal));
                        var sourceActor = prefab.GetComponents<MonoBehaviour>().First(a => a.GetType() == actor.GetType());
                        Check(Hp(actor) == 2 * Hp(sourceActor), "Red opening health x2: " + prefab.name);
                        Check(Vector2.Distance(player.transform.position, actor.transform.position) >= 4, "Red opening pack respects player exclusion");
                    }
                    Check(red.EffectiveEnemyLimit == 32 && red.EffectiveSpawnInterval < .7f, "Opening red wave has 32 enemies at fast spawn rate");
                    Check(Get<int>(red, "maxConcurrentLateEnemies") == 48, "Red population capped at 48 simultaneous actors");
                    Check(Enemies(red).Select(o => o.name).Distinct().Count() == 9, "First shuffled pack contains all nine species");
                    Check(WaveSpawner.SavedArena() == red, "Saved checkpoint selects correct arena");
                    var ground = red.transform.root.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>().First(t => t.name == "CrimsonGround");
                    Check(!red.transform.root.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>().Any(t => t.name.Contains("Barear")), "Old water tiles do not masquerade as safe island ground");
                    foreach (var point in Get<List<Transform>>(red, "spawnPoints"))
                        if (red.ContainsArenaPoint(point.position)) Check(ground.HasTile(ground.WorldToCell(point.position)), "Safe spawn point has visible dry land underneath");
                    Call(red, "UpdateWaveUI", red.RemainingWaveTime, 90f, true);
                    var titleText = Get<UnityEngine.UI.Text>(red, "waveNumberText");
                    Check(titleText.preferredWidth <= titleText.rectTransform.rect.width + 1, "Compact red wave title fits the existing HUD");
                    var redCenter = red.GetComponent<Collider2D>().bounds.center;
                    Check(UnityEngine.Object.FindObjectsOfType<DeadZone>().Where(z => Get<WaveSpawner>(z, "arenaOwner") == null)
                        .All(z => !z.GetComponent<Collider2D>().OverlapPoint(redCenter)), "Existing world death zones do not overlap the new island interior");
                    float firstHealth = (float)typeof(WaveSpawner).GetProperty("HealthMultiplier", Private).GetValue(red);
                    Set(red, "currentWaveIndex", 1);
                    Check((float)typeof(WaveSpawner).GetProperty("HealthMultiplier", Private).GetValue(red) > firstHealth
                        && red.EffectiveEnemyLimit > 32, "Red health and population increase on the next wave"); Set(red, "currentWaveIndex", 0);
                    TestRewards();
                    Capture("crimson-v19-island.png", new Vector3(redCenter.x, redCenter.y, -10), 26);
                    var water = red.transform.root.GetComponentInChildren<DeadZone>();
                    Vector3 outside = redCenter + Vector3.right * 29; outside.z = player.transform.position.z; Place(outside);
                    Call(red, "OnTriggerExit2D", player.GetComponent<Collider2D>());
                    Check(red.IsRunning, "Crossing shoreline does not cancel boundary protection");
                    Call(water, "LateUpdate"); Check(red.ContainsArenaPoint(player.transform.position), "Death zone returns an out-of-bounds dash to dry land");
                    Capture("crimson-v19-shore.png", player.transform.position + new Vector3(2,0,-6), 6);
                    player.TeleportPlayerHome(); Next(5); break;
                case 5:
                    Check(!red.IsRunning && WaveSpawner.ActiveArena == null && Prefs.GetInt("ArenaRunActive", 0) == 0, "Mirror/home return abandons red run synchronously");
                    Check(player.transform.position.x < 40, "Water does not pull a camp return back to the island");
                    Place(red.GetComponent<Collider2D>().bounds.center);
                    Call(red, "OnTriggerEnter2D", player.GetComponent<Collider2D>()); Set(red, "currentWaveIndex", 4); Call(red, "StartWave", 4, false);
                    Next(6); break;
                case 6:
                    if (Time.time - began < 1.25f) return;
                    red.StopAllCoroutines();
                    slimeBoss = Enemies(red).Select(o => o != null ? o.GetComponent<SlimeBoss>() : null).FirstOrDefault(a => a != null);
                    assassin = Enemies(red).Select(o => o != null ? o.GetComponent<Roberto>() : null).FirstOrDefault(a => a != null);
                    Check(slimeBoss != null && assassin != null && slimeBoss.IsAlive && assassin.IsAlive, "Every fifth red wave brings both bosses alive together");
                    Check(Get<bool>(slimeBoss, "arenaBossEncounter") && Get<bool>(assassin, "arenaBossEncounter"), "Paired enemies retain true boss reward classification");
                    Check(Get<GameObject>(red, "bossHPBar10").activeSelf && Get<GameObject>(red, "bossHPBar15").activeSelf, "Both paired boss health panels are displayed");
                    Check(!Get<GameObject>(normal, "bossHPBar10").activeSelf && !Get<GameObject>(normal, "bossHPBar15").activeSelf, "Paired bosses never toggle normal arena health panels");
                    foreach (var obj in Enemies(red)) if (obj != null) Freeze(ArrowDef.FindEnemy(obj.transform));
                    TestCombatPolish();
                    Capture("crimson-v19-boss-pair.png", red.GetComponent<Collider2D>().bounds.center + new Vector3(0,2,-6), 7);
                    int slimeHp = Hp(slimeBoss), assassinHp = Hp(assassin);
                    Call(red, "EnterOvertime");
                    Check(Hp(slimeBoss) == slimeHp * 10 && Hp(assassin) == assassinHp * 10, "Paired bosses also receive timer penalty once");
                    bosses = stats.countBossDead; coins = player.totalCoins;
                    bossCoinReward = Get<int>(slimeBoss, "bossKillBonus") * 2;
                    slimeBoss.enabled = assassin.enabled = true;
                    slimeBoss.TakeDamage(10000000, false); assassin.TakeDamage(10000000); Next(7); break;
                case 7:
                    if (Time.time - began < 4f) return;
                    Check(stats.countBossDead == bosses + 2, "Both simultaneous bosses count once each");
                    Check(player.totalCoins >= coins + bossCoinReward, "Red slime boss guaranteed money is doubled");
                    foreach (var obj in Enemies(red).ToArray()) if (obj != null) UnityEngine.Object.Destroy(obj); Enemies(red).Clear();
                    potions = player.PotionCount; mirrors = player.MirrorCount; coins = player.totalCoins;
                    Call(red, "CompleteWave", true); Check(player.PotionCount == potions + 4 && player.MirrorCount == mirrors + 1
                        && player.totalCoins == coins + 1000, "Paired wave grants four potions, mirror and 1000 bonus coins");
                    int rewardCoins = player.totalCoins; Call(red, "CompleteWave", true);
                    Check(player.totalCoins == rewardCoins, "Wave clear reward cannot be claimed twice");
                    player.TeleportPlayerHome();
                    Check(GameLocalization.English("Красная арена · Волна 5") == "Crimson Arena · Wave 5", "New arena wave label supports English");
                    Check(GameLocalization.English("Требуется 2 000 MMR") == "Requires 2 000 MMR", "Locked portal supports English");
                    Finish(true, "Safe spawning, overtime, red progression, rewards, MMR, paired bosses, checkpoint routing, mirror return and shoreline checks passed"); break;
            }
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }
    static void SetActive(WaveSpawner owner) => typeof(WaveSpawner).GetProperty("ActiveArena").SetValue(null, owner);
    static void TestSafeSpawning()
    {
        var points = Get<List<Transform>>(normal, "spawnPoints"); var radius = normal.GetComponent<Collider2D>().bounds;
        foreach (var point in points)
        {
            Place(point.position);
            for (int i = 0; i < 8; i++)
            {
                object[] arguments = { player.transform.position, Vector3.zero };
                Check((bool)typeof(WaveSpawner).GetMethod("TrySafeSpawn", Private).Invoke(normal, arguments), "Safe fallback exists at spawn point " + point.name + "/" + i);
                var position = (Vector3)arguments[1];
                Check(Vector2.Distance(player.transform.position, position) >= 4 && normal.ContainsArenaPoint(position), "Fallback is inside arena and at least four units away");
            }
        }
        Place(radius.center); var offset = Get<Vector2>(normal, "spawnOffsetRange");
        var only = new GameObject("UnsafeOnly").transform; only.position = player.transform.position;
        Set(normal, "spawnPoints", new List<Transform> { only }); Set(normal, "spawnOffsetRange", Vector2.zero);
        Check(!(bool)Call(normal, "SpawnEnemy", Get<List<WaveConfig>>(normal, "waves")[0]), "Ordinary spawn is deferred when no safe position exists");
        Set(normal, "spawnPoints", points); Set(normal, "spawnOffsetRange", offset); UnityEngine.Object.Destroy(only.gameObject);
    }
    static MonoBehaviour NewEnemy(string name, Vector3 point)
    {
        var obj = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/" + name + ".prefab"), point, Quaternion.identity);
        foreach (var collider in obj.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        return ArrowDef.FindEnemy(obj.transform);
    }
    static void CreateOvertimeMatrix()
    {
        foreach (string name in new[] { "SlimeTrue", "Bomber", "SceletSheld", "SceletMag", "HealerEnemy", "HolyGhost" })
        {
            var actor = NewEnemy(name, player.transform.position + new Vector3(7 + matrix.Count, 4, 0));
            if (actor is Enemy melee) melee.damageTouch = 3;
            matrix.Add(actor); Enemies(normal).Add(actor.gameObject);
        }
    }
    static void TestRewards()
    {
        foreach (string id in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id));
            if (prefab.GetComponent<Coin>() == null && prefab.GetComponent<AddHolyArrow>() == null && prefab.GetComponent<NewArrow>() == null) continue;
            var loot = WaveSpawner.SpawnEnemyLoot(prefab, red.GetComponent<Collider2D>().bounds.center, Quaternion.identity);
            if (loot.TryGetComponent<Coin>(out var coin)) Check(Get<int>(coin, "coinValue") == Get<int>(prefab.GetComponent<Coin>(), "coinValue") * 2, "Coin pickup value x2: " + prefab.name);
            if (loot.TryGetComponent<AddHolyArrow>(out var holy)) Check(Get<int>(holy, "countArrow") == Get<int>(prefab.GetComponent<AddHolyArrow>(), "countArrow") * 2, "Holy ammunition pickup x2");
            if (loot.TryGetComponent<NewArrow>(out var arrows)) Check(Get<int>(arrows, "countArrow") == Get<int>(prefab.GetComponent<NewArrow>(), "countArrow") * 2, "Ammunition pickup x2: " + prefab.name);
            UnityEngine.Object.Destroy(loot);
        }
        kills = stats.TotalKills; waves = stats.completedWaves; mmr = stats.Mmr;
        stats.AddEnemyKill(Stats.EnemyKind.Slime); stats.AddElitEnemyDead(Stats.EnemyKind.Skeleton); stats.AddBossDead();
        Check(stats.TotalKills == kills + 3 && stats.Mmr == mmr + 6, "Red MMR x2 preserves accurate enemy statistics");
        potions = player.PotionCount; coins = player.totalCoins;
        Call(red, "CompleteWave", true);
        Check(stats.completedWaves == waves + 1 && stats.Mmr == mmr + 206, "Red wave gives 200 MMR with one actual completed wave");
        Check(player.PotionCount == potions + 1 && player.totalCoins == coins, "Ordinary red clear grants a guaranteed potion without inventing coins");
        var summary = Stats.ReadLeaderboardSummary();
        Check(GameProgress.MmrScore(summary.waves, summary.kills, summary.bonusMmr) == stats.Mmr, "Stored leaderboard payload retains red bonus MMR");
        Check(GameProgress.MmrScore(20, 100) == 2100, "Legacy normal arena rating remains unchanged");
    }
    static void TestCombatPolish()
    {
        float waveDuration = (float)typeof(WaveSpawner).GetProperty("EffectiveWaveDuration", Private).GetValue(red);
        Call(red, "UpdateWaveUI", waveDuration, waveDuration, true);
        Call(red, "LayoutPairedBossHud"); Canvas.ForceUpdateCanvases();
        CheckPairedHud();
        var barrels = UnityEngine.Object.FindObjectsOfType<ArenaFeature>().Where(f => f.Kind == ArenaFeature.FeatureKind.Barrel).ToArray();
        Check(barrels.Length > 0 && barrels.All(b => Get<int>(b, "barrelEnemyDamage") == 90), "All arena barrel instances use doubled base damage (90)");
        var barrel = barrels.First(b => Get<WaveSpawner>(b, "spawner") == red);
        Check(barrel.BarrelDamage == Mathf.CeilToInt(90 * (float)typeof(WaveSpawner).GetProperty("HealthMultiplier", Private).GetValue(red)),
            "Blood arena barrel damage retains the difficulty multiplier");
        var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/HolyOrc.prefab"));
        actor.transform.position = barrel.transform.position;
        var enemy = actor.GetComponent<Enemy>(); enemy.health = 1000; Freeze(enemy); Enemies(red).Add(actor);
        var otherEnemies = Enemies(red).Where(o => o != actor).ToArray(); Enemies(red).Clear(); Enemies(red).Add(actor);
        Call(barrel, "DamageArea", barrel.BarrelDamage, .5f, false);
        Enemies(red).AddRange(otherEnemies);
        Check(enemy.health == 1000 - barrel.BarrelDamage, "Actual barrel area damage uses the stronger value, including against holy enemies");
        actor.transform.position = player.transform.position + Vector3.right * 2;
        var tips = UnityEngine.Object.FindObjectOfType<TipsManager>();
        var bow = player.crossbowController;
        bool numbersEnabled = DamageNumbers.IsEnabled; DamageNumbers.SetEnabled(false);
        GameLocalization.SetSelection("ru");
        ResetHint();
        var arrow = UnityEngine.Object.Instantiate(bow.arrowPrefabs[0]).GetComponent<ArrowDef>();
        int hp = enemy.health; float scale = Time.timeScale;
        Call(arrow, "ApplyHit", enemy);
        Check(enemy.health == hp, "Immunity hint never changes the holy enemy's damage immunity");
        var hint = Get<RectTransform>(tips, "combatHintRoot"); var text = Get<TMPro.TMP_Text>(tips, "combatHintText");
        Check(hint != null && hint.gameObject.activeSelf && text.text.Contains("только красные"), "Wrong-arrow hit explains holy immunity even with damage numbers disabled");
        Check(Time.timeScale == scale && !hint.GetComponent<UnityEngine.UI.Image>().raycastTarget && !text.raycastTarget,
            "Combat hints do not pause combat or intercept input");
        float remaining = Get<float>(tips, "combatHintRemaining"); TipsManager.ReportArrowImmunity();
        Check(Get<float>(tips, "combatHintRemaining") == remaining, "Rapid repeated hits cannot restart the hint timer");
        Check(hint.anchorMin.y == 1 && hint.anchoredPosition.y < -50, "Combat hint is fixed below the wave HUD");
        Vector2 hintPosition = hint.anchoredPosition; Vector3 position = player.transform.position;
        player.transform.position += Vector3.right; Call(tips, "TickCombatHints");
        Check(hint.anchoredPosition == hintPosition, "Moving the hero does not move or jump the combat hint"); player.transform.position = position;
        GameLocalization.SetSelection("en"); Check(text.text.StartsWith("Immune!"), "Active combat hint immediately switches to English");
        var controls = UnityEngine.Object.FindObjectOfType<MobileControls>();
        YG.YG2.envir.isDesktop = false; YG.YG2.envir.isMobile = true; YG.YG2.envir.deviceType = "mobile";
        controls.RefreshLayout(); Call(tips, "TickCombatHints"); Call(red, "LayoutPairedBossHud"); Canvas.ForceUpdateCanvases();
        CheckPairedHud();
        Check(text.text.Contains("Tap an arrow"), "Mobile immunity hint points at touch arrow selection");
        text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "English combat hint fits its mobile panel");
        Capture("combat-v25-mobile-en.png", red.GetComponent<Collider2D>().bounds.center + new Vector3(0,2,-6), 7);
        GameLocalization.SetSelection("ru"); text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Russian combat hint fits its mobile panel");
        Capture("combat-v25-mobile-ru.png", red.GetComponent<Collider2D>().bounds.center + new Vector3(0,2,-6), 7);
        YG.YG2.envir.isDesktop = true; YG.YG2.envir.isMobile = false; YG.YG2.envir.deviceType = "desktop";
        controls.RefreshLayout(); Call(tips, "TickCombatHints");
        var counts = Get<int[]>(bow, "arrowCounts"); int holyStock = counts[2]; counts[2] = 0;
        ResetHint(); TipsManager.ReportArrowImmunity(); Check(text.text.Contains("торговца"), "Empty holy stock explains how to restock"); counts[2] = holyStock;
        Time.timeScale = 0; Call(tips, "TickCombatHints"); Check(!hint.gameObject.activeSelf, "Combat hint hides while paused");
        Time.timeScale = scale; ResetHint(); Set(tips, "holyEnemyExplained", false); Set(tips, "nextHolyProbe", 0f);
        Call(tips, "TickCombatHints"); Check(hint.gameObject.activeSelf && text.text.StartsWith("Красные враги"), "Nearby holy enemy is explained proactively before the first wrong hit");
        UnityEngine.Object.Destroy(arrow.gameObject);
        ResetHint();
        var holyArrow = UnityEngine.Object.Instantiate(bow.arrowPrefabs[2]).GetComponent<ArrowDef>();
        float oldHealth = Get<float>(slimeBoss, "currentHealth"); Set(slimeBoss, "currentHealth", oldHealth - 10);
        Call(holyArrow, "ApplyHit", slimeBoss);
        Check(Get<float>(slimeBoss, "currentHealth") > oldHealth - 10 && text.text.Contains("лечат обычных"),
            "Holy-arrow healing of an ordinary enemy displays the opposite arrow rule"); Set(slimeBoss, "currentHealth", oldHealth);
        ResetHint(); TipsManager.ReportHolyArrowsSelected(); Check(text.text.Contains("лечат всех остальных"), "Selecting holy arrows explains their healing restriction");
        Set(tips, "combatHintRemaining", 0f); Call(tips, "TickCombatHints");
        Check(!hint.gameObject.activeSelf, "Combat hint expires without a lingering overlay");
        DamageNumbers.SetEnabled(numbersEnabled);
        Enemies(red).Remove(actor); UnityEngine.Object.Destroy(actor); UnityEngine.Object.Destroy(holyArrow.gameObject);

        void ResetHint()
        {
            Set(tips, "nextCombatHint", 0f); Set(tips, "combatHintRemaining", 0f);
            Array.Clear(Get<float[]>(tips, "nextCombatHintKind"), 0, 4);
        }
    }
    static void CheckPairedHud()
    {
        var first = Get<GameObject>(red, "bossHPBar10").GetComponent<RectTransform>();
        var second = Get<GameObject>(red, "bossHPBar15").GetComponent<RectTransform>();
        foreach (var panel in new[] { first, second })
        {
            Check(panel.anchorMin.y == 0 && panel.anchorMax.y == 0 && panel.pivot.y == 0, "Paired boss health is bottom anchored: " + panel.name);
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            var canvas = panel.GetComponentInParent<Canvas>();
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float bottom = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]).y;
            float top = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[1]).y;
            Check(bottom >= Screen.safeArea.yMin && top < Screen.height * .25f, "Paired boss health stays near the safe bottom: " + panel.name);
            foreach (var label in panel.GetComponentsInChildren<TMPro.TMP_Text>())
            { label.ForceMeshUpdate(); Check(!label.isTextOverflowing && !label.raycastTarget, "Paired boss label fits without blocking input: " + label.text); }
        }
        Check(second.anchoredPosition.y >= first.anchoredPosition.y + first.rect.height * first.localScale.y, "The two bottom boss health panels never overlap");
    }
    static void Capture(string name, Vector3 point, float size)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        foreach (var virtualCamera in UnityEngine.Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>()) virtualCamera.enabled = false;
        var camera = Camera.main; camera.transform.position = point; camera.orthographicSize = size;
        typeof(LocalizationChecks).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { name });
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.GetFullPath("../crimson-v19-checks.txt"), (success ? "PASS" : "FAIL") + "\n" + message + "\n" + string.Join("\n", checks));
        Debug.Log((success ? "CRIMSON_V19_PASS: " : "CRIMSON_V19_FAIL: ") + message); EditorApplication.Exit(success ? 0 : 1);
    }
}
