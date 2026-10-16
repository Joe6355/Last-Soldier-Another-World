using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

// Run in the isolated validation project: -executeMethod ShotgunChecks.Run.
[InitializeOnLoad]
public static class ShotgunChecks
{
    private const string Flag = "LastSoldier.ShotgunChecks";
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly List<string> checks = new List<string>();
    private static CrossbowController weapon;
    private static int phase, afterFrame, arrowType;
    private static double deadline;
    private static float shotAt;
    static ShotgunChecks() { if (SessionState.GetBool(Flag, false)) Attach(); }
    public static void Run()
    {
        PlayerSettings.companyName = "LastSoldierShotgunValidation";
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        SessionState.SetBool(Flag, true); Attach(); EditorApplication.EnterPlaymode();
    }
    private static void Attach()
    {
        deadline = EditorApplication.timeSinceStartup + 150;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Finish(false, message + "\n" + stack);
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks.Add(label); Debug.Log("SHOTGUN_CHECK: " + label);
    }
    private static T Get<T>(string field) => (T)typeof(CrossbowController).GetField(field, Private).GetValue(weapon);
    private static void Set(string field, object value) => typeof(CrossbowController).GetField(field, Private).SetValue(weapon, value);
    private static void Upgrades(int reloadLevel, bool unlocked = true)
    {
        var items = new List<Beka.UpgradeItem>();
        if (unlocked)
        {
            items.Add(new Beka.UpgradeItem { itemType=Beka.UpgradeItemType.Shotgun, purchaseCount=1, maxPurchases=1 });
            items.Add(new Beka.UpgradeItem { itemType=Beka.UpgradeItemType.Automatic, purchaseCount=1, maxPurchases=1 });
        }
        for (int i=0; i<4; i++) items.Add(new Beka.UpgradeItem {
            itemType=Beka.UpgradeItemType.ReloadSpeed, arrowType=i, purchaseCount=reloadLevel, maxPurchases=10 });
        weapon.RestoreWeaponUpgrades(items.ToArray());
    }
    private static void Ammo(int count) => Set("arrowCounts", new[] { count, count, count, count });
    private static void Shot(int projectileCount, int ammoCost, string label)
    {
        int before=UnityEngine.Object.FindObjectsOfType<ArrowDef>().Length;
        int ammo=weapon.GetArrowCount(weapon.SelectedArrowIndex);
        weapon.Shoot(Vector2.down);
        Check(UnityEngine.Object.FindObjectsOfType<ArrowDef>().Length == before+projectileCount, label+": immediate projectile count");
        Check(weapon.GetArrowCount(weapon.SelectedArrowIndex) == ammo-ammoCost, label+": ammunition cost");
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup>deadline) { Finish(false,"Timeout phase "+phase); return; }
        if (!EditorApplication.isPlaying || !GameProgress.IsReady || Time.frameCount<12 || Time.frameCount<afterFrame) return;
        try
        {
            YG2.infoYG.Simulation.pauseOnFocusGame=false;
            switch (phase)
            {
                case 0:
                    if (YG2.isPauseGame) { YG2.PauseGame(false); return; }
                    var menu=UnityEngine.Object.FindObjectOfType<MainMenuController>();
                    if (menu==null) return;
                    menu.Play(); phase++; afterFrame=Time.frameCount+12; break;
                case 1:
                    if (SceneManager.GetActiveScene().name!="SampleScene") return;
                    weapon=UnityEngine.Object.FindObjectOfType<CrossbowController>();
                    if (weapon==null) return;
                    foreach (var guide in UnityEngine.Object.FindObjectsOfType<TipsManager>()) guide.SkipGuide();
                    UnityEngine.Object.FindObjectOfType<Ui>().StartGame();
                    Check(!YG2.isPauseGame && Time.timeScale==1, "Live gameplay is running");
                    Upgrades(0); Ammo(100); weapon.canShoot=true;
                    Check(weapon.TrySelectMode(2),"Purchased shotgun is selectable");
                    Check(Mathf.Approximately(weapon.ShotInterval(0,2),.35f),"Base shotgun interval is 0.35 seconds");
                    Check(Mathf.Approximately(weapon.ShotInterval(0,1),.35f),"Single fire interval remains 0.35 seconds");
                    Check(Mathf.Approximately(weapon.ShotInterval(0,3),.2f),"Automatic interval remains 0.20 seconds");
                    Shot(3,3,"Unupgraded shotgun volley"); shotAt=Time.time;
                    Check(Mathf.Abs(Get<float>("nextShotTime")-shotAt-.35f)<.0001f,"Shotgun arms the full base cooldown");
                    Shot(0,0,"Repeated shotgun press in same frame");
                    phase++; break;
                case 2:
                    if (Time.time<shotAt+.16f) return;
                    Check(Time.time<shotAt+.35f,"Base-cooldown sample is before ready time");
                    Shot(0,0,"Shotgun press before base cooldown expires"); phase++; break;
                case 3:
                    if (Time.time<Get<float>("nextShotTime")) return;
                    Shot(3,3,"Shotgun after base cooldown expires");
                    phase++; break;
                case 4:
                    if (Time.time<Get<float>("nextShotTime")) return;
                    Upgrades(10);
                    Check(weapon.TrySelectArrowType(arrowType),"Arrow type selectable: "+arrowType);
                    float interval=weapon.ShotInterval(arrowType,2);
                    Check(Mathf.Abs(interval-.1590909f)<.00001f,"Maximum reload gives 0.15909 second shotgun interval: "+arrowType);
                    Check(interval<.35f*.5f,"Maximum reload more than doubles shotgun volley rate: "+arrowType);
                    Shot(3,3,"Upgraded shotgun volley type "+arrowType); shotAt=Time.time;
                    Check(Mathf.Abs(Get<float>("nextShotTime")-shotAt-interval)<.0001f,"Upgraded cooldown applied to actual shot: "+arrowType);
                    Shot(0,0,"Upgraded cooldown still rejects repeat: "+arrowType);
                    phase++; break;
                case 5:
                    if (Time.time<Get<float>("nextShotTime")) return;
                    Check(Time.time-shotAt<.35f,"Upgraded volley is ready before old base interval: "+arrowType);
                    Shot(3,3,"Upgraded second volley type "+arrowType);
                    if (++arrowType<4) phase=4; else phase++;
                    break;
                case 6:
                    if (Time.time<Get<float>("nextShotTime")) return;
                    Ammo(2); Shot(0,0,"Shotgun with two arrows refuses without consumption");
                    Ammo(100); weapon.canShoot=false; Shot(0,0,"External shooting lock"); weapon.canShoot=true;
                    Time.timeScale=0; Shot(0,0,"Time-scale pause lock"); Time.timeScale=1;
                    YG2.PauseGame(true); Shot(0,0,"SDK pause lock"); YG2.PauseGame(false);
                    Upgrades(0,false);
                    Check(!weapon.TrySelectMode(2),"Locked shotgun cannot be selected");
                    Set("shootingMode",2); Shot(0,0,"Locked shotgun cannot fire even with stale mode");
                    Upgrades(0); Check(weapon.TrySelectMode(1),"Single fire selectable");
                    Shot(1,1,"Single fire remains immediate"); shotAt=Time.time;
                    Check(Mathf.Abs(Get<float>("nextShotTime")-shotAt-.35f)<.0001f,"Single fire still applies 0.35 second cooldown");
                    phase++; break;
                case 7:
                    if (Time.time<Get<float>("nextShotTime")) return;
                    Check(weapon.TrySelectMode(3),"Purchased automatic selectable");
                    Shot(1,1,"Automatic remains immediate"); shotAt=Time.time;
                    Check(Mathf.Abs(Get<float>("nextShotTime")-shotAt-.2f)<.0001f,"Automatic still applies 0.20 second cooldown");
                    Shot(0,0,"Automatic cooldown remains enforced");
                    Finish(true,"Live scene shotgun, ammunition, all four reload upgrades and safety locks passed"); break;
            }
        }
        catch (Exception exception) { Finish(false,exception.ToString()); }
    }
    private static void Finish(bool success,string message)
    {
        SessionState.SetBool(Flag,false); EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
        File.WriteAllText(Path.GetFullPath("../shotgun-checks.txt"),(success?"PASS":"FAIL")+"\n"+message+"\n"+string.Join("\n",checks));
        Debug.Log((success?"SHOTGUN_PASS: ":"SHOTGUN_FAIL: ")+message);
        EditorApplication.Exit(success?0:1);
    }
}
