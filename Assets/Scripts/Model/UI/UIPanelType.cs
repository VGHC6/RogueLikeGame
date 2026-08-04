public enum UIPanelType
{
    None,
    Start,//开始阶段
    GamePlay,//游戏进行
    Pause,//暂停
    GameOver,//游戏结束
}

public interface IGameStateModel : IModel
{
    BindableProperty<UIPanelType> _currentPhase { get; }
    bool IsWin { get; }
    void StartGame();//开始游戏
    void GameOver(bool isWin);//游戏结束
    void ReturnToMenu();//返回主菜单
}

public class GameStateModel : AbstractModel,IGameStateModel
{
    public BindableProperty<UIPanelType> _currentPhase { get; } = new BindableProperty<UIPanelType>();
    public bool IsWin { get; set; }

    protected override void OnInit(){}

    public void StartGame()
    {
        _currentPhase.Value = UIPanelType.GamePlay;
    }
    public void ReturnToMenu()
    {
        _currentPhase.Value = UIPanelType.Start;
    }

    public void GameOver(bool isWin)
    {
        IsWin = isWin;
        _currentPhase.Value = UIPanelType.GameOver;
    }
}
