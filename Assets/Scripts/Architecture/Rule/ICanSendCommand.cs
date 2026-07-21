//√¸¡Ó
public interface ICanSendCommand : IBelongToAchitecture
{

}

public static class CanSendCommandExtension
{
    public static void SendCommand<T>(this ICanSendCommand self) where T : ICommand, new()
    {
        self.GetArchitecture().SendCommand<T>();
    }

    public static void SendCommand<T>(this ICanSendCommand self, T command) where T : ICommand
    {
        self.GetArchitecture().SendCommand<T>(command);
    }
}