//使用工具类的接口
public interface ICanGetUtility : IBelongToAchitecture
{

}

public static class CanGetUtilityExtension
{
    public static T GetUtility<T>(this ICanGetUtility self) where T : class, IUtility
    {
        return self.GetArchitecture().GetUtility<T>();
    }
}