public enum PlayerStateType
{
    Idle,
    Attack,
    Move,
    Hurt
}
public interface IPlayerModel : IModel
{
    BindableProperty<PlayerStateType> _currentState { get; }//´æ´¢µ±Ç°×´Ì¬
}


public class PlayerModel : AbstractModel, IPlayerModel
{
    public BindableProperty<PlayerStateType> _currentState { get; } = new BindableProperty<PlayerStateType>()
    {
        Value = PlayerStateType.Idle
    };

    protected override void OnInit()
    { }
}