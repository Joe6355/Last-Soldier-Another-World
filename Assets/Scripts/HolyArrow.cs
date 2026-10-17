using UnityEngine;

public class HolyArrow : ArrowDef
{
    public int healingAmount = 2; //  оличество здоровь€, которое восстанавливаетс€

    public override bool IsHoly { get { return true; } }
}
