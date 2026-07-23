//ÇÐ»»×´Ì¬£¬¹¥»÷
public class TryAttackCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();
        if (fsm._currentState.StateType != PlayerStateType.Attack) fsm.ChangeState<FsmAttackState>();
    }
}