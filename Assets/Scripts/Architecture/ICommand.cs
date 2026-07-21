//指令,由表现层发出
public interface ICommand : IBelongToAchitecture,ICanSetArchitecture,ICanGetUtility,ICanGetSystem,ICanGetModel,ICanSendEvent
{
    void Excute();
}

public abstract class AbstractCommand : ICommand
{
    private IAchitecture _architecture;
    public IAchitecture GetArchitecture()
    {
       return  _architecture;
    }

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture= architecture;
    }

    void ICommand.Excute()
    {
        OnExcute();
    }

    protected abstract void OnExcute();
}