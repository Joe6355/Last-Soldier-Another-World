using System.Collections.Generic;
using TMPro;
using UnityEngine;

public partial class TipsManager
{
    private enum CombatHint { HolyEnemy, Immune, HolyHealing, HolyArrows }
    private RectTransform combatHintRoot;
    private TextMeshProUGUI combatHintText;
    private CombatHint combatHint;
    private float combatHintRemaining, nextCombatHint, nextHolyProbe;
    private readonly float[] nextCombatHintKind = new float[4];
    private readonly List<MonoBehaviour> nearbyHintEnemies = new List<MonoBehaviour>();
    private bool holyEnemyExplained;
    private bool combatHintTouch, combatHintHasHoly;

    public static void ReportArrowImmunity() { if (guide != null) guide.ShowCombatHint(CombatHint.Immune); }
    public static void ReportHolyHealing() { if (guide != null) guide.ShowCombatHint(CombatHint.HolyHealing); }
    public static void ReportHolyArrowsSelected() { if (guide != null) guide.ShowCombatHint(CombatHint.HolyArrows); }

    private bool CanShowCombatHints => isActiveAndEnabled && guidedTutorial && !running && player != null && player.hp > 0 && Time.timeScale > 0
        && hudCanvas != null && GameProgress.IsReady && WaveSpawner.ActiveArena != null
        && WaveSpawner.ActiveArena.IsCombatActive && !Suspended && !shop.IsOpen;

    private void TickCombatHints()
    {
        if (!CanShowCombatHints)
        {
            HideCombatHint();
            if (WaveSpawner.ActiveArena == null || running) combatHintRemaining = 0;
            return;
        }
        if (combatHintRemaining > 0)
        {
            combatHintRemaining = Mathf.Max(0, combatHintRemaining - Time.deltaTime);
            combatHintRoot.gameObject.SetActive(combatHintRemaining > 0);
            if (combatHintTouch != (controls != null && controls.UseTouch)
                || combatHintHasHoly != (player.crossbowController.GetArrowCount(2) > 0)) RenderCombatHint();
            LayoutCombatHint();
        }
        else HideCombatHint();
        if (holyEnemyExplained || Time.time < nextHolyProbe) return;
        nextHolyProbe = Time.time + 1f;
        WaveSpawner.ActiveArena.FillArenaTargets(nearbyHintEnemies, player.transform.position, 7f);
        foreach (var enemy in nearbyHintEnemies)
            if (enemy != null && enemy.CompareTag("HolyEnemy"))
            {
                if (ShowCombatHint(CombatHint.HolyEnemy)) holyEnemyExplained = true;
                break;
            }
    }

    private bool ShowCombatHint(CombatHint kind)
    {
        if (!CanShowCombatHints || Time.timeScale <= 0 || Time.time < nextCombatHint
            || Time.time < nextCombatHintKind[(int)kind]) return false;
        if (combatHintRoot == null)
        {
            combatHintRoot = GuideImage("CombatHint", hudCanvas.transform, GuideGreen, false).rectTransform;
            combatHintText = GuideText("Message", combatHintRoot,
                messageText != null ? messageText.font : TMP_Settings.defaultFontAsset, GuideCream);
            GuideStretch(combatHintText.rectTransform, 8);
            combatHintText.alignment = TextAlignmentOptions.Center;
            combatHintText.enableWordWrapping = true;
            combatHintText.enableAutoSizing = true;
            combatHintText.fontSizeMin = 15; combatHintText.fontSizeMax = 18;
            combatHintText.raycastTarget = false;
            GameLocalization.Protect(combatHintText);
        }
        combatHint = kind;
        combatHintRemaining = 5f;
        nextCombatHint = Time.time + 8f;
        nextCombatHintKind[(int)kind] = Time.time + 30f;
        RenderCombatHint(); LayoutCombatHint();
        combatHintRoot.gameObject.SetActive(true);
        return true;
    }

    private void RenderCombatHint()
    {
        if (combatHintText == null) return;
        combatHintTouch = controls != null && controls.UseTouch;
        combatHintHasHoly = player.crossbowController.GetArrowCount(2) > 0;
        string message = combatHint switch
        {
            CombatHint.Immune => "Иммунитет! Красного врага ранят только красные святые стрелы.",
            CombatHint.HolyHealing => "Красные стрелы лечат обычных врагов. Для них выбери обычные, зелёные или пробивные.",
            CombatHint.HolyArrows => "Красные стрелы ранят святых красных врагов, но лечат всех остальных.",
            _ => "Красные враги — святые. Их ранят только красные святые стрелы."
        };
        bool needsHoly = combatHint == CombatHint.HolyEnemy || combatHint == CombatHint.Immune;
        string action = needsHoly && !combatHintHasHoly
            ? "Нет святых стрел? Пополни запас у торговца."
            : combatHintTouch ? "Нажми нужный тип стрел справа." : "Q — сменить тип стрел.";
        combatHintText.text = GameLocalization.Text(message) + "\n" + GameLocalization.Text(action);
    }

    private void LayoutCombatHint()
    {
        if (combatHintRoot == null || !(hudCanvas.transform is RectTransform parent)) return;
        combatHintRoot.anchorMin = combatHintRoot.anchorMax = combatHintRoot.pivot = new Vector2(.5f, 1);
        float safeTop = Screen.height > 0 ? (Screen.height - Screen.safeArea.yMax) / Screen.height * parent.rect.height : 0;
        float safeCenter = Screen.width > 0 ? (Screen.safeArea.center.x / Screen.width - .5f) * parent.rect.width : 0;
        combatHintRoot.anchoredPosition = new Vector2(safeCenter, -safeTop - 76);
        combatHintRoot.sizeDelta = new Vector2(Mathf.Min(440, parent.rect.width * .42f), 88);
    }

    private void HideCombatHint() { if (combatHintRoot != null) combatHintRoot.gameObject.SetActive(false); }
    private void DestroyCombatHint() { if (combatHintRoot != null) Destroy(combatHintRoot.gameObject); }
}
