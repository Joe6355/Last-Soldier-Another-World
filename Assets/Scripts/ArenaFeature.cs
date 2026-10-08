using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Upgrade = Beka.UpgradeItemType;
using PlayerPrefs = RedefineYG.PlayerPrefs;

// Один компонент для точек гарнизона; волны, покупки и урон используют существующие системы.
[DefaultExecutionOrder(600)]
public sealed class ArenaFeature : MonoBehaviour
{
    public enum FeatureKind { Outpost, Tower, GravityRune, Roots, Altar, Barrel, Mushroom, Barricade }

    [Header("Механика и связи")]
    [SerializeField] private FeatureKind kind;
    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private PlayerController player;
    [SerializeField] private Beka upgrades;
    [SerializeField] private int slotIndex;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform bowVisual;

    [Header("Отображение в мире")]
    [SerializeField] private GameObject visual;
    [SerializeField] private LineRenderer zoneRing;
    [SerializeField] private LineRenderer progressArc;
    [SerializeField] private TextMeshPro caption;
    [SerializeField] private Collider2D obstacle;
    [SerializeField] private float radius = 1.5f;
    [SerializeField] private int environmentHealth = 6;

    private readonly List<MonoBehaviour> targets = new List<MonoBehaviour>();
    private readonly Dictionary<MonoBehaviour, Vector3> roots = new Dictionary<MonoBehaviour, Vector3>();
    private readonly List<MonoBehaviour> expiredRoots = new List<MonoBehaviour>();
    private bool selected, claimed, placed, spent, destroyed;
    private float capture, cooldown, effectTime, warningTime, tickTime, flashTime;
    private int remainingHealth;
    private string lastCaption;
    private static readonly Color Gold = new Color32(227, 186, 101, 255);
    private static readonly Color Violet = new Color32(166, 130, 219, 255);
    private static readonly Color Green = new Color32(126, 191, 119, 255);
    private static readonly Color Danger = new Color32(225, 111, 72, 255);

    public FeatureKind Kind => kind;
    public int SlotIndex => slotIndex;
    public float CaptureProgress => capture;
    public bool IsClaimed => claimed;
    public bool IsPlaced => placed;
    public bool IsSpent => spent;
    public bool IsDestroyed => destroyed;
    public float EffectRemaining => effectTime;
    public float CaptureDuration => CaptureSeconds(Level(Upgrade.OutpostCapture));
    public int CaptureReward => Reward(Level(Upgrade.OutpostReward));
    public float EffectRadius => kind == FeatureKind.GravityRune ? RuneRadius(Level(Upgrade.RuneRadius))
        : kind == FeatureKind.Tower ? TowerRange(Level(Upgrade.TowerRange))
        : kind == FeatureKind.Roots ? RootRadius(Level(Upgrade.Roots)) : radius;

    public static int Level(Upgrade type, Beka.UpgradeItem[] items)
    {
        if (items != null)
            foreach (var item in items)
                if (item != null && item.itemType == type)
                    return Mathf.Clamp(item.purchaseCount, 0, item.maxPurchases > 0 ? item.maxPurchases : 10);
        return 0;
    }
    private int Level(Upgrade type) => Level(type, upgrades != null ? upgrades.upgradeItems : null);
    public static int Reward(int level) => 40 + 20 * level;
    public static float CaptureSeconds(int level) => Mathf.Max(3f, 8f - .75f * level);
    public static float TowerRange(int level) => 4.5f + .5f * level;
    public static float TowerInterval(int level) => 1.6f / (1f + .2f * level);
    public static float RuneRadius(int level) => 2.4f + .3f * level;
    public static float RuneDuration(int level) => 1.8f + .3f * level;
    public static float RootRadius(int level) => 1.5f + .15f * level;
    public static float AltarCooldown(int level) => Mathf.Max(10f, 25f - 2f * level);

    public static int UpgradeGroup(Upgrade type)
    {
        if (type == Upgrade.OutpostReward || type == Upgrade.OutpostCapture) return 0;
        if (type == Upgrade.TowerPower || type == Upgrade.TowerRange || type == Upgrade.TowerRate) return 1;
        if (type == Upgrade.RunePower || type == Upgrade.RuneRadius || type == Upgrade.RuneDuration) return 2;
        return 3;
    }
    public static bool CanUpgrade(Upgrade type, Beka.UpgradeItem[] items)
    {
        if (type == Upgrade.TowerRange || type == Upgrade.TowerRate) return Level(Upgrade.TowerPower, items) > 0;
        if (type == Upgrade.RuneRadius || type == Upgrade.RuneDuration) return Level(Upgrade.RunePower, items) > 0;
        return true;
    }
    public static string UpgradeSummary(Upgrade type, int level, int max, Beka.UpgradeItem[] items)
    {
        int next = Mathf.Min(level + 1, max);
        string detail;
        switch (type)
        {
            case Upgrade.OutpostReward: detail = $"{Reward(level)} → {Reward(next)} монет за захват"; break;
            case Upgrade.OutpostCapture: detail = $"{CaptureSeconds(level):0.##} → {CaptureSeconds(next):0.##} с удержания"; break;
            case Upgrade.TowerPower: detail = level == 0 ? "Построить арбалетную башню\nВторая башня на уровне 3"
                : $"Урон {2 + level * 2} → {2 + next * 2}\nБашен: {(level >= 3 ? 2 : 1)} / 2"; break;
            case Upgrade.TowerRange: detail = $"Дальность {TowerRange(level):0.#} → {TowerRange(next):0.#}"; break;
            case Upgrade.TowerRate: detail = $"Выстрел: {TowerInterval(level):0.##} → {TowerInterval(next):0.##} с"; break;
            case Upgrade.RunePower: detail = level == 0 ? "Открыть руну притяжения\nR: поставить · 1 за волну"
                : $"Взрыв: {4 + level * 3} → {4 + next * 3} урона\nОдна руна за волну"; break;
            case Upgrade.RuneRadius: detail = $"Радиус {RuneRadius(level):0.#} → {RuneRadius(next):0.#}"; break;
            case Upgrade.RuneDuration: detail = $"Стяжка {RuneDuration(level):0.#} → {RuneDuration(next):0.#} с"; break;
            case Upgrade.Roots: detail = level == 0 ? "Открыть две ловушки из корней\nУдержание и шипы" : $"Удержание {1 + level * .4f:0.#} → {1 + next * .4f:0.#} с\nРадиус {RootRadius(level):0.##} → {RootRadius(next):0.##}\nШипы: {level} → {next} урона"; break;
            case Upgrade.Altar: detail = level == 0 ? "Восстановить алтарь\nСтой рядом 2 с для лечения" : $"Лечение {15 + level * 5} → {15 + next * 5} HP\nВосстановление {AltarCooldown(level):0} → {AltarCooldown(next):0} с"; break;
            default: return "";
        }
        if (!CanUpgrade(type, items)) detail += type == Upgrade.TowerRange || type == Upgrade.TowerRate
            ? "\nСначала построй башню" : "\nСначала открой руну";
        return $"{detail}\nУровень {level} / {max}";
    }

    private void Start() { if (remainingHealth == 0) remainingHealth = environmentHealth; RefreshVisuals(); }

    public void BeginWave(bool activeOutpost, bool restore)
    {
        selected = activeOutpost;
        capture = kind == FeatureKind.Outpost && selected && restore ? Mathf.Clamp(PlayerPrefs.GetFloat("ArenaOutpostCapture", 0), 0, CaptureDuration) : 0;
        claimed = kind == FeatureKind.Outpost && selected && restore && PlayerPrefs.GetInt("ArenaOutpostClaimed", 0) == 1;
        spent = kind == FeatureKind.GravityRune && restore && PlayerPrefs.GetInt("ArenaRuneSpent", 0) == 1;
        placed = false;
        destroyed = false;
        remainingHealth = environmentHealth;
        cooldown = effectTime = warningTime = tickTime = flashTime = 0;
        roots.Clear();
        if (kind == FeatureKind.GravityRune && restore && !spent && PlayerPrefs.GetInt("ArenaRunePlaced", 0) == 1)
            TryPlaceRune(new Vector2(PlayerPrefs.GetFloat("ArenaRuneX"), PlayerPrefs.GetFloat("ArenaRuneY")), true);
        RefreshVisuals();
    }

    public void EndRun()
    {
        selected = placed = spent = claimed = destroyed = false;
        capture = cooldown = effectTime = warningTime = tickTime = flashTime = 0;
        remainingHealth = environmentHealth;
        roots.Clear();
        RefreshVisuals();
    }

