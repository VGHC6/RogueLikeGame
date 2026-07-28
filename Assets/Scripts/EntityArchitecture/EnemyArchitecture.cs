//实体架构，不走单例，负责管理敌人

public class EnemyArchitecture : EntityArchitecture
{
    public EnemyArchitecture(IAchitecture parent) : base(parent)
    {
        RegisterModel<IEntityModel>(new EntityModel());//角色属性，在每个实体都是唯一的
        RegisterModel<ICombatModel>(new CombatModel());//战斗属性
        //角色控制器
        RegisterSystem<FsmIdleState>(new FsmIdleState());
        RegisterSystem<FsmMoveState>(new FsmMoveState());
        RegisterSystem<FsmAttackState>(new FsmAttackState());
        RegisterSystem<FsmHurtState>(new FsmHurtState());
        //状态机管理器
        RegisterSystem<IFSMSystem>(new FSMSystem());

        InitEntities();
    }
}