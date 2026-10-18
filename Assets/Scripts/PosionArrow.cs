using System.Linq;
using UnityEngine;

public class PoisonArrow : ArrowDef
{
    [SerializeField] protected int poisonDamagePerTick = 1; // Урон яда за тик
    [SerializeField] protected int poisonTicks = 3; // Количество тиков
    [SerializeField] protected float tickInterval = 1f; // Интервал между тиками

    protected override void ApplyHit(MonoBehaviour target)
    {
        base.ApplyHit(target);
        if (target == null || !target.isActiveAndEnabled || target.CompareTag("HolyEnemy")) return;
        if (target is ShaulaHitZone zone) zone.ApplyPoison(poisonDamagePerTick, poisonTicks, tickInterval);
        else if (target is ShaulaBoss shaula) shaula.ApplyPoison(poisonDamagePerTick, poisonTicks, tickInterval);
        else if (target is Enemy enemy) enemy.ApplyPoison(poisonDamagePerTick, poisonTicks, tickInterval);
        else if (target is SceletonDef skeleton) skeleton.ApplyPoison(poisonDamagePerTick, poisonTicks, tickInterval);
        else if (target is SceletMag mage) mage.ApplyPoison(poisonDamagePerTick, poisonTicks, tickInterval);
        else if (target is Slime slime) slime.ApplyPoison(poisonDamagePerTick, poisonTicks, tickInterval);
        else if (target is HealerEnemy healer) healer.ApplyPoison(poisonDamagePerTick, poisonTicks, tickInterval);
        else if (target is SlimeBoss boss) boss.ApplyPoison(poisonDamagePerTick, poisonTicks, tickInterval);
    }
}