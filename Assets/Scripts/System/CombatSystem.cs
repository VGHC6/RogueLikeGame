using UnityEngine;
//战斗系统
public struct DamageResult
{
    public int FinalDamage;//最终伤害
    public bool IsDead;//是否死亡
}

public interface ICombatSystem : ISystem
{
    DamageResult ApplyDamage(ICombatModel targetCombat, int rawDamage);//应用伤害
}

public class CombatSystem : AbstractSystem, ICombatSystem
{
    public DamageResult ApplyDamage(ICombatModel targetCombat, int rawDamage)//应用伤害
    {
        int finalDamage = Mathf.Max(1, rawDamage - targetCombat.DefensePower.Value);//伤害减去护甲
        targetCombat.CurrentHp.Value = Mathf.Max(0, targetCombat.CurrentHp.Value - finalDamage);
        bool dead = targetCombat.CurrentHp.Value <= 0;
        targetCombat.IsDead.Value = dead;

        //发送伤害事件
        this.SendEvent(new DamageEvent
        {
            RawDamage = rawDamage,
            FinalDamage = finalDamage,
            IsDead = dead,
        });

        //返回伤害结果
        return new DamageResult
        {
            FinalDamage = finalDamage,
            IsDead = dead
        };
    }

    protected override void OnInit(){}
}