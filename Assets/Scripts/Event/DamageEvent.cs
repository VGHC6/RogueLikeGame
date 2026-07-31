/// <summary>
/// 伤害计算完成后广播。
/// UI 层订阅此事件做伤害数字、血条等反应。
/// </summary>
public class DamageEvent
{
    public int RawDamage { get; set; }
    public int FinalDamage { get; set; }
    public bool IsDead { get; set; }
    public int? EnemyId { get; set; }//攻击者 ID，用于 UI 层显示伤害来源
}
