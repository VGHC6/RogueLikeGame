using System.Linq.Expressions;

public interface IUISystem:ISystem
{
    UIPanelType _currentPanelType { get; }//当前面板类型
    void Changepanel(UIPanelType newPanelType);//切换面板
}

public class UISystem : AbstractSystem, IUISystem
{
    public UIPanelType _currentPanelType { get; private set; } = UIPanelType.None;

    public void Changepanel(UIPanelType newPanelType)
    {
        if (_currentPanelType == newPanelType) return;
        var old = _currentPanelType;//保存旧面板
        _currentPanelType = newPanelType;
        //切换面板
        //发送改变事件
        this.SendEvent(new UIPanelChangeEvent
        {
            OldPanel = old,
            NewPanel = newPanelType
        });
    }

    protected override void OnInit() { }
}