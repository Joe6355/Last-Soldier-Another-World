using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Prefs = RedefineYG.PlayerPrefs;

public partial class TipsManager
{
    public enum GuideAction { Move, Fire, Collect, Arrow, Dash, Shield, Heal, Utilities, Mirror, Interact, Goods, Quantity, Purchase, Upgrades, Upgrade, Mode }
    // Keep the first sixteen values unchanged for existing saved lessons.
    public enum GuideStep { Welcome, Move, Fire, Loot, Arrow, Dash, Shield, Heal, Utilities, Mirror, Merchant, Goods, Quantity, UpgradeTab, Health, Finished,
        ArenaPortal, ArenaIntro, ArenaCombat, ArenaLoot, ArenaBreak, ArenaReturn, ArenaFinished }
    [Header("Наглядное обучение")]
    [SerializeField] private bool guidedTutorial;
    [SerializeField] private PlayerController player;
    [SerializeField] private MobileControls controls;
    [SerializeField] private Shop shop;
    [SerializeField] private Beka upgrades;
    [SerializeField] private Ui gameUi;
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Transform campSpawn;
    [SerializeField] private Slime trainingSlimePrefab;
    [SerializeField] private Coin trainingCoinPrefab;
    [SerializeField] private Sprite mentorPortrait;
    [SerializeField] private RectTransform desktopEquipment;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button settingsReplayButton;
    [SerializeField] private Portal arenaEntrance;
    [SerializeField] private Portal arenaExit;
    private static TipsManager guide;
    private const string Prefix = "GuidedTutorial.v1.";
    private GuideStep step;
    private bool running, ready, result, bypass, replay, prepared;
    private bool guideLastPlatformTouch;
    private string outcome;
    private Vector2 movementStart;
    private float distanceMoved, potionStartHp, shieldPractice;
    private int hits, purchaseArrows, purchaseCoins, healthLevel;
    private Slime trainingTarget;
    private Coin trainingCoin;
    private readonly List<Slime> arenaPracticeEnemies = new List<Slime>();
    private int arenaKills, returnMirrorCount;
    private RectTransform targetUi;
    private Transform targetWorld;
    private WaveSpawner waves;
    private Cinemachine.CinemachineVirtualCamera lessonCamera;
    private static bool GuideInstalled => guide != null;
    public static bool IsGuideActive => guide != null && guide.running;
    public GuideStep CurrentStep => step;
    public bool HasStepResult => result;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGuide() { guide = null; displayTipEvent = null; disabeTipEvent = null; }
    public static bool Allows(GuideAction action)
    {
        if (!IsGuideActive) return true;
        if (guide.result) return false;
        return guide.step switch
        {
            GuideStep.Move => action == GuideAction.Move,
            GuideStep.Fire => action == GuideAction.Fire,
            GuideStep.Loot => action == GuideAction.Move || action == GuideAction.Collect,
            GuideStep.Arrow => action == GuideAction.Arrow,
            GuideStep.Dash => action == GuideAction.Dash,
            GuideStep.Shield => action == GuideAction.Shield,
            GuideStep.Heal => action == GuideAction.Heal,
            GuideStep.Utilities => action == GuideAction.Utilities,
            GuideStep.Mirror => action == GuideAction.Mirror || action == GuideAction.Utilities,
            GuideStep.Merchant => action == GuideAction.Interact || action == GuideAction.Move,
            GuideStep.Goods => action == GuideAction.Goods,
            GuideStep.Quantity => action == GuideAction.Quantity || action == GuideAction.Purchase,
            GuideStep.UpgradeTab => action == GuideAction.Upgrades,
            GuideStep.Health => action == GuideAction.Upgrade,
            GuideStep.ArenaPortal or GuideStep.ArenaReturn => action == GuideAction.Move,
            GuideStep.ArenaCombat => action == GuideAction.Move || action == GuideAction.Fire
                || action == GuideAction.Dash || action == GuideAction.Shield,
            GuideStep.ArenaLoot => action == GuideAction.Move || action == GuideAction.Collect,
            _ => false
        };
    }
    public static void Report(GuideAction action) { if (IsGuideActive && !guide.result) guide.OnAction(action); }
    public static bool BlocksShot(Vector2 point) => IsGuideActive && guide.IsPointerOverGuide(point);
    public static bool AllowsGoods(int index) => !IsGuideActive || !guide.result && index == 0 && (guide.step == GuideStep.Goods || guide.step == GuideStep.Quantity);
    public static bool AllowsQuantity(int quantity) => !IsGuideActive || Allows(GuideAction.Purchase) && quantity == 2;
    public static bool AllowsUpgrade(int index) => !IsGuideActive || Allows(GuideAction.Upgrade) && index == 0;
    public static bool AllowsPortal(Portal portal) => !IsGuideActive || !guide.result && !guide.Suspended
        && (guide.step == GuideStep.ArenaPortal && portal == guide.arenaEntrance
            || guide.step == GuideStep.ArenaReturn && portal == guide.arenaExit);
    public static void ReportPortal(Portal portal)
    {
        if (!IsGuideActive) return;
        if (guide.step == GuideStep.ArenaPortal && portal == guide.arenaEntrance)
            guide.Complete("Ты на арене. Здесь начинается учебная волна 0 — обычные волны пока ждут.");
        else if (guide.step == GuideStep.ArenaReturn && portal == guide.arenaExit)
        {
            guide.waves.EndTutorialWave();
            guide.Complete($"Ты вернулся в лагерь бесплатно. Зеркала: {guide.returnMirrorCount:N0} → {guide.player.MirrorCount:N0}.");
        }
    }
    private void StartGuided()
    {
        if (!guidedTutorial) return;
        guide = this;
        waves = FindObjectOfType<WaveSpawner>();
        if (arenaExit == null && waves != null && waves.TutorialExit != null)
            arenaExit = waves.TutorialExit.GetComponent<Portal>();
        if (arenaEntrance == null && arenaExit != null && arenaExit.linkedPortal != null)
            arenaEntrance = arenaExit.linkedPortal.GetComponent<Portal>();
        lessonCamera = FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();
        replayButton.onClick.AddListener(RestartGuide);
        if (settingsReplayButton != null) settingsReplayButton.onClick.AddListener(RestartGuide);
        CreateGuideView(hudCanvas, mentorPortrait, messageText != null ? messageText.font : TMP_Settings.defaultFontAsset);
        guideRoot.gameObject.SetActive(false);
    }
    private bool Suspended => !GameProgress.IsReady || YG.YG2.isPauseGame || YG.YG2.nowAdsShow
        || gameUi.IsMenuOpen || gameUi.IsMobileOrientationPaused || player.IsAwaitingRevive;
    private void TickGuided()
    {
        if (!guidedTutorial || guideRoot == null || player == null) return;
        bool canReplay = GameProgress.IsReady && !player.IsAwaitingRevive && !(waves != null && waves.IsRunning);
        replayButton.interactable = canReplay;
        if (settingsReplayButton != null) settingsReplayButton.interactable = canReplay;
        if (!ready && GameProgress.IsReady) { ready = true; RestoreExerciseHealth(); }
        if (ready && !running && (Prefs.GetInt(Prefix + "Done", 0) == 0 || Prefs.GetInt(Prefix + "ArenaDone", 0) == 0) && !Suspended
            && !(waves != null && waves.IsRunning)) BeginGuide(false);
        if (!running) return;
        if (Suspended) { PositionGuide(null, null, false, true); return; }
        if (!prepared) PrepareStep();
        if (guideLastPlatformTouch != controls.UseTouch) RenderCurrent();
        if (!result && step == GuideStep.Move)
        {
            Vector2 position = player.transform.position;
            distanceMoved += Vector2.Distance(position, movementStart); movementStart = position;
            if (distanceMoved >= 1.2f) Complete("Ты управляешь героем. Движение помогает уходить от атак и собирать добычу.");
        }
        if (!result && step == GuideStep.Fire && player.crossbowController.GetArrowCount(0) == 0)
            SetBypass("Стрелы закончились. Их можно пополнить у торговца; этот шаг можно пройти позже.");
        if (!result && step == GuideStep.ArenaCombat && player.crossbowController.GetArrowCount(0) == 0)
            SetBypass("Стрелы закончились. Пополни их у торговца; учебный бой можно пропустить.");
        if (!result && step == GuideStep.Merchant && shop.IsOpen) OnAction(GuideAction.Interact);
        if (!result && step == GuideStep.Goods && shop.IsQuantityOpen && shop.SelectedItemIndex == 0) OnAction(GuideAction.Goods);
        RefreshTarget();
        if (!result && step == GuideStep.Quantity) RenderCurrent();
        PositionGuide(result ? null : targetUi, targetWorld,
            !result && (step == GuideStep.ArenaPortal || step == GuideStep.ArenaCombat || step == GuideStep.ArenaReturn
                || !controls.UseTouch && (step == GuideStep.Fire || step == GuideStep.Move || step == GuideStep.Loot || step == GuideStep.Dash || step == GuideStep.Merchant || step == GuideStep.ArenaLoot)), false);
    }
    private void BeginGuide(bool repeating)
    {
        replay = repeating; running = true; result = false; prepared = false; bypass = false;
        step = repeating ? GuideStep.Welcome : (GuideStep)Mathf.Clamp(Prefs.GetInt(Prefix + "Step", 0), 0, (int)GuideStep.ArenaFinished);
        if (!repeating && Prefs.GetInt(Prefix + "Done", 0) == 1 && step < GuideStep.ArenaPortal)
        {
            step = GuideStep.ArenaPortal;
            Prefs.SetInt(Prefix + "Result", 0);
        }
        result = !repeating && step != GuideStep.Welcome && Prefs.GetInt(Prefix + "Result", 0) == 1;
        outcome = result ? "Действие уже выполнено. Продолжим обучение." : null;
        if (step >= GuideStep.ArenaIntro && step <= GuideStep.ArenaReturn
            || step == GuideStep.ArenaPortal && result)
            RestoreArenaLesson();
        else if (campSpawn != null) MoveGuidePlayer(campSpawn.position);
        controls.ResetInput(); RenderCurrent();
    }
    public void RestartGuide()
    {
        if (!GameProgress.IsReady || YG.YG2.isPauseGame || player.IsAwaitingRevive || (waves != null && waves.IsRunning)) return;
        if (running) CleanExercise();
        shop.CloseShop(); gameUi.StartGame(); BeginGuide(true);
    }
    public void AdvanceGuide()
    {
        if (!running || Suspended) return;
        if (step == GuideStep.ArenaPortal && bypass) { EndGuide(); return; }
        if (step == GuideStep.Welcome)
        {
            if (!replay && Prefs.GetInt(Prefix + "Supplies", 0) == 0)
            {
                Prefs.SetInt(Prefix + "Supplies", 1); player.AddCoin(200);
                var bow = player.crossbowController;
                if (bow.GetArrowCount(0) < 20) bow.AddArrows(0, 20 - bow.GetArrowCount(0));
                if (bow.GetArrowCount(1) < 3) bow.AddArrows(1, 3 - bow.GetArrowCount(1));
                if (player.PotionCount < 1) player.AddPoitonHeal(1);
                if (player.MirrorCount < 1) player.AddMirorr(1);
            }
        }
        else if (step == GuideStep.Finished)
        {
            Prefs.SetInt(Prefix + "Done", 1); shop.CloseShop();
        }
        else if (step == GuideStep.ArenaIntro || step == GuideStep.ArenaBreak) { }
        else if (step == GuideStep.ArenaFinished) { EndGuide(); return; }
        else if (!result && !bypass) return;
        if (step == GuideStep.ArenaCombat && bypass) ClearArenaPractice();
        controls.ResetInput(); step++; result = false; bypass = false; outcome = null; prepared = false;
        SaveStage(); PrepareStep(); GameProgress.SaveNow();
    }
    public void SkipGuide() { if (running && !Suspended) EndGuide(); }
    private void EndGuide()
    {
        bool inArena = waves != null && (waves.IsTutorialWave || waves.ContainsArenaPoint(player.transform.position))
            || step >= GuideStep.ArenaIntro && step <= GuideStep.ArenaReturn;
        CleanExercise(); shop.CloseShop();
        if (inArena && campSpawn != null) MoveGuidePlayer(campSpawn.position);
        running = false; controls.ResetInput(); guideRoot.gameObject.SetActive(false);
        Prefs.SetInt(Prefix + "Done", 1);
        Prefs.SetInt(Prefix + "ArenaDone", 1);
        if (!replay) { Prefs.SetInt(Prefix + "Step", 0); Prefs.SetInt(Prefix + "Result", 0); }
        GameProgress.SaveNow();
    }
    private void DisableGuided()
    {
        if (guidedTutorial && running) CleanExercise();
        if (guide == this) guide = null;
        if (guideRoot != null) guideRoot.gameObject.SetActive(false);
        prepared = false;
    }
    private void EnableGuided() { if (guidedTutorial && guideRoot != null) guide = this; }
    private void CleanExercise()
    {
        if (trainingTarget != null) Destroy(trainingTarget.gameObject);
        if (trainingCoin != null) Destroy(trainingCoin.gameObject);
        trainingTarget = null; trainingCoin = null;
        ClearArenaPractice();
        if (waves != null) waves.EndTutorialWave();
        RestoreExerciseHealth(); if (player != null) player.CancelTouchActions();
    }
    private void RestoreExerciseHealth()
    {
        float original = Prefs.GetFloat(Prefix + "ExerciseHP", 0);
        if (original <= 0 || player == null) return;
        player.hp = Mathf.Min(player.maxHp, Mathf.Max(player.hp, original));
        Prefs.SetFloat(Prefix + "ExerciseHP", 0); player.SavePlayerData();
    }
    private void SaveStage()
    {
        if (replay) return;
        Prefs.SetInt(Prefix + "Step", (int)step); Prefs.SetInt(Prefix + "Result", result ? 1 : 0); GameProgress.RequestSave();
    }
    private void Complete(string text)
    {
        if (result) return;
        result = true; outcome = text;
        if (step != GuideStep.Utilities) controls.ResetInput();
        SaveStage(); RenderCurrent(); GameProgress.SaveNow();
    }
    private void SetBypass(string text) { bypass = true; outcome = text; RenderCurrent(); }
    private void PrepareStep()
    {
        prepared = true;
        if (result) { RenderCurrent(); return; }
        if (step == GuideStep.Move) { movementStart = player.transform.position; distanceMoved = 0; }
        if (step == GuideStep.Fire)
        {
            hits = 0;
            player.crossbowController.RestoreTutorialSelection();
            trainingTarget = Instantiate(trainingSlimePrefab, player.transform.position + Vector3.down * 2.5f, Quaternion.identity);
            trainingTarget.name = "TutorialSlime";
            trainingTarget.ConfigureTrainingTarget(OnTrainingHit, Mathf.Max(3, player.crossbowController.ArrowDamage(0) * 3));
        }
        if (step == GuideStep.Loot && trainingCoin == null)
        {
            trainingCoin = Instantiate(trainingCoinPrefab, player.transform.position + Vector3.down * 1.5f, Quaternion.identity);
            trainingCoin.name = "TutorialCoin";
            trainingCoin.ConfigureTrainingCoin(replay || Prefs.GetInt(Prefix + "Loot", 0) == 1 ? 0 : 10, OnTrainingLoot);
        }
        if (step == GuideStep.Heal)
        {
            if (player.PotionCount == 0 || player.hp <= 1) SetBypass("Зелье восстанавливает здоровье. Сейчас упражнение недоступно: пополни зелья у торговца.");
            else { Prefs.SetFloat(Prefix + "ExerciseHP", player.hp); player.hp = Mathf.Max(1, player.hp - 20); potionStartHp = player.hp; player.SavePlayerData(); }
        }
        if (step == GuideStep.Shield) shieldPractice = 0;
        if (step == GuideStep.Utilities && !controls.UseTouch) Complete("Зеркало возвращает в лагерь. Удерживай E до заполнения полосы; урон или отпускание прерывают перенос.");
        if (step == GuideStep.Mirror)
        {
            if (player.MirrorCount == 0) SetBypass("Зеркала закончились. Купи их у торговца. Для переноса нужно удерживать кнопку.");
            else if (controls.UseTouch) controls.ShowTutorialUtilities();
        }
        if (step == GuideStep.Merchant) player.transform.position = shop.transform.position + Vector3.down * .7f;
        if (step >= GuideStep.Goods && step <= GuideStep.Health)
        {
            if (!shop.IsOpen)
            {
                player.transform.position = shop.transform.position + Vector3.down * .7f;
                prepared = false;
                if (shop.IsPlayerInRange) { shop.OpenShopForGuide(); prepared = true; }
            }
            if (prepared && step == GuideStep.Quantity)
            {
                if (!shop.IsQuantityOpen) shop.BuyItem(0);
                purchaseArrows = player.crossbowController.GetArrowCount(0); purchaseCoins = player.totalCoins;
                if (shop.MaximumQuantity() < 2) SetBypass("Для двух наборов нужны монеты и место в запасе. Здесь выбирается количество и показана общая цена.");
            }
            if (prepared && step == GuideStep.Health)
            {
                shop.ShowUpgradesForGuide(); healthLevel = upgrades.upgradeItems[0].purchaseCount;
                if (upgrades.upgradeItems[0].ExactPrice > player.totalCoins || upgrades.upgradeItems[0].IsMaxed)
                    SetBypass("Улучшения покупаются за монеты. Здоровье увеличивает максимум HP; сейчас покупка недоступна.");
            }
        }
        if (step == GuideStep.ArenaPortal)
        {
            shop.CloseShop();
            if (arenaEntrance == null || arenaExit == null || waves == null)
                SetBypass("Портал арены не настроен. Урок можно пропустить.");
        }
        if (step == GuideStep.ArenaIntro) waves.BeginTutorialWave(0);
        if (step == GuideStep.ArenaCombat)
        {
            arenaKills = 0;
            player.crossbowController.RestoreTutorialSelection();
            waves.BeginTutorialWave(3);
            Vector3[] offsets = { new Vector3(3, 1.5f), new Vector3(4, 0), new Vector3(3, -1.5f) };
            foreach (var offset in offsets)
            {
                var enemy = Instantiate(trainingSlimePrefab, player.transform.position + offset, Quaternion.identity);
                enemy.name = "WaveZeroSlime";
                enemy.ConfigureTrainingTarget(OnArenaTrainingHit, Mathf.Max(2, player.crossbowController.ArrowDamage(0) * 2), true);
                arenaPracticeEnemies.Add(enemy);
            }
        }
        if (step == GuideStep.ArenaLoot)
        {
            waves.BeginTutorialBreak();
            if (trainingCoin == null)
            {
                trainingCoin = Instantiate(trainingCoinPrefab, player.transform.position + Vector3.right * 1.5f, Quaternion.identity);
                trainingCoin.name = "WaveZeroCoin";
                trainingCoin.ConfigureTrainingCoin(replay || Prefs.GetInt(Prefix + "ArenaLoot", 0) == 1 ? 0 : 10, OnTrainingLoot);
            }
        }
        if (step == GuideStep.ArenaBreak || step == GuideStep.ArenaReturn)
        {
            waves.BeginTutorialBreak(); returnMirrorCount = player.MirrorCount;
        }
        RenderCurrent();
    }
    private void MoveGuidePlayer(Vector3 position)
    {
        Vector3 delta = position - player.transform.position;
        player.transform.position = position;
        var body = player.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = position; body.velocity = Vector2.zero; }
        if (lessonCamera != null)
        {
            lessonCamera.OnTargetObjectWarped(player.transform, delta);
            lessonCamera.PreviousStateIsValid = false;
        }
    }
    private void RestoreArenaLesson()
    {
        if (arenaEntrance == null || arenaExit == null || waves == null) return;
        bool returned = step == GuideStep.ArenaReturn && result;
        MoveGuidePlayer(returned ? arenaEntrance.transform.position + (Vector3)arenaExit.offset
            : arenaExit.transform.position + (Vector3)arenaEntrance.offset);
        if (!returned)
        {
            waves.BeginTutorialWave(0);
            if (step >= GuideStep.ArenaLoot || step == GuideStep.ArenaCombat && result) waves.BeginTutorialBreak();
        }
    }
    private void ClearArenaPractice()
    {
        foreach (var enemy in arenaPracticeEnemies) if (enemy != null) Destroy(enemy.gameObject);
        arenaPracticeEnemies.Clear();
    }
    private void OnArenaTrainingHit(int damage, bool killed)
    {
        if (!running || step != GuideStep.ArenaCombat || !killed) return;
        arenaKills++; waves.SetTutorialEnemiesLeft(3 - arenaKills);
        if (arenaKills == 3)
        {
            waves.BeginTutorialBreak();
            Complete("Волна 0 зачищена. В обычном бою зачистка даёт MMR и начинается перерыв. Сейчас открылся бесплатный портал в лагерь.");
        }
        else RenderCurrent();
    }
    private void OnTrainingHit(int damage, bool killed)
    {
        if (!running || step != GuideStep.Fire) return;
        hits++; RenderCurrent();
        if (killed) Complete($"Цель поражена. Попаданий: {hits}. Стрелы расходуются при выстреле; урон показан над целью.");
    }
    private void OnTrainingLoot()
    {
        if (!running || step != GuideStep.Loot && step != GuideStep.ArenaLoot) return;
        string key = Prefix + (step == GuideStep.ArenaLoot ? "ArenaLoot" : "Loot");
        bool rewarded = !replay && Prefs.GetInt(key, 0) == 0;
        if (rewarded) Prefs.SetInt(key, 1);
        Complete(rewarded ? $"+10 монет. В кошельке: {player.totalCoins:N0}. Добыча нужна для покупки снаряжения." : "Добыча собирается при приближении. Повторный урок не выдаёт монеты.");
    }
    private void OnAction(GuideAction action)
    {
        if (step == GuideStep.Arrow && action == GuideAction.Arrow && player.crossbowController.SelectedArrowIndex == 1) Complete("Выбраны ядовитые стрелы. Они отравляют обычных врагов; каждый тип имеет отдельный запас.");
        else if (step == GuideStep.Dash && action == GuideAction.Dash) Complete($"Рывок выполнен. Выносливость: {player.stamina:0} / {player.maxStamina:0}; она восстанавливается сама.");
        else if (step == GuideStep.Shield && action == GuideAction.Shield)
        {
            shieldPractice += Time.deltaTime;
            if (shieldPractice >= .6f) Complete("Щит поднят. Он поглощает урон, расходует запас щита и замедляет героя.");
        }
        else if (step == GuideStep.Heal && action == GuideAction.Heal) { RestoreExerciseHealth(); Complete($"Зелье использовано. Здоровье: {potionStartHp:0} → {player.hp:0}. Осталось зелий: {player.PotionCount:N0}."); }
        else if (step == GuideStep.Utilities && action == GuideAction.Utilities) Complete("В быстрых предметах находится зеркало. Удерживай его кнопку до заполнения полосы переноса.");
        else if (step == GuideStep.Mirror && action == GuideAction.Mirror) Complete($"Ты снова в лагере. Потрачено одно зеркало; осталось: {player.MirrorCount:N0}.");
        else if (step == GuideStep.Merchant && action == GuideAction.Interact && shop.IsOpen) Complete("Торговец открыт. Во время торговли бой на паузе. Здесь есть товары, улучшения, арена, бонусы и достижения.");
        else if (step == GuideStep.Goods && action == GuideAction.Goods && shop.SelectedItemIndex == 0) Complete("Открылось окно покупки. Цена набора и общий итог видны до подтверждения.");
        else if (step == GuideStep.Quantity && action == GuideAction.Purchase) Complete($"Куплено {player.crossbowController.GetArrowCount(0) - purchaseArrows:N0} стрел за {purchaseCoins - player.totalCoins:N0} монет. Запас: {player.crossbowController.GetArrowCount(0):N0}.");
        else if (step == GuideStep.UpgradeTab && action == GuideAction.Upgrades) Complete("Открыты улучшения героя. Карточки показывают текущий параметр, прирост и цену.");
        else if (step == GuideStep.Health && action == GuideAction.Upgrade && upgrades.upgradeItems[0].purchaseCount > healthLevel) Complete($"Здоровье улучшено. Максимум: {player.maxHp:0} HP. Покупка и новый уровень сохранены.");
    }
    private void RefreshTarget()
    {
        targetUi = null; targetWorld = null;
        if (step == GuideStep.ArenaPortal) { if (!result) targetWorld = arenaEntrance != null ? arenaEntrance.transform : null; return; }
        if (step == GuideStep.ArenaIntro || step == GuideStep.ArenaBreak) { targetUi = waves != null ? waves.TutorialHud : null; return; }
        if (step == GuideStep.ArenaCombat)
        {
            float nearest = float.PositiveInfinity;
            foreach (var enemy in arenaPracticeEnemies)
                if (enemy != null && (enemy.transform.position - player.transform.position).sqrMagnitude < nearest)
                { nearest = (enemy.transform.position - player.transform.position).sqrMagnitude; targetWorld = enemy.transform; }
            if (controls.UseTouch) targetUi = controls.GuideTarget(GuideAction.Fire);
            return;
        }
        if (step == GuideStep.ArenaLoot)
        { targetWorld = trainingCoin != null ? trainingCoin.transform : null; if (controls.UseTouch && !result) targetUi = controls.GuideTarget(GuideAction.Move); return; }
        if (step == GuideStep.ArenaReturn) { if (!result) targetWorld = arenaExit != null ? arenaExit.transform : null; return; }
        if (step == GuideStep.Fire) { targetWorld = trainingTarget != null ? trainingTarget.transform : null; if (controls.UseTouch) targetUi = controls.GuideTarget(GuideAction.Fire); }
        else if (step == GuideStep.Loot) { targetWorld = trainingCoin != null ? trainingCoin.transform : null; if (controls.UseTouch) targetUi = controls.GuideTarget(GuideAction.Move); }
        else if (step == GuideStep.Merchant) targetUi = controls.GuideTarget(GuideAction.Interact);
        else if (step == GuideStep.Goods) targetUi = shop.GuideTarget(GuideAction.Goods);
        else if (step == GuideStep.Quantity) targetUi = shop.GuideTarget(GuideAction.Quantity);
        else if (step == GuideStep.UpgradeTab) targetUi = shop.GuideTarget(GuideAction.Upgrades);
        else if (step == GuideStep.Health) targetUi = upgrades.GuideTarget(0);
        else if (controls.UseTouch) targetUi = controls.GuideTarget(step switch { GuideStep.Move => GuideAction.Move, GuideStep.Arrow => GuideAction.Arrow, GuideStep.Dash => GuideAction.Dash, GuideStep.Shield => GuideAction.Shield, GuideStep.Heal => GuideAction.Heal, GuideStep.Utilities => GuideAction.Utilities, GuideStep.Mirror => GuideAction.Mirror, _ => GuideAction.Move });
        else if (step == GuideStep.Arrow) targetUi = desktopEquipment;
    }
    private void RenderCurrent()
    {
        bool touch = controls.UseTouch;
        guideLastPlatformTouch = touch;
        string[] titles = { "Добро пожаловать!", "Движение", "Первый бой", "Собери добычу", "Выбор стрел", "Рывок", "Защита", "Зелье лечения", "Быстрые предметы", "Возвращение в лагерь", "Познакомься с торговцем", "Покупка стрел", "Количество и цена", "Улучшения героя", "Стань сильнее", "Теперь на арену!",
            "Портал на арену", "Учебная волна 0", "Зачисти волну", "Добыча после боя", "Между волнами", "Бесплатно в лагерь", "Обучение завершено!" };
        string body = outcome ?? (step switch
        {
            GuideStep.Welcome => replay ? "Повторим управление, магазин и учебную волну 0. Припасы повторно не выдаются; покупки расходуют твои монеты." : "Покажу лагерь, управление и арену. Для первого урока получишь 200 монет и недостающие припасы. Обычные волны пока ждут.",
            GuideStep.Move => touch ? "Потяни левый джойстик и пройди немного. Герой движется в выбранную сторону." : "Нажми W, A, S или D и пройди немного. Движение помогает уходить от врагов.",
            GuideStep.Fire => (touch ? "Нажми «Огонь» и отпусти: автоприцел выберет цель. Перетаскивание кнопки задаёт направление." : "Наведи мышь на учебного слайма и нажми левую кнопку мыши.") + $"\nПопадания: {hits} / 3. Цель не атакует.",
            GuideStep.Loot => touch ? "Джойстиком подойди к золотой монете. Добыча пополняет кошелёк." : "Подойди к золотой монете с помощью WASD. Добыча пополняет кошелёк.",
            GuideStep.Arrow => touch ? "Нажми зелёную стрелу. Тип меняет эффект выстрела; количество указано под иконкой." : "Нажимай Q до выбора ядовитых стрел. Тип и запас показаны слева вверху.",
            GuideStep.Dash => touch ? "Нажми «Рывок». Он быстро перемещает героя и тратит выносливость." : "Направь мышь в нужную сторону и нажми Shift. Рывок тратит выносливость.",
            GuideStep.Shield => touch ? "Нажми «Щит», чтобы поднять его. Повторное нажатие опускает щит." : "Удерживай Space, чтобы поднять щит. Он поглощает урон и замедляет движение.",
            GuideStep.Heal => touch ? "Для упражнения немного снизилось HP. Нажми кнопку зелья — здоровье восстановится." : "Для упражнения немного снизилось HP. Нажми H — зелье восстановит здоровье.",
            GuideStep.Utilities => "Нажми «•••», чтобы открыть быстрые предметы. Здесь находится зеркало переноса.",
            GuideStep.Mirror => touch ? "Удерживай кнопку зеркала до заполнения полоски. Это вернёт тебя в лагерь." : "Удерживай E до заполнения полоски. Это вернёт тебя в лагерь.",
            GuideStep.Merchant => touch ? "Нажми плашку «Торговец». Магазин откроется, бой остановится." : "Нажми плашку «Торговец» мышкой или клавишу F. Магазин откроется на паузе.",
            GuideStep.Goods => "Нажми цену обычных стрел. Откроется выбор количества наборов.",
            GuideStep.Quantity => shop.SelectedQuantity == 2 ? $"Выбрано 2 набора: {shop.SelectedAmount:N0} стрел за {shop.SelectedPrice:N0} монет. Нажми «Купить»; деньги спишутся, стрелы пополнятся."
                : $"Выбери 2 набора ползунком или кнопкой «+». Сейчас: {shop.SelectedQuantity:N0} набор(а), {shop.SelectedAmount:N0} стрел за {shop.SelectedPrice:N0} монет.",
            GuideStep.UpgradeTab => "Нажми вкладку «Улучшения». Здесь повышаются характеристики героя и оружия.",
            GuideStep.Health => "Нажми цену в карточке «Здоровье». Покупка увеличит максимум HP; новый уровень сохранится.",
            GuideStep.Finished => "Лагерь освоен. Теперь покажу настоящий портал на арену, учебную волну 0 и бесплатный возврат между волнами.",
            GuideStep.ArenaPortal => touch ? "Иди джойстиком к отмеченному порталу «АРЕНА» и зайди внутрь. Указатель у края экрана ведёт к нему." : "Иди по указателю с помощью WASD к порталу «АРЕНА» и зайди внутрь. Нажимать F не нужно.",
            GuideStep.ArenaIntro => "Ты на боевой арене. Сверху — номер волны и её состояние. В волне 0 встретишь трёх слаймов; урок безопасен и не даёт MMR. Начнём?",
            GuideStep.ArenaCombat => (touch ? "Двигайся джойстиком и стреляй кнопкой «Огонь»." : "Двигайся WASD, целься мышью и стреляй ЛКМ.") + $" Слаймы приближаются: отходи от них.\nПобеждено: {arenaKills} / 3.",
            GuideStep.ArenaLoot => touch ? "После зачистки подойди джойстиком к монете. Добыча нужна для улучшений и снаряжения." : "После зачистки подойди к монете с помощью WASD. Добыча нужна для улучшений и снаряжения.",
            GuideStep.ArenaBreak => "Между волнами идёт перерыв: портал «В ЛАГЕРЬ» открыт. Войди в него — зеркало не потратится. В обычной игре успей до конца таймера; учебный перерыв ждёт тебя.",
            GuideStep.ArenaReturn => (touch ? "Иди джойстиком" : "Иди с помощью WASD") + " в отмеченный портал «В ЛАГЕРЬ». Это бесплатный выход во время перерыва. Во время боя портал закрыт — для отступления нужно зеркало.",
            _ => "Ты знаешь путь на арену и бесплатный выход между волнами. Снова войди в портал «АРЕНА» — начнётся настоящая волна 1. Повторить обучение можно в меню."
        });
        bool explanation = step == GuideStep.Welcome || step == GuideStep.Finished || step == GuideStep.ArenaIntro || step == GuideStep.ArenaBreak || step == GuideStep.ArenaFinished;
        string counter = step >= GuideStep.ArenaPortal ? $"АРЕНА · {(int)step - (int)GuideStep.ArenaPortal + 1} / 7" : step == GuideStep.Welcome || step == GuideStep.Finished ? "НАСТАВНИК ЛАГЕРЯ" : $"ЛАГЕРЬ · {(int)step} / 14";
        RenderGuide(titles[(int)step], body, counter,
            step == GuideStep.Welcome ? "Начать обучение" : step == GuideStep.Finished ? "Покажи портал" : step == GuideStep.ArenaIntro ? "Начать волну 0" : step == GuideStep.ArenaFinished ? "Готово" : bypass ? "Пропустить шаг" : "Дальше",
            explanation || result || bypass, step == GuideStep.Welcome || step == GuideStep.Finished || step == GuideStep.ArenaFinished);
    }
}
