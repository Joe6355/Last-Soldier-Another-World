using UnityEngine;

public class HolyArrow : ArrowDef
{
    public int healingAmount = 2; //  оличество здоровь€, которое восстанавливаетс€

    protected override void ApplyHit(MonoBehaviour target)
    {
        DamageEnemy(target, damage, holy: true);
    }
}