    public void SaveCheckpoint()
    {
        if (kind == FeatureKind.Outpost && selected)
        {
            PlayerPrefs.SetInt("ArenaOutpostClaimed", claimed ? 1 : 0);
            PlayerPrefs.SetFloat("ArenaOutpostCapture", capture);
        }
        if (kind == FeatureKind.GravityRune)
        {
            PlayerPrefs.SetInt("ArenaRuneSpent", spent ? 1 : 0);
            PlayerPrefs.SetInt("ArenaRunePlaced", placed && !spent ? 1 : 0);
            PlayerPrefs.SetFloat("ArenaRuneX", transform.position.x);
            PlayerPrefs.SetFloat("ArenaRuneY", transform.position.y);
        }
    }

    private bool CanAct => spawner != null && spawner.IsCombatActive && player != null && player.hp > 0 && !player.IsAwaitingRevive
        && GameProgress.IsReady && Time.timeScale > 0 && !YG.YG2.isPauseGame;
    private bool Near(float distance) => player != null && Vector2.Distance(player.transform.position, transform.position) <= distance;

    private void Update()
    {
        if (CanAct)
        {
            float dt = Time.deltaTime;
            cooldown = Mathf.Max(0, cooldown - dt);
            flashTime = Mathf.Max(0, flashTime - dt);
            switch (kind)
            {
                case FeatureKind.Outpost:
                    if (selected && !claimed && Near(radius))
                    {
                        capture = Mathf.Min(CaptureDuration, capture + dt);
                        if (capture >= CaptureDuration)
                        {
                            claimed = true;
                            flashTime = 2f;
                            player.AddCoin(CaptureReward);
                            spawner.SaveCheckpoint();
                        }
                    }
                    break;
                case FeatureKind.Tower: UpdateTower(); break;
                case FeatureKind.GravityRune: UpdateRune(dt); break;
                case FeatureKind.Roots: UpdateRoots(dt); break;
                case FeatureKind.Altar:
                    if (Level(Upgrade.Altar) > 0 && cooldown <= 0 && Near(radius) && player.hp < player.maxHp)
                    {
                        capture += dt;
                        if (capture >= 2f) { player.HealFromArena(15 + Level(Upgrade.Altar) * 5); capture = 0; cooldown = AltarCooldown(Level(Upgrade.Altar)); }
                    }
                    else capture = 0;
                    break;
                case FeatureKind.Barrel:
                case FeatureKind.Mushroom: UpdateEnvironment(dt); break;
            }
        }
        RefreshVisuals();
    }

    private bool TowerBuilt => Level(Upgrade.TowerPower) >= (slotIndex == 0 ? 1 : 3);
    private void UpdateTower()
    {
        if (!TowerBuilt || cooldown > 0 || arrowPrefab == null || muzzle == null) return;
        spawner.FillArenaTargets(targets, transform.position, EffectRadius);
        MonoBehaviour target = null;
        float closest = float.MaxValue;
        foreach (var enemy in targets)
        {
            float distance = ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude;
            if (distance < closest) { closest = distance; target = enemy; }
        }
        if (target == null) return;
        Vector2 direction = (target.transform.position - muzzle.position).normalized;
        if (bowVisual != null) bowVisual.up = direction;
        var arrow = Instantiate(arrowPrefab, muzzle.position, Quaternion.FromToRotation(Vector3.up, direction));
        var hit = arrow.GetComponent<ArrowDef>();
        hit.damage = 2 + Level(Upgrade.TowerPower) * 2;
        // Триггерный путь общего снаряда также поддерживает обоих боссов.
        hit.maxEnemyHits = 1;
        hit.triggerProjectile = true;
        var collider = arrow.GetComponent<Collider2D>();
        if (collider != null) collider.isTrigger = true;
        var body = arrow.GetComponent<Rigidbody2D>();
        if (body != null) body.velocity = direction * 9f;
        cooldown = TowerInterval(Level(Upgrade.TowerRate));
        flashTime = .12f;
    }

    public bool TryPlaceRune(Vector2 point, bool restore = false)
    {
        if (kind != FeatureKind.GravityRune || Level(Upgrade.RunePower) == 0 || spent || placed) return false;
        if (!restore && (!CanAct || Vector2.Distance(point, player.transform.position) > 3f)) return false;
        if (spawner == null || !spawner.ContainsArenaPoint(point, .45f)) return false;
        var hits = Physics2D.OverlapCircleAll(point, .25f);
        foreach (var hit in hits)
            if (!hit.isTrigger && ArrowDef.FindEnemy(hit) == null && hit.GetComponentInParent<PlayerController>() == null) return false;
        transform.position = new Vector3(point.x, point.y, transform.position.z);
        placed = true;
        if (!restore) { SaveCheckpoint(); GameProgress.RequestSave(); }
        RefreshVisuals();
        return true;
    }

    private void UpdateRune(float dt)
    {
        if (!placed && !spent && Level(Upgrade.RunePower) > 0 && Input.GetKeyDown(KeyCode.R) && Camera.main != null)
            TryPlaceRune(Camera.main.ScreenToWorldPoint(Input.mousePosition));
        if (!placed) return;
        if (!spent)
        {
            spawner.FillArenaTargets(targets, transform.position, .8f);
            if (targets.Count > 0)
            {
                spent = true;
                effectTime = RuneDuration(Level(Upgrade.RuneDuration));
                SaveCheckpoint();
                GameProgress.RequestSave();
            }
        }
        else if (effectTime > 0)
        {
            effectTime = Mathf.Max(0, effectTime - dt);
            if (effectTime == 0) { DamageArea(4 + Level(Upgrade.RunePower) * 3, EffectRadius, false); flashTime = .35f; }
        }
        else if (flashTime <= 0) placed = false;
    }

    private void UpdateRoots(float dt)
    {
        if (Level(Upgrade.Roots) == 0) return;
        if (effectTime > 0)
        {
            effectTime = Mathf.Max(0, effectTime - dt);
            if (effectTime == 0) { roots.Clear(); cooldown = 8f; }
        }
        else if (cooldown <= 0)
        {
            spawner.FillArenaTargets(targets, transform.position, EffectRadius);
            if (targets.Exists(enemy => !(enemy is Roberto) && !(enemy is SlimeBoss)))
            {
                effectTime = 1f + Level(Upgrade.Roots) * .4f;
                DamageArea(Level(Upgrade.Roots), EffectRadius, false);
            }
        }
    }

    private void LateUpdate()
    {
        if (!CanAct || effectTime <= 0 || kind != FeatureKind.Roots && kind != FeatureKind.GravityRune) return;
        spawner.FillArenaTargets(targets, transform.position, EffectRadius);
        foreach (var enemy in targets)
        {
            if (enemy is Roberto || enemy is SlimeBoss) continue;
            Vector3 point;
            if (kind == FeatureKind.Roots)
            {
                if (!roots.TryGetValue(enemy, out point)) roots.Add(enemy, point = enemy.transform.position);
            }
            else point = Vector3.MoveTowards(enemy.transform.position, transform.position, 5f * Time.deltaTime);
            var body = enemy.GetComponent<Rigidbody2D>();
            if (body != null) { body.velocity = Vector2.zero; body.position = point; }
            enemy.transform.position = new Vector3(point.x, point.y, enemy.transform.position.z);
        }
        if (kind == FeatureKind.Roots)
        {
            expiredRoots.Clear();
            foreach (var entry in roots) if (entry.Key == null || !targets.Contains(entry.Key)) expiredRoots.Add(entry.Key);
            foreach (var enemy in expiredRoots) roots.Remove(enemy);
        }
    }

    public void TakeEnvironmentDamage(int amount)
    {
        if (!CanAct || destroyed || warningTime > 0 || amount <= 0
            || kind != FeatureKind.Barrel && kind != FeatureKind.Mushroom && kind != FeatureKind.Barricade) return;
        remainingHealth -= amount;
        flashTime = .15f;
        if (remainingHealth > 0) return;
        if (obstacle != null) obstacle.enabled = false;
        if (kind == FeatureKind.Barricade) destroyed = true;
        else warningTime = .7f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var arrow = collision.gameObject.GetComponent<ArrowDef>();
        if (arrow != null) TakeEnvironmentDamage(arrow.damage);
    }

    private void UpdateEnvironment(float dt)
    {
        if (warningTime > 0)
        {
            warningTime = Mathf.Max(0, warningTime - dt);
            if (warningTime == 0)
            {
                destroyed = true;
                if (kind == FeatureKind.Barrel) { DamageArea(18, radius, true); flashTime = .4f; }
                else { effectTime = 6f; tickTime = 0; }
            }
        }
        if (kind == FeatureKind.Mushroom && effectTime > 0)
        {
            effectTime = Mathf.Max(0, effectTime - dt);
            tickTime -= dt;
            if (tickTime <= 0) { tickTime = .5f; DamageArea(2, radius, true); }
        }
    }

    private void DamageArea(int amount, float distance, bool friendlyFire)
    {
        spawner.FillArenaTargets(targets, transform.position, distance);
        foreach (var enemy in targets) ArrowDef.DamageEnemy(enemy, amount);
        if (friendlyFire && Near(distance)) player.TakeDamage(kind == FeatureKind.Barrel ? 8 : 1);
    }

    private void RefreshVisuals()
    {
        bool running = spawner != null && spawner.IsRunning;
        bool show = kind == FeatureKind.Outpost ? running && selected
            : kind == FeatureKind.Tower ? running && TowerBuilt
            : kind == FeatureKind.GravityRune ? running && placed
            : kind == FeatureKind.Roots ? running && Level(Upgrade.Roots) > 0
            : kind == FeatureKind.Altar ? running && Level(Upgrade.Altar) > 0 : !destroyed;
        if (visual != null && visual.activeSelf != show) visual.SetActive(show);
        if (obstacle != null) obstacle.enabled = !destroyed && warningTime <= 0;
        Color color = kind == FeatureKind.GravityRune ? Violet : kind == FeatureKind.Roots || kind == FeatureKind.Mushroom ? Green
            : kind == FeatureKind.Barrel ? Danger : Gold;
        bool ring = running && (show || warningTime > 0 || effectTime > 0 || flashTime > 0)
            && (kind != FeatureKind.Tower || Near(1.8f)) && kind != FeatureKind.Barricade;
        if (kind == FeatureKind.Barrel || kind == FeatureKind.Mushroom)
            ring &= warningTime > 0 || effectTime > 0 || flashTime > 0;
        float ringRadius = kind == FeatureKind.GravityRune && !spent ? .8f : EffectRadius;
        DrawRing(zoneRing, ringRadius, 1, ring, new Color(color.r, color.g, color.b, claimed ? .25f : .65f));
        float progress = kind == FeatureKind.Outpost ? claimed ? 1f : capture / CaptureDuration
            : kind == FeatureKind.Altar ? capture / 2f
            : effectTime > 0 ? effectTime / (kind == FeatureKind.GravityRune ? RuneDuration(Level(Upgrade.RuneDuration))
                : kind == FeatureKind.Roots ? 1 + Level(Upgrade.Roots) * .4f : 6f)
            : warningTime > 0 ? 1 - warningTime / .7f : 0;
        DrawRing(progressArc, ringRadius + .07f, progress, ring && progress > 0 && !(kind == FeatureKind.Outpost && claimed), color);
        string text = "";
        if (running && show)
        {
            if (kind == FeatureKind.Outpost) text = claimed ? flashTime > 0 ? $"+{CaptureReward} монет" : "Аванпост захвачен" : $"Аванпост · {Mathf.FloorToInt(capture)} / {CaptureDuration:0.#} с";
            else if (kind == FeatureKind.GravityRune) text = !spent ? "Руна · ждёт врага" : effectTime > 0 ? "Стяжка" : "";
            else if (Near(2f)) text = kind == FeatureKind.Tower ? "Арбалетная башня"
                : kind == FeatureKind.Roots ? cooldown > 0 ? $"Корни · {Mathf.CeilToInt(cooldown)} с" : "Корни"
                : kind == FeatureKind.Altar ? cooldown > 0 ? $"Алтарь · {Mathf.CeilToInt(cooldown)} с" : "Алтарь · лечение за 2 с" : "";
        }
        if (warningTime > 0) text = kind == FeatureKind.Barrel ? "Отойди! Взрыв" : "Отойди! Яд";
        if (caption != null)
        {
            if (lastCaption != text) { caption.text = text; lastCaption = text; }
            caption.gameObject.SetActive(text.Length > 0);
        }
    }

    private static void DrawRing(LineRenderer line, float distance, float progress, bool visible, Color color)
    {
        if (line == null) return;
        line.enabled = visible;
        if (!visible) return;
        line.startColor = line.endColor = color;
        const int segments = 64;
        int count = Mathf.Max(2, Mathf.CeilToInt(segments * Mathf.Clamp01(progress)) + 1);
        line.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 2 * i / segments;
            line.SetPosition(i, new Vector3(Mathf.Sin(angle) * distance, Mathf.Cos(angle) * distance, 0));
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = kind == FeatureKind.GravityRune ? Violet : Gold;
        Gizmos.DrawWireSphere(transform.position, EffectRadius);
    }
}
