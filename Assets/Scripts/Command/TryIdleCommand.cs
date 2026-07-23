//ÇÐ»»×´Ì¬£¬Õ¾Á¢
public class TryIdleCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();
        if (fsm._currentState.StateType != PlayerStateType.Idle) fsm.ChangeState<FsmIdleState>();
    }
}