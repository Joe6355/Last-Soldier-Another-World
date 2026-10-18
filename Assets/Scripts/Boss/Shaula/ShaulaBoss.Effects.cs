using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class ShaulaBoss
{
    private static readonly Color Warning = new Color(1, .3f, .19f, .9f);
    private static readonly Color Ivory = new Color(.9f, .98f, 1, 1);
    [SerializeField] private Material effectMaterial;
    private readonly List<GameObject> activeEffects = new List<GameObject>();
    private readonly RaycastHit2D[] pushHits = new RaycastHit2D[32];
    public int ActiveEffectCount { get { activeEffects.RemoveAll(go => go == null); return activeEffects.Count; } }

    private GameObject Effect(string name)
    {
        activeEffects.RemoveAll(go => go == null);
        var go = new GameObject("Shaula_" + name); activeEffects.Add(go);
        return go;
    }
    private LineRenderer Line(Vector2 start, Vector2 end, Color color, float width)
    {
        var line = Effect("MagicLine").AddComponent<LineRenderer>();
        line.sharedMaterial = effectMaterial;
        line.useWorldSpace = true; line.positionCount = 2;
        line.startColor = line.endColor = color; line.startWidth = line.endWidth = width;
        line.numCapVertices = 2; line.sortingLayerID = bodySprite.sortingLayerID;
        line.sortingOrder = bodySprite.sortingOrder + 2;
        SetLine(line, start, end); return line;
    }
    private static void SetLine(LineRenderer line, Vector2 start, Vector2 end)
    {
        if (line == null) return;
        line.SetPosition(0, new Vector3(start.x, start.y, 0));
        line.SetPosition(1, new Vector3(end.x, end.y, 0));
    }
    private LineRenderer Ring(Vector2 center, float radius, Color color, float width)
    {
        var line = Line(center, center, color, width);
        line.loop = true; line.positionCount = 40; SetRing(line, center, radius); return line;
    }
    private static void SetRing(LineRenderer line, Vector2 center, float radius)
    {
        if (line == null) return;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2 / line.positionCount;
            line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, 0));
        }
    }
    private void RemoveEffect(Component effect) { if (effect != null) RemoveEffect(effect.gameObject); }
    private void RemoveEffect(GameObject effect)
    {
        if (effect == null) return;
        activeEffects.Remove(effect); effect.SetActive(false); Destroy(effect);
        DestroyEffectMeshes(effect);
    }
    private void ClearEffects()
    {
        foreach (var go in activeEffects) if (go != null) { DestroyEffectMeshes(go); go.SetActive(false); Destroy(go); }
        activeEffects.Clear();
    }
    private static void DestroyEffectMeshes(GameObject effect)
    { foreach (var filter in effect.GetComponentsInChildren<MeshFilter>()) if (filter.sharedMesh != null) Destroy(filter.sharedMesh); }

    // A quiet filled danger zone plus a growing rim: the player sees both its footprint and the countdown.
    private LineRenderer WarningCircle(Vector2 center, float radius)
    {
        var border = Ring(center, radius, Warning, .035f); border.gameObject.name = "Shaula_AreaWarning";
        border.sortingOrder = bodySprite.sortingOrder - 3;
        var vertices = new Vector3[50]; var triangles = new int[48 * 3];
        vertices[0] = center;
        for (int i = 0; i <= 48; i++)
        {
            float angle = i * Mathf.PI * 2 / 48;
            vertices[i + 1] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (i < 48) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
        }
        FilledShape(border.transform, vertices, triangles, new Color(1, .13f, .1f, .15f));
        CountdownRim(border.transform); return border;
    }
    private LineRenderer WarningLane(Vector2 start, Vector2 end, float radius)
    {
        var border = Line(start, end, Warning, .035f); border.gameObject.name = "Shaula_LaneWarning";
        border.sortingOrder = bodySprite.sortingOrder - 3;
        border.loop = true; border.positionCount = 4;
        var fill = FilledShape(border.transform, new Vector3[4], new[] { 0, 1, 2, 0, 2, 3 }, new Color(1, .13f, .1f, .17f));
        SetWarningLane(border, start, end, radius, 0);
        return border;
    }
    private MeshFilter FilledShape(Transform parent, Vector3[] vertices, int[] triangles, Color color)
    {
        var obj = new GameObject("DangerFootprint"); obj.transform.SetParent(parent, false);
        var filter = obj.AddComponent<MeshFilter>(); var mesh = new Mesh { name = "ShaulaEffectMesh" };
        var colors = new Color[vertices.Length]; for (int i = 0; i < colors.Length; i++) colors[i] = color;
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.colors = colors; mesh.RecalculateBounds(); filter.sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = effectMaterial;
        renderer.sortingLayerID = bodySprite.sortingLayerID; renderer.sortingOrder = bodySprite.sortingOrder - 3;
        return filter;
    }
    private void CountdownRim(Transform parent)
    {
        var obj = new GameObject("Countdown"); obj.transform.SetParent(parent, false);
        var rim = obj.AddComponent<LineRenderer>(); rim.sharedMaterial = effectMaterial;
        rim.useWorldSpace = true; rim.positionCount = 2; rim.startWidth = rim.endWidth = .06f;
        rim.startColor = rim.endColor = new Color(1, .84f, .5f, .95f);
        rim.sortingLayerID = bodySprite.sortingLayerID; rim.sortingOrder = bodySprite.sortingOrder - 3;
    }
    private void SetWarningCircle(LineRenderer border, Vector2 center, float radius, float progress)
    {
        if (border == null) return;
        SetRing(border, center, radius);
        var rim = border.transform.Find("Countdown").GetComponent<LineRenderer>();
        rim.positionCount = Mathf.Max(2, Mathf.CeilToInt(progress * 48) + 1);
        for (int i = 0; i < rim.positionCount; i++)
        {
            float angle = Mathf.PI * .5f + Mathf.PI * 2 * progress * i / (rim.positionCount - 1);
            rim.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        border.startColor = border.endColor = progress > .78f ? new Color(1, .66f, .28f, 1) : Warning;
    }
    private void SetWarningLane(LineRenderer border, Vector2 start, Vector2 end, float radius, float progress)
    {
        if (border == null) return;
        Vector2 normal = Vector2.Perpendicular((end - start).normalized) * radius;
        var vertices = new[] { (Vector3)(start + normal), (Vector3)(end + normal), (Vector3)(end - normal), (Vector3)(start - normal) };
        border.SetPositions(vertices);
        var mesh = border.GetComponentInChildren<MeshFilter>().sharedMesh; mesh.vertices = vertices; mesh.RecalculateBounds();
        var color = progress > .68f ? new Color(1, .81f, .45f, 1) : Warning;
        border.startColor = border.endColor = color;
        border.startWidth = border.endWidth = .035f + progress * .025f;
    }

    private IEnumerator Flash(Vector2 point, float size)
    {
        var root = Effect("MuzzleFlash");
        var vertices = new Vector3[17]; var triangles = new int[16 * 3]; vertices[0] = point;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8;
            vertices[i + 1] = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? size : size * .22f);
            triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % 16 + 1;
        }
        var filter = FilledShape(root.transform, vertices, triangles, Color.white);
        filter.GetComponent<MeshRenderer>().sortingOrder = bodySprite.sortingOrder + 4;
        yield return Wait(.16f, t =>
        {
            if (filter == null) return;
            var colors = filter.sharedMesh.colors;
            for (int i = 0; i < colors.Length; i++) colors[i].a = 1 - t;
            filter.sharedMesh.colors = colors;
        });
        RemoveEffect(root);
    }

    private void Burst(Vector2 point, int count, float size)
    {
        var go = Effect("DryMagicSparkles"); go.transform.position = point;
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false; main.playOnAwake = false; main.duration = .1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.2f, .48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(size, size * 3);
        main.startSize = new ParticleSystem.MinMaxCurve(.04f, .1f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.9f, .98f, 1), new Color(1, .85f, .55f));
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.useUnscaledTime = false;
        main.maxParticles = count; main.stopAction = ParticleSystemStopAction.Destroy;
        var emission = particles.emission; emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .12f * size;
        var fade = particles.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); fade.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = effectMaterial; renderer.sortingLayerID = bodySprite.sortingLayerID;
        renderer.sortingOrder = bodySprite.sortingOrder + 3; particles.Play();
    }

    private IEnumerator WhiteLaser(Vector2 origin, Vector2 end, Vector2 impact)
    {
        var glow = Line(origin, end, new Color(.55f, .85f, 1, .3f), .95f);
        var edge = Line(origin, end, new Color(.75f, .94f, 1, .85f), .48f);
        var core = Line(origin, end, Color.white, .24f); core.gameObject.name = "Shaula_LaserCore";
        Punch(1.5f); ImpactShake(.7f);
        StartCoroutine(Flash(origin, .85f)); StartCoroutine(Flash(impact, .7f));
        var ring = Ring(impact, .15f, Color.white, .06f);
        Burst(origin, 24, 1); Burst(impact, 24, 1.5f);
        yield return Wait(.22f, t =>
        {
            var lines = new[] { glow, edge, core };
            var alpha = new[] { .3f, .85f, 1 };
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != null) { var c = lines[i].startColor; c.a = alpha[i] * (1 - t); lines[i].startColor = lines[i].endColor = c; }
            if (core != null) core.startWidth = core.endWidth = .24f * (1 - t * .7f);
            SetRing(ring, impact, .15f + t * .8f);
        });
        foreach (var line in new[] { glow, edge, core }) RemoveEffect(line);
        yield return Wait(.2f, t => { if (ring == null) return; SetRing(ring, impact, .95f + .4f * t); var c = Color.white; c.a = 1 - t; ring.startColor = ring.endColor = c; });
        RemoveEffect(ring);
    }

    private IEnumerator Shockwave(Vector2 center, float radius, float duration, int damage)
    {
        var wave = Ring(center, .15f, Ivory, .12f);
        bool hit = false; float previous = 0;
        yield return Wait(duration, t =>
        {
            if (wave == null) return;
            float current = Mathf.Lerp(.15f, radius, t);
            SetRing(wave, center, current);
            float distance = Vector2.Distance(center, PlayerPoint);
            if (!hit && distance <= current + PlayerRadius && distance >= previous - PlayerRadius)
            { hit = true; Hurt(damage, center, .8f); }
            previous = current;
        });
        RemoveEffect(wave);
    }
    private IEnumerator Needle(Vector2 origin, Vector2 direction, float speed)
    {
        var needle = Line(origin, origin + direction * .38f, Ivory, .065f);
        float age = 0;
        while (age < 3.5f && needle != null && IsAlive)
        {
            if (CanAct)
            {
                Vector2 next = origin + direction * (speed * Time.deltaTime);
                if (DamageLine(origin, next, .08f, 7, 0)) { Burst(next, 6, .25f); break; }
                if (!owner.ContainsArenaPoint(next)) break;
                origin = next; age += Time.deltaTime; SetLine(needle, origin, origin - direction * .38f);
            }
            yield return null;
        }
        RemoveEffect(needle);
    }

    private float PlayerRadius => .23f;
    private static Vector2 ClosestOnLine(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float t = delta.sqrMagnitude > .00001f ? Mathf.Clamp01(Vector2.Dot(point - start, delta) / delta.sqrMagnitude) : 0;
        return start + delta * t;
    }
    private bool DamageLine(Vector2 start, Vector2 end, float radius, int damage, float knockback)
    {
        if (!CanAct || Vector2.Distance(PlayerPoint, ClosestOnLine(PlayerPoint, start, end)) > radius + PlayerRadius) return false;
        Hurt(damage, start, knockback); return true;
    }
    private void DamageCircle(Vector2 point, float radius, int damage, float knockback)
    { if (CanAct && Vector2.Distance(PlayerPoint, point) <= radius + PlayerRadius) Hurt(damage, point, knockback); }
    private void Hurt(int damage, Vector2 origin, float push)
    {
        if (!CanAct) return;
        float previous = player.hp;
        player.TakeDamage(Mathf.CeilToInt(damage * damageScale * (crimson ? 1.3f : 1)));
        if (push <= 0 || player.hp >= previous || player.hp <= 0) return; // Shields and invulnerability also stop the push.
        Vector2 direction = (PlayerPoint - origin).normalized;
        Vector2 destination = PlayerPoint + direction * push;
        var body = player.GetComponent<Rigidbody2D>();
        if (body == null || !owner.ContainsArenaPoint(destination, .35f)) return;
        var filter = new ContactFilter2D(); filter.NoFilter(); filter.useTriggers = false;
        int count = body.Cast(direction, filter, pushHits, push);
        for (int i = 0; i < count; i++)
            if (pushHits[i].collider != null && ArrowDef.FindEnemy(pushHits[i].collider) == null)
                push = Mathf.Min(push, Mathf.Max(0, pushHits[i].distance - .05f));
        body.position += direction * push;
    }
    private Vector2 RayEnd(Vector2 origin, Vector2 direction, float maxDistance = 26f)
    {
        Vector2 end = origin;
        for (float d = .25f; d <= maxDistance; d += .25f)
        {
            Vector2 next = origin + direction * d;
            if (!owner.ContainsArenaPoint(next)) break;
            end = next;
        }
        return end;
    }
}
