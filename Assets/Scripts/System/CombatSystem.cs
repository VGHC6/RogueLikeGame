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
    DamageResult ApplyEnemyDamage(int enemyID, int rawDamage);//应用伤害
}

public class CombatSystem : AbstractSystem, ICombatSystem
{
    public DamageResult ApplyDamage(ICombatModel targetCombat, int rawDamage)//应用伤害,角色
    {
        int finalDamage = Mathf.Max(1, rawDamage - targetCombat.DefensePower.Value);//伤害减去护甲
        targetCombat.CurrentHp.Value = Mathf.Max(0, targetCombat.CurrentHp.Value - finalDamage);//扣血
        bool dead = targetCombat.CurrentHp.Value <= 0;//是否死亡
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

    public DamageResult ApplyEnemyDamage(int enemyID, int rawDamage)//应用伤害,敌人
    {
        var model = this.GetModel<IEnemyModel>();//得到敌人模型
        var data = model.Get(enemyID);//得到敌人数据
        int finalDamage = Mathf.Max(1, rawDamage - data.DefensePower);//伤害减去护甲
        int newHp=Mathf.Max(0, data.CurrentHp - finalDamage);//扣血
        model.SetCurrentHp(enemyID, newHp);//扣血
        this.SendEvent(new DamageEvent
        {
            RawDamage = rawDamage,
            FinalDamage = finalDamage,
            IsDead =newHp <= 0,
            EnemyId = enemyID,
        });

        return new DamageResult
        {
            FinalDamage = finalDamage,
            IsDead = newHp <= 0,
        };
    }

    protected override void OnInit(){}
}