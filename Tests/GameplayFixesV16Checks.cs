// Copy into the isolated validation project's Assets/Editor directory.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

[InitializeOnLoad]
public static class GameplayFixesV16Checks
{
    const string Flag = "LastSoldier.GameplayFixesV16Checks";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static int phase, afterFrame, sample;
    static double deadline;
    static float began, expectedHealth, rewardDelay;
    static readonly List<string> checks = new List<string>();
    static PlayerController player;
    static CrossbowController bow;
    static WaveSpawner wave;
    static GameObject target;
    static ArrowDef[] pellets;
    static GameObject[] escort;
    static int coinsBefore, bossesBefore;
    static Stats stats;

    static GameplayFixesV16Checks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run()
    {
        PlayerSettings.companyName = "LastSoldierGameplayV16Validation";
        PlayerSettings.productName = "GameplayV16Validation";
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        SessionState.SetBool(Flag, true); Attach(); EditorApplication.EnterPlaymode();
    }
    static void Attach()
    {
        deadline = EditorApplication.timeSinceStartup + 150;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Finish(false, message + "\n" + stack);
    }
    static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, Private).GetValue(value);
    static void Set(object value, string field, object data) => value.GetType().GetField(field, Private).SetValue(value, data);
    static void Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, Private).Invoke(value, args);
    static void Check(bool ok, string label)
    {
        if (!ok) throw new InvalidOperationException(label);
        checks.Add(label); Debug.Log("GAMEPLAY_V16_CHECK: " + label);
    }
    static void Next(int next, float seconds = 0) { phase = next; began = Time.time; afterFrame = Time.frameCount + 2; }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout phase " + phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount < afterFrame) return;
        try
        {
            YG2.infoYG.Simulation.pauseOnFocusGame = false;
            if (YG2.isPauseGame) { YG2.PauseGame(false); return; }
            switch (phase)
            {
                case 0:
                    UnityEngine.Object.FindObjectOfType<MainMenuController>().Play(); Next(1); break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                    UnityEngine.Object.FindObjectOfType<Ui>().StartGame();
                    player = UnityEngine.Object.FindObjectOfType<PlayerController>();
                    bow = player.crossbowController;
                    wave = UnityEngine.Object.FindObjectOfType<WaveSpawner>();
                    stats = UnityEngine.Object.FindObjectOfType<Stats>();
                    player.transform.position = new Vector3(1000, 1000, player.transform.position.z);
                    player.GetComponent<Rigidbody2D>().position = player.transform.position;
                    Set(player, "invulnerableUntil", Time.time + 120);
                    bow.RestoreWeaponUpgrades(new[] { new Beka.UpgradeItem {
                        itemType = Beka.UpgradeItemType.Shotgun, purchaseCount = 1, maxPurchases = 1 } });
                    Set(bow, "arrowCounts", new[] { 100, 100, 100, 100 });
                    bow.canShoot = true;
                    Check(bow.TrySelectMode(2), "Real shotgun selected");
                    Next(2); break;
                case 2:
                    if (Time.time < Get<float>(bow, "nextShotTime")) return;
                    int type = sample < 4 ? sample : sample == 4 || sample == 7 ? 2 : sample == 5 ? 1 : 0;
                    bool holyTarget = sample == 2 || sample == 5 || sample == 7;
                    if (sample >= 6)
                    {
                        target = UnityEngine.Object.Instantiate(Get<GameObject>(wave, "regularSlimeTemplate"));
                        var boss = target.GetComponent<SlimeBoss>();
                        boss.ConfigureAsRegularEnemy(wave, 1, 1, 1, 1);
                        Set(boss, "maxHealth", 100f);
                        target.SetActive(true);
                    }
                    else
                    {
                        target = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/SlimeTrue.prefab"));
                        Set(target.GetComponent<Slime>(), "health", 100);
                        Set(target.GetComponent<Slime>(), "agrDist", 0f);
                    }
                    target.name = "ShotgunEffectTarget" + sample;
                    target.tag = holyTarget ? "HolyEnemy" : "Slime";
                    target.transform.position = player.transform.position + Vector3.down * 4;
                    var body = target.GetComponent<Rigidbody2D>();
                    if (body != null) { body.position = target.transform.position; body.constraints = RigidbodyConstraints2D.FreezeAll; }
                    foreach (var collider in target.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                    // Child collider exercises the parent-enemy lookup used by real animated enemies.
                    var hitbox = new GameObject("ChildHitbox"); hitbox.transform.SetParent(target.transform, false);
                    hitbox.AddComponent<BoxCollider2D>().size = new Vector2(5, .7f);
                    Check(bow.TrySelectArrowType(type), "Arrow selected for physics sample " + sample);
                    expectedHealth = holyTarget && type != 2 ? 100 : 100 + (sample == 4 ? 1 : -1) * 3 * bow.ArrowDamage(type)
                        - (sample == 1 ? 9 : 0);
                    Next(3); break;
                case 3:
                    int ammo = bow.GetArrowCount(bow.SelectedArrowIndex);
                    bow.Shoot(Vector2.down);
                    pellets = UnityEngine.Object.FindObjectsOfType<ArrowDef>();
                    Debug.Log("V16_PHYSICS_START: player=" + player.transform.position + "; target=" + target.transform.position
                        + "; pellets=" + string.Join(" | ", pellets.Select(p => p.transform.position + " velocity=" + p.GetComponent<Rigidbody2D>().velocity)));
                    Check(pellets.Length == 3 && bow.GetArrowCount(bow.SelectedArrowIndex) == ammo - 3,
                        "Three pellets and exactly three ammunition spent: sample " + sample);
                    foreach (var first in pellets)
                        foreach (var second in pellets.Where(item => item != first))
                            Check(Physics2D.GetIgnoreCollision(first.GetComponent<Collider2D>(), second.GetComponent<Collider2D>()),
                                "Pellets ignore one another: sample " + sample);
                    Next(4); break;
                case 4:
                    if (Time.time - began < 4f / bow.ArrowSpeed(bow.SelectedArrowIndex) + .4f
                        + (sample == 1 || sample == 5 ? 2.1f : 0)) return;
                    float health = sample >= 6 ? Get<float>(target.GetComponent<SlimeBoss>(), "currentHealth")
                        : Get<int>(target.GetComponent<Slime>(), "health");
                    Debug.Log("V16_PHYSICS_END: player=" + player.transform.position + "; target=" + target.transform.position
                        + "; pellets=" + string.Join(" | ", pellets.Select(p => p == null ? "destroyed" : p.transform.position + " velocity=" + p.GetComponent<Rigidbody2D>().velocity)));
                    Check(Mathf.Approximately(health, expectedHealth),
                        "Actual physics damage/effect sample " + sample + ": expected " + expectedHealth + ", got " + health);
                    foreach (var arrow in UnityEngine.Object.FindObjectsOfType<ArrowDef>()) UnityEngine.Object.Destroy(arrow.gameObject);
                    UnityEngine.Object.Destroy(target);
                    if (++sample < 8) Next(2); else Next(5);
                    break;
                case 5:
                    bow.canShoot = false;
                    var shop = UnityEngine.Object.FindObjectOfType<Shop>();
                    var trainer = Get<Beka>(shop, "upgradeShop");
                    Set(shop, "isPlayerInRange", true);
                    player.AddCoin(10000000); shop.OpenShop();
                    Check(shop.IsOpen && !AudioListener.pause, "Shop pauses combat while feedback audio remains enabled");
                    var audio = shop.audioSrc;
                    Check(audio.spatialBlend == 0, "Upgrade feedback uses the existing 2D shop source");
                    int purchased = 0;
                    for (int i = 0; i < trainer.upgradeItems.Length; i++)
                    {
                        var item = trainer.upgradeItems[i];
                        bool arena = (int)item.itemType >= (int)Beka.UpgradeItemType.TowerPower
                            && (int)item.itemType <= (int)Beka.UpgradeItemType.Altar
                            || item.itemType == Beka.UpgradeItemType.LootAttraction || item.itemType == Beka.UpgradeItemType.LootAttractionRange;
                        bool weapon = item.itemType >= Beka.UpgradeItemType.Shotgun && item.itemType <= Beka.UpgradeItemType.ReloadSpeed;
                        if (arena) shop.ShowArena();
                        else { shop.ShowUpgrades(); if (weapon) { trainer.ShowWeaponUpgrades(); trainer.SelectArrowUpgrades(item.arrowType); } }
                        if (trainer.GuideTarget(i) == null || !trainer.GuideTarget(i).gameObject.activeInHierarchy || item.IsMaxed
                            || !ArenaFeature.CanUpgrade(item.itemType, trainer.upgradeItems)) continue;
                        int level = item.purchaseCount, coins = player.totalCoins, price = item.CurrentPrice;
                        audio.Stop(); trainer.BuyItem(i);
                        Check(item.purchaseCount == level + 1 && player.totalCoins == coins - price,
                            "Actual upgrade purchase succeeds: " + item.itemType + "/" + item.arrowType);
                        Check(audio.isPlaying, "Successful upgrade plays 2D feedback: " + item.itemType + "/" + item.arrowType);
                        purchased++;
                    }
                    Check(purchased >= 22, "Hero, weapon and arena upgrades exercised: " + purchased);
                    shop.CloseShop();
                    wave.enabled = false;
                    Set(wave, "playerInsideZone", true); Set(wave, "currentWaveIndex", 9);
                    Call(wave, "StartWave", 9, false); Next(6); break;
                case 6:
                    if (!Get<bool>(wave, "bossActivated")) return;
                    escort = Get<List<GameObject>>(wave, "activeEnemies").ToArray();
                    Check(escort.Length == 6 && escort.All(e => e != null && e.activeInHierarchy && e.GetComponent<Slime>() != null && !e.CompareTag("HolyEnemy")),
                        "Dedicated slime boss appears with six ordinary tracked slimes");
                    Check(UnityEngine.Object.FindObjectsOfType<ParticleSystem>().Any(p => p.name == "SlimeSpawnSparkles" && p.particleCount > 0),
                        "Reinforcements have a visible green spawn burst");
                    Time.timeScale = 0; Call(wave, "SpawnSlimeBossEscort");
                    Check(wave.ActiveEnemyCount == 6, "No extra escorts spawn during pause"); Time.timeScale = 1;
                    coinsBefore = player.totalCoins; bossesBefore = stats.countBossDead;
                    var slimeBoss = Get<GameObject>(wave, "bossWave10").GetComponent<SlimeBoss>();
                    rewardDelay = Get<float>(slimeBoss, "deathAnimationTime");
                    slimeBoss.TakeDamage(1000000, false); slimeBoss.TakeDamage(1000000, false);
                    Check(player.totalCoins == coinsBefore, "Boss bonus keeps the existing delayed defeat timing");
                    Next(7); break;
                case 7:
                    if (Time.time - began < rewardDelay + .3f) return;
                    Check(player.totalCoins == coinsBefore + 250 && stats.countBossDead == bossesBefore + 1,
                        "Dedicated slime boss grants exactly 250 bonus coins and one boss kill");
                    Call(wave, "CompleteWave", true);
                    Check(wave.ActiveEnemyCount == 0, "Boss defeat clears escorts before the intermission");
                    Call(wave, "StartWave", 9, false); Next(8); break;
                case 8:
                    if (!Get<bool>(wave, "bossActivated")) return;
                    Check(escort.All(e => e == null), "Restarting the boss wave removes the previous escort");
                    Check(wave.ActiveEnemyCount == 6, "Restarted boss wave creates exactly one new escort");
                    wave.StopAllCoroutines(); Call(wave, "ClearActiveEnemies"); Call(wave, "HideDedicatedBosses");
                    Next(9); break;
                case 9:
                    Check(wave.ActiveEnemyCount == 0, "Arena cleanup clears the tracked reinforcement list");
                    Finish(true, "Shotgun effects, upgrade feedback, slime boss escort/reward and restart cleanup passed"); break;
            }
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.GetFullPath("../gameplay-v16-checks.txt"), (success ? "PASS" : "FAIL") + "\n" + message + "\n" + string.Join("\n", checks));
        Debug.Log((success ? "GAMEPLAY_V16_PASS: " : "GAMEPLAY_V16_FAIL: ") + message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
