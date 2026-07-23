using UnityEngine;
using UnityEngine.InputSystem;

public interface IInputUtility : IUtility
{
    Vector2 Move { get; }
    bool Attack { get; }

    void Awake();
    void Enable();
    void Disable();
}

public class InputUtility : IInputUtility
{
    PlayerInput _playerInput;

    public Vector2 Move => _playerInput.Player.Move.ReadValue<Vector2>();
    public bool Attack => _playerInput.Player.Attack.ReadValue<float>() > 0.5f;

    public IAchitecture GetArchitecture() => null;

    public void Awake()
    {
        _playerInput = new PlayerInput();
    }

    public void Enable()
    {
        _playerInput.Enable();
    }

    public void Disable()
    {
        _playerInput.Disable();
    }
}
