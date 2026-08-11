//战斗属性
public interface ICombatModel : IModel
{
    BindableProperty<int> CurrentHp { get; }//当前血量
    BindableProperty<int> MaxHp { get; }//最大血量
    BindableProperty<int> AttackPower { get; }//攻击力
    BindableProperty<int> DefensePower { get; }//防御力
    BindableProperty<float> AttackRange { get; }//攻击范围
    BindableProperty<bool> IsDead { get; }//是否死亡
}


/// <summary>
/// 玩家战斗属性
/// </summary>
public class PlayerCombatModel : AbstractModel, ICombatModel
{
    protected override void OnInit()
    {
        MaxHp.Value = 6;
        CurrentHp.Value = MaxHp.Value;
        AttackPower.Value = 10;
        DefensePower.Value = 1;
        IsDead.Value = false;
    }

    public BindableProperty<int> CurrentHp { get; } = new BindableProperty<int>();

    public BindableProperty<int> MaxHp { get; } = new BindableProperty<int>();

    public BindableProperty<int> AttackPower { get; } = new BindableProperty<int>();

    public BindableProperty<int> DefensePower { get; } = new BindableProperty<int>();

    public BindableProperty<bool> IsDead { get; } = new BindableProperty<bool>();

    public BindableProperty<float> AttackRange { get; } =new BindableProperty<float>() { Value = 0.6f };
}
