using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GuidedTutorialAuthoring
{
    [MenuItem("Tools/Last Soldier/Configure Guided Tutorial")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var mobile = UnityEngine.Object.FindObjectOfType<MobileControls>(true);
        var player = UnityEngine.Object.FindObjectOfType<PlayerController>(true);
        var shop = UnityEngine.Object.FindObjectOfType<Shop>(true);
        var upgrades = Get<Beka>(shop, "upgradeShop");
        var ui = Get<Ui>(mobile, "gameUi");
        var canvas = Get<Canvas>(mobile, "canvas");
        var managers = UnityEngine.Object.FindObjectsOfType<TipsManager>(true).OrderBy(t => t.name).ToArray();
        var guide = managers.FirstOrDefault(t => GetBool(t, "guidedTutorial")) ?? managers.First();
        foreach (var old in managers.Where(t => t != guide)) old.gameObject.SetActive(false);
        foreach (var trigger in UnityEngine.Object.FindObjectsOfType<TipsTrigger>(true)) trigger.enabled = false;
        guide.gameObject.SetActive(true); guide.enabled = true; guide.gameObject.name = "GuidedTutorial";
        var oldAnim = guide.GetComponent<Animator>(); if (oldAnim != null) oldAnim.enabled = false;
        foreach (var graphic in guide.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
        Set(guide, "guidedTutorial", true);
        Set(guide, "player", player); Set(guide, "controls", mobile); Set(guide, "shop", shop);
        Set(guide, "upgrades", upgrades); Set(guide, "gameUi", ui); Set(guide, "hudCanvas", canvas);
        Set(guide, "campSpawn", Get<Transform>(player, "mirrorHome"));
        Set(guide, "desktopEquipment", Get<GameObject>(mobile, "combatEquipment").GetComponent<RectTransform>());
        Set(guide, "trainingSlimePrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/SlimeTrue.prefab").GetComponent<Slime>());
        Set(guide, "trainingCoinPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Money/MoneyGold Variant.prefab").GetComponent<Coin>());
        Set(guide, "mentorPortrait", shop.GetComponent<SpriteRenderer>().sprite);
        var waves = UnityEngine.Object.FindObjectOfType<WaveSpawner>(true);
        var exit = Get<GameObject>(waves, "breakIndicator").GetComponent<Portal>();
        var entrance = exit.linkedPortal.GetComponent<Portal>();
        Set(guide, "arenaEntrance", entrance); Set(guide, "arenaExit", exit);
        Set(entrance, "destinationLabel", "АРЕНА"); Set(exit, "destinationLabel", "В ЛАГЕРЬ");
        Set(entrance, "labelFont", Get<TMP_Text>(guide, "messageText").font);
        Set(exit, "labelFont", Get<TMP_Text>(guide, "messageText").font);
        var quantityDialog = Get<TextMeshProUGUI>(shop, "quantityTitle").transform.parent;
        var area = quantityDialog.Find("TutorialQuantityControls");
        if (area == null)
        {
            var obj = new GameObject("TutorialQuantityControls", typeof(RectTransform));
            obj.transform.SetParent(quantityDialog, false); area = obj.transform;
        }
        var quantityRect = area.GetComponent<RectTransform>();
        quantityRect.anchorMin = new Vector2(.04f, .46f); quantityRect.anchorMax = new Vector2(.96f, .74f);
        quantityRect.offsetMin = quantityRect.offsetMax = Vector2.zero;
        Set(shop, "guideQuantityControls", quantityRect);
        var menu = Get<GameObject>(mobile, "gameMenuPanel");
        var settings = Get<GameObject>(mobile, "gameSettingsPanel");
        var font = menu.GetComponentInChildren<TMP_Text>(true).font;
        Set(guide, "replayButton", MakeButton(menu.transform, "ReplayTutorial", font, .49f, .05f, .785f, .13f));
        Set(guide, "settingsReplayButton", MakeButton(settings.transform, "ReplayTutorial", font, .44f, .025f, .79f, .095f));
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        Debug.Log("GUIDED_TUTORIAL_CONFIGURED: existing TipsManager, real HUD/shop targets and practice prefabs.");
    }
    private static Button MakeButton(Transform parent, string name, TMP_FontAsset font, float x1, float y1, float x2, float y2)
    {
        var old = parent.Find(name);
        if (old != null)
        {
            var oldRect = old.GetComponent<RectTransform>();
            oldRect.anchorMin = new Vector2(x1, y1); oldRect.anchorMax = new Vector2(x2, y2);
            oldRect.offsetMin = oldRect.offsetMax = Vector2.zero;
            return old.GetComponent<Button>();
        }
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(x1, y1); rect.anchorMax = new Vector2(x2, y2); rect.offsetMin = rect.offsetMax = Vector2.zero;
        obj.GetComponent<Image>().color = new Color32(35, 68, 59, 255);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(obj.transform, false);
        var text = label.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = "Обучение"; text.fontSize = 28;
        text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = 28; text.alignment = TextAlignmentOptions.Center;
        text.color = new Color32(244, 240, 223, 255); text.raycastTarget = false;
        var labelRect = label.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8, 2); labelRect.offsetMax = new Vector2(-8, -2);
        return obj.GetComponent<Button>();
    }
    private static T Get<T>(UnityEngine.Object obj, string name) where T : UnityEngine.Object => (T)new SerializedObject(obj).FindProperty(name).objectReferenceValue;
    private static bool GetBool(UnityEngine.Object obj, string name) => new SerializedObject(obj).FindProperty(name).boolValue;
    private static void Set(UnityEngine.Object obj, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(obj); serialized.FindProperty(name).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Set(UnityEngine.Object obj, string name, bool value)
    {
        var serialized = new SerializedObject(obj); serialized.FindProperty(name).boolValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Set(UnityEngine.Object obj, string name, string value)
    {
        var serialized = new SerializedObject(obj); serialized.FindProperty(name).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
