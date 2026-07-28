//实体架构，不走单例，负责管理玩家
public class PlayerArchitecture : EntityArchitecture
{
    public PlayerArchitecture(IAchitecture parent) : base(parent)
    {
        RegisterModel<IEntityModel>(new EntityModel());//数据层,状态机状态,移动属性
        RegisterModel<ICombatModel>(new CombatModel());//数据层,玩家属性
        //玩家状态机
        RegisterSystem<FsmIdleState> (new FsmIdleState());
        RegisterSystem<FsmMoveState>(new FsmMoveState());
        RegisterSystem<FsmAttackState>(new FsmAttackState());
        RegisterSystem<FsmHurtState>(new FsmHurtState());
        //状态机管理器
        RegisterSystem<IFSMSystem>(new FSMSystem());
        //初始化 
        InitEntities();
    }
}