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
}

public class EntityModel : AbstractModel, IEntityModel//实体特有接口
{
    public BindableProperty<PlayerStateType> _currentState { get; } = new BindableProperty<PlayerStateType>()
    {
        Value = PlayerStateType.Idle
    };
    public Vector2 MoveDelta { get; set; }
    public float MoveSpeed { get; set; } = 5f;

    protected override void OnInit()
    { }
}
