using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class YandexBuild
{
    public const string AndroidTestVersion = "1.0.1";
    public const int AndroidTestVersionCode = 2;
    public const string AndroidTestDefines = "PLUGIN_YG_2;Storage_yg;Authorization_yg;Leaderboards_yg;EnvirData_yg;RedefinePlayerPrefs_yg;TMP_YG2;InterstitialAdv_yg;RewardedAdv_yg;StickyAdv_yg;LAST_SOLDIER_ANDROID_TEST";

    [MenuItem("Tools/Last Soldier/Build Android Test APK")]
    public static void BuildAndroidTest()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new InvalidOperationException("Установите Android Build Support, SDK, NDK и OpenJDK в Unity Hub.");

        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, AndroidTestDefines);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Minimal);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.joe6355.lastsoldier.test");
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

        string product = PlayerSettings.productName;
        string version = PlayerSettings.bundleVersion;
        var orientation = PlayerSettings.defaultInterfaceOrientation;
        bool portrait = PlayerSettings.allowedAutorotateToPortrait, upsideDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown;
        bool left = PlayerSettings.allowedAutorotateToLandscapeLeft, right = PlayerSettings.allowedAutorotateToLandscapeRight;
        try
        {
            PlayerSettings.productName = "Последний солдат — тест";
            PlayerSettings.bundleVersion = AndroidTestVersion;
            PlayerSettings.Android.bundleVersionCode = AndroidTestVersionCode;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                target = BuildTarget.Android,
                locationPathName = "Builds/Android/last-soldier-android-test.apk",
                options = BuildOptions.Development
            });
            string result = report.summary.result + "; errors=" + report.summary.totalErrors + "; bytes=" + report.summary.totalSize;
            File.WriteAllText("Builds/android-test-build-result.txt", result);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(result);
            Debug.Log("ANDROID_TEST_BUILD_PASS: " + result);
        }
        finally
        {
            PlayerSettings.productName = product;
            PlayerSettings.bundleVersion = version;
            PlayerSettings.defaultInterfaceOrientation = orientation;
            PlayerSettings.allowedAutorotateToPortrait = portrait; PlayerSettings.allowedAutorotateToPortraitUpsideDown = upsideDown;
            PlayerSettings.allowedAutorotateToLandscapeLeft = left; PlayerSettings.allowedAutorotateToLandscapeRight = right;
        }
    }

    [MenuItem("Tools/Last Soldier/Build Yandex WebGL")]
    public static void Build()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new InvalidOperationException("Install WebGL Build Support for Unity 2022.3.47f1 in Unity Hub.");
        PlayerSettings.WebGL.template = "PROJECT:YandexGames";
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/YandexGames",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("WebGL build failed: " + report.summary.result);
        long size = Directory.GetFiles("Builds/YandexGames", "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
        Debug.Log("Yandex WebGL build succeeded; unpacked bytes: " + size);
        if (size > 100 * 1024 * 1024)
            Debug.LogWarning("Build exceeds the Yandex Games 100 MB unpacked limit. Optimize assets before upload.");
    }
}
