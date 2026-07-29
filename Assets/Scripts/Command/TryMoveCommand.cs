//ÇÐ»»×´Ì¬£¬ÒÆ¶¯
public class TryMoveCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();
        if (fsm._currentState.StateType != PlayerStateType.Move) fsm.ChangeState<FsmMoveState>();
    }
}

/// <summary>
/// ÇÐ»»×´Ì¬£¬ÒÆ¶¯(µÐÈË)
/// </summary>
public class TryEnemyMoveCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();
        if (fsm._currentState.StateType != PlayerStateType.Move) fsm.ChangeState<EnemyMoveState>();
    }
}