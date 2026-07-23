using UnityEngine;

public class PlayerController : MonoBehaviour, IController
{
    private IInputUtility _inputUtility;
    private IFSMSystem _fsmSystem;
    private IPlayerModel _playerModel;

    private Rigidbody2D _rigidbody2D;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void Awake()
    {
        _inputUtility = this.GetUtility<IInputUtility>();
        _fsmSystem = this.GetSystem<IFSMSystem>();
        _playerModel = this.GetModel<IPlayerModel>();

        _rigidbody2D=this.GetComponent<Rigidbody2D>();//物理组件

        _inputUtility.Awake();
    }

    public void OnEnable()
    {
        _inputUtility.Enable();
    }

    public void Update()
    {
        _fsmSystem.Update(Time.deltaTime);
    }

    public void FixedUpdate()
    {
        _fsmSystem.FixUpdate(Time.fixedDeltaTime);

        _rigidbody2D.velocity = _playerModel.MoveDelta;//设置刚体的速度

    }

    public void OnDisable()
    {
        _inputUtility.Disable();
    }
}
