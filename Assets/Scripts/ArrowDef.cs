using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ArrowDef : MonoBehaviour
{
    public float lifeTime = 2f;
    public int damage = 1;
    [Min(1)] public int maxEnemyHits = 1;
    private readonly HashSet<int> hitEnemies = new HashSet<int>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (maxEnemyHits <= 1 || hitEnemies.Count >= maxEnemyHits || other.isTrigger || other.CompareTag("Player") || other.CompareTag("Projectile")) return;
        MonoBehaviour target = other.GetComponentInParent<Roberto>();
        if (target == null) target = other.GetComponentInParent<Enemy>();
        if (target == null) target = other.GetComponentInParent<SceletonDef>();
        if (target == null) target = other.GetComponentInParent<SceletMag>();
        if (target == null) target = other.GetComponentInParent<Slime>();
        if (target == null) target = other.GetComponentInParent<HealerEnemy>();
        if (target == null) target = other.GetComponentInParent<SlimeBoss>();
        if (target == null)
        {
            Destroy(gameObject); // Стены и прочие твёрдые препятствия.
            return;
        }
        if (!hitEnemies.Add(target.GetInstanceID())) return;
        if (target is Roberto assassin) assassin.TakeDamage(damage);
        else if (target is Enemy enemy) enemy.TakeDamage(damage, false);
        else if (target is SceletonDef skeleton) skeleton.TakeDamage(damage, false);
        else if (target is SceletMag mage) mage.TakeDamage(damage, false);
        else if (target is Slime slime) slime.TakeDamage(damage, false);
        else if (target is HealerEnemy healer) healer.TakeDamage(damage, false);
        else if (target is SlimeBoss boss) boss.TakeDamage(damage, false);
        if (hitEnemies.Count >= maxEnemyHits) Destroy(gameObject);
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
        // Проверяем, есть ли на объекте компонент ассассина (Roberto)
        Roberto assassin = coll.gameObject.GetComponent<Roberto>();
        if (assassin != null)
        {
            // Если компонент найден, наносим ассассину урон
            assassin.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Если тег объекта входит в список вражеских тегов
        if (enemyTeg.Contains(coll.gameObject.tag))
        {
            // Проверяем наличие различных компонентов врагов и наносим урон
            Enemy enemy = coll.gameObject.GetComponent<Enemy>();
            SceletonDef skeletonDef = coll.gameObject.GetComponent<SceletonDef>();
            SceletMag sceletMag = coll.gameObject.GetComponent<SceletMag>();
            Slime slime = coll.gameObject.GetComponent<Slime>();
            HealerEnemy healer = coll.gameObject.GetComponent<HealerEnemy>(); // <-- Добавлено

            if (enemy != null)
            {
                enemy.TakeDamage(damage, false);
            }
            if (skeletonDef != null)
            {
                skeletonDef.TakeDamage(damage, false);
            }
            if (sceletMag != null)
            {
                sceletMag.TakeDamage(damage, false);
            }
            if (slime != null)
            {
                slime.TakeDamage(damage, false);
            }
            if (healer != null)
            {
                healer.TakeDamage(damage, false);
            }
        }

        Destroy(gameObject); // Уничтожаем стрелу после попадания
    }
    // else 
    // {
    //     // Это не враг, можно просто уничтожить стрелу или что-то ещё
    // }
}
