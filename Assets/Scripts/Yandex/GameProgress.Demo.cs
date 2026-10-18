using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

public sealed partial class GameProgress
{
    private static SavesYG demoSnapshot;
    private static bool restoringDemo;
    public static bool IsArenaDemo => demoSnapshot != null;
    public static bool DemoToolsAvailable
    {
        get
        {
#if UNITY_EDITOR || LAST_SOLDIER_DEMO
            return true;
#else
            return false;
#endif
        }
    }

    public static bool BeginArenaDemo()
    {
        if (!DemoToolsAvailable || instance == null || !IsReady || AuthPending || YG2.isPauseGame || YG2.nowAdsShow) return false;
        if (IsArenaDemo) return !restoringDemo;
        SaveNow();
        // The normal run is committed before detaching a disposable copy.
        demoSnapshot = YG2.saves;
        YG2.saves = JsonUtility.FromJson<SavesYG>(JsonUtility.ToJson(demoSnapshot));
        return true;
    }

    public static void ExitArenaDemo()
    {
        if (!IsArenaDemo || restoringDemo || YG2.isPauseGame || YG2.nowAdsShow) return;
        restoringDemo = true;
        // Keep the disposable save installed through the old scene's teardown.
        instance.StartCoroutine(ReloadAfterDemoFrame());
    }

    private static System.Collections.IEnumerator ReloadAfterDemoFrame()
    {
        yield return null;
        SceneManager.LoadScene("SampleScene");
    }

    private static void RestoreDemoSnapshotAfterSceneLoad()
    {
        if (!IsArenaDemo) return;
        YG2.saves = demoSnapshot;
        demoSnapshot = null; restoringDemo = false;
        if (instance != null)
        {
            instance.localDirty = false;
            instance.lastSnapshot = JsonUtility.ToJson(YG2.saves);
        }
        AudioListener.pause = false;
        Changed?.Invoke();
    }
}
