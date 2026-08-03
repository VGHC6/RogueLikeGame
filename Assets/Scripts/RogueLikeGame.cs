//全局唯一的入口
public class RogueLikeGame : Architecture<RogueLikeGame>
{
    protected override void Init()
    {
        // ========== Model ==========
        this.RegisterModel<IEntityModel>(new PlayerEntityModel());
        this.RegisterModel<ICombatModel>(new PlayerCombatModel());
        this.RegisterModel<IEnemyModel>(new EnemyModel());

        // ========== System ==========
        this.RegisterSystem<ICombatSystem>(new CombatSystem());
        this.RegisterSystem<IEnemyManagerSystem>(new EnemyManagerSystem());
        this.RegisterSystem<IUISystem>(new UISystem());

        // Player FSM
        this.RegisterSystem<FsmIdleState>(new FsmIdleState());
        this.RegisterSystem<FsmMoveState>(new FsmMoveState());
        this.RegisterSystem<FsmAttackState>(new FsmAttackState());
        this.RegisterSystem<FsmHurtState>(new FsmHurtState());
        this.RegisterSystem<IFSMSystem>(new FSMSystem());

        // ========== Utility ==========
        this.RegisterUtility<IInputUtility>(new InputUtility());
        this.RegisterUtility<IHitstopUtility>(new HitstopUtility());
        this.RegisterUtility<ICameraUtility>(new CameraUtility());
        this.RegisterUtility<IAnimationUtility>(new AnimationUtility());
    }
}
