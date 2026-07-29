public interface ICombatModel : IModel
{
    BindableProperty<int> CurrentHp { get; }
    BindableProperty<int> MaxHp { get; }
    BindableProperty<int> AttackPower { get; }//攻击力
    BindableProperty<int> DefensePower { get; }//防御力
    BindableProperty<float> AttackRange { get; }//攻击范围
    BindableProperty<bool> IsDead { get; }
}


/// <summary>
/// 玩家数据
/// </summary>
public class PlayerCombatModel : AbstractModel, ICombatModel
{
    protected override void OnInit()
    {
        MaxHp.Value = 6;
        CurrentHp.Value = MaxHp.Value;
        AttackPower.Value = 1;
        DefensePower.Value = 1;
        IsDead.Value = false;
    }

    public BindableProperty<int> CurrentHp { get; } = new BindableProperty<int>();

    public BindableProperty<int> MaxHp { get; } = new BindableProperty<int>();

    public BindableProperty<int> AttackPower { get; } = new BindableProperty<int>();

    public BindableProperty<int> DefensePower { get; } = new BindableProperty<int>();

    public BindableProperty<bool> IsDead { get; } = new BindableProperty<bool>();
}


/// <summary>
/// 敌人数据
/// </summary>
public class EnemyCombatModel : AbstractModel, ICombatModel
{
    protected override void OnInit()
    {
        MaxHp.Value = 6;
        CurrentHp.Value = MaxHp.Value;
        AttackPower.Value = 1;
        DefensePower.Value = 1;
        IsDead.Value = false;
    }

    public BindableProperty<int> CurrentHp { get; } = new BindableProperty<int>();

    public BindableProperty<int> MaxHp { get; } = new BindableProperty<int>();

    public BindableProperty<int> AttackPower { get; } = new BindableProperty<int>();

    public BindableProperty<int> DefensePower { get; } = new BindableProperty<int>();

    public BindableProperty<bool> IsDead { get; } = new BindableProperty<bool>();
}