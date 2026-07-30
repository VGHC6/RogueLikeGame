//这里放置全局的单例
public class RogueLikeGame : Architecture<RogueLikeGame>
{
    protected override void Init()
    {
        this.RegisterSystem<ICombatSystem>(new CombatSystem());//战斗系统

        //这里放置其他系统
        this.RegisterUtility<IInputUtility>(new InputUtility());//输入工具
        this.RegisterUtility<IHitstopUtility>(new HitstopUtility());//硬直工具
        this.RegisterUtility<ICameraUtility>(new CameraUtility());//相机工具
    }
}