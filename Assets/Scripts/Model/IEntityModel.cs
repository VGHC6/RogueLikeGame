using UnityEngine;
/// <summary>
/// 状态机状态
/// </summary>
public enum PlayerStateType
{
    Idle,
    Attack,
    Move,
    Hurt
}

public interface IEntityModel : IModel
{
    BindableProperty<PlayerStateType> _currentState { get; }
    Vector2 MoveDelta { get; set; }
    float MoveSpeed { get; set; }
    Vector2 Position { get; set; }
    float KnockbackForce { get; }
    float KnockbackDecay { get; }
    Vector2 KnockbackDirection { get; set; }
}


/// <summary>
/// 玩家实体运行时数据
/// </summary>
public class PlayerEntityModel : AbstractModel, IEntityModel
{
    public BindableProperty<PlayerStateType> _currentState { get; } = new BindableProperty<PlayerStateType>()//当前状态
    {
        Value = PlayerStateType.Idle
    };
    public Vector2 MoveDelta { get; set; }//移动方向
    public float MoveSpeed { get; set; } = 5f;//速度
    public Vector2 Position { get; set; }//玩家位置
    public float KnockbackForce { get; } = 8f;//击退力
    public float KnockbackDecay { get; } = 0.85f;//击退衰减
    public Vector2 KnockbackDirection { get; set; }//击退方向

    protected override void OnInit()
    { }
}
