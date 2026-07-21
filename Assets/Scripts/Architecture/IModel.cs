public interface IModel : IBelongToAchitecture,ICanSetArchitecture,ICanGetUtility,ICanSendEvent
{
    void Init();
}

//抽象接口，使用了接口的部分阉割
public abstract class AbstractModel : IModel
{
    private IAchitecture _architecture;
    IAchitecture IBelongToAchitecture.GetArchitecture()
    {
        return _architecture;
    }
    void ICanSetArchitecture.SetArchitecture(IAchitecture architecture)
    {
        _architecture= architecture;
    }

    void IModel.Init()
    {
        OnInit();
    }

    protected abstract void OnInit();
}