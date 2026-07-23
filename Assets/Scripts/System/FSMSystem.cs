
public interface IFSMSystem : ISystem
{
    IFSMState _currentState { get; }
    void Update(float deltaTime);
    void FixUpdate(float deltaTime);
    void ChangeState<T>() where T : class, IFSMState;
}


public class FSMSystem : AbstractSystem, IFSMSystem
{
    public IFSMState _currentState { get; private set; }
    private IPlayerModel _playerModel;

    protected override void OnInit()
    {
        _currentState = this.GetSystem<FsmIdleState>();
        _playerModel = this.GetModel<IPlayerModel>();
    }

    public void Update(float deltaTime)
    {
        _currentState?.OnUpdate(deltaTime);
    }

    public void FixUpdate(float deltaTime)
    {
        _currentState?.OnFixUpdate(deltaTime);
    }

    public void ChangeState<T>() where T : class, IFSMState
    {
        var newState = this.GetSystem<T>();
        var newStateType = newState.StateType;

        if (_currentState != null) _currentState.OnExit();

        _currentState = newState;
        _currentState.OnEnter();

        _playerModel._currentState.Value = newStateType;

        this.SendEvent(new PlayerStateChangedEvent
        {
            StateType = newStateType,
            AnimationName = newState.AnimationName
        });
    }
}
