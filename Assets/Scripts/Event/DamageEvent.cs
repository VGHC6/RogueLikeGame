/// <summary>
/// 伤害结算完毕后发送。
/// UI 层订阅用来弹出伤害数字、震屏等反馈。
/// </summary>
public class DamageEvent
{
    public int RawDamage { get; set; }//攻击力
    public int FinalDamage { get; set; }//最终伤害
    public bool IsDead { get; set; }//是否死亡
}