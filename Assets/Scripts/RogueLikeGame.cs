//全局唯一的入口
public class RogueLikeGame : Architecture<RogueLikeGame>
{
    protected override void Init()
    {
        // ========== Model ==========
        this.RegisterModel<IEntityModel>(new PlayerEntityModel());
        this.RegisterModel<ICombatModel>(new PlayerCombatModel());
        this.RegisterModel<IEnemyModel>(new EnemyModel());
        this.RegisterModel<IGameStateModel>(new GameStateModel());
        this.RegisterModel<IMapModel>(new MapModel());
        this.RegisterModel<IItemModel>(new ItemModel());

        // ========== System ==========
        this.RegisterSystem<IMapGeneratorSystem>(new MapGeneratorSystem());//这个要在IEnemyManagerSystem之前，因为敌人需要地图信息来生成
        this.RegisterSystem<ICombatSystem>(new CombatSystem());
        this.RegisterSystem<IEnemyManagerSystem>(new EnemyManagerSystem());
        this.RegisterSystem<IUISystem>(new UISystem());
        this.RegisterSystem<IDropSystem>(new DropSystem());
        
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
        this.RegisterUtility<ISpawnUtility>(new SpawnUtility());
    }
}
