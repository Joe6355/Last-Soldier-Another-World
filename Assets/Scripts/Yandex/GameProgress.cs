using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;
using YG.Utils;
using CloudPrefs = RedefineYG.PlayerPrefs;

// Existing game keys are stored by the SDK's RedefinePlayerPrefs module.
// This component coordinates startup, legacy migration and save frequency.
[DefaultExecutionOrder(-31000)]
public sealed class GameProgress : MonoBehaviour
{
    public const string LeaderboardName = "mmr";
    private const string BackupPrefix = "LastSoldier.Progress.v1.";
    private const float CloudInterval = 10f;
    private static GameProgress instance;
    public static bool IsReady { get; private set; }
    public static bool AuthPending { get; private set; }
    public static bool CloudAvailable { get; private set; }
    public static event Action Changed;
    public static event Action<string> LeaderboardLoadFailed;
    private bool localDirty, cloudDirty, sending, capturing, wasGuest;
    private float nextCloudSave, nextCapture;
    private string owner;
    private int sentRevision;
    private string lastSnapshot;
    private float nextRating;
    private int submittedRating = -1;
    private bool ratingSending;
    private int captureAfterFrame;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int GameCloudStatus_js();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        IsReady = AuthPending = CloudAvailable = false;
        Changed = null;
        LeaderboardLoadFailed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        var host = new GameObject("GameProgress");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<GameProgress>();
    }

    private void Start()
    {
        captureAfterFrame = Time.frameCount + 1;
        // TMP 3.0.7 needs its first atlas cached before creating additional atlases.
        var font = TMPro.TMP_Settings.defaultFontAsset;
        if (font != null && font.fallbackFontAssetTable != null)
            foreach (var fallback in font.fallbackFontAssetTable)
                if (fallback != null) { var firstAtlas = fallback.atlasTexture; }
        YG2.onGetSDKData += LoadProgress;
        YG2.onPauseGame += OnSdkPause;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Startup runs before gameplay Start methods, including direct scene testing.
        YG2.StartInit();
        if (!IsReady && YG2.isSDKEnabled) LoadProgress();
    }

    private void OnDestroy()
    {
        YG2.onGetSDKData -= LoadProgress;
        YG2.onPauseGame -= OnSdkPause;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private static string CurrentOwner => YG2.player.auth && !string.IsNullOrEmpty(YG2.player.id)
        ? YG2.player.id : "guest";

    private void LoadProgress()
    {
        if (!YG2.isSDKEnabled) return;
        string newOwner = CurrentOwner;
        // Environment/profile refreshes must not reload live gameplay data.
        if (IsReady && owner == newOwner && !AuthPending) return;
        int cloudState = ReadCloudState();
        // LoggedIn also emits a data event before the asynchronous cloud request finishes.
        if (cloudState == -2) return;
        bool cloudReadOk = cloudState == 1;
        SavesYG cloud = YG2.saves;
        // The SDK preserves idSave when resetting defaults after login.
        bool cloudEmpty = cloud != null && cloud.progressVersion == 0 && string.IsNullOrEmpty(cloud.ownerId);
        bool cloudValid = IsValid(cloud, newOwner);
        SavesYG backup = ReadBackup(newOwner);

        if (cloudValid && cloudReadOk)
            YG2.saves = backup != null && backup.cloudBaseKnown && backup.idSave > cloud.idSave ? backup : cloud;
        else if (backup != null)
            YG2.saves = backup;
        else if (cloudEmpty && cloudReadOk && AuthPending && wasGuest)
            YG2.saves = ReadBackup("guest") ?? new SavesYG();
        else
            YG2.saves = new SavesYG();

        // A corrupt or foreign save must never be overwritten with defaults.
        CloudAvailable = cloudReadOk && (cloudEmpty || cloudValid);
        owner = newOwner;
        YG2.saves.ownerId = owner;
        if (CloudAvailable) YG2.saves.cloudBaseKnown = true;
        bool migrated = MigrateLegacy();
        YG2.saves.progressVersion = 1;
        IsReady = true;
        AuthPending = false;
        sending = false;
        lastSnapshot = null;
        localDirty = migrated || cloudEmpty || (backup != null && YG2.saves == backup);
        cloudDirty = localDirty;
        CommitLocal();
        LocalStorage.SetKey(BackupPrefix + owner, JsonUtility.ToJson(YG2.saves));
        submittedRating = -1;
        ratingSending = false;
        Changed?.Invoke();
    }

    private static int ReadCloudState()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return GameCloudStatus_js();
#elif UNITY_EDITOR
        return 1; // SDK simulation; no request is made to Yandex.
#else
        return 0;
#endif
    }

    private static SavesYG ReadBackup(string id)
    {
        string json = LocalStorage.GetKey(BackupPrefix + id, "");
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            SavesYG data = JsonUtility.FromJson<SavesYG>(json);
            return IsValid(data, id) ? data : null;
        }
        catch (ArgumentException) { return null; }
    }

    public static bool IsValid(SavesYG data, string id)
    {
        if (data == null || data.progressVersion != 1 || data.ownerId != id || data.idSave < 0)
            return false;
        if (!ValidList(data.intKeys, data.intValues) || !ValidList(data.floatKeys, data.floatValues)
            || !ValidList(data.stringKeys, data.stringValues)) return false;
        foreach (int value in data.intValues) if (value < 0) return false;
        foreach (float value in data.floatValues)
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
        int arrowKey = data.stringKeys.IndexOf("ArrowCounts");
        if (arrowKey >= 0)
        {
            string[] counts = (data.stringValues[arrowKey] ?? "").Split(',');
            if (counts.Length != 3 && counts.Length != 4) return false;
            foreach (string count in counts)
                if (!int.TryParse(count, out int value) || value < 0) return false;
        }
        return true;
    }

    private static bool ValidList<T>(List<string> keys, List<T> values) =>
        keys != null && values != null && keys.Count == values.Count && keys.Count <= 128
        && !keys.Exists(string.IsNullOrEmpty) && new HashSet<string>(keys).Count == keys.Count;

    private static bool MigrateLegacy()
    {
        if (CloudPrefs.HasKey("LegacyMigrationComplete")) return false;
        // Cloud data always wins. Old native preferences are imported once per browser.
        if (!CloudPrefs.HasKey("Coins") && !UnityEngine.PlayerPrefs.HasKey("LastSoldier.LegacyMigrated"))
        {
            foreach (string key in new[] { "Coins", "MirrorRemainder", "potionCount", "ArrowDamageUpgrade",
                "CoinsSpawned", "countEnemyDead", "countPlayerDead", "countBossDead", "countElitEnemyDead" })
                if (UnityEngine.PlayerPrefs.HasKey(key))
                    CloudPrefs.SetInt(key, Mathf.Max(0, UnityEngine.PlayerPrefs.GetInt(key)));
            foreach (string key in new[] { "PlayerHP", "PlayerMaxHP", "ShieldValue", "ShieldMaxValue",
                "PlayerStamina", "PlayerMaxStamina", "PlayerSpeed", "PlayerHPBar" })
                if (UnityEngine.PlayerPrefs.HasKey(key))
                    CloudPrefs.SetFloat(key, UnityEngine.PlayerPrefs.GetFloat(key));
            if (UnityEngine.PlayerPrefs.HasKey("ArrowCounts"))
                CloudPrefs.SetString("ArrowCounts", UnityEngine.PlayerPrefs.GetString("ArrowCounts"));
            for (int i = 0; i < 5; i++)
            {
                string key = "UpgradeShop_Item" + i + "_Count";
                if (UnityEngine.PlayerPrefs.HasKey(key))
                    CloudPrefs.SetInt(key, Mathf.Max(0, UnityEngine.PlayerPrefs.GetInt(key)));
            }
        }
        UnityEngine.PlayerPrefs.SetInt("LastSoldier.LegacyMigrated", 1);
        UnityEngine.PlayerPrefs.Save();
        CloudPrefs.SetInt("LegacyMigrationComplete", 1);
        return true;
    }

    public static void RequestSave()
    {
        if (instance == null || !IsReady || instance.capturing) return;
        instance.localDirty = true;
    }

    public static void SaveNow()
    {
        if (instance == null || !IsReady) return;
        instance.CaptureScene();
        instance.CommitLocal();
        instance.SendCloud();
    }

    public static void BeginAuthorization()
    {
        if (!IsReady || AuthPending || YG2.player.auth) return;
        SaveNow();
        instance.wasGuest = instance.owner == "guest";
        AuthPending = true;
        IsReady = false;
        Changed?.Invoke();
#if UNITY_EDITOR
        YG2.infoYG.Authorization.authorized = true;
        YG2.player.name = YG2.infoYG.Authorization.playerName;
        YG2.OpenAuthDialog();
#else
        YG2.OpenAuthDialog();
#endif
    }

    public static void AuthorizationClosed()
    {
        // Successful login is completed after its cloud data arrives.
        if (!AuthPending || YG2.player.auth) return;
        AuthPending = false;
        IsReady = true;
        Changed?.Invoke();
    }

    private void LateUpdate()
    {
        if (!IsReady) return;
        if (Time.unscaledTime >= nextCapture)
        {
            nextCapture = Time.unscaledTime + 5f;
            CaptureScene();
        }
        if (localDirty)
        {
            CaptureScene(); // A purchase is committed with both its cost and item.
            CommitLocal();
        }
        SendCloud();
        SendRating();
    }

    private void CaptureScene()
    {
        if (!IsReady || capturing || Time.frameCount <= captureAfterFrame) return;
        capturing = true;
        try
        {
            FindObjectOfType<PlayerController>()?.SavePlayerData();
            FindObjectOfType<CrossbowController>()?.SaveArrowCounts();
            FindObjectOfType<Stats>()?.SaveInfo();
            FindObjectOfType<WaveSpawner>()?.SaveCheckpoint();
            FindObjectOfType<Beka>(true)?.SaveUpgrades();
        }
        finally { capturing = false; }
        if (JsonUtility.ToJson(YG2.saves) != lastSnapshot) localDirty = true;
    }

    private void CommitLocal()
    {
        if (!localDirty || !IsReady) return;
        if (JsonUtility.ToJson(YG2.saves) == lastSnapshot && YG2.saves.idSave > 0)
        {
            localDirty = false;
            return;
        }
        YG2.saves.idSave++;
        YG2.saves.ownerId = owner;
        lastSnapshot = JsonUtility.ToJson(YG2.saves);
        LocalStorage.SetKey(BackupPrefix + owner, lastSnapshot);
        UnityEngine.PlayerPrefs.Save();
        localDirty = false;
        cloudDirty = true;
    }

    private void SendCloud()
    {
        if (!cloudDirty || sending || !CloudAvailable || AuthPending || Time.unscaledTime < nextCloudSave)
            return;
        nextCloudSave = Time.unscaledTime + CloudInterval;
        sentRevision = YG2.saves.idSave;
#if UNITY_WEBGL && !UNITY_EDITOR
        sending = true;
        YG2.iPlatform.SaveCloud();
#elif UNITY_EDITOR
        YG.Insides.YGInsides.SaveEditor();
        cloudDirty = false;
#endif
    }

    public static void CloudSaved(string receipt)
    {
        if (instance == null) return;
        var ack = JsonUtility.FromJson<SaveReceipt>(receipt);
        if (ack.ownerId != instance.owner || ack.idSave != instance.sentRevision) return;
        instance.sending = false;
        instance.cloudDirty = YG2.saves.idSave != ack.idSave;
    }

    public static void CloudSaveFailed()
    {
        if (instance == null) return;
        instance.sending = false;
        instance.cloudDirty = true;
    }

    private void SendRating()
    {
        if (!YG2.player.auth || !CloudAvailable || ratingSending || Time.unscaledTime < nextRating) return;
        var summary = Stats.ReadLeaderboardSummary();
        int score = MmrScore(summary.waves, summary.kills);
        if (score == 0 || score == submittedRating) return;
        nextRating = Time.unscaledTime + 30f;
        ratingSending = true;
        YG2.SetLeaderboard(LeaderboardName, score, JsonUtility.ToJson(summary));
#if UNITY_EDITOR
        RatingSaved(score.ToString());
#endif
    }

    public static void RatingSaved(string score)
    {
        if (instance == null) return;
        if (int.TryParse(score, out int parsed)) instance.submittedRating = parsed;
        instance.ratingSending = false;
    }

    public static void RatingFailed()
    {
        if (instance != null) instance.ratingSending = false;
    }

    [Serializable] private sealed class SaveReceipt { public string ownerId; public int idSave; }

    public static int MmrScore(int completedWaves, int kills)
    {
        long rating = Math.Max(0, completedWaves) * 100L + Math.Max(0, kills);
        return (int)Math.Min(int.MaxValue, rating);
    }

    public static void ReportLeaderboardFailure(string name) => LeaderboardLoadFailed?.Invoke(name);

    private void OnSdkPause(bool paused) { if (paused) SaveNow(); }
    private void OnApplicationFocus(bool focused) { if (!focused) SaveNow(); }
    private void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
    private void OnApplicationQuit() => SaveNow();
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        captureAfterFrame = Time.frameCount + 1;
        nextCapture = Time.unscaledTime + 5f;
    }
}
