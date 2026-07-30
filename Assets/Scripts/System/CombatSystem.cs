using UnityEngine;

public struct DamageResult
{
    public int FinalDamage;
    public bool IsDead;
}

public interface ICombatSystem : ISystem
{

    DamageResult ApplyDamage(ICombatModel targetCombat, int rawDamage);
}

public class CombatSystem : AbstractSystem, ICombatSystem
{
    public DamageResult ApplyDamage(ICombatModel targetCombat, int rawDamage)
    {
        int finalDamage = Mathf.Max(1, rawDamage - targetCombat.DefensePower.Value);
        targetCombat.CurrentHp.Value = Mathf.Max(0, targetCombat.CurrentHp.Value - finalDamage);
        bool dead = targetCombat.CurrentHp.Value <= 0;
        targetCombat.IsDead.Value = dead;

        
        this.SendEvent(new DamageEvent
        {
            RawDamage = rawDamage,
            FinalDamage = finalDamage,
            IsDead = dead,
        });

        return new DamageResult
        {
            FinalDamage = finalDamage,
            IsDead = dead
        };
    }

    protected override void OnInit(){}
}