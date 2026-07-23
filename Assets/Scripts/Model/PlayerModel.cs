using UnityEngine;

public enum PlayerStateType
{
    Idle,
    Attack,
    Move,
    Hurt
}

public interface IPlayerModel : IModel
{
    BindableProperty<PlayerStateType> _currentState { get; }
    Vector2 MoveDelta { get; set; }
    float MoveSpeed { get; set; }
}

public class PlayerModel : AbstractModel, IPlayerModel
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
