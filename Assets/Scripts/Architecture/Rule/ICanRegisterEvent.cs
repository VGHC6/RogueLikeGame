using System;

public interface ICanRegisterEvent : IBelongToAchitecture
{

}

public static class CanRegisterEventExtension
{
    public static IUnRegister RegisterEvent<T>(this ICanRegisterEvent self, Action<T> OnEvent)
    {
        return self.GetArchitecture().RegisterEvent(OnEvent);
    }

    public static void UnRegisterEvent<T>(this ICanRegisterEvent self, Action<T> OnEvent)
    {
        self.GetArchitecture().UnRegisterEvent(OnEvent);
    }
}