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

// Runs only in the isolated validation project; never changes the player's save.
[InitializeOnLoad]
public static class FriendlyDefeatChecks
{
    const string Flag = "LastSoldier.FriendlyDefeatChecks";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int phase, frame, sample, kills, bosses;
    static double deadline, wake;
    static readonly List<string> checks = new List<string>();
    static GameObject actor, template;
    static WaveSpawner wave;
    static Stats stats;
    static PlayerController player;
    static GameObject[] owned;
    static ParticleSystem effect;
    static int particleCount;
    static float rewardDelay;
    static bool IsSlime => sample % 2 == 0;
    static bool IsRegular => sample >= 2;
    static FriendlyDefeatChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run()
    {
        PlayerSettings.companyName = "LastSoldierFriendlyDefeatValidation";
        PlayerSettings.productName = "FriendlyDefeatValidation";
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
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Finish(false, message + "\n" + stack);
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout at " + phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount < frame || EditorApplication.timeSinceStartup < wake) return;
        try
        {
            YG2.infoYG.Simulation.pauseOnFocusGame = false;
            if (YG2.isPauseGame) { YG2.PauseGame(false); return; }
            switch (phase)
            {
                case 0:
                    UnityEngine.Object.FindObjectOfType<MainMenuController>().Play(); Next(.2, 20); break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    player = UnityEngine.Object.FindObjectOfType<PlayerController>();
                    stats = UnityEngine.Object.FindObjectOfType<Stats>();
                    wave = WaveSpawner.FindArena(false);
                    foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                    UnityEngine.Object.FindObjectOfType<Ui>().StartGame();
                    player.TeleportPlayerHome();
                    Set(player, "invulnerableUntil", Time.time + 1000);
                    phase = 2; break;
                case 2:
                    if (IsRegular) wave.BeginTutorialWave(1); // Real combat state without unrelated automatic spawns.
                    template = Get<GameObject>(wave, IsSlime ? "regularSlimeTemplate" : "regularAssassinTemplate");
                    Check(template != null && !template.activeSelf, "Source template is inactive: " + sample);
                    actor = UnityEngine.Object.Instantiate(template);
                    actor.name = "DefeatValidation" + sample;
                    actor.transform.position = player.transform.position + Vector3.right * 2;
                    if (IsRegular)
                    {
                        if (IsSlime) actor.GetComponent<SlimeBoss>().ConfigureAsRegularEnemy(wave, 1, 1, 1, 1);
                        else actor.GetComponent<Roberto>().ConfigureAsRegularEnemy(wave, 1, 1, 1, 1);
                    }
                    actor.SetActive(true); Next(.1, 5); break;
                case 3:
                    kills = stats.countEnemyDead; bosses = stats.countBossDead;
                    Check(actor.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "Living boss is visible: " + sample);
                    if (IsSlime)
                    {
                        var slime = actor.GetComponent<SlimeBoss>();
                        Call(slime, "Attack");
                        owned = Get<List<GameObject>>(slime, "ownedProjectiles").ToArray();
                        rewardDelay = Get<float>(slime, "deathAnimationTime");
                        slime.TakeDamage(1000000, false); slime.TakeDamage(1000000, false);
                        Check(stats.countBossDead == bosses && stats.countEnemyDead == kills, "Slime retains delayed reward timing: " + sample);
                    }
                    else
                    {
                        var assassin = actor.GetComponent<Roberto>();
                        owned = new[] { new GameObject("OwnedWarning"), new GameObject("OwnedSummon") };
                        Get<List<GameObject>>(assassin, "ownedAttacks").Add(owned[0]);
                        Get<List<GameObject>>(assassin, "summonedEnemies").Add(owned[1]);
                        assassin.TakeDamage(1000000); rewardDelay = .8f;
                    }
                    Next(.02, 2); break;
                case 4:
                    Check(actor != null && actor.GetComponentsInChildren<Renderer>().All(r => !r.enabled), "No body or weapon remains visible: " + sample);
                    Check(actor.GetComponentsInChildren<Animator>().All(a => !a.enabled), "Fall animation cannot play: " + sample);
                    Check(actor.GetComponentsInChildren<Collider2D>().All(c => !c.enabled), "Defeated enemy cannot block arrows or player: " + sample);
                    Check(owned.All(o => o == null), "Owned attacks/summons are cleared: " + sample);
                    effect = UnityEngine.Object.FindObjectsOfType<ParticleSystem>().FirstOrDefault(p => p.name == "EnemyDefeatSparkles");
                    Check(effect != null && effect.particleCount > 0, "Visible magical particles replace the body: " + sample);
                    particleCount = effect.particleCount;
                    Time.timeScale = 0; Next(.15, 3); break;
                case 5:
                    Check(effect != null && effect.particleCount == particleCount && !effect.main.useUnscaledTime, "Particles respect gameplay pause: " + sample);
                    Time.timeScale = 1; Next(rewardDelay + .3, 6); break;
                case 6:
                    Check(actor == null, "Existing destruction timing completes: " + sample);
                    Check(effect == null, "Particles clean themselves up: " + sample);
                    Check(stats.countBossDead == bosses + (IsRegular ? 0 : 1), "Dedicated boss credit exactly once: " + sample);
                    Check(stats.countEnemyDead == kills + (IsRegular ? 1 : 0), "Regular boss credit exactly once: " + sample);
                    Check(template != null && !template.activeSelf && template.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled), "Pristine source remains unchanged: " + sample);
                    if (++sample < 4) { phase = 2; break; }
                    Finish(true, "All four boss/regular variants disappear immediately, cannot collide, preserve credit timing and clean up attacks/particles."); break;
            }
        }
        catch (Exception error) { Finish(false, "phase=" + phase + " sample=" + sample + ": " + error); }
    }
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    static object Call(object target, string method) => target.GetType().GetMethod(method, Fields).Invoke(target, null);
    static void Next(double seconds, int frames) { phase++; wake = EditorApplication.timeSinceStartup + seconds; frame = Time.frameCount + frames; }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message); Debug.Log("FRIENDLY_DEFEAT_CHECK: " + message);
    }
    static void Finish(bool pass, string message)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        Time.timeScale = 1;
        SessionState.SetBool(Flag, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.GetFullPath("../friendly-defeat-checks.txt"), (pass ? "PASS\n" : "FAIL\n") + message + "\n" + string.Join("\n", checks));
        Debug.Log((pass ? "FRIENDLY_DEFEAT_PASS: " : "FRIENDLY_DEFEAT_FAIL: ") + message);
        EditorApplication.Exit(pass ? 0 : 1);
    }
}
