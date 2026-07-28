//这里放置全局的单例
public class RogueLikeGame : Architecture<RogueLikeGame>
{
    protected override void Init()
    {
        this.RegisterSystem<ICombatSystem>(new CombatSystem());//战斗系统

        this.RegisterUtility<IInputUtility>(new InputUtility());//输入工具
    }
}