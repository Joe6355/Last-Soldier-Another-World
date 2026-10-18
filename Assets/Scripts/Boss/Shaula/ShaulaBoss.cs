using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ShaulaBoss : Sounds
{
    public enum Ability { Laser, Grasp, Slam, Stars, Pursuit, Sting, Needles }
    public enum Pose { Idle, Walk, Grasp, Slam, Laser, Sting, Stars, Needles, TailBreak, TailRegrow, Rage, Defeat }
    [Serializable] public sealed class Motion
    {
        public Sprite[] normal, crimson;
        public float[] durations;
        public bool loop;
    }
    [Header("Approved concept presentation")]
    [SerializeField] private SpriteRenderer bodySprite;
    [SerializeField] private Transform visual;
    [SerializeField] private Motion[] motions;
    [SerializeField] private AudioClip[] effects;
    [SerializeField] private AudioClip musicNormal, musicCrimson;
    [SerializeField] private ShaulaHitZone tailZone;
    [Header("Base balance, before wave reinforcement")]
    [SerializeField, Min(1)] private float maxHealth = 720;
    [SerializeField, Min(1)] private float maxTailHealth = 90;
    [SerializeField, Min(.1f)] private float moveSpeed = 1.65f;
    [SerializeField, Range(.1f, 1)] private float armorMultiplier = .6f;
    [SerializeField, Min(0)] private int coinReward = 1000;
    private float health, tailHealth, damageScale = 1, shotSpeedScale = 1;
    private float tailBrokenRemaining, vulnerableRemaining, poseTime, stepTimer;
    private bool crimson, ragePending, defeating, rewarded, configured;
    private int decision;
    private Pose pose;
    private Ability lastAbility = Ability.Grasp;
    private readonly float[] abilityReady = new float[7];
    private float fightClock;
    private WaveSpawner owner;
    private PlayerController player;
    private Ui gameUi;
    private GameObject healthPanel;
    private Image healthFill, tailFill;
    private TMP_Text title, status;
    private Vector3 tailRestPosition;
    private static readonly Vector2[] laserMuzzles = { new Vector2(.645f, .156f), new Vector2(.516f, .269f),
        new Vector2(.512f, .251f), new Vector2(.573f, .298f), new Vector2(.496f, .291f), new Vector2(.698f, .158f) };
    public bool IsAlive => configured && health > 0 && !defeating;
    public bool TailIntact { get; private set; } = true;
    public bool TailExposed { get; private set; }
    public bool IsCrimson => crimson;
    public bool IsDefeating => defeating;
    public bool VictoryAwarded => rewarded;
    public float Health => health;
    public float MaximumHealth => maxHealth;
    public float TailHealth => tailHealth;
    public Ability CurrentAbility { get; private set; }
    public bool IsAttacking { get; private set; }
    private bool CanSimulate => configured && owner != null && owner.IsCombatActive && GameProgress.IsReady
        && Time.timeScale > 0 && !YG.YG2.isPauseGame && player != null && player.hp > 0 && !player.IsAwaitingRevive;
    private bool CanAct => CanSimulate && IsAlive;
    private float Tempo => crimson ? 1.55f : 1f;
    private float MovementTempo => crimson ? 1.5f : 1f;
    private int StarCount => crimson ? 16 : 8;
    private float StarRadius => 1.15f;
    private int NeedleCount => crimson ? 14 : 8;
    private int PursuitCount => crimson ? 4 : 3;
    private Vector2 PlayerPoint => player != null ? (Vector2)player.transform.position : (Vector2)transform.position;
    private Vector2 Center => rigReady ? (Vector2)visual.position : (Vector2)transform.position + new Vector2(0, .65f) * transform.localScale.y;
    private Vector2 TailPoint => tailZone != null ? (Vector2)tailZone.transform.position : Center + Vector2.up;
    private float BodyRadius => 1.2f * transform.localScale.y;

    public void Configure(WaveSpawner wave, float healthMultiplier, float damageMultiplier, float movementMultiplier,
        float projectileMultiplier, GameObject panel, Image hpFill, Image stingFill, TMP_Text nameText, TMP_Text stateText)
    {
        if (configured) return;
        configured = true; owner = wave;
        maxHealth *= healthMultiplier; maxTailHealth *= healthMultiplier;
        health = maxHealth; tailHealth = maxTailHealth;
        damageScale = damageMultiplier; moveSpeed *= movementMultiplier; shotSpeedScale = projectileMultiplier;
        player = FindObjectOfType<PlayerController>(); gameUi = FindObjectOfType<Ui>();
        var hero = player != null ? player.GetComponent<SpriteRenderer>() : null;
        if (hero != null && bodySprite != null) bodySprite.sortingOrder = hero.sortingOrder - 4;
        healthPanel = panel; healthFill = hpFill; tailFill = stingFill; title = nameText; status = stateText;
        if (tailZone != null) tailRestPosition = tailZone.transform.localPosition;
    }

    private void OnEnable()
    {
        if (!configured) return; // Prefab remains dormant until its arena configures it.
        BuildRig();
        LayoutBossHud();
        if (healthPanel != null) healthPanel.SetActive(true);
        GameLocalization.Changed += RefreshHud;
        SetPose(Pose.Idle); RefreshHud();
        if (gameUi != null) gameUi.SetBossMusic(musicNormal);
        StartCoroutine(Encounter());
    }

    private void Update()
    {
        LayoutBossHud();
        audioSrc.mute = Ui.sfxVolume <= 0;
        if (!CanSimulate) { if (audioSrc.isPlaying) { audioSrc.Pause(); soundPaused = true; } return; }
        if (soundPaused) { audioSrc.UnPause(); soundPaused = false; }
        poseTime += Time.deltaTime;
        if (IsAlive)
        {
            fightClock += Time.deltaTime;
            vulnerableRemaining = Mathf.Max(0, vulnerableRemaining - Time.deltaTime);
            if (!TailIntact) tailBrokenRemaining = Mathf.Max(0, tailBrokenRemaining - Time.deltaTime);
        }
        RenderPose();
    }
    private bool soundPaused;

    private IEnumerator Encounter()
    {
        Sound("arrival", .75f); Burst(Center, 22, 1.4f); yield return Wait(.75f);
        while (IsAlive)
        {
            if (!CanAct) { yield return null; continue; }
            if (ragePending)
            {
                ragePending = false; crimson = true;
                SetPose(Pose.Rage); Sound("rage", .8f); Burst(Center, 28, 1.6f);
                if (gameUi != null) gameUi.SetBossMusic(musicCrimson);
                RefreshHud(); yield return Wait(1.05f);
                continue;
            }
            if (!TailIntact && tailBrokenRemaining <= 0)
            {
                yield return RegrowTail(); continue;
            }
            Ability? next = ChooseAbility(Vector2.Distance(Center, PlayerPoint));
            if (!next.HasValue)
            {
                if (pose != Pose.Walk) SetPose(Pose.Walk);
                Face(PlayerPoint);
                yield return Wait(.35f, _ => MoveTowards(PlayerPoint, moveSpeed * (TailIntact ? 1 : 1.3f) * MovementTempo));
                continue;
            }
            CurrentAbility = lastAbility = next.Value; IsAttacking = true;
            Face(PlayerPoint);
            yield return Perform(next.Value);
            IsAttacking = false; TailExposed = false;
            abilityReady[(int)next.Value] = fightClock + Cooldown(next.Value) / Tempo;
            SetPose(Pose.Walk);
            yield return Wait(crimson ? .12f : .3f, _ =>
            {
                if (Vector2.Distance(Center, PlayerPoint) > BodyRadius + 1.5f)
                { Face(PlayerPoint); MoveTowards(PlayerPoint, moveSpeed * MovementTempo); }
            });
        }
    }

    private Ability? ChooseAbility(float distance)
    {
        if (distance > 8.5f) return null;
        Ability[] choices = distance > 5f ? new[] { Ability.Laser, Ability.Stars, Ability.Needles, Ability.Pursuit }
            : distance > 2.7f ? new[] { Ability.Sting, Ability.Pursuit, Ability.Grasp, Ability.Laser }
            : new[] { Ability.Slam, Ability.Grasp, Ability.Pursuit };
        for (int i = 0; i < choices.Length; i++)
        {
            Ability value = choices[(decision + i) % choices.Length];
            if (!TailIntact && UsesTail(value) || abilityReady[(int)value] > fightClock || value == lastAbility) continue;
            decision++; return value;
        }
        return null;
    }
    private static bool UsesTail(Ability ability) => ability == Ability.Laser || ability == Ability.Stars
        || ability == Ability.Sting || ability == Ability.Needles;
    private static float Cooldown(Ability ability) => ability == Ability.Stars ? 11 : ability == Ability.Pursuit ? 9
        : ability == Ability.Laser ? 8 : ability == Ability.Slam ? 4 : 6;

    private IEnumerator Wait(float seconds, Action<float> step = null, bool requireTail = false)
    {
        float age = 0;
        while (age < seconds)
        {
            if (requireTail && !TailIntact) yield break;
            if (CanSimulate) { age += Time.deltaTime; step?.Invoke(Mathf.Clamp01(age / seconds)); }
            yield return null;
        }
    }
    private void SetPose(Pose next) { pose = next; poseTime = 0; RenderPose(); }
    private void RenderPose()
    {
        if (rigReady) { RenderRig(); return; }
        if (motions == null || motions.Length != 12 || bodySprite == null) return;
        Motion motion = motions[(int)pose];
        int frame = FrameAt(motion, poseTime);
        if (!TailIntact && pose != Pose.TailRegrow && pose != Pose.Defeat && pose != Pose.TailBreak)
        { motion = motions[(int)Pose.TailBreak]; frame = 5; }
        Sprite[] frames = crimson || defeating ? motion.crimson : motion.normal;
        if (frames != null && frames.Length > frame) bodySprite.sprite = frames[frame];
        if (TailIntact && !TailExposed && tailZone != null)
            tailZone.transform.localPosition = pose == Pose.Laser ? MuzzleLocal(frame) : tailRestPosition;
        // Only a horizontal mirror: a large scorpion must never rotate upside-down.
        float bob = pose == Pose.Walk ? Mathf.Sin(poseTime * 12) * .025f : 0;
        bodySprite.transform.localPosition = new Vector3(0, bob, 0);
    }
    private static int FrameAt(Motion motion, float time)
    {
        if (motion == null || motion.durations == null || motion.durations.Length == 0) return 0;
        float total = 0; foreach (float duration in motion.durations) total += duration;
        if (motion.loop && total > 0) time %= total;
        for (int i = 0; i < motion.durations.Length; i++) { if (time < motion.durations[i]) return i; time -= motion.durations[i]; }
        return motion.durations.Length - 1;
    }
    private void Face(Vector2 point)
    {
        if (visual == null) return;
        if (rigReady)
        {
            Vector2 direction = point - (Vector2)transform.position;
            if (direction.sqrMagnitude > .01f) visual.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90);
        }
        else visual.localScale = new Vector3(point.x > transform.position.x ? -1 : 1, 1, 1);
    }
    private static Vector3 MuzzleLocal(int frame)
    {
        var anchor = laserMuzzles[Mathf.Clamp(frame, 0, 5)];
        return new Vector3((anchor.x - .53f) * 209f / 48f, (.77f - anchor.y) * 209f / 48f, 0);
    }
    private Vector2 FiringMuzzle => rigReady ? RigMuzzle : (Vector2)visual.TransformPoint(MuzzleLocal(3));
    private void MoveTowards(Vector2 point, float speed)
    {
        Vector2 next = Vector2.MoveTowards(transform.position, point, speed * Time.deltaTime);
        if (owner != null && owner.ContainsArenaPoint(next, Mathf.Max(BodyRadius + .4f, 2.4f * transform.localScale.y)))
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0) { Sound((decision % 2 == 0) ? "step-a" : "step-b", .18f); stepTimer = .26f; }
    }

    public void TakeDamage(int amount, bool holy = false) => ReceiveHit(amount, holy, ShaulaHitZone.Zone.Shell, transform);
    public void ReceiveHit(int amount, bool holy, ShaulaHitZone.Zone zone, Transform hitPoint)
    {
        if (!CanAct || amount <= 0) return;
        if (holy) { health = Mathf.Min(maxHealth, health + amount); RefreshHud(); return; }
        if (zone == ShaulaHitZone.Zone.Tail && !TailIntact) return;
        float multiplier = zone == ShaulaHitZone.Zone.Tail ? TailExposed ? 2f : 1.5f
            : zone == ShaulaHitZone.Zone.Joint ? 1.35f : vulnerableRemaining > 0 ? 1f : armorMultiplier;
        float previous = health;
        health = Mathf.Max(0, health - amount * multiplier);
        hitFlash = .65f;
        DamageNumbers.Show(hitPoint != null ? hitPoint : transform, previous, health);
        if (zone == ShaulaHitZone.Zone.Tail) tailHealth = Mathf.Max(0, tailHealth - amount * multiplier);
        if (health <= 0) { BeginDefeat(); return; }
        if (!crimson && health <= maxHealth * .5f) ragePending = true;
        if (TailIntact && tailHealth <= 0) BreakTail();
        else if (zone == ShaulaHitZone.Zone.Shell && Time.time >= nextArmorSound)
        { Sound("armor-block", .25f); nextArmorSound = Time.time + .22f; }
        RefreshHud();
    }
    private float nextArmorSound;
    public void ApplyPoison(int damage, int ticks, float interval)
    {
        if (IsAlive) StartCoroutine(Poison(damage, ticks, interval));
    }
    private IEnumerator Poison(int damage, int ticks, float interval)
    {
        for (int i = 0; i < ticks && IsAlive; i++)
        {
            while (!CanAct && IsAlive) yield return null;
            TakeDamage(damage); yield return Wait(Mathf.Max(.05f, interval));
        }
    }
    private void BreakTail()
    {
        chargeAmount = 0;
        ResetStingMotion();
        TailIntact = false; TailExposed = false; tailBrokenRemaining = 9f;
        SetTailColliders(false); ClearEffects(); audioSrc.Stop();
        Sound("tail-break", .8f); Burst(TailPoint, 24, 1); SetPose(Pose.TailBreak);
    }
    private void SetTailColliders(bool enabled)
    { if (tailZone != null) foreach (var collider in tailZone.GetComponents<Collider2D>()) collider.enabled = enabled; }
    private void BeginDefeat()
    {
        ResetStingMotion();
        ClearImpactNoise();
        defeating = true; IsAttacking = false; TailExposed = false;
        StopAllCoroutines(); ClearEffects(); audioSrc.Stop();
        foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        if (gameUi != null) gameUi.SetBossMusic(null);
        Sound("transform", .8f);
        SetPose(Pose.Defeat); Burst(Center, 36, 1.5f); RefreshHud();
        StartCoroutine(Defeat());
    }
    private IEnumerator Defeat()
    {
        yield return Wait(2.7f);
        if (rewarded || owner == null || !owner.IsCombatActive) yield break;
        rewarded = true;
        if (!GameProgress.IsArenaDemo)
        {
            FindObjectOfType<Stats>()?.AddBossDead();
            player.AddCoin(coinReward); player.AddPoitonHeal(3); player.crossbowController.AddArrows(0, 30);
        }
        owner.NotifyShaulaDefeated(this);
        if (healthPanel != null) healthPanel.SetActive(false);
        // The little scorpion is cosmetic. It has no enabled collider or attack coroutine.
        float age = 0;
        while (age < 1.8f)
        {
            if (GameProgress.IsReady && Time.timeScale > 0 && !YG.YG2.isPauseGame)
            {
                age += Time.deltaTime; transform.position += Vector3.left * (.45f * Time.deltaTime);
                bodySprite.color = new Color(1, 1, 1, Mathf.Clamp01((1.8f - age) / .7f));
                FadeRig(bodySprite.color.a);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
    private void RefreshHud()
    {
        if (healthFill != null) { healthFill.fillAmount = health / maxHealth; healthFill.color = crimson ? new Color(.9f, .28f, .22f) : new Color(.9f, .72f, .35f); }
        if (tailFill != null) tailFill.fillAmount = TailIntact ? tailHealth / maxTailHealth : 0;
        if (title != null) title.text = GameLocalization.Text(crimson ? "Шаула · Багровая ярость" : "Шаула");
        if (status != null) { status.text = ""; status.gameObject.SetActive(false); }
    }
    private void Sound(string name, float volume = .65f)
    {
        if (effects == null) return;
        foreach (var clip in effects) if (clip != null && clip.name == name) { PlaySound(clip, volume, spatial: false); break; }
    }
    private void OnDisable()
    {
        ClearImpactNoise();
        StopAllCoroutines(); ClearEffects();
        if (configured)
        {
            if (gameUi != null) gameUi.SetBossMusic(null);
            if (healthPanel != null) healthPanel.SetActive(false);
        }
        GameLocalization.Changed -= RefreshHud;
        if (GetComponent<AudioSource>() != null) GetComponent<AudioSource>().Stop();
    }
    private void OnDestroy() { DestroyRigMaterial(); }
}
