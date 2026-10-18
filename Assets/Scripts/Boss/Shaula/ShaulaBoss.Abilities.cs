using System.Collections;
using UnityEngine;

public sealed partial class ShaulaBoss
{
    private IEnumerator Perform(Ability ability)
    {
        switch (ability)
        {
            case Ability.Laser: yield return Laser(); break;
            case Ability.Grasp: yield return Grasp(); break;
            case Ability.Slam: yield return Slam(); break;
            case Ability.Stars: yield return Stars(); break;
            case Ability.Pursuit: yield return Pursuit(); break;
            case Ability.Sting: yield return Sting(); break;
            case Ability.Needles: yield return Needles(); break;
        }
    }

    private IEnumerator Laser()
    {
        if (!TailIntact) yield break;
        if (Vector2.Distance(Center, PlayerPoint) < 4.5f)
        {
            SetPose(Pose.Walk);
            Vector2 away = Center + (Center - PlayerPoint).normalized * 3;
            yield return Wait(.25f, _ => MoveTowards(away, moveSpeed * 2.2f), true);
        }
        if (!TailIntact) yield break;
        tailAimPoint = PlayerPoint; SetPose(Pose.Laser); Sound("laser-charge", .75f);
        var warning = WarningLane(FiringMuzzle, PlayerPoint, .24f);
        var charge = Ring(TailPoint, .2f, Ivory, .04f);
        Vector2 lockedPoint = PlayerPoint, origin = FiringMuzzle, direction = (PlayerPoint - FiringMuzzle).normalized;
        // Faster charge, while preserving a fixed final .35-second dodge window in both phases.
        yield return Wait(1.1f, progress =>
        {
            chargeAmount = Mathf.Min(progress, .75f / 1.1f);
            if (progress < .75f / 1.1f) { lockedPoint = PlayerPoint; tailAimPoint = lockedPoint; origin = FiringMuzzle; direction = (lockedPoint - origin).normalized; }
            SetWarningLane(warning, origin, RayEnd(origin, direction), .24f, progress);
            SetRing(charge, TailPoint, .15f + .25f * progress);
        }, true);
        chargeAmount = 0; RemoveEffect(warning); RemoveEffect(charge);
        if (!TailIntact || !CanAct) yield break;
        PoseFrame(3); Sound("laser-fire", .9f);
        Vector2 end = RayEnd(origin, direction);
        bool hit = DamageLine(origin, end, .14f, 38, 0);
        Vector2 impact = hit ? ClosestOnLine(PlayerPoint, origin, end) : end;
        StartCoroutine(WhiteLaser(origin, end, impact));
        Sound("laser-impact", .6f);
        yield return Wait(.38f, requireTail: true);
    }

    private IEnumerator Grasp()
    {
        SetPose(Pose.Grasp); Sound("claw-ready", .6f);
        Vector2 direction = (PlayerPoint - Center).normalized;
        Vector2 origin = Center, end = Center + direction * 5f;
        var warning = WarningLane(origin, end, BodyRadius * .7f);
        yield return Wait(.44f / Tempo, t => SetWarningLane(warning, origin, end, BodyRadius * .7f, t));
        RemoveEffect(warning); PoseFrame(2);
        bool hit = false;
        yield return Dash(direction, 5f, .24f / Tempo, 36, value => hit |= value);
        Sound("claw-hit", .75f); Burst(Center + direction * BodyRadius, 12, .55f);
        ImpactShake(.3f);
        yield return Recovery(hit ? .5f : 1.05f);
    }

    private IEnumerator Slam()
    {
        SetPose(Pose.Slam); Sound("claw-ready", .65f);
        float radius = BodyRadius + 1.8f;
        var warning = WarningCircle(Center, radius);
        yield return Wait(.56f / Tempo, t => SetWarningCircle(warning, Center, radius, t));
        RemoveEffect(warning); PoseFrame(2); Sound("ground-slam", .85f);
        Burst(Center, 28, 1.2f); ImpactShake(.5f);
        yield return Shockwave(Center, radius, .32f / Tempo, 28);
        yield return Recovery(.5f);
    }

    private IEnumerator Stars()
    {
        if (!TailIntact) yield break;
        SetPose(Pose.Stars); Sound("star-cast", .7f);
        Burst(TailPoint, 12, .45f); StartCoroutine(Flash(FiringMuzzle, .35f));
        int count = StarCount;
        float warningTime = crimson ? .6f : .75f, spacing = crimson ? .065f : .14f;
        for (int i = 0; i < count; i++)
        {
            Vector2 point = PlayerPoint;
            if (i > 0)
            {
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    Vector2 candidate = PlayerPoint + Random.insideUnitCircle * 7f;
                    if (owner.ContainsArenaPoint(candidate, 1)) { point = candidate; break; }
                }
            }
            var marker = WarningCircle(point, StarRadius);
            StartCoroutine(StarStrike(marker, point, warningTime + i * spacing));
        }
        yield return Wait(warningTime + (count - 1) * spacing + .2f, requireTail: true);
    }

    private IEnumerator StarStrike(LineRenderer marker, Vector2 point, float warningTime)
    {
        yield return Wait(warningTime, t => SetWarningCircle(marker, point, StarRadius, t), true);
        if (marker == null || !TailIntact || !CanAct) yield break;
        var fallGlow = Line(point + Vector2.up * 6f, point, new Color(.65f, .88f, 1, .4f), .55f);
        var fall = Line(point + Vector2.up * 6f, point, Color.white, .22f);
        fall.gameObject.name = "Shaula_SkyLaserCore";
        yield return Wait(.12f, requireTail: true);
        if (marker == null || !TailIntact || !CanAct) { RemoveEffect(fall); RemoveEffect(fallGlow); yield break; }
        RemoveEffect(marker);
        Burst(point, 20, 1.05f); StartCoroutine(Flash(point, .65f)); Sound("laser-impact", .3f); Punch(.4f);
        DamageCircle(point, StarRadius, 14, .25f);
        yield return Wait(.1f, t => {
            if (fall != null) { var c = Color.white; c.a = 1 - t; fall.startColor = fall.endColor = c; }
        }, true);
        RemoveEffect(fall); RemoveEffect(fallGlow);
    }

    private IEnumerator Pursuit()
    {
        int count = PursuitCount;
        for (int i = 0; i < count && IsAlive; i++)
        {
            Face(PlayerPoint); SetPose(Pose.Grasp);
            Vector2 direction = (PlayerPoint - Center).normalized;
            Vector2 origin = Center, end = Center + direction * 5.5f;
            var warning = WarningLane(origin, end, BodyRadius * .7f);
            Sound("claw-ready", .4f);
            yield return Wait((i == 0 ? .48f : .34f) / Tempo, t => SetWarningLane(warning, origin, end, BodyRadius * .7f, t));
            RemoveEffect(warning); PoseFrame(2);
            yield return Dash(direction, 5.5f, .24f / Tempo, i == count - 1 ? 36 : 16);
            if (i == count - 1)
            {
                Sound("claw-hit", .8f); Burst(Center + direction * BodyRadius, 18, .8f);
            }
            else yield return Wait(.08f);
        }
        yield return Recovery(.85f);
    }

    private IEnumerator Dash(Vector2 direction, float distance, float duration, int damage, System.Action<bool> result = null)
    {
        bool hit = false;
        Vector2 previous = Center;
        yield return Wait(duration, _ =>
        {
            MoveTowards((Vector2)transform.position + direction * distance, distance / duration);
            Vector2 current = Center;
            if (!hit) hit = DamageLine(previous, current, BodyRadius * .7f, damage, .6f);
            previous = current;
        });
        result?.Invoke(hit);
    }

    private IEnumerator Sting()
    {
        if (!TailIntact) yield break;
        SetPose(Pose.Sting); Sound("claw-ready", .5f);
        Vector2 origin = TailPoint;
        Vector2 direction = (PlayerPoint - origin).normalized;
        Vector2 end = RayEnd(origin, direction, 6.5f);
        Vector2 coil = origin - direction * .32f;
        stingAnimating = true; stingAimPoint = end + direction;
        SetStingPoint(origin, 0);
        var warning = WarningLane(origin, end, .28f);
        yield return Wait(.48f / Tempo, t => {
            SetWarningLane(warning, origin, end, .28f, t);
            SetStingPoint(Vector2.Lerp(origin, coil, Mathf.SmoothStep(0, 1, t)), 0);
        }, true);
        RemoveEffect(warning);
        if (!TailIntact || !CanAct) { ResetStingMotion(); yield break; }
        PoseFrame(2); Sound("sting", .8f);
        bool hit = false;
        Vector2 previous = coil;
        yield return Wait(.14f / Tempo, t => {
            float extension = Mathf.SmoothStep(0, 1, t);
            Vector2 point = Vector2.Lerp(coil, end, extension);
            SetStingPoint(point, extension);
            if (!hit) hit = DamageLine(previous, point, .22f, 42, .4f);
            previous = point;
        }, true);
        if (!TailIntact || !CanAct) { ResetStingMotion(); yield break; }
        SetStingPoint(end, 1); Burst(end, 14, .65f); ImpactShake(.25f);
        LineRenderer weakSpot = null;
        if (!hit && TailIntact)
        {
            TailExposed = true;
            weakSpot = Ring(end, .5f, Ivory, .055f);
            RefreshHud(); vulnerableRemaining = crimson ? .7f : 1f;
            yield return Wait(vulnerableRemaining, requireTail: true);
        }
        else yield return Wait(.12f / Tempo, requireTail: true);
        RemoveEffect(weakSpot); TailExposed = false;
        if (TailIntact)
            yield return Wait(.22f / Tempo, t => {
                float progress = Mathf.SmoothStep(0, 1, t);
                SetStingPoint(Vector2.Lerp(end, origin, progress), 1 - progress);
            }, true);
        ResetStingMotion(); RefreshHud();
    }

    private IEnumerator Needles()
    {
        if (!TailIntact) yield break;
        SetPose(Pose.Needles);
        Vector2 direction = (PlayerPoint - TailPoint).normalized;
        var marker = Ring(PlayerPoint, .35f, Warning, .035f);
        yield return Wait(.32f, requireTail: true);
        RemoveEffect(marker);
        int count = NeedleCount;
        for (int i = 0; i < count && TailIntact && IsAlive; i++)
        {
            Vector2 desired = (PlayerPoint - TailPoint).normalized;
            direction = Vector3.RotateTowards(direction, desired, 14f * Mathf.Deg2Rad, 0);
            Sound("needle", .5f); Punch(.3f); StartCoroutine(Flash(FiringMuzzle, .25f));
            StartCoroutine(Needle(FiringMuzzle, direction, (crimson ? 13f : 11f) * shotSpeedScale));
            yield return Wait(.105f / Tempo, requireTail: true);
        }
        yield return Wait(.2f, requireTail: true);
    }

    private IEnumerator RegrowTail()
    {
        SetPose(Pose.TailRegrow); Sound("tail-regrow", .8f);
        float radius = BodyRadius + 1.5f;
        var warning = WarningCircle(Center, radius);
        Burst(TailPoint, 22, .8f);
        yield return Wait(1.22f, t => SetWarningCircle(warning, Center, radius, t));
        TailIntact = true; tailHealth = maxTailHealth; SetTailColliders(true);
        RemoveEffect(warning); Burst(TailPoint, 22, 1.2f); RefreshHud();
        yield return Shockwave(Center, radius, .5f, 16);
        yield return Wait(.2f);
    }

    private IEnumerator Recovery(float seconds)
    {
        vulnerableRemaining = Mathf.Max(vulnerableRemaining, seconds);
        SetPose(Pose.Idle);
        yield return Wait(seconds / Tempo);
    }
    private void PoseFrame(int frame)
    {
        if (rigReady) { Punch(); return; }
        poseTime = 0;
        for (int i = 0; i < frame; i++) poseTime += motions[(int)pose].durations[i];
        RenderPose();
    }
}
