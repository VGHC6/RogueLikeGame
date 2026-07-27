// 战斗系统
using UnityEngine;
using UnityEngine.UI;

public struct DamageResult
{
    public int FinalDamage;
    public bool IsDead;
}

public interface ICombatSystem : ISystem
{
    /// <summary>
    /// 受击处理
    /// </summary>
    /// <param name="RawDamage"></param>
    /// <returns></returns>
    DamageResult ApplyDamage(int RawDamage);//造成伤害
}

public class CombatSystem : AbstractSystem, ICombatSystem
{
    public DamageResult ApplyDamage(int RawDamage)
    {
        var combat = this.GetModel<CombatModel>();
        int finalDamage = Mathf.Max(1, RawDamage - combat.DefensePower.Value);
        bool dead = combat.CurrentHp.Value <= 0;
        combat.IsDead.Value = dead;

        //发射伤害事件
        this.SendEvent(new DamageEvent
        {
            RawDamage = RawDamage,
            FinalDamage = finalDamage,
            IsDead = dead,
        });

        return new DamageResult
        {
            FinalDamage = finalDamage,
            IsDead = dead
        };
    }

    protected override void OnInit()
    {
        throw new System.NotImplementedException();
    }
}