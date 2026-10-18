using UnityEngine;

// A hit on any limb still belongs to one enemy (including piercing volleys).
public sealed class ShaulaHitZone : MonoBehaviour
{
    public enum Zone { Shell, Joint, Tail }
    public ShaulaBoss boss;
    public Zone zone;
    public bool IsAlive => boss != null && boss.IsAlive && (zone != Zone.Tail || boss.TailIntact);
    public float AimWeight => zone == Zone.Tail ? boss.TailExposed ? .35f : .8f : 1f;
    public void TakeDamage(int amount, bool holy) => boss?.ReceiveHit(amount, holy, zone, transform);
    public void ApplyPoison(int damage, int ticks, float interval) => boss?.ApplyPoison(damage, ticks, interval);
}
