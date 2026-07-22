public class TryMoveCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();
        if (fsm._currentState.StateType != PlayerStateType.Move) fsm.ChangeState<FsmMoveState>();
    }
}