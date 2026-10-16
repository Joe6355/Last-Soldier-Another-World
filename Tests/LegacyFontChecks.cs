using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class LegacyFontChecks
{
    public static void Run()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if (font == null) throw new InvalidOperationException("Missing bundled font");
        string sample = "Закрыть 500 монет";
        font.RequestCharactersInTexture(sample, 32, FontStyle.Normal);
        foreach (char character in sample)
            if (!font.HasCharacter(character)) throw new InvalidOperationException("Missing glyph: " + character);
        int total = 0;
        foreach (string name in new[] { "Menu", "SampleScene" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    if (text.font != font) throw new InvalidOperationException("Unbundled font: " + text.name);
                    total++;
                }
        }
        if (total != 45) throw new InvalidOperationException("Unexpected legacy text count: " + total);
        Debug.Log("LEGACY_FONT_PASS: 45 scene texts reference bundled font; Russian close/price glyphs present.");
    }
}
