using UnityEngine;

public interface IMoveSystem : ISystem
{
    BindableProperty<Vector2> Move { get; }
    BindableProperty<bool> Attack { get; }
}


public class MoveSystem : AbstractSystem, IMoveSystem
{
    public BindableProperty<Vector2> Move => new BindableProperty<Vector2>()
    {
        Value = Vector2.zero
    };

    public BindableProperty<bool> Attack =>new BindableProperty<bool>()
    {
        Value = false
    };

    protected override void OnInit()
    {

    }
}