using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ArrowDef : MonoBehaviour
{
    public float lifeTime = 2f;
    public int damage = 1;
    [Min(1)] public int maxEnemyHits = 1;
    public bool triggerProjectile;
    private readonly HashSet<int> hitEnemies = new HashSet<int>();
    private Collider2D[] ownerColliders;
    private Collider2D[] projectileColliders;
    private static readonly HashSet<ArrowDef> activeArrows = new HashSet<ArrowDef>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetArrows() => activeArrows.Clear();

    protected virtual void OnEnable()
    {
        hitEnemies.Clear();
        projectileColliders = GetComponentsInChildren<Collider2D>(true);
        // Keep player arrows from blocking one another in shotgun volleys,
        // while retaining their contacts with enemies and scenery.
        foreach (var arrow in activeArrows)
        {
            if (arrow == null || arrow.projectileColliders == null) continue;
            foreach (var own in projectileColliders)
                foreach (var other in arrow.projectileColliders)
                    if (own != null && other != null && own.isActiveAndEnabled && other.isActiveAndEnabled)
                        Physics2D.IgnoreCollision(own, other);
        }
        activeArrows.Add(this);
    }

    protected virtual void OnDisable() => activeArrows.Remove(this);

    public void IgnoreOwnerCollisions(Collider2D[] colliders)
    {
        ownerColliders = colliders;
        projectileColliders = GetComponentsInChildren<Collider2D>(true);
        RestoreOwnerCollisionIgnores();
    }

    protected virtual void FixedUpdate()
    {
        // Выключение щита сбрасывает IgnoreCollision; восстановить пары до симуляции.
        RestoreOwnerCollisionIgnores();
    }

    private void RestoreOwnerCollisionIgnores()
    {
        if (ownerColliders == null || projectileColliders == null) return;
        foreach (var projectileCollider in projectileColliders)
        {
            if (projectileCollider == null || !projectileCollider.isActiveAndEnabled) continue;
            foreach (var ownerCollider in ownerColliders)
                if (ownerCollider != null && ownerCollider.isActiveAndEnabled
                    && !Physics2D.GetIgnoreCollision(projectileCollider, ownerCollider))
                    Physics2D.IgnoreCollision(projectileCollider, ownerCollider);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((!triggerProjectile && maxEnemyHits <= 1) || hitEnemies.Count >= maxEnemyHits || other.isTrigger || other.CompareTag("Player") || other.CompareTag("Projectile")) return;
        MonoBehaviour target = FindEnemy(other);
        if (target == null)
        {
            other.GetComponentInParent<ArenaFeature>()?.TakeEnvironmentDamage(damage);
            Destroy(gameObject); // Стены и прочие твёрдые препятствия.
            return;
        }
        if (!hitEnemies.Add(EnemyHitId(target))) return;
        ApplyHit(target);
        if (hitEnemies.Count >= maxEnemyHits) Destroy(gameObject);
    }

    // Общий путь урона для пробивных стрел и укреплений арены.
    public static MonoBehaviour FindEnemy(Component source)
    {
        if (source == null) return null;
        var shaulaZone = source.GetComponentInParent<ShaulaHitZone>();
        if (shaulaZone != null) return shaulaZone;
        var shaula = source.GetComponentInParent<ShaulaBoss>();
        if (shaula != null) return shaula;
        MonoBehaviour target = source.GetComponentInParent<Roberto>();
        if (target == null) target = source.GetComponentInParent<Enemy>();
        if (target == null) target = source.GetComponentInParent<SceletonDef>();
        if (target == null) target = source.GetComponentInParent<SceletMag>();
        if (target == null) target = source.GetComponentInParent<Slime>();
        if (target == null) target = source.GetComponentInParent<HealerEnemy>();
        if (target == null) target = source.GetComponentInParent<SlimeBoss>();
        return target;
    }

    public virtual bool IsHoly => false;

    private static int EnemyHitId(MonoBehaviour target) => target is ShaulaHitZone zone && zone.boss != null
        ? zone.boss.GetInstanceID() : target.GetInstanceID();

    public static bool CanDamageEnemy(Component target, bool holy)
        => target != null && target.CompareTag("HolyEnemy") == holy;

    protected virtual void ApplyHit(MonoBehaviour target)
    {
        if (target == null) return;
        if (target.CompareTag("HolyEnemy") && !IsHoly)
        {
            DamageNumbers.ShowImmune(target.transform);
            return;
        }
        DamageEnemy(target, damage, IsHoly);
    }

    public static void DamageEnemy(MonoBehaviour target, int amount, bool holy = false)
    {
        if (target == null || !target.gameObject.activeInHierarchy || amount <= 0) return;
        if (target is ShaulaHitZone zone) zone.TakeDamage(amount, holy);
        else if (target is ShaulaBoss shaula) shaula.TakeDamage(amount, holy);
        else if (target is Roberto assassin)
        {
            if (holy && !assassin.CompareTag("HolyEnemy")) assassin.RestoreHealth(amount);
            else assassin.TakeDamage(amount);
        }
        else if (target is Enemy enemy) enemy.TakeDamage(amount, holy);
        else if (target is SceletonDef skeleton) skeleton.TakeDamage(amount, holy);
        else if (target is SceletMag mage) mage.TakeDamage(amount, holy);
        else if (target is Slime slime) slime.TakeDamage(amount, holy);
        else if (target is HealerEnemy healer) healer.TakeDamage(amount, holy);
        else if (target is SlimeBoss boss) boss.TakeDamage(amount, holy);
    }

    // Списки тегов для врагов (добавим "Healer" сюда, если хотите бить и хилера)
    protected string[] enemyTeg = { "Slime", "Skeleton", "Healer" };
    protected string[] enemyTegHoly = { "HolyEnemy" };

    // Публичные свойства для доступа (если где-то ещё используются)
    public string[] EnemyTags => enemyTeg;
    public string[] HolyEnemyTags => enemyTegHoly;
    
    protected virtual void Update()
    {
        // Уничтожаем стрелу через указанное время
        Destroy(gameObject, lifeTime);
    }

    protected virtual void OnCollisionEnter2D(Collision2D coll)
    {
        var other = coll.collider;
        if (other.GetComponentInParent<PlayerController>() != null
            || other.GetComponentInParent<ArrowDef>() != null) return;
        if (triggerProjectile || maxEnemyHits > 1) return;
        var target = FindEnemy(other);
        if (target != null && hitEnemies.Add(EnemyHitId(target))) ApplyHit(target);
        Destroy(gameObject);
    }
}
