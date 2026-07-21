//系统层接口
public interface ISystem : IBelongToAchitecture, ICanSetArchitecture, ICanGetModel, ICanGetUtility, ICanSendEvent, ICanRegisterEvent,ICanGetSystem
{
    void Init();
}

public abstract class AbstractSystem : ISystem
{
    IAchitecture _architecture = null;
    public IAchitecture GetArchitecture()
    {
        return _architecture;
    }

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
    }

    void ISystem.Init()
    {
        OnInit();
    }

    protected abstract void OnInit();
}