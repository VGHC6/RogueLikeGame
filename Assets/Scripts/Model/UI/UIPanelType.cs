public enum UIPanelType
{
    None,
    Start,//开始阶段
    GamePlay,//游戏进行
    Pause,//暂停
    GameOver,//游戏结束
    SaveLoad,//保存/加载
}

public interface IGameStateModel : IModel
{
    BindableProperty<UIPanelType> _currentPhase { get; }
    UIPanelType SaveReturnPanel { get; }//保存/加载返回面板
    SavePanelMode SaveMode { get; }
    bool IsWin { get; }
    void StartGame();//开始游戏
    void GameOver(bool isWin);//游戏结束
    void ReturnToMenu();//返回主菜单

    void OpenSaveLoadPanel(SavePanelMode mode,UIPanelType returnType);//打开保存/加载面板
    void CloseSaveLoadPanel();//关闭保存/加载面板
}

public class GameStateModel : AbstractModel,IGameStateModel
{
    public BindableProperty<UIPanelType> _currentPhase { get; } = new BindableProperty<UIPanelType>();
    public bool IsWin { get; set; }
    public UIPanelType SaveReturnPanel { get; set; }
    public SavePanelMode SaveMode{ get; set; }

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

    /// <summary>
    /// 打开保存/加载面板
    /// </summary>
    /// <param name="mode"></param>
    /// <param name="returnType"></param>
    public void OpenSaveLoadPanel(SavePanelMode mode, UIPanelType returnType)
    {
        SaveMode= mode;
        SaveReturnPanel = returnType;
        _currentPhase.Value = UIPanelType.SaveLoad;
    }

    /// <summary>
    /// 关闭保存/加载面板
    /// </summary>
    public void CloseSaveLoadPanel()
    {
     _currentPhase.Value = SaveReturnPanel;
    }
}
