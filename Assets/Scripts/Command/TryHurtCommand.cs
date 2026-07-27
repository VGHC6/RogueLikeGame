public class TryHurtCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();//状态机系统
        var combat = this.GetModel<ICombatModel>();//战斗模型

        if (combat.IsDead.Value == true) return;
        if (fsm._currentState.StateType == PlayerStateType.Hurt) return;

        fsm.ChangeState<FsmHurtState>();
    }
}