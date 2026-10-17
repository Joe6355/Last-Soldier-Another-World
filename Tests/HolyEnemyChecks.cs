// Run only in the isolated validation project; saves use a separate product name.
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
public static class HolyEnemyChecks
{
    const string Flag = "LastSoldier.HolyEnemyChecks";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static int phase, afterFrame, arrowType;
    static double deadline, realBegan;
    static float began, beforeHp;
    static PlayerController player;
    static WaveSpawner wave;
    static MobileControls mobile;
    static Enemy orc, ghost;
    static GameObject projectile;
    static Vector3 orbPosition;
    static readonly List<MonoBehaviour> actors = new List<MonoBehaviour>();
    static readonly List<string> checks = new List<string>();
    static readonly HashSet<string> frames = new HashSet<string>();
    static readonly string[] EnemyPaths = { "SlimeTrue", "Bomber", "SceletSheld", "SceletMag", "HealerEnemy", "HolySlime", "HolyOrc", "HolyGhost" };
    static HolyEnemyChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run()
    {
        PlayerSettings.companyName = "LastSoldierHolyV17Checks"; PlayerSettings.productName = "HolyV17Checks";
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
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message); Debug.Log("HOLY_CHECK: " + message);
    }
    static void Next(int next) { phase = next; afterFrame = Time.frameCount + 2; began = Time.time; realBegan = EditorApplication.timeSinceStartup; frames.Clear(); }
    static int Health(MonoBehaviour actor)
    {
        if (actor is Enemy enemy) return enemy.health;
        if (actor is Roberto) return Mathf.RoundToInt(Get<float>(actor, "currentHP"));
        if (actor is SlimeBoss) return Mathf.RoundToInt(Get<float>(actor, "currentHealth"));
        return Get<int>(actor, "health");
    }
    static void SetHealth(MonoBehaviour actor, int health)
    {
        if (actor is Enemy enemy) enemy.health = health;
        else if (actor is Roberto) Set(actor, "currentHP", (float)health);
        else if (actor is SlimeBoss) Set(actor, "currentHealth", (float)health);
        else Set(actor, "health", health);
    }
    static void Sample(Enemy actor) { if (actor != null) frames.Add(actor.GetComponent<SpriteRenderer>().sprite.name); }
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
                    SpriteAtlasChecks.CheckLoadedScene();
                    UnityEngine.Object.FindObjectOfType<MainMenuController>().Play(); Next(1); break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                    UnityEngine.Object.FindObjectOfType<Ui>().StartGame();
                    player = UnityEngine.Object.FindObjectOfType<PlayerController>();
                    wave = WaveSpawner.FindArena(false);
                    mobile = UnityEngine.Object.FindObjectOfType<MobileControls>();
                    wave.enabled = false;
                    player.transform.position = new Vector3(1000, 1000, player.transform.position.z);
                    player.GetComponent<Rigidbody2D>().position = player.transform.position;
                    player.crossbowController.canShoot = false;
                    CheckWaveRosters(); CheckSpriteImports();
                    foreach (string name in EnemyPaths)
                    {
                        var obj = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/" + name + ".prefab"));
                        obj.name = "HolyMatrix" + name; obj.transform.position = player.transform.position + new Vector3(10 + actors.Count * 3, 20, 0);
                        foreach (var collider in obj.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                        var actor = ArrowDef.FindEnemy(obj.transform); actors.Add(actor);
                        if (actor is Enemy melee) melee.agrDistantion = 0;
                        else if (actor is Slime) Set(actor, "agrDist", 0f);
                        else if (actor is SceletonDef skeleton) skeleton.agrDist = 0;
                        else if (actor is SceletMag) Set(actor, "agrDist", 0f);
                    }
                    foreach (string field in new[] { "regularSlimeTemplate", "regularAssassinTemplate" })
                    {
                        var obj = UnityEngine.Object.Instantiate(Get<GameObject>(wave, field));
                        obj.transform.position = player.transform.position + new Vector3(40 + actors.Count, 20, 0);
                        if (obj.TryGetComponent<SlimeBoss>(out var boss)) { boss.ConfigureAsRegularEnemy(wave, 1, 1, 1, 1); Set(boss, "maxHealth", 100f); }
                        else obj.GetComponent<Roberto>().ConfigureAsRegularEnemy(wave, 1, 1, 1, 1);
                        obj.SetActive(true); foreach (var collider in obj.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                        actors.Add(ArrowDef.FindEnemy(obj.transform));
                    }
                    Next(2); break;
                case 2:
                    foreach (var actor in actors)
                    {
                        actor.StopAllCoroutines(); actor.CancelInvoke(); SetHealth(actor, 90);
                        var arrow = UnityEngine.Object.Instantiate(player.crossbowController.arrowPrefabs[arrowType], Vector3.one * 2000, Quaternion.identity).GetComponent<ArrowDef>();
                        arrow.damage = 2;
                        Check(arrow.IsHoly == (arrowType == 2), "Holy classification for ammunition " + arrowType);
                        typeof(ArrowDef).GetMethod("ApplyHit", Private).Invoke(arrow, new object[] { actor });
                        bool holy = actor.CompareTag("HolyEnemy");
                        int expected = holy && arrowType != 2 ? 90 : arrowType == 2 && !holy ? 92 : 88 - (arrowType == 1 && !(actor is Roberto) ? 1 : 0);
                        Check(Health(actor) == expected, "Hit rule " + actor.name + "/type " + arrowType + ": " + expected);
                        UnityEngine.Object.Destroy(arrow.gameObject);
                    }
                    Next(3); break;
                case 3:
                    if (arrowType == 1 && Time.time - began < 2.2f) return;
                    if (arrowType == 1)
                        foreach (var actor in actors)
                            Check(Health(actor) == (actor.CompareTag("HolyEnemy") ? 90 : actor is Roberto ? 88 : 85),
                                "Poison ticks respect immunity: " + actor.name);
                    if (++arrowType < 4) { Next(2); break; }
                    foreach (var actor in actors)
                    {
                        SetHealth(actor, 90);
                        ArrowDef.DamageEnemy(actor, 2, true);
                        Check(Health(actor) == (actor.CompareTag("HolyEnemy") ? 88 : 92),
                            "Holy damage/healing works with no arrow object in the scene: " + actor.name);
                    }
                    CheckMobileTargets();
                    foreach (var actor in actors) UnityEngine.Object.Destroy(actor.gameObject); actors.Clear();
                    orc = NewEnemy("HolyOrc", player.transform.position + Vector3.right * 3);
                    Next(4); break;
                case 4:
                    Sample(orc); if (Time.time - began < .8f) return;
                    Debug.Log("HOLY_ORC_WALK_DIAGNOSTIC: hp=" + player.hp + "; actor=" + orc.transform.position + "; player=" + player.transform.position
                        + "; frames=" + string.Join(",", frames) + "; style=" + orc.Style + "; canAct=" + typeof(Enemy).GetProperty("CanAct", Private).GetValue(orc));
                    Check(frames.Count(f => f.Contains("_Walk_")) == 4, "Orc plays all four walk frames while approaching");
                    Check(Vector2.Distance(player.transform.position, orc.transform.position) < 2.5f, "Orc moves toward player");
                    orc.agrDistantion = 0; Next(5); break;
                case 5:
                    Sample(orc); if (Time.time - began < .8f) return;
                    Check(frames.Count(f => f.Contains("_Idle_")) == 4, "Orc plays all four idle frames");
                    Place(orc, player.transform.position + Vector3.right * .7f); orc.agrDistantion = 1000;
                    Set(orc, "nextAttackTime", 0f); Set(player, "invulnerableUntil", 0f);
                    player.hp = player.maxHp; beforeHp = player.hp; Next(6); break;
                case 6:
                    Sample(orc); if (Time.time - began < .65f) return;
                    Check(frames.Count(f => f.Contains("_Attack_")) == 4, "Orc club swing uses all four attack frames");
                    Check(Mathf.Approximately(player.hp, beforeHp - orc.damageTouch), "Orc club deals one hit after its wind-up");
                    beforeHp = player.hp; Set(orc, "nextAttackTime", 0f); Next(7); break;
                case 7:
                    if (Time.time - began < .12f) return;
                    player.transform.position += Vector3.left * 3;
                    player.GetComponent<Rigidbody2D>().position = player.transform.position; Next(8); break;
                case 8:
                    if (Time.time - began < .55f) return;
                    Check(Mathf.Approximately(player.hp, beforeHp), "Leaving the club's range during wind-up avoids damage");
                    orc.TakeDamage(1000000, true); Next(9); break;
                case 9:
                    Sample(orc); if (Time.time - began < .54f) return;
                    Check(frames.Count(f => f.Contains("_Defeat_")) == 4, "Orc plays its four-frame magical disappearance");
                    ghost = NewEnemy("HolyGhost", player.transform.position + Vector3.up * 3);
                    Set(ghost, "nextAttackTime", Time.time + 10); Next(10); break;
                case 10:
                    Sample(ghost); if (Time.time - began < .8f) return;
                    Check(frames.Count(f => f.Contains("_Idle_")) == 4, "Ghost has a four-frame hover loop");
                    Place(ghost, player.transform.position + Vector3.up * 5); Next(11); break;
                case 11:
                    Sample(ghost); if (Time.time - began < .8f) return;
                    Check(frames.Count(f => f.Contains("_Walk_")) == 4, "Ghost has a four-frame gliding loop");
                    Place(ghost, player.transform.position + Vector3.up * 3); Set(ghost, "nextAttackTime", 0f); Next(12); break;
                case 12:
                    Sample(ghost); if (Time.time - began < .6f) return;
                    Check(frames.Count(f => f.Contains("_Attack_")) == 4, "Ghost cast uses all four attack frames");
                    projectile = Get<List<GameObject>>(ghost, "ownedProjectiles").Single();
                    Check(projectile != null && projectile.GetComponent<EnemyProgject>() != null && projectile.GetComponent<Collider2D>().isTrigger,
                        "Ghost launches the existing enemy projectile type");
                    Check(projectile.GetComponent<SpriteRenderer>().sprite.name == "HolyGhost_CastOrb", "Projectile uses the orb slice, not the whole character");
                    // Leave travel margin for pause/resume checks during batch-mode rendering.
                    projectile.GetComponent<EnemyProgject>().speed = .2f;
                    orbPosition = projectile.transform.position; Time.timeScale = 0; Next(13); break;
                case 13:
                    if (EditorApplication.timeSinceStartup - realBegan < .2) return;
                    Check(projectile.transform.position == orbPosition, "Ghost projectile stops during pause");
                    Time.timeScale = 1; Next(14); break;
                case 14:
                    if (Time.time - began < .15f) return;
                    Check(projectile.transform.position != orbPosition, "Ghost projectile resumes after pause");
                    ghost.TakeDamage(1000000, true); Next(15); break;
                case 15:
                    Sample(ghost); if (Time.time - began < .54f) return;
                    Check(frames.Count(f => f.Contains("_Defeat_")) == 4, "Ghost disappears through all four defeat frames");
                    Check(projectile == null, "Ghost clears its owned projectile on defeat");
                    Check(orc == null, "Defeated orc is removed after its animation");
                    player.TeleportPlayerHome();
                    var camDelta = player.transform.position - new Vector3(997, 1000, player.transform.position.z);
                    foreach (var cam in UnityEngine.Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>())
                    { cam.OnTargetObjectWarped(player.transform, camDelta); cam.PreviousStateIsValid = false; }
                    orc = NewEnemy("HolyOrc", player.transform.position + new Vector3(-1.5f, -1, 0)); orc.agrDistantion = 0;
                    ghost = NewEnemy("HolyGhost", player.transform.position + new Vector3(1.5f, -1, 0)); ghost.agrDistantion = 0;
                    Next(16); break;
                case 16:
                    if (Time.time - began < .5f) return;
                    SpriteAtlasChecks.CheckLoadedScene();
                    typeof(LocalizationChecks).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "holy-v17-camp.png" });
                    Finish(true, "Holy immunity/healing, all enemy classes, mobile targeting, animations, combat, pause and diverse wave rosters passed"); break;
            }
        }
        catch (Exception ex) { Finish(false, ex.ToString()); }
    }
    static Enemy NewEnemy(string name, Vector3 point)
    {
        var obj = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/" + name + ".prefab"));
        var enemy = obj.GetComponent<Enemy>(); Place(enemy, point); return enemy;
    }
    static void Place(Enemy enemy, Vector3 point)
    {
        enemy.transform.position = point; enemy.GetComponent<Rigidbody2D>().position = point;
    }
    static void CheckMobileTargets()
    {
        var method = typeof(MobileControls).GetMethod("IsAimCandidate", Private);
        Set(mobile, "gameCamera", null);
        for (int type = 0; type < 4; type++)
        {
            Check(player.crossbowController.TrySelectArrowType(type), "Mobile ammunition selection " + type);
            foreach (var actor in actors.Take(8))
            {
                var collider = actor.GetComponent<Collider2D>(); collider.enabled = true;
                object[] arguments = { collider, (Vector2)player.transform.position, 100f, null, Vector2.zero };
                bool candidate = (bool)method.Invoke(mobile, arguments);
                Check(candidate == (actor.CompareTag("HolyEnemy") == (type == 2)), "Auto-aim skips incompatible targets: " + actor.name + "/" + type);
                collider.enabled = false;
            }
        }
    }
    static void CheckWaveRosters()
    {
        var prefabs = Get<List<GameObject>>(wave, "enemyPrefabs"); var configs = Get<List<WaveConfig>>(wave, "waves");
        Check(prefabs.Count == 9 && prefabs[7].name == "HolyOrc" && prefabs[8].name == "HolyGhost", "New enemies extend the existing roster without replacing old prefabs");
        var select = typeof(WaveSpawner).GetMethod("SelectEnemyIndex", Private);
        var bag = Get<List<int>>(wave, "spawnBag");
        for (int i = 0; i < configs.Count; i++)
        {
            Set(wave, "currentWaveIndex", i); bag.Clear();
            if (i < 4 || i == 9 || i == 14) { Check(configs[i].enemyTypes.Length == 0, "Early/boss wave path preserved: " + (i + 1)); continue; }
            var indices = configs[i].enemyTypes;
            Check(indices.Contains(7) && indices.Contains(8) && indices.Any(x => x >= 3 && x <= 6), "Mixed wave includes new holy enemies and skeletons: " + (i + 1));
            var firstBag = Enumerable.Range(0, indices.Length).Select(_ => (int)select.Invoke(wave, new object[] { configs[i] })).ToArray();
            Check(firstBag.Distinct().Count() == indices.Length && firstBag.All(indices.Contains), "All species occur once per shuffled bag: " + (i + 1));
            Check(configs[i].maxEnemies >= firstBag.Length || i == 7, "Wave quota permits a varied roster: " + (i + 1));
        }
        Set(wave, "currentWaveIndex", 0); bag.Clear();
    }
    static void CheckSpriteImports()
    {
        foreach (string name in new[] { "HolyOrc", "HolyGhost" })
        {
            string path = "Assets/Sprits/Holy/" + name + ".png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Check(sprites.Length == (name == "HolyGhost" ? 17 : 16), "All generated sprites are sliced: " + name);
            var sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Check(sourceTexture.width == 128 && sourceTexture.filterMode == FilterMode.Point, "Small point-filtered source texture before atlas packing: " + name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/" + name + ".prefab");
            Check(prefab.CompareTag("HolyEnemy") && prefab.GetComponent<Enemy>() != null, "New enemy reuses existing health and reward system: " + name);
            Check(prefab.GetComponent<SpriteRenderer>().sprite.bounds.size.x <= 1.01f, "New enemy matches the scene's character scale: " + name);
        }
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.GetFullPath("../holy-v17-checks.txt"), (success ? "PASS" : "FAIL") + "\n" + message + "\n" + string.Join("\n", checks));
        Debug.Log((success ? "HOLY_PASS: " : "HOLY_FAIL: ") + message); EditorApplication.Exit(success ? 0 : 1);
    }
}
