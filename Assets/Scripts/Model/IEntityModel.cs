using UnityEngine;

public enum PlayerStateType
{
    Idle,
    Attack,
    Move,
    Hurt
}

public interface IEntityModel : IModel//框架底层接口
{
    BindableProperty<PlayerStateType> _currentState { get; }
    Vector2 MoveDelta { get; set; }
    float MoveSpeed { get; set; }
    Vector2 Position { get; set; }
     float KnockbackForce { get; }   // 被击退的力度
     float KnockbackDecay { get; }  // 每帧衰减系数
    Vector2 KnockbackDirection { get; set; }
}


/// <summary>
/// 玩家实体特有接口
/// </summary>
public class PlayerEntityModel : AbstractModel, IEntityModel//实体特有接口
{
    public BindableProperty<PlayerStateType> _currentState { get; } = new BindableProperty<PlayerStateType>()
    {
        Value = PlayerStateType.Idle
    };
    public Vector2 MoveDelta { get; set; }
    public float MoveSpeed { get; set; } = 5f;
    public Vector2 Position { get; set; }
    public float KnockbackForce { get; } = 8f;    // 玩家被击退的力度
    public float KnockbackDecay { get; } = 0.85f;  // 每帧衰减系数
    public Vector2 KnockbackDirection { get; set; }

    protected override void OnInit()
    { }
}

/// <summary>
/// 敌人实体特有接口
/// </summary>
public class EnemyEntityModel : AbstractModel, IEntityModel//实体特有接口
{
    public BindableProperty<PlayerStateType> _currentState { get; } = new BindableProperty<PlayerStateType>()
    {
        Value = PlayerStateType.Idle
    };
    public Vector2 MoveDelta { get; set; }
    public float MoveSpeed { get; set; } = 3f;
    public float ChaseRange { get; } = 5f;
    public float AttackRange { get; set; } = 1f;
    public Vector2 Position { get; set; }
    public float KnockbackForce { get; } = 6f;    // 被击退的力度
    public float KnockbackDecay { get; } = 0.85f;  // 每帧衰减系数
    public Vector2 KnockbackDirection { get; set; }
    protected override void OnInit()
    { }
}
