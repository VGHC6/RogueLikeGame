using UnityEngine;
using UnityEngine.InputSystem;

public interface IInputUtility : IUtility
{
    Vector2 Move { get; }
    bool Attack { get; }

    bool Pause { get; }
    void Enable();
    void Disable();
}

public class InputUtility : IInputUtility
{
    PlayerInput _playerInput=new PlayerInput();
    private IAchitecture _architecture;
    public Vector2 Move => _playerInput.Player.Move.ReadValue<Vector2>();
    public bool Attack => _playerInput.Player.Attack.ReadValue<float>() > 0.5f;
    public bool Pause => _playerInput.Player.Back.ReadValue<float>() > 0.5f;

    public IAchitecture GetArchitecture() => _architecture;

    public void Enable()
    {
        _playerInput.Enable();
    }

    public void Disable()
    {
        _playerInput.Disable();
    }

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture= architecture;
    }
}
