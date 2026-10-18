using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ArenaDemoAuthoring
{
    [MenuItem("Tools/Last Soldier/Enable Draft Wave Tests")]
    public static void Enable()
    {
        string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL);
        if (!symbols.Split(';').Contains("LAST_SOLDIER_DEMO"))
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL, symbols + ";LAST_SOLDIER_DEMO");
        PlayerSettings.bundleVersion = "0.0.0.22";
        AssetDatabase.SaveAssets();
        Debug.Log("ARENA_DEMO_DRAFT_ENABLED: key 9; disposable save; release build must omit LAST_SOLDIER_DEMO");
    }

    [MenuItem("Tools/Last Soldier/Disable Draft Wave Tests")]
    public static void Disable()
    {
        var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL).Split(';');
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL,
            string.Join(";", symbols.Where(symbol => !string.Equals(symbol, "LAST_SOLDIER_DEMO", StringComparison.Ordinal))));
        AssetDatabase.SaveAssets();
    }
}
