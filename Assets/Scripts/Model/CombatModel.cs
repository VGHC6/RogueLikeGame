public interface ICombatModel : IModel
{
    BindableProperty<int> CurrentHp { get; }
    BindableProperty<int> MaxHp { get; }
    BindableProperty<int> AttackPower { get; }//¹¥»÷Á¦
    BindableProperty<int> DefensePower { get; }//·ÀÓùÁ¦
    BindableProperty<bool> IsDead { get; }
}

public class CombatModel : AbstractModel, ICombatModel
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