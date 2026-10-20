using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Real scene, physics and coroutines; a separate product name protects the user's save.
[InitializeOnLoad]
public static class ShaulaChecks
{
    const string Flag = "LastSoldier.ShaulaChecks";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<string> checks = new List<string>();
    static int phase, frame, sample, bossesBefore, wavesBefore, coinsBefore, potionsBefore, arrowsBefore;
    static double deadline;
    static double pauseDeadline;
    static float wake, healthBefore, tailBefore, poseBefore, clockBefore;
    static Vector3 arenaCenter;
    static Vector2 aimBefore;
    static WaveSpawner wave;
    static ShaulaBoss boss;
    static ShaulaHitZone tail, shell, joint;
    static PlayerController player;
    static Ui ui;
    static Stats stats;
    static LineRenderer warning;
    static GameObject physicsArrow;
    static int arrowSample;
    static float musicTime;
    static AudioSource musicChannel;
    static float originalSfxVolume;
    static float gaitBefore;
    static Vector3 legBefore;
    static readonly List<Vector2> stingPoints = new List<Vector2>();
    static Vector2 stingStart;
    static ShaulaChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }

    public static void Run()
    {
        PlayerSettings.companyName = "LastSoldierShaulaValidation";
        PlayerSettings.productName = "ShaulaValidation";
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        SessionState.SetBool(Flag, true); Attach(); EditorApplication.EnterPlaymode();
    }
    static void Attach()
    {
        deadline = EditorApplication.timeSinceStartup + 420;
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
            YG.YG2.infoYG.Simulation.pauseOnFocusGame = false;
            if (YG.YG2.isPauseGame) { YG.YG2.PauseGame(false); return; }
            switch (phase)
            {
                case 0:
                    UnityEngine.Object.FindObjectOfType<MainMenuController>().Play(); Next(.1f, 15); break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    player = UnityEngine.Object.FindObjectOfType<PlayerController>(); stats = UnityEngine.Object.FindObjectOfType<Stats>();
                    ui = UnityEngine.Object.FindObjectOfType<Ui>(); wave = WaveSpawner.FindArena(false);
                    foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                    ui.StartGame();
                    foreach (var camera in UnityEngine.Object.FindObjectsOfType<Cinemachine.CinemachineVirtualCamera>()) camera.enabled = false;
                    arenaCenter = wave.GetComponent<Collider2D>().bounds.center;
                    player.transform.position = arenaCenter + new Vector3(-5, -2, 0);
                    player.hp = 10000; Set(player, "invulnerableUntil", Time.time + 10000);
                    Set(wave, "playerInsideZone", true); Set(wave, "currentWaveIndex", 19);
                    typeof(WaveSpawner).GetProperty("ActiveArena").SetValue(null, wave);
                    Call(wave, "ShowSpawnerUI");
                    Call(wave, "StartWave", 19, false);
                    Time.timeScale = 2; Next(1.4f, 10); break;
                case 2:
                    boss = wave.Shaula;
                    Check(boss != null && boss.IsAlive && boss.gameObject.activeInHierarchy, "Wave 20 spawns live Shaula");
                    Near(boss.MaximumHealth, 1400, "Wave 20 Shaula has 1400 HP, up from 900 by exactly 500");
                    DamageNumbers.SetEnabled(true);
                    Check(wave.CurrentWaveNumber == 20 && wave.ActiveEnemyCount == 0, "Wave 20 follows the dedicated encounter path");
                    Check(!Get<GameObject>(wave, "bossWave10").activeSelf && !Get<GameObject>(wave, "bossWave15").activeSelf, "Earlier bosses remain dormant");
                    Check(Get<ShaulaBoss>(WaveSpawner.FindArena(true), "shaulaPrefab") == null, "Red paired waves are unchanged");
                    Check(Get<GameObject>(wave, "shaulaHealthPanel").activeSelf, "Named bottom health panel is visible");
                    Check(Get<bool>(boss, "rigReady"), "The articulated presentation starts with the encounter");
                    var parts = Get<Sprite[]>(boss, "rigParts");
                    Check(parts.Length == 9 && parts.All(s => s != null) && parts.Distinct().Count() == 9, "Nine distinct measured sprite parts are assigned");
                    Check(parts.Select(p => p.texture).Distinct().Count() == 1, "All parts share one texture for consistent sampling");
                    Check(Get<SpriteRenderer[,]>(boss, "legParts").GetLength(0) == 8, "All eight legs have two independently animated segments");
                    Check(Get<SpriteRenderer[]>(boss, "tailSegments").Length == 5, "The tail has five articulated plates");
                    Check(Get<ShaulaBoss.Motion[]>(boss, "motions").Length == 0, "The broken legacy frame sheets are not referenced by the prefab");
                    Check(Get<SpriteRenderer>(boss, "bodySprite").sharedMaterial.shader.name == "LastSoldier/ShaulaCarapace", "The boss uses its transparent, palette-aware sprite shader");
                    Check(Get<SpriteRenderer>(boss, "bodySprite").sharedMaterial.shader.isSupported, "The carapace shader is supported");
                    Check(Get<AudioClip[]>(boss, "effects").Length == 17, "All seventeen original sound effects are connected");
                    Check(Get<AudioClip>(boss, "musicNormal") != null && Get<AudioClip>(boss, "musicCrimson") != null, "Both music loops are connected");
                    Check(Get<SpriteRenderer>(boss, "bodySprite").sortingOrder < player.GetComponent<SpriteRenderer>().sortingOrder,
                        "The giant boss never hides the player sprite during close combat");
                    var heroSprite = player.GetComponent<SpriteRenderer>();
                    float heroHeight = heroSprite.sprite.bounds.size.y * Mathf.Abs(heroSprite.transform.lossyScale.y);
                    var bounds = new Bounds(boss.transform.position, Vector3.zero);
                    foreach (var part in boss.GetComponentsInChildren<SpriteRenderer>()) if (part.enabled) bounds.Encapsulate(part.bounds);
                    Check(Mathf.Max(bounds.size.x, bounds.size.y) / heroHeight > 5.5f, "The assembled adult silhouette retains its giant size");
                    Check(boss.GetComponentsInChildren<SpriteRenderer>().All(r => r.sortingOrder < heroSprite.sortingOrder), "Every limb stays behind the hero during overlap");
                    var hudRect = Get<GameObject>(wave, "shaulaHealthPanel").GetComponent<RectTransform>();
                    Check(hudRect.anchorMin.y == 0 && hudRect.anchorMax.y == 0 && hudRect.anchoredPosition.y > 0, "Boss health is anchored inside the bottom of the screen");
                    Check(hudRect.anchoredPosition.y < 30 && hudRect.sizeDelta.y == 76, "Compact boss HUD sits close to the safe bottom edge");
                    Check(Get<TMPro.TMP_Text>(wave, "shaulaState") == null && hudRect.Find("State") == null, "The weak-point explanation is absent from the HUD");
                    Check(!Get<TMPro.TextMeshProUGUI>(wave, "arenaHint").gameObject.activeSelf, "The outpost banner is hidden during the boss wave");
                    StopBoss();
                    var zones = boss.GetComponentsInChildren<ShaulaHitZone>();
                    tail = zones.First(z => z.zone == ShaulaHitZone.Zone.Tail); shell = zones.First(z => z.zone == ShaulaHitZone.Zone.Shell); joint = zones.First(z => z.zone == ShaulaHitZone.Zone.Joint);
                    Check(zones.All(z => z.boss == boss && !z.CompareTag("HolyEnemy")), "All zones belong to the same ordinary (non-holy) boss");
                    Check(ArrowDef.FindEnemy(tail.GetComponent<Collider2D>()) == tail && ArrowDef.FindEnemy(boss.transform) == boss, "Collider lookup preserves weak zones and area damage target");
                    int a = (int)typeof(ArrowDef).GetMethod("EnemyHitId", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { tail });
                    int b = (int)typeof(ArrowDef).GetMethod("EnemyHitId", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { joint });
                    Check(a == b && a == boss.GetInstanceID(), "Piercing arrows cannot double-hit the same boss through multiple zones");
                    healthBefore = boss.Health; ArrowDef.DamageEnemy(shell, 20); Near(boss.Health, healthBefore - 12, "Shell reduces direct damage to sixty percent");
                    healthBefore = boss.Health; ArrowDef.DamageEnemy(joint, 20); Near(boss.Health, healthBefore - 27, "Pale joints take increased damage");
                    healthBefore = boss.Health; tailBefore = boss.TailHealth; ArrowDef.DamageEnemy(tail, 20);
                    Near(boss.Health, healthBefore - 30, "Tail hit damages main health"); Near(boss.TailHealth, tailBefore - 30, "Tail durability is independently reduced");
                    healthBefore = boss.Health; ArrowDef.DamageEnemy(tail, 10, true); Near(boss.Health, healthBefore + 10, "Holy arrows heal Shaula instead of damaging her");
                    var healingNumbers = UnityEngine.Object.FindObjectOfType<DamageNumbers>();
                    Check(healingNumbers.GetComponentsInChildren<TMPro.TextMeshPro>().Any(t => t.text == "+10" && t.color.g > t.color.r),
                        "Holy healing through the tail displays a green positive health amount");
                    healthBefore = boss.Health; boss.ApplyPoison(4, 2, .08f); Next(.3f); break;
                case 3:
                    Near(boss.Health, healthBefore - 4.8f, "Poison ticks reach the new boss through the common damage rules");
                    arrowSample = 0; phase = 30; break;
                case 30:
                    StopBoss(); Physics2D.SyncTransforms(); healthBefore = boss.Health;
                    physicsArrow = UnityEngine.Object.Instantiate(player.crossbowController.arrowPrefabs[arrowSample], tail.transform.position + Vector3.up * 1.2f, Quaternion.identity);
                    var arrow = physicsArrow.GetComponent<ArrowDef>(); arrow.damage = 12; arrow.maxEnemyHits = 4; arrow.triggerProjectile = true;
                    foreach (var collider in physicsArrow.GetComponentsInChildren<Collider2D>()) collider.isTrigger = true;
                    var projectileBody = physicsArrow.GetComponent<Rigidbody2D>(); projectileBody.gravityScale = 0; projectileBody.velocity = Vector2.down * 8;
                    Next(.8f); break;
                case 31:
                    if (arrowSample == 0) Near(boss.Health, healthBefore - 18, "Physical piercing projectile damages the tail only once across overlapping zones");
                    else if (arrowSample == 1) Check(boss.Health < healthBefore - 18, "Physical poison arrow applies its impact and poison tick");
                    else Near(boss.Health, healthBefore + 12, "Physical holy arrow heals without losing the boss hit lookup");
                    if (physicsArrow != null) UnityEngine.Object.Destroy(physicsArrow);
                    if (++arrowSample < 3) { phase = 30; break; }
                    StopBoss(); PositionActors(); Call(boss, "SetPose", ShaulaBoss.Pose.Walk);
                    gaitBefore = Get<float>(boss, "gait"); legBefore = Get<SpriteRenderer[,]>(boss, "legParts")[0, 1].transform.localPosition;
                    boss.StartCoroutine((IEnumerator)Call(boss, "Wait", 1.15f, new Action<float>(_ => Call(boss, "MoveTowards", (Vector2)player.transform.position, 1.4f)), false));
                    sample = 0; phase = 40; wake = Time.time + .12f; break;
                case 40:
                    Capture("shaula-v24-walk-" + sample.ToString("00") + ".png");
                    if (++sample < 8) { wake = Time.time + .1f; break; }
                    Check(Get<float>(boss, "gait") > gaitBefore + .3f, "Walking cadence advances with distance instead of restarting on AI decisions");
                    Check(Vector3.Distance(Get<SpriteRenderer[,]>(boss, "legParts")[0, 1].transform.localPosition, legBefore) > .03f, "Actual walking moves the feet relative to the shell");
                    StopBoss(); phase = 41; break;
                case 41:
                    healthBefore = boss.Health; poseBefore = Get<float>(boss, "poseTime"); clockBefore = Get<float>(boss, "fightClock");
                    musicChannel = Get<AudioSource[]>(ui, "encounterMusic").First(source => source.isPlaying);
                    musicTime = musicChannel.time;
                    Call(ui, "PauseGame", true); boss.TakeDamage(100);
                    originalSfxVolume = Ui.sfxVolume; ui.SetSFXVolume(0);
                    pauseDeadline = EditorApplication.timeSinceStartup + .6;
                    Near(boss.Health, healthBefore, "Damage is blocked while paused");
                    phase = 4; frame = Time.frameCount + 8; wake = 0; break;
                case 4:
                    if (EditorApplication.timeSinceStartup < pauseDeadline) return;
                    Near(Get<float>(boss, "poseTime"), poseBefore, "Animation freezes during pause");
                    Near(Get<float>(boss, "fightClock"), clockBefore, "Ability clock freezes during pause");
                    Check(musicChannel.isPlaying && musicChannel.time > musicTime, "Shop-style pause keeps the current boss music playing continuously (playing="
                        + musicChannel.isPlaying + ", time=" + musicChannel.time + "/" + musicTime + ", listenerPaused=" + AudioListener.pause + ")");
                    Check(boss.audioSrc.mute, "Muting effects also silences an already playing boss cue");
                    ui.SetSFXVolume(originalSfxVolume);
                    Call(ui, "ResumeGame");
                    Time.timeScale = 2; Set(boss, "health", boss.MaximumHealth * .5f); boss.TakeDamage(1);
                    boss.StartCoroutine((IEnumerator)Call(boss, "Encounter")); Next(1.3f); break;
                case 5:
                    Check(boss.IsCrimson, "Crossing fifty percent starts the permanent crimson phase");
                    StopBoss(); boss.TakeDamage(10000, true); Check(boss.IsCrimson && boss.Health == boss.MaximumHealth, "Healing cannot revert the second phase");
                    Check(ArrowDef.CanDamageEnemy(tail, false) && !ArrowDef.CanDamageEnemy(tail, true), "Crimson armor never changes the holy classification");
                    ArrowDef.DamageEnemy(tail, 100);
                    Check(!boss.TailIntact && !tail.GetComponent<Collider2D>().enabled, "Enough tail damage removes its hit zone");
                    for (int i = 0; i < 15; i++)
                    {
                        var selected = (ShaulaBoss.Ability?)Call(boss, "ChooseAbility", i % 2 == 0 ? 8f : 2f);
                        Check(!selected.HasValue || !(bool)typeof(ShaulaBoss).GetMethod("UsesTail", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { selected.Value }), "Broken tail excludes every ranged/tail action");
                    }
                    StopBoss(); boss.StartCoroutine((IEnumerator)Call(boss, "Perform", ShaulaBoss.Ability.Laser));
                    Check(boss.ActiveEffectCount == 0, "A broken tail cannot start a laser warning or projectile");
                    boss.StartCoroutine((IEnumerator)Call(boss, "RegrowTail")); Next(1.1f); break;
                case 6:
                    Check(!boss.TailIntact, "Regeneration remains warned before the restoration pulse"); Next(.85f); break;
                case 7:
                    Check(boss.TailIntact && tail.GetComponent<Collider2D>().enabled && boss.TailHealth > 0, "Regeneration restores durability and the tail hit zone");
                    StopBoss(); Set(boss, "crimson", false); Set(boss, "ragePending", false); Call(boss, "RefreshHud");
                    player.hp = player.maxHp;
                    PositionActors();
                    Time.timeScale = .5f; boss.StartCoroutine((IEnumerator)Call(boss, "Perform", ShaulaBoss.Ability.Laser)); Next(.82f); break;
                case 8:
                    warning = Effects().Select(go => go.GetComponent<LineRenderer>()).First(l => l != null && l.name == "Shaula_LaneWarning");
                    aimBefore = ((warning.GetPosition(1) + warning.GetPosition(2)) - (warning.GetPosition(0) + warning.GetPosition(3))).normalized;
                    Check(warning.GetComponentInChildren<MeshFilter>() != null, "Laser warning shows its complete filled damage lane");
                    Check(!Effects().Any(go => go.name == "Shaula_LaserCore"), "Laser does not fire before its 1.1-second warning");
                    Capture("shaula-v24-laser-warning.png");
                    player.transform.position += Vector3.up * 1.5f; Next(.18f); break;
                case 9:
                    Vector2 current = ((warning.GetPosition(1) + warning.GetPosition(2)) - (warning.GetPosition(0) + warning.GetPosition(3))).normalized;
                    Check(Vector2.Dot(current, aimBefore) > .999f, "Laser stops tracking for the final dodge window");
                    Next(.12f, 1); break;
                case 10:
                    var beam = Effects().Select(go => go.GetComponent<LineRenderer>()).FirstOrDefault(l => l != null && l.name == "Shaula_LaserCore");
                    Check(beam != null && beam.startColor.r > .95f && beam.startColor.g > .95f && beam.startColor.b > .95f, "Laser fires a white core with a visible flash");
                    Check(Effects().Any(go => go.name == "Shaula_MuzzleFlash"), "The discharge has a distinct muzzle flash");
                    Check(Get<float>(boss, "recoil") > .1f, "The tail and shell visibly recoil on the laser shot");
                    Capture("shaula-v24-laser.png"); Next(.65f); break;
                case 11:
                    Check(!Effects().Any(go => go.name == "Shaula_LaserCore"), "Laser beam is brief and leaves no damage volume behind");
                    StopBoss(); Time.timeScale = .25f; PositionActors(); stingStart = tail.transform.position;
                    stingPoints.Clear(); sample = 0;
                    boss.StartCoroutine((IEnumerator)Call(boss, "Perform", ShaulaBoss.Ability.Sting));
                    phase = 51; wake = Time.time + .48f; frame = Time.frameCount + 1; break;
                case 51:
                    stingPoints.Add(tail.transform.position);
                    Capture("shaula-v24-sting-motion-" + sample.ToString("00") + ".png");
                    if (++sample < 8) { wake = Time.time + .018f; frame = Time.frameCount + 1; break; }
                    Check(stingPoints.Select(p => Mathf.RoundToInt(p.x * 100) + ":" + Mathf.RoundToInt(p.y * 100)).Distinct().Count() >= 5,
                        "Tail strike renders intermediate positions instead of teleporting to the endpoint");
                    Check(stingPoints.Zip(stingPoints.Skip(1), (a, b) => Vector2.Distance(a, b)).Max() < 3,
                        "Tail strike has no full-distance jump between consecutive samples");
                    phase = 52; wake = Time.time + .08f; break;
                case 52:
                    Check(boss.TailExposed, "The animated missed strike still exposes the tail");
                    Check(Vector2.Distance(Get<SpriteRenderer>(boss, "stinger").transform.position, tail.transform.position) < .05f,
                        "Visible stinger and weak-point collider stay together at the end of the strike");
                    phase = 53; wake = Time.time + 1.3f; break;
                case 53:
                    Check(!Get<bool>(boss, "stingAnimating") && !boss.TailExposed, "The tail finishes its smooth return and releases the weak point");
                    Check(Vector2.Distance(tail.transform.position, stingStart) < .3f, "The returning tail reaches its resting position");
                    StopBoss(); Time.timeScale = 3; PositionActors();
                    Set(boss, "crimson", false); boss.StartCoroutine((IEnumerator)Call(boss, "Perform", ShaulaBoss.Ability.Stars));
                    var normalMarkers = Effects().Where(go => go.name == "Shaula_AreaWarning").ToArray();
                    Check(normalMarkers.Length == 8, "Normal sky barrage creates eight warned strikes");
                    foreach (var marker in normalMarkers)
                    {
                        var center = marker.GetComponentInChildren<MeshFilter>().sharedMesh.vertices[0];
                        Near(Vector2.Distance(marker.GetComponent<LineRenderer>().GetPosition(0), center), 1.15f, "Sky strike warning uses the larger damage radius");
                    }
                    phase = 55; wake = Time.time + .8f; break;
                case 55:
                    var skyBeam = Effects().Where(go => go.name == "Shaula_SkyLaserCore").Select(go => go.GetComponent<LineRenderer>()).FirstOrDefault();
                    Check(skyBeam != null && skyBeam.startWidth >= .22f && Vector3.Distance(skyBeam.GetPosition(0), skyBeam.GetPosition(1)) >= 5.9f,
                        "The actual sky laser is visibly wider and taller");
                    Capture("shaula-v24-sky-laser.png");
                    StopBoss(); Set(boss, "crimson", true);
                    boss.StartCoroutine((IEnumerator)Call(boss, "Perform", ShaulaBoss.Ability.Stars));
                    Check(Effects().Count(go => go.name == "Shaula_AreaWarning") == 16, "Crimson sky barrage creates sixteen warned strikes");
                    Check((float)boss.GetType().GetProperty("Tempo", Fields).GetValue(boss) == 1.55f, "Crimson attack tempo is increased");
                    Check((float)boss.GetType().GetProperty("MovementTempo", Fields).GetValue(boss) == 1.5f, "Crimson movement multiplier is increased");
                    Check((int)boss.GetType().GetProperty("NeedleCount", Fields).GetValue(boss) == 14, "Crimson volleys contain fourteen needles");
                    Check((int)boss.GetType().GetProperty("PursuitCount", Fields).GetValue(boss) == 4, "Crimson pursuit has four dashes");
                    StopBoss(); Set(boss, "crimson", false); PositionActors();
                    player.transform.position = boss.transform.position + Vector3.left * 2f;
                    player.hp = 10000; Set(player, "invulnerableUntil", 0f); Set(player, "shieldActive", false);
                    healthBefore = player.hp;
                    boss.StartCoroutine((IEnumerator)Call(boss, "Perform", ShaulaBoss.Ability.Grasp));
                    phase = 54; wake = Time.time + 1.4f; break;
                case 54:
                    Near(healthBefore - player.hp, Mathf.CeilToInt(36 * Get<float>(boss, "damageScale")), "Actual claw dash applies the increased melee damage exactly once");
                    Set(player, "invulnerableUntil", Time.time + 10000); player.hp = player.maxHp;
                    StopBoss(); Time.timeScale = 3;
                    foreach (var ability in Enum.GetValues(typeof(ShaulaBoss.Ability)).Cast<ShaulaBoss.Ability>())
                        Check(Call(boss, "Perform", ability) is IEnumerator, "Every configured ability has an executable coroutine: " + ability);
                    sample = 0; phase = 12; break;
                case 12:
                    StopBoss(); PositionActors();
                    boss.StartCoroutine((IEnumerator)Call(boss, "Perform", (ShaulaBoss.Ability)sample));
                    Next(sample == (int)ShaulaBoss.Ability.Sting ? .85f : .3f); break;
                case 13:
                    Check(boss.ActiveEffectCount > 0, "Attack has a real warning, strike or recovery effect: " + (ShaulaBoss.Ability)sample);
                    Capture("shaula-v24-attack-" + ((ShaulaBoss.Ability)sample).ToString().ToLowerInvariant() + ".png");
                    if (sample == (int)ShaulaBoss.Ability.Sting)
                    {
                        Check(boss.TailExposed, "A missed sting exposes the tail for a counterattack");
                        healthBefore = boss.Health; tailBefore = boss.TailHealth; ArrowDef.DamageEnemy(tail, 5);
                        Near(boss.Health, healthBefore - 10, "Stuck sting takes double health damage"); Near(boss.TailHealth, tailBefore - 10, "Stuck sting takes double durability damage");
                        var controls = UnityEngine.Object.FindObjectOfType<MobileControls>();
                        Set(controls, "autoAimTarget", shell.transform); Set(controls, "autoAimCollider", shell.GetComponent<Collider2D>());
                        Physics2D.SyncTransforms(); Call(controls, "AutoAimDirection", Vector2.left);
                        Check(Get<Transform>(controls, "autoAimTarget") == tail.transform, "Mobile auto-aim switches from shell to the exposed tail");
                    }
                    Next(5f); break;
                case 14:
                    Check(boss.IsAlive && !boss.TailExposed, "Attack terminates and releases any weak-point lock: " + (ShaulaBoss.Ability)sample);
                    if (++sample < 7) { phase = 12; break; }
                    StopBoss(); PositionActors(); player.hp = 10000; Set(player, "invulnerableUntil", 0f); Set(player, "shieldActive", false);
                    healthBefore = player.hp;
                    Call(boss, "DamageLine", (Vector2)player.transform.position - Vector2.right, (Vector2)player.transform.position + Vector2.right, .2f, 10, 0f);
                    Check(player.hp < healthBefore, "Native boss attack applies player damage through TakeDamage");
                    Set(player, "shieldActive", true); Set(player, "shieldValue", 100f); healthBefore = player.hp;
                    Call(boss, "DamageCircle", (Vector2)player.transform.position, 1f, 10, .8f);
                    Near(player.hp, healthBefore, "Shield still blocks boss attacks");
                    Check(Get<float>(player, "shieldValue") < 100, "Blocked boss attack consumes the existing shield resource");
                    Set(player, "shieldActive", false); Set(player, "invulnerableUntil", Time.time + 10000);
                    bossesBefore = stats.countBossDead; wavesBefore = stats.completedWaves;
                    coinsBefore = player.totalCoins; potionsBefore = player.PotionCount; arrowsBefore = player.crossbowController.GetArrowCount(0);
                    boss.StartCoroutine((IEnumerator)Call(boss, "Perform", ShaulaBoss.Ability.Stars));
                    boss.TakeDamage(100000); boss.TakeDamage(100000);
                    Check(boss.IsDefeating && !boss.IsAlive && !boss.VictoryAwarded, "Zero HP starts transformation without immediate credit");
                    Check(boss.GetComponentsInChildren<Collider2D>().All(c => !c.enabled), "Transforming and tiny forms cannot collide or damage the player");
                    Check(!Effects().Any(go => go.GetComponent<LineRenderer>() != null), "Defeat clears every projectile and warning immediately");
                    Next(1.6f); break;
                case 15:
                    Check(!boss.VictoryAwarded && stats.countBossDead == bossesBefore && stats.completedWaves == wavesBefore, "Victory waits for the end of the transformation");
                    Capture("shaula-v24-transformation.png"); Next(1.35f); break;
                case 16:
                    Check(boss != null && boss.VictoryAwarded, "Tiny harmless scorpion completes the encounter");
                    Check(stats.countBossDead == bossesBefore + 1 && stats.completedWaves == wavesBefore + 1, "Exactly one boss and one wave are credited");
                    Check(player.totalCoins == coinsBefore + 1000 && player.PotionCount == potionsBefore + 3, "Guaranteed reward is awarded exactly once");
                    Check(player.crossbowController.GetArrowCount(0) == arrowsBefore + 30, "Victory grants the configured ammunition reward");
                    Check(!wave.IsCombatActive && !Get<GameObject>(wave, "shaulaHealthPanel").activeSelf, "Wave enters its normal break and hides boss HUD");
                    Next(2f); break;
                case 17:
                    Check(boss == null, "Little scorpion leaves and cleans itself up");
                    Check(stats.countBossDead == bossesBefore + 1 && player.totalCoins == coinsBefore + 1000, "Cleanup cannot duplicate rewards");
                    Set(wave, "currentWaveIndex", 19); Call(wave, "StartWave", 19, false); Next(1.3f); break;
                case 18:
                    boss = wave.Shaula; Check(boss != null && boss.IsAlive && boss.Health == boss.MaximumHealth && !boss.IsCrimson, "Re-entering wave 20 creates a pristine encounter");
                    StopBoss(); PositionActors(); player.hp = player.maxHp; Call(boss, "RefreshHud");
                    Capture("shaula-v24-pc-ru.png"); CheckHudGeometry();
                    Set(boss, "crimson", true); Call(boss, "SetPose", ShaulaBoss.Pose.Idle); Call(boss, "RefreshHud");
                    GameLocalization.SetSelection("en");
                    YG.YG2.envir.isDesktop = false; YG.YG2.envir.isMobile = true; YG.YG2.envir.deviceType = "mobile";
                    UnityEngine.Object.FindObjectOfType<MobileControls>().RefreshLayout();
                    phase = 21; wake = Time.time + .1f; frame = Time.frameCount + 4; break;
                case 21:
                    Check(GameLocalization.Text("Босс") == "Boss", "Dedicated boss-wave label is localized");
                    Check(Get<TMPro.TMP_Text>(boss, "title").text == "Shaula · Crimson Rage", "Boss HUD follows the English language selection");
                    Check(Get<Material>(boss, "carapaceMaterial").GetFloat("_Rage") == 1, "The crimson palette is applied to every articulated part");
                    Check(UnityEngine.Object.FindObjectOfType<MobileControls>().GuideTarget(TipsManager.GuideAction.Fire) != null, "Touch fire control is available during the encounter");
                    CheckHudGeometry(); Capture("shaula-v24-mobile-en.png");
                    GameLocalization.SetSelection("ru"); Next(.1f); break;
                case 22:
                    Check(Get<TMPro.TMP_Text>(boss, "title").text == "Шаула · Багровая ярость", "Boss HUD switches back to Russian");
                    CheckHudGeometry(); Capture("shaula-v24-mobile-ru.png");
                    YG.YG2.envir.isDesktop = true; YG.YG2.envir.isMobile = false; YG.YG2.envir.deviceType = "desktop";
                    UnityEngine.Object.FindObjectOfType<MobileControls>().RefreshLayout();
                    boss.StartCoroutine((IEnumerator)Call(boss, "Perform", ShaulaBoss.Ability.Needles));
                    Call(wave, "HideShaula"); Next(.1f); break;
                case 23:
                    Check(wave.Shaula == null && !UnityEngine.Object.FindObjectsOfType<LineRenderer>().Any(l => l.name.StartsWith("Shaula_")), "Abandoning the encounter removes every owned effect");
                    Finish(true, "Wave-20 encounter, animation data, arrows, armor, tail, phases, all seven attacks, pause, shield, rewards and cleanup passed."); break;
            }
        }
        catch (Exception error) { Finish(false, "Phase " + phase + ": " + error); }
    }
    static void PositionActors()
    {
        boss.transform.position = arenaCenter + new Vector3(2, 0, 0);
        player.transform.position = arenaCenter + new Vector3(-4.5f, -1.7f, 0);
        Call(boss, "Face", (Vector2)player.transform.position); Physics2D.SyncTransforms();
    }
    static void StopBoss() { boss.StopAllCoroutines(); Call(boss, "ClearEffects"); Call(boss, "ResetStingMotion"); Call(boss, "SetPose", ShaulaBoss.Pose.Idle); }
    static GameObject[] Effects() => Get<List<GameObject>>(boss, "activeEffects").Where(go => go != null && go.activeSelf).ToArray();
    static void Capture(string file)
    {
        Call(player, "UpdateHudValues");
        Camera.main.transform.position = arenaCenter + new Vector3(-.8f, .5f, -10); Camera.main.orthographicSize = 5;
        typeof(LocalizationChecks).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { file });
    }
    static void CheckHudGeometry()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var label in Get<GameObject>(wave, "shaulaHealthPanel").GetComponentsInChildren<TMPro.TMP_Text>())
        {
            label.ForceMeshUpdate();
            Check(label.preferredWidth <= label.rectTransform.rect.width + 2, "Boss label fits its rectangle: " + label.name);
            Check(!label.raycastTarget, "Boss HUD does not intercept combat input: " + label.name);
        }
    }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Fields).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Fields).SetValue(obj, value);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Fields).Invoke(obj, args);
    static void Next(float seconds, int frames = 2) { phase++; wake = Time.time + seconds; frame = Time.frameCount + frames; }
    static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .02f, message + " (" + actual + "/" + expected + ")");
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add(message); }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.GetFullPath("../shaula-v24-checks.txt"), (success ? "PASS" : "FAIL") + "\n" + message + "\n" + string.Join("\n", checks));
        Debug.Log((success ? "SHAULA_V24_PASS: " : "SHAULA_V24_FAIL: ") + message); EditorApplication.Exit(success ? 0 : 1);
    }
}
