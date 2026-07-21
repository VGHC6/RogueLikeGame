//Êý¾Ý²ã
public interface ICanGetModel : IBelongToAchitecture
{

}

public static class CanGetModelExtension
{
    public static T GetModel<T>(this ICanGetModel self) where T : class, IModel
    {
        return self.GetArchitecture().GetModel<T>();
    }
}