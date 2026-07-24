using UnityEngine;
using UnityEngine.Windows;

public interface IFSMState : ISystem
{
    string AnimationName { get; }
    public PlayerStateType StateType { get; }
    void OnEnter();
    void OnUpdate(float datetime);
    void OnFixUpdate(float datetime);
    void OnExit();
}


/// <summary>
/// 空闲状态
/// </summary>
public class FsmIdleState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Idle";
    public PlayerStateType StateType { get; } = PlayerStateType.Idle;

    private bool _prevAttack;

    public void OnEnter()
    {
        _prevAttack = this.GetUtility<IInputUtility>().Attack;
        Debug.Log("OnEnter FsmIdleState");
    }

    public void OnUpdate(float datetime)
    {
        var input = this.GetUtility<IInputUtility>();

        if (input.Attack && !_prevAttack)
        {
            this.SendCommand<TryAttackCommand>();
        }
        else if (Mathf.Abs(input.Move.x) > 0.1f || Mathf.Abs(input.Move.y) > 0.1f)
        {
            this.SendCommand<TryMoveCommand>();
        }

        _prevAttack = input.Attack;
    }

    public void OnFixUpdate(float datetime) { }

    public void OnExit()
    {
        Debug.Log("OnExit FsmIdleState");
    }

    protected override void OnInit() { }
}


/// <summary>
/// 移动状态
/// </summary>
public class FsmMoveState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Move";
    public PlayerStateType StateType { get; } = PlayerStateType.Move;

    private bool _prevAttack;

    public void OnEnter()
    {
        _prevAttack = this.GetUtility<IInputUtility>().Attack;
        Debug.Log("OnEnter FsmMoveState");
    }

    public void OnUpdate(float datetime)
    {
        var input = this.GetUtility<IInputUtility>();

        if (input.Attack && !_prevAttack)
        {
            this.SendCommand<TryAttackCommand>();
        }
        else if (Mathf.Abs(input.Move.x) <= 0.1f && Mathf.Abs(input.Move.y) <= 0.1f)
        {
            this.SendCommand<TryIdleCommand>();
        }

        _prevAttack = input.Attack;
    }

    public void OnFixUpdate(float datetime)
    {
        var model = this.GetModel<IPlayerModel>();
        var input = this.GetUtility<IInputUtility>();

        Vector2 direction = new Vector3(input.Move.x, input.Move.y).normalized;//方位
        Vector3 movement = direction * model.MoveSpeed;

        model.MoveDelta = movement;//这个后续要重构,不应该在System直接修改Model
    }

    public void OnExit()
    {
        var model = this.GetModel<IPlayerModel>();
        model.MoveDelta = Vector3.zero;//这个后续要重构,不应该在System直接修改Model
    }

    protected override void OnInit() { }
}


/// <summary>
/// 攻击状态
/// </summary>
public class FsmAttackState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Attack";
    public PlayerStateType StateType { get; } = PlayerStateType.Attack;

    private float _elapsedTime;
    private const float AttackDuration = 0.5f;

    public void OnEnter()
    {
        _elapsedTime = 0f;
        Debug.Log("OnEnter FsmAttackState");
    }

    public void OnUpdate(float datetime)
    {
        _elapsedTime += datetime;

        var input = this.GetUtility<IInputUtility>();

        if (_elapsedTime >= AttackDuration)
        {
            this.SendCommand<TryIdleCommand>();
        }
        else if ((Mathf.Abs(input.Move.x) > 0.1f || Mathf.Abs(input.Move.y) > 0.1f) && _elapsedTime >= AttackDuration)
        {
            this.SendCommand<TryMoveCommand>();
        }
    }

    public void OnFixUpdate(float datetime) { }

    public void OnExit()
    {
        Debug.Log("OnExit FsmAttackState");
    }

    protected override void OnInit() { }
}
