using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class YandexBuild
{
    public const string RuStoreDefines = "PLUGIN_YG_2;Storage_yg;Authorization_yg;Leaderboards_yg;EnvirData_yg;RedefinePlayerPrefs_yg;TMP_YG2;InterstitialAdv_yg;RewardedAdv_yg;StickyAdv_yg;LAST_SOLDIER_RUSTORE";
    [Serializable] private sealed class Signing { public string keystore, alias, passwordFile; }

    [MenuItem("Tools/Last Soldier/RuStore/Prepare QA")]
    public static void PrepareRuStoreQa() => ConfigureRuStore(true);

    [MenuItem("Tools/Last Soldier/RuStore/Prepare release")]
    public static void PrepareRuStoreRelease() => ConfigureRuStore(false);

    private static void ConfigureRuStore(bool qa)
    {
        var settings = RuStoreSettings.Load();
        if (settings == null) throw new InvalidOperationException("Missing Resources/RuStoreSettings.asset");
        if (!qa) ValidateRuStoreRelease(settings);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, RuStoreDefines + (qa ? ";LAST_SOLDIER_RUSTORE_QA" : ""));
        PlayerSettings.companyName = "Joe6355";
        PlayerSettings.productName = qa ? "Последний солдат — проверка" : "Последний солдат: другой мир";
        PlayerSettings.bundleVersion = settings.versionName;
        PlayerSettings.Android.bundleVersionCode = settings.versionCode;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, settings.packageName);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Minimal);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.runInBackground = false;
        PlayerSettings.Android.forceInternetPermission = true;
        EditorUserBuildSettings.buildAppBundle = false;
        // Unity 2022 validates JDK 11. Export native code with its supported
        // toolchain; Tools/Build-RuStore.ps1 packages it with AGP 8 / JDK 17.
        EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.connectProfiler = false;

        string signingPath = Environment.GetEnvironmentVariable("LAST_SOLDIER_SIGNING_CONFIG") ?? "Builds/Signing/signing.local.json";
        if (!File.Exists(signingPath)) throw new InvalidOperationException("Release signing config is required: " + signingPath);
        var signing = JsonUtility.FromJson<Signing>(File.ReadAllText(signingPath));
        if (signing == null || !File.Exists(signing.keystore) || !File.Exists(signing.passwordFile))
            throw new InvalidOperationException("Invalid local signing config");
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = signing.keystore;
        PlayerSettings.Android.keyaliasName = signing.alias;
        PlayerSettings.Android.keystorePass = File.ReadAllText(signing.passwordFile).Trim();
        PlayerSettings.Android.keyaliasPass = PlayerSettings.Android.keystorePass;

        const string iconPath = "Assets/UI/ForestGarrison/RuStoreIcon.png";
        File.Copy("Publishing/Card/icon.png", iconPath, true);
        AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        var sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Android, IconKind.Any);
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, sizes.Select(_ => icon).ToArray(), IconKind.Any);
        foreach (var kind in PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.Android))
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var platformIcon in icons)
                for (int layer = 0; layer < platformIcon.maxLayerCount; layer++) platformIcon.SetTexture(icon, layer);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("RUSTORE_CONFIGURED: " + (qa
            ? (settings.useTestAds ? "QA with vendor demo IDs; not for publication" : "QA with publisher IDs; not for publication")
            : "Production"));
    }

    private static void ConfigureRuStoreToolchain()
    {
        foreach (string key in new[] { "SdkUseEmbedded", "NdkUseEmbedded", "JdkUseEmbedded" }) EditorPrefs.SetBool(key, false);
        string tools = Path.GetFullPath("Builds/Toolchain");
        string android = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer");
        string sdk = Environment.GetEnvironmentVariable("LAST_SOLDIER_ANDROID_SDK")
            ?? (Directory.Exists(Path.Combine(tools, "SDK")) ? Path.Combine(tools, "SDK") : Path.Combine(android, "SDK"));
        string ndk = Environment.GetEnvironmentVariable("LAST_SOLDIER_ANDROID_NDK") ?? Path.Combine(android, "NDK");
        string jdk = Environment.GetEnvironmentVariable("LAST_SOLDIER_UNITY_JDK") ?? Path.Combine(android, "OpenJDK");
        if (!Directory.Exists(sdk) || !Directory.Exists(ndk) || !Directory.Exists(jdk))
            throw new InvalidOperationException("Install Android SDK 35, NDK 23.1 and JDK 11, or configure LAST_SOLDIER_ANDROID_SDK / ANDROID_NDK / UNITY_JDK");
        AndroidExternalToolsSettings.sdkRootPath = sdk;
        AndroidExternalToolsSettings.ndkRootPath = ndk;
        AndroidExternalToolsSettings.jdkRootPath = jdk;
    }

    public static void ValidateRuStoreRelease(RuStoreSettings settings)
    {
        if (settings.useTestAds) throw new InvalidOperationException("Production must disable useTestAds");
        if (!settings.releasePrivacyReviewed || !Uri.TryCreate(settings.privacyUrl, UriKind.Absolute, out var policy) || policy.Scheme != "https")
            throw new InvalidOperationException("Publish and review the final privacy policy, then enable releasePrivacyReviewed");
        foreach (var id in new[] { settings.rewardedAdUnitId, settings.interstitialAdUnitId, settings.bannerAdUnitId })
            if (!System.Text.RegularExpressions.Regex.IsMatch(id ?? "", "^R-M-[0-9]+-[0-9]+$"))
                throw new InvalidOperationException("Configure production Mobile Ads block IDs");
        if (string.IsNullOrWhiteSpace(settings.supportEmail)) throw new InvalidOperationException("Developer support contact is required");
        if (!Application.unityVersion.Contains("2022.3.62f2"))
            throw new InvalidOperationException("Use the validated patched Unity 2022.3.62f2 toolchain");
    }

    [MenuItem("Tools/Last Soldier/RuStore/Export QA Gradle project")]
    public static void BuildRuStoreQa() => BuildRuStore(true);

    [MenuItem("Tools/Last Soldier/RuStore/Export release Gradle project")]
    public static void BuildRuStoreRelease() => BuildRuStore(false);

    private static void BuildRuStore(bool qa)
    {
        var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android).Split(';');
        if (!symbols.Contains("LAST_SOLDIER_RUSTORE") || qa != symbols.Contains("LAST_SOLDIER_RUSTORE_QA"))
            throw new InvalidOperationException("Prepare the corresponding RuStore configuration first, then build after compilation");
        if (symbols.Contains("LAST_SOLDIER_DEMO") || symbols.Contains("LAST_SOLDIER_ANDROID_TEST") || symbols.Contains("YandexGamesPlatform_yg"))
            throw new InvalidOperationException("RuStore must not include browser, test-ad or demo platform defines");
        ConfigureRuStore(qa);
        GameSpriteAtlas.Synchronize();
        Directory.CreateDirectory("Builds/RuStore");
        string[] embeddedKeys = { "SdkUseEmbedded", "NdkUseEmbedded", "JdkUseEmbedded", "GradleUseEmbedded" };
        bool[] embedded = embeddedKeys.Select(key => EditorPrefs.GetBool(key, true)).ToArray();
        string[] pathKeys = { "AndroidSdkRoot", "AndroidNdkRootR23B", "JdkPath", "GradlePath" };
        string[] paths = pathKeys.Select(key => EditorPrefs.GetString(key, "")).ToArray();
        BuildReport report;
        try
        {
            ConfigureRuStoreToolchain();
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                target = BuildTarget.Android,
                locationPathName = qa ? "Builds/RuStore/GradleQA" : "Builds/RuStore/GradleRelease",
                options = BuildOptions.DetailedBuildReport
            });
        }
        finally
        {
            for (int i = 0; i < pathKeys.Length; i++) EditorPrefs.SetString(pathKeys[i], paths[i]);
            for (int i = 0; i < embeddedKeys.Length; i++) EditorPrefs.SetBool(embeddedKeys[i], embedded[i]);
            PlayerSettings.Android.keystorePass = PlayerSettings.Android.keyaliasPass = "";
        }
        Directory.CreateDirectory("Publishing/RuStore");
        string text = "result=" + report.summary.result + ";errors=" + report.summary.totalErrors + ";bytes=" + report.summary.totalSize;
        File.WriteAllText("Builds/RuStore/build-result.txt", text);
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(text);
        Debug.Log("RUSTORE_EXPORT_PASS: " + text + "; package with Tools/Build-RuStore.ps1");
    }
}

public sealed class RuStoreGradleSettings : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 500;
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (!PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android).Split(';').Contains("LAST_SOLDIER_RUSTORE")) return;
        string launcher = Path.GetFullPath(Path.Combine(path, "../launcher/build.gradle"));
        if (File.Exists(launcher)) File.AppendAllText(launcher, "\nandroid { ndkVersion '23.1.7779620' }\n");
    }
}
