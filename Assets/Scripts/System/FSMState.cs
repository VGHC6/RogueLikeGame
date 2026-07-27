using UnityEngine;

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
/// 待机状态。纯生命周期，输入判断由 PlayerController（IController）负责。
/// </summary>
public class FsmIdleState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Idle";
    public PlayerStateType StateType { get; } = PlayerStateType.Idle;

    public void OnEnter() { }

    public void OnUpdate(float datetime) { }

    public void OnFixUpdate(float datetime) { }

    public void OnExit() { }

    protected override void OnInit() { }
}


/// <summary>
/// 移动状态。OnFixUpdate 读 Utility 计算移动量（上层→下层数据查询，合规）。
/// 输入驱动的状态切换由 PlayerController 发 Command 完成。
/// </summary>
public class FsmMoveState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Move";
    public PlayerStateType StateType { get; } = PlayerStateType.Move;

    public void OnEnter() { }

    public void OnUpdate(float datetime) { }

    public void OnFixUpdate(float datetime)
    {
        var model = this.GetModel<IPlayerModel>();
        var input = this.GetUtility<IInputUtility>();

        Vector2 direction = new Vector2(input.Move.x, input.Move.y).normalized;
        Vector3 movement = direction * model.MoveSpeed;

        model.MoveDelta = movement;
    }

    public void OnExit()
    {
        var model = this.GetModel<IPlayerModel>();
        model.MoveDelta = Vector3.zero;
    }

    protected override void OnInit() { }
}


/// <summary>
/// 攻击状态。管理攻击计时和判定帧。
/// 攻击结束自动回 Idle（时间驱动，System → System 直接调用）。
/// </summary>
public class FsmAttackState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Attack";
    public PlayerStateType StateType { get; } = PlayerStateType.Attack;

    private float _elapsedTime;
    private bool _hitChecked;
    private const float HitCheckTime = 0.25f;
    private const float AttackDuration = 0.5f;

    public void OnEnter()
    {
        _elapsedTime = 0f;
        _hitChecked = false;
    }

    public void OnUpdate(float datetime)
    {
        _elapsedTime += datetime;

        if (!_hitChecked && _elapsedTime >= HitCheckTime)
        {
            _hitChecked = true;
            this.SendEvent(new RequestAttackHitCheckEvent());
        }

        if (_elapsedTime >= AttackDuration)
        {
            this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
        }
    }

    public void OnFixUpdate(float datetime) { }

    public void OnExit() { }

    protected override void OnInit() { }
}

/// <summary>
/// 受伤状态
/// </summary>
public class FsmHurtState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Hurt";

    public PlayerStateType StateType { get; } = PlayerStateType.Hurt;

    private float _elapsed;
    private const float HurtDuration = 0.4f;//伤害时间

    protected override void OnInit()
    {
        //throw new System.NotImplementedException();
    }

    public void OnEnter()
    {
        _elapsed = 0f;
    }

    public void OnUpdate(float datetime)
    {
        _elapsed += datetime;

        if (_elapsed >= HurtDuration)
        {
            var combat = this.GetModel<ICombatModel>();
            if (combat.IsDead.Value)
            {
                this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
            }
            else
            {
                this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
            }
        }
    }

    public void OnFixUpdate(float datetime)
    {
        //throw new System.NotImplementedException();
    }

    public void OnExit()
    {
        //throw new System.NotImplementedException();
    }
}