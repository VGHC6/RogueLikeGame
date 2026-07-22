using System.Numerics;

public interface IInputUtility : IUtility
{
    Vector2 Move { get; }
    bool Attack { get; }

    void Update();
}

public class InputUtility : IInputUtility
{
    public Vector2 Move { get; } =Vector2.Zero;

    public bool Attack { get; } =false;

    public IAchitecture GetArchitecture()
    {
        return RogueLikeGame.Interface;
    }

    public void Update()
    {
        //“∆∂Ø ‰»Î
    }
}