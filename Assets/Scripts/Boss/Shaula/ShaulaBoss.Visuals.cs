using UnityEngine;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;

public sealed partial class ShaulaBoss
{
    [Header("Articulated pixel-art presentation")]
    [SerializeField] private Sprite[] rigParts;
    [SerializeField] private Material carapaceMaterial;
    private SpriteRenderer[,] legParts;
    private SpriteRenderer[] arms, claws, tailSegments;
    private SpriteRenderer stinger, little;
    private Transform rigRoot;
    private MaterialPropertyBlock carapaceProperties;
    private SpriteRenderer[] rigRenderers;
    private Vector2 previousGround;
    private float gait, moving, strikePulse, recoil, hitFlash, chargeAmount;
    private Vector2 tailAimPoint;
    private bool stingAnimating;
    private Vector2 stingPoint, stingAimPoint;
    private float stingExtension;
    private Rect lastHudSafeArea;
    private int lastHudWidth, lastHudHeight;
    private bool rigReady;
    private NoiseSettings impactNoise;
    private readonly List<CinemachineBasicMultiChannelPerlin> impactChannels = new List<CinemachineBasicMultiChannelPerlin>();
    private readonly List<CinemachineVirtualCamera> addedNoise = new List<CinemachineVirtualCamera>();
    private static readonly int RageProperty = Shader.PropertyToID("_Rage"), FlashProperty = Shader.PropertyToID("_Flash");

    private void BuildRig()
    {
        if (rigReady || rigParts == null || rigParts.Length != 9 || carapaceMaterial == null) return;
        carapaceMaterial = new Material(carapaceMaterial) { name = "ShaulaEncounterCarapace" };
        rigRoot = new GameObject("ArticulatedScorpion").transform; rigRoot.SetParent(visual, false);
        carapaceProperties = new MaterialPropertyBlock();
        bodySprite.sprite = rigParts[0]; bodySprite.sharedMaterial = carapaceMaterial;
        bodySprite.transform.localPosition = Vector3.zero;
        Fit(bodySprite, new Vector2(1.35f, 2.2f));
        legParts = new SpriteRenderer[8, 2];
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 2; j++) legParts[i, j] = RigPart("Leg" + i + "_" + j, 3 + j, -2);
        arms = new SpriteRenderer[2]; claws = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++) { arms[i] = RigPart("Forearm" + i, 5, -1); claws[i] = RigPart("Pincer" + i, 1, 1); }
        tailSegments = new SpriteRenderer[5];
        for (int i = 0; i < tailSegments.Length; i++) tailSegments[i] = RigPart("TailSegment" + i, 6, i < 2 ? -1 : 2);
        stinger = RigPart("Stinger", 7, 3); little = RigPart("LittleScorpion", 8, 0);
        little.enabled = false; previousGround = transform.position;
        rigRenderers = visual.GetComponentsInChildren<SpriteRenderer>(); rigReady = true;
    }

    private SpriteRenderer RigPart(string name, int sprite, int order)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>(); renderer.transform.SetParent(rigRoot, false);
        renderer.sprite = rigParts[sprite]; renderer.sharedMaterial = carapaceMaterial;
        renderer.sortingLayerID = bodySprite.sortingLayerID; renderer.sortingOrder = bodySprite.sortingOrder + order;
        return renderer;
    }
    private static void Fit(SpriteRenderer renderer, Vector2 size)
    { renderer.transform.localScale = new Vector3(size.x / renderer.sprite.bounds.size.x, size.y / renderer.sprite.bounds.size.y, 1); }
    private static void Segment(SpriteRenderer renderer, Vector2 from, Vector2 to, float thickness, bool vertical = false)
    {
        Vector2 delta = to - from;
        renderer.transform.localPosition = (from + to) * .5f;
        renderer.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - (vertical ? 90 : 0));
        Fit(renderer, vertical ? new Vector2(thickness, delta.magnitude + .07f) : new Vector2(delta.magnitude + .07f, thickness));
    }

    private void RenderRig()
    {
        float traveled = Vector2.Distance(previousGround, transform.position);
        previousGround = transform.position;
        if (traveled < .6f) gait += traveled * 7.5f / Mathf.Max(.1f, transform.localScale.x);
        float targetMoving = traveled > .0001f && pose != Pose.Defeat ? 1 : 0;
        moving = Mathf.MoveTowards(moving, targetMoving, Time.deltaTime * 9);
        recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * 5);
        strikePulse = Mathf.MoveTowards(strikePulse, 0, Time.deltaTime * 7);
        hitFlash = Mathf.MoveTowards(hitFlash, 0, Time.deltaTime * 8);
        float t = poseTime;
        float breath = Mathf.Sin(fightClock * 3) * .012f;
        bool small = defeating && t >= 1.65f;
        float shrink = defeating ? Mathf.SmoothStep(1, .16f, Mathf.Clamp01((t - .5f) / 1.15f)) : 1;
        rigRoot.localScale = Vector3.one * shrink;
        bodySprite.enabled = !small;
        bodySprite.transform.localPosition = new Vector3(0, breath - recoil * .08f, 0);
        bodySprite.transform.localScale = new Vector3(1.35f / rigParts[0].bounds.size.x * shrink,
            2.2f / rigParts[0].bounds.size.y * shrink * (1 + breath), 1);
        little.enabled = small;
        little.transform.localPosition = Vector3.zero; Fit(little, new Vector2(1.6f, 1.9f));
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1 : 1;
            for (int row = 0; row < 4; row++)
            {
                int index = side * 4 + row;
                float cycle = gait + (row % 2 == side ? 0 : Mathf.PI);
                float stride = Mathf.Sin(cycle) * .28f * moving;
                float lift = Mathf.Max(0, Mathf.Cos(cycle)) * .16f * moving;
                float y = -.58f + row * .38f;
                Vector2 hip = new Vector2(sign * .48f, y);
                Vector2 knee = new Vector2(sign * (1.0f + lift), y + .12f + stride * .45f);
                Vector2 foot = new Vector2(sign * (1.52f - lift * .25f), y - .34f + stride);
                Segment(legParts[index, 0], hip, knee, .24f);
                Segment(legParts[index, 1], knee, foot, .18f);
                legParts[index, 0].enabled = legParts[index, 1].enabled = !small;
            }
            float liftClaw = pose == Pose.Slam ? Mathf.Sin(Mathf.Clamp01(t / (.56f / Tempo)) * Mathf.PI * .5f) * .65f * (1 - Mathf.Clamp01(strikePulse)) : 0;
            float spread = pose == Pose.Grasp ? Mathf.Clamp01(1 - t / (.5f / Tempo)) * .3f : 0;
            Vector2 shoulder = new Vector2(sign * .5f, -.55f);
            Vector2 wrist = new Vector2(sign * (1.1f + spread - strikePulse * .27f), -1.12f + liftClaw - strikePulse * .42f);
            Segment(arms[side], shoulder, wrist, .3f);
            claws[side].sprite = rigParts[strikePulse > .18f ? 2 : 1];
            claws[side].transform.localPosition = wrist + new Vector2(sign * .08f, -.36f);
            claws[side].transform.localRotation = Quaternion.Euler(0, 0, -sign * (12 + spread * 30));
            Fit(claws[side], new Vector2(.9f, 1.05f));
            arms[side].enabled = claws[side].enabled = !small;
        }
        Vector2 start = new Vector2(.06f, .82f);
        Vector2 tip = new Vector2(.48f + Mathf.Sin(fightClock * 2.1f) * .08f, 2.04f + chargeAmount * .16f);
        if (pose == Pose.Stars || pose == Pose.Needles) tip += Vector2.up * (Mathf.Sin(t * 12) * .08f + .22f);
        if (stingAnimating) tip = visual.InverseTransformPoint(stingPoint);
        else if (pose == Pose.Sting && TailExposed) tip = visual.InverseTransformPoint(tailZone.transform.position);
        if (!stingAnimating) tip += Vector2.up * recoil * .4f;
        Vector2 control = new Vector2(1.3f + chargeAmount * .13f, 1.85f + chargeAmount * .2f);
        if (stingAnimating) control = Vector2.Lerp(control, (start + tip) * .5f + Vector2.right * .45f, stingExtension);
        float tailScale = TailIntact ? 1 : pose == Pose.TailRegrow ? Mathf.Clamp01(t / 1.22f) : pose == Pose.TailBreak ? Mathf.Clamp01(1 - t / .28f) : 0;
        for (int i = 0; i < tailSegments.Length; i++)
        {
            Vector2 a = TailCurve(start, control, tip, i / 5f), b = TailCurve(start, control, tip, (i + 1) / 5f);
            Segment(tailSegments[i], a, b, .37f - i * .025f, true);
            tailSegments[i].enabled = !small && tailScale > (i + .5f) / 5f;
        }
        stinger.enabled = !small && tailScale >= .99f;
        stinger.transform.localPosition = tip;
        Vector2 localAim = visual.InverseTransformPoint(stingAnimating ? stingAimPoint : pose == Pose.Laser ? tailAimPoint : PlayerPoint) - (Vector3)tip;
        stinger.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(localAim.y, localAim.x) * Mathf.Rad2Deg + 90);
        Fit(stinger, new Vector2(.57f, .82f));
        if (TailIntact && !TailExposed && tailZone != null) tailZone.transform.localPosition = tip;
        carapaceMaterial.SetFloat(RageProperty, crimson ? 1 : 0); carapaceMaterial.SetFloat(FlashProperty, hitFlash);
        foreach (var renderer in rigRenderers)
        {
            carapaceProperties.SetTexture("_MainTex", renderer.sprite.texture);
            renderer.SetPropertyBlock(carapaceProperties);
            renderer.color = new Color(1, 1, 1, bodySprite.color.a);
        }
    }
    private static Vector2 TailCurve(Vector2 a, Vector2 b, Vector2 c, float t)
    { float u = 1 - t; return u * u * a + 2 * u * t * b + t * t * c; }
    private void Punch(float strength = 1) { strikePulse = strength; recoil = strength; }
    private void FadeRig(float alpha)
    { if (rigRenderers != null) foreach (var renderer in rigRenderers) renderer.color = new Color(1, 1, 1, alpha); }
    private void LayoutBossHud()
    {
        if (healthPanel == null || (lastHudWidth == Screen.width && lastHudHeight == Screen.height && lastHudSafeArea == Screen.safeArea)) return;
        lastHudWidth = Screen.width; lastHudHeight = Screen.height; lastHudSafeArea = Screen.safeArea;
        var rect = healthPanel.GetComponent<RectTransform>();
        var parent = rect.parent as RectTransform;
        if (parent == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
        float safeBottom = Screen.height > 0 ? Screen.safeArea.yMin / Screen.height * parent.rect.height : 0;
        rect.anchoredPosition = new Vector2(0, safeBottom + 12);
        float scale = Mathf.Min(1, parent.rect.width * .5f / 760f);
        rect.localScale = new Vector3(scale, scale, 1);
    }
    private Vector2 RigMuzzle => stinger != null ? (Vector2)stinger.transform.TransformPoint(new Vector3(0, -.35f / stinger.transform.localScale.y, 0)) : TailPoint;
    private void SetStingPoint(Vector2 point, float extension)
    {
        stingPoint = point; stingExtension = extension;
        if (tailZone != null) tailZone.transform.position = point;
    }
    private void ResetStingMotion()
    {
        stingAnimating = false; stingExtension = 0; TailExposed = false;
        if (tailZone != null) tailZone.transform.localPosition = tailRestPosition;
    }
    private void ImpactShake(float strength)
    {
        if (impactNoise == null)
        {
            impactNoise = ScriptableObject.CreateInstance<NoiseSettings>();
            impactNoise.PositionNoise = new[] { new NoiseSettings.TransformNoiseParams {
                X = new NoiseSettings.NoiseParams { Amplitude = .65f, Frequency = 29 },
                Y = new NoiseSettings.NoiseParams { Amplitude = .4f, Frequency = 33 } } };
            foreach (var camera in FindObjectsOfType<CinemachineVirtualCamera>())
            {
                if (!camera.isActiveAndEnabled) continue;
                var channel = camera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
                if (channel != null) continue; // Existing camera effects retain their own profile and owner.
                channel = camera.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
                channel.m_NoiseProfile = impactNoise; channel.m_AmplitudeGain = 0;
                impactChannels.Add(channel); addedNoise.Add(camera);
            }
        }
        if (impactChannels.Count > 0) StartCoroutine(ImpactNoisePulse(strength));
    }
    private IEnumerator ImpactNoisePulse(float strength)
    {
        yield return Wait(.23f, t => {
            foreach (var channel in impactChannels) if (channel != null) channel.m_AmplitudeGain = strength * (1 - t) * (1 - t);
        });
        foreach (var channel in impactChannels) if (channel != null) channel.m_AmplitudeGain = 0;
    }
    private void ClearImpactNoise()
    {
        foreach (var camera in addedNoise) if (camera != null) camera.DestroyCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        addedNoise.Clear(); impactChannels.Clear();
        if (impactNoise != null) Destroy(impactNoise); impactNoise = null;
    }
    private void DestroyRigMaterial()
    { if (rigReady && carapaceMaterial != null) Destroy(carapaceMaterial); }
}
