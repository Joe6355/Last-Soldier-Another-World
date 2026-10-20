using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YG;
using Prefs = RedefineYG.PlayerPrefs;

[InitializeOnLoad]
public static class ArenaDemoChecks
{
    const string Flag = "LastSoldier.ArenaDemoChecks";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<string> checks = new List<string>();
    static int phase, frame, coins, arrows, waves, bosses, kills, deaths, normalWave, normalRevision;
    static double deadline;
    static float wake, savedHealth;
    static WaveSpawner arena;
    static PlayerController player;
    static Stats stats;
    static Ui ui;
    static ShaulaBoss shaula;
    static SavesYG original;
    static string originalJson;
    static GameObject picker;
    static ArenaDemoChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run()
    {
        PlayerSettings.companyName = "LastSoldierArenaDemoValidation"; PlayerSettings.productName = "ArenaDemoValidation";
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        SessionState.SetBool(Flag, true); Attach(); EditorApplication.EnterPlaymode();
    }
    static void Attach()
    {
        deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Finish(false, message + "\n" + stack); }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout at phase " + phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount < frame || Time.time < wake) return;
        try
        {
            YG2.infoYG.Simulation.pauseOnFocusGame = false;
            if (YG2.isPauseGame) { YG2.PauseGame(false); return; }
            switch (phase)
            {
                case 0: UnityEngine.Object.FindObjectOfType<MainMenuController>().Play(); Next(.2f, 15); break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    BindScene(); foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide(); ui.StartGame();
                    Check(GameProgress.DemoToolsAvailable, "Editor test tools are available");
                    Check(!PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL).Split(';').Contains("LAST_SOLDIER_DEMO"), "Public WebGL excludes the demo define while Editor tools remain available");
                    Check(!arena.OpenDemoPicker(), "The wave picker cannot open outside an active arena");
                    player.transform.position = arena.GetComponent<Collider2D>().bounds.center + new Vector3(-5, -2, 0);
                    Physics2D.SyncTransforms(); Next(.15f, 4); break;
                case 2:
                    Check(arena.IsRunning, "Entering the arena starts its ordinary run");
                    Set(player, "invulnerableUntil", Time.time + 10000);
                    normalWave = arena.CurrentWaveNumber;
                    coins = player.totalCoins; arrows = player.crossbowController.GetArrowCount(0); savedHealth = player.hp;
                    waves = stats.completedWaves; bosses = stats.countBossDead; kills = stats.countEnemyDead; deaths = stats.countPlayerDead;
                    Check(arena.OpenDemoPicker(), "Wave picker opens during combat");
                    Check(Time.timeScale == 0 && WaveSpawner.BlocksDemoInput && !ui.IsMenuOpen, "Picker pauses combat and owns Escape without opening the pause menu");
                    picker = Get<GameObject>(arena, "demoPicker");
                    Capture("arena-demo-v22-picker-ru.png"); CheckLabelsFit();
                    Check(!arena.TryStartDemoWave(0) && !arena.TryStartDemoWave(arena.DemoWaveLimit + 1), "Out-of-range waves cannot start or alter the run");
                    var field = Get<TMP_InputField>(arena, "demoWaveInput"); field.SetTextWithoutNotify("9999"); Call(arena, "StartDemoFromInput");
                    Check(arena.IsDemoPickerOpen && !GameProgress.IsArenaDemo && arena.CurrentWaveNumber == normalWave, "Invalid text keeps the picker open and normal progress intact");
                    Button("Shaula").onClick.Invoke();
                    Check(GameProgress.IsArenaDemo && !arena.IsDemoPickerOpen && arena.CurrentWaveNumber == 20, "The Shaula shortcut starts test wave 20");
                    original = (SavesYG)typeof(GameProgress).GetField("demoSnapshot", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    originalJson = JsonUtility.ToJson(original); normalRevision = original.idSave;
                    Check(original != null && !ReferenceEquals(original, YG2.saves), "The test has a separate save object");
                    Check(Prefs.GetInt("ArenaWaveIndex", -1) == normalWave - 1, "Test wave selection never becomes the saved normal checkpoint");
                    Time.timeScale = 2; Next(1.4f, 4); break;
                case 3:
                    shaula = arena.Shaula;
                    Check(shaula != null && shaula.IsAlive && arena.ActiveEnemyCount == 0, "Selection clears the previous pack and spawns the real boss prefab");
                    player.crossbowController.AddArrows(0, -1); player.hp -= 7; player.SavePlayerData(); player.crossbowController.SaveArrowCounts();
                    Prefs.SetInt("ArenaDemoValidationProbe", 1234);
                    int revision = YG2.saves.idSave; Prefs.Save(); YG2.SaveProgress(); GameProgress.SaveNow();
                    Check(YG2.saves.idSave == revision && original.idSave == normalRevision, "All direct and coordinated save paths reject the demo data");
                    Check(JsonUtility.ToJson(original) == originalJson, "Consumed supplies and health never mutate the original save");
                    stats.AddEnemyKill(); stats.AddBossDead(); stats.AddCompletedWave(); stats.AddPlayerDead(); stats.RecordAchievementEvent(Stats.AchievementMetric.Slimes);
                    player.AddCoin(50000);
                    Check(stats.completedWaves == waves && stats.countBossDead == bosses && stats.countEnemyDead == kills && stats.countPlayerDead == deaths, "Kills, deaths, achievements and completed waves are not credited in tests");
                    Check(player.totalCoins == coins, "Test rewards cannot add coins");
                    var coin = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadAssetAtPath<GameObject>).First(prefab => prefab.GetComponent<Coin>() != null);
                    Check(WaveSpawner.SpawnEnemyLoot(coin, player.transform.position, Quaternion.identity) == null, "Demo kills do not spawn collectible loot");
                    GameProgress.BeginAuthorization(); Check(!GameProgress.AuthPending, "Authorization cannot change the owner of a demo save");
                    shaula.TakeDamage(1000000); Next(3.1f); break;
                case 4:
                    Check(!arena.IsCombatActive && arena.CurrentWaveNumber == 20, "Defeating demo Shaula finishes the test without real progression");
                    Check(stats.completedWaves == waves && stats.countBossDead == bosses && player.totalCoins == coins, "Boss victory produces no real reward or rating credit");
                    Next(4); break;
                case 5:
                    Check(arena.CurrentWaveNumber == 20 && !arena.IsCombatActive, "A completed test waits for another selection instead of advancing waves");
                    Check(arena.OpenDemoPicker(), "Picker can reopen after the test ends");
                    Check(Get<Button>(arena, "demoExit").gameObject.activeSelf, "An explicit return-to-normal action is visible");
                    GameLocalization.SetSelection("en"); GameLocalization.RefreshAll(); CheckLabelsFit(); Capture("arena-demo-v22-picker-en.png");
                    GameLocalization.SetSelection("ru");
                    Get<TMP_InputField>(arena, "demoWaveInput").SetTextWithoutNotify("15"); Button("Start").onClick.Invoke();
                    Check(arena.CurrentWaveNumber == 15 && arena.Shaula == null, "Numeric selection replaces Shaula with wave 15");
                    Next(1.4f); break;
                case 6:
                    Check(Get<GameObject>(arena, "bossWave15").activeInHierarchy, "The existing assassin encounter still starts through the picker");
                    Check(arena.OpenDemoPicker(), "Picker can interrupt and replace an active test boss");
                    Check(arena.TryStartDemoWave(21), "Late ordinary waves can be selected");
                    Check(!Get<GameObject>(arena, "bossWave15").activeSelf, "Changing the wave deactivates the old boss and health bar");
                    Check(JsonUtility.ToJson(original) == originalJson, "Repeated selection leaves the original snapshot unchanged");
                    Next(.4f); break;
                case 7:
                    Check(arena.ActiveEnemyCount > 0, "An ordinary selected wave spawns its actual enemy roster");
                    Check(arena.OpenDemoPicker(), "Picker opens during the ordinary test wave");
                    Get<Button>(arena, "demoExit").onClick.Invoke();
                    Next(.05f, 10); break;
                case 8:
                    if (GameProgress.IsArenaDemo) return;
                    BindScene();
                    Check(ReferenceEquals(original, YG2.saves), "Exit restores the original save object");
                    Check(!Prefs.HasKey("ArenaDemoValidationProbe"), "Disposable save keys are discarded");
                    Check(player.totalCoins == coins && player.crossbowController.GetArrowCount(0) == arrows && Mathf.Abs(player.hp - savedHealth) < .01f, "Exit restores original coins, ammunition and health");
                    Check(stats.completedWaves == waves && stats.countBossDead == bosses && stats.countEnemyDead == kills && stats.countPlayerDead == deaths, "Exit restores all normal statistics");
                    Check(arena.CurrentWaveNumber == normalWave && arena.IsRunning, "Normal saved wave resumes after the scene reload");
                    Check(Time.timeScale > 0 && !WaveSpawner.BlocksDemoInput, "No stale picker or pause survives the reload");
                    Check(arena.TryStartDemoWave(20), "Another demo session can start after restoring normal play");
                    Next(.15f); break;
                case 9:
                    Check(GameProgress.IsArenaDemo, "Second demo session is isolated too");
                    GameMonetization.HandleDeath(player); Next(.05f, 10); break;
                case 10:
                    if (GameProgress.IsArenaDemo) return;
                    BindScene();
                    Check(!YG2.nowAdsShow && !player.IsAwaitingRevive, "Demo defeat returns to the normal run without a rewarded ad");
                    Check(player.totalCoins == coins && stats.countPlayerDead == deaths && player.crossbowController.GetArrowCount(0) == arrows, "Demo defeat does not penalize normal resources or statistics");
                    Finish(true, "Wave picker, Shaula shortcut, arbitrary waves, no rewards, save/cloud isolation, normal-run restoration and demo death passed."); break;
            }
        }
        catch (Exception error) { Finish(false, "Phase " + phase + ": " + error); }
    }
    static void BindScene()
    { player = UnityEngine.Object.FindObjectOfType<PlayerController>(); stats = UnityEngine.Object.FindObjectOfType<Stats>(); ui = UnityEngine.Object.FindObjectOfType<Ui>(); arena = WaveSpawner.FindArena(false); }
    static Button Button(string name) => picker.GetComponentsInChildren<Button>(true).First(button => button.name == name);
    static void CheckLabelsFit()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var label in picker.GetComponentsInChildren<TMP_Text>())
        { label.ForceMeshUpdate(); Check(!label.isTextOverflowing && label.textBounds.size.x <= label.rectTransform.rect.width + 4 && label.preferredHeight <= label.rectTransform.rect.height + 4, "Picker text fits: " + label.name + "/" + GameLocalization.Language); }
    }
    static void Capture(string file)
    { typeof(LocalizationChecks).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { file }); }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Fields).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Fields).SetValue(obj, value);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Fields).Invoke(obj, args);
    static void Next(float seconds, int frames = 2) { phase++; wake = Time.time + seconds; frame = Time.frameCount + frames; }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add(message); }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.GetFullPath("../arena-demo-v22-checks.txt"), (success ? "PASS" : "FAIL") + "\n" + message + "\n" + string.Join("\n", checks));
        Debug.Log((success ? "ARENA_DEMO_V22_PASS: " : "ARENA_DEMO_V22_FAIL: ") + message); EditorApplication.Exit(success ? 0 : 1);
    }
}
