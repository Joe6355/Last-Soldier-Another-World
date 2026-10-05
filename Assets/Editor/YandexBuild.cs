using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class YandexBuild
{
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
