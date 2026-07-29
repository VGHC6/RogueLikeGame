using UnityEngine;

/// <summary>
/// µÐÈË´ý»ú×´Ì¬
/// </summary>
public class EnemyIdleState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Idle";
    public PlayerStateType StateType { get; } = PlayerStateType.Idle;
    public void OnEnter()
    {
    }
    public void OnExit()
    {
    }
    public void OnFixUpdate(float datetime)
    {
    }
    public void OnUpdate(float datetime)
    {
    }
    protected override void OnInit()
    {
    }
}

/// <summary>
/// µÐÈËÒÆ¶¯×´Ì¬
/// </summary>
public class EnemyMoveState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } ="Move";
    public PlayerStateType StateType { get; } =PlayerStateType.Move;
    protected override void OnInit()
    {
    }
    public void OnEnter()
    {
    }
    public void OnFixUpdate(float datetime)
    {
        var model=this.GetModel<IEntityModel>();
        var ai = this.GetUtility<IEnemyAIUtility>();
        if (ai.HasTarget)
        {
            model.MoveDelta = ai.ChaseDirection;
        }
        else
        {
            model.MoveDelta=Vector2.zero;
        }
    }
    public void OnUpdate(float datetime)
    {
    }
    public void OnExit()
    {
        var model = this.GetModel<IEntityModel>();
        model.MoveDelta = Vector2.zero;//Í£Ö¹ÒÆ¶¯
    }
}


public class EnemyAttackState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } ="Attack";

    public PlayerStateType StateType { get; } =PlayerStateType.Attack;
    protected override void OnInit()
    {
    }
    public void OnEnter()
    {
    }
    public void OnFixUpdate(float datetime)
    {
    }

    public void OnUpdate(float datetime)
    {
    }
    public void OnExit()
    {
    }
}


