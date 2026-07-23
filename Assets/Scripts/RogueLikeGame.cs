//�ܹ����
public class RogueLikeGame : Architecture<RogueLikeGame>
{
    protected override void Init()
    {
        this.RegisterSystem<FsmIdleState>(new FsmIdleState());
        this.RegisterSystem<FsmMoveState>(new FsmMoveState());
        this.RegisterSystem<FsmAttackState>(new FsmAttackState());
        this.RegisterSystem<IFSMSystem>(new FSMSystem());
        this.RegisterModel<IPlayerModel>(new PlayerModel());
        this.RegisterUtility<IInputUtility>(new InputUtility());
    }
}