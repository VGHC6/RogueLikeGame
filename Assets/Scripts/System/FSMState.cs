using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IFSMState : ISystem
{
    string AnimationName { get; }//¶¯»­Ãû³Æ
    public PlayerStateType StateType { get; }
    void OnEnter();
    void OnUpdate(float datetime);
    void OnFixUpdate(float datetime);
    void OnExit();
}

/// <summary>
/// Õ¾Á¢×´Ì¬
/// </summary>
public class FsmIdleState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Idle";
    public PlayerStateType StateType { get; } = PlayerStateType.Idle;
    public void OnEnter()
    {
    }

    public void OnFixUpdate(float datetime)
    {
    }

    public void OnUpdate(float datetime)
    {
        var input = this.GetUtility<IInputUtility>();//»ñÈ¡ÊäÈë
        if (input.Attack)
        {
            this.SendCommand<TryAttackCommand>();//·¢ËÍ¹¥»÷ÃüÁî
        }
        else if (input.Move.X > 0.1 || input.Move.Y > 0.1)
        {
            this.SendCommand<TryMoveCommand>();//·¢ËÍÒÆ¶¯ÃüÁî
        }
    }

    public void OnExit()
    {
    }

    protected override void OnInit()
    {
    }
}


/// <summary>
/// ÐÐ×ß×´Ì¬
/// </summary>
public class FsmMoveState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Move";

    public PlayerStateType StateType { get; } = PlayerStateType.Move;

    public void OnEnter()
    {
        throw new System.NotImplementedException();
    }

    public void OnExit()
    {
        throw new System.NotImplementedException();
    }

    public void OnFixUpdate(float datetime)
    {
        throw new System.NotImplementedException();
    }

    public void OnUpdate(float datetime)
    {
        throw new System.NotImplementedException();
    }

    protected override void OnInit()
    {
    }
}


/// <summary>
/// ¹¥»÷×´Ì¬
/// </summary>
public class FsmAttackState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Attack";

    public PlayerStateType StateType { get; } = PlayerStateType.Attack;

    public void OnEnter()
    {
        throw new System.NotImplementedException();
    }

    public void OnExit()
    {
        throw new System.NotImplementedException();
    }

    public void OnFixUpdate(float datetime)
    {
        throw new System.NotImplementedException();
    }

    public void OnUpdate(float datetime)
    {
        throw new System.NotImplementedException();
    }

    protected override void OnInit()
    {
    }
}