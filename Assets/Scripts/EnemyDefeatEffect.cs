using UnityEngine;

// Both bosses disappear in a short magical burst; their existing reward timing stays intact.
public static class EnemyDefeatEffect
{
    private static Material sparkleMaterial;

    public static void PlayAndHide(GameObject enemy)
    {
        if (enemy == null) return;
        PlayBurst(enemy, false);

        // Hide every body/weapon sprite now, before another animation frame can show a fall.
        foreach (var animator in enemy.GetComponentsInChildren<Animator>()) animator.enabled = false;
        foreach (var renderer in enemy.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        foreach (var collider in enemy.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        foreach (var body in enemy.GetComponentsInChildren<Rigidbody2D>()) body.simulated = false;
    }

    public static void PlaySpawn(GameObject enemy)
    {
        if (enemy != null) PlayBurst(enemy, true);
    }

    public static void PlayEnrage(GameObject enemy)
    {
        if (enemy != null) PlayBurst(enemy, false, true);
    }

    private static void PlayBurst(GameObject enemy, bool spawning, bool enraged = false)
    {
        var sprite = enemy.GetComponentInChildren<SpriteRenderer>();
        Vector3 origin = sprite != null ? sprite.bounds.center : enemy.transform.position;
        float size = sprite != null ? Mathf.Clamp(sprite.bounds.size.magnitude * .25f, .2f, .7f) : .3f;
        int sortingLayer = sprite != null ? sprite.sortingLayerID : 0;
        int sortingOrder = sprite != null ? sprite.sortingOrder + 1 : 1;
        var effect = new GameObject(enraged ? "EnemyOvertimeEnrage" : spawning ? "SlimeSpawnSparkles" : "EnemyDefeatSparkles");
        effect.transform.position = origin;
        var particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = .15f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(size * 1.5f, size * 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .12f, size * .24f);
        main.startColor = enraged
            ? new ParticleSystem.MinMaxGradient(new Color(1f, .1f, .1f), new Color(1f, .65f, .2f))
            : spawning
            ? new ParticleSystem.MinMaxGradient(new Color(.35f, 1f, .45f), new Color(.8f, 1f, .55f))
            : new ParticleSystem.MinMaxGradient(new Color(1f, .87f, .48f), new Color(.8f, 1f, .87f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = false;
        main.maxParticles = 16;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var emission = particles.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, 14) });
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = size * .25f;
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        var rendererFx = particles.GetComponent<ParticleSystemRenderer>();
        if (sparkleMaterial == null) sparkleMaterial = new Material(Shader.Find("Sprites/Default"));
        rendererFx.sharedMaterial = sparkleMaterial;
        rendererFx.sortingLayerID = sortingLayer;
        rendererFx.sortingOrder = sortingOrder;
        particles.Play();
    }
}
