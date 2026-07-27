using UnityEngine;

public class PlayerController : MonoBehaviour, IController
{
    private IInputUtility _inputUtility;
    private IFSMSystem _fsmSystem;
    private IPlayerModel _playerModel;

    private Rigidbody2D _rigidbody2D;
    private bool _prevAttack;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void Awake()
    {
        _inputUtility = this.GetUtility<IInputUtility>();
        _fsmSystem = this.GetSystem<IFSMSystem>();
        _playerModel = this.GetModel<IPlayerModel>();

        _rigidbody2D = this.GetComponent<Rigidbody2D>();

        _inputUtility.Awake();


        //攻击事件
        this.RegisterEvent<RequestAttackHitCheckEvent>(e =>
        {
            PerformAttackHitCheck();
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    public void OnEnable()
    {
        _inputUtility.Enable();
    }

    public void Update()
    {
        var input = this.GetUtility<IInputUtility>();
        var currentState = _fsmSystem._currentState.StateType;
        bool attackPressed = input.Attack && !_prevAttack;
        bool hasMoveInput = Mathf.Abs(input.Move.x) > 0.1f || Mathf.Abs(input.Move.y) > 0.1f;

        // Hurt 硬直、Attack 攻击期间不接受输入切换（Attack 时间驱动自动结束）
        if (currentState != PlayerStateType.Hurt && currentState != PlayerStateType.Attack)
        {
            if (attackPressed)
            {
                this.SendCommand<TryAttackCommand>();
            }
            else if (hasMoveInput && currentState != PlayerStateType.Move)
            {
                this.SendCommand<TryMoveCommand>();
            }
            else if (!hasMoveInput && currentState == PlayerStateType.Move)
            {
                this.SendCommand<TryIdleCommand>();
            }
        }

        _prevAttack = input.Attack;

        // 驱动 FSM
        _fsmSystem.Update(Time.deltaTime);
    }

    public void FixedUpdate()
    {
        _fsmSystem.FixUpdate(Time.fixedDeltaTime);

        _rigidbody2D.velocity = _playerModel.MoveDelta;
    }

    public void OnDisable()
    {
        _inputUtility.Disable();
    }



    /// <summary>
    /// 攻击判定
    /// </summary>
    private void PerformAttackHitCheck()
    {
        var combat= this.GetModel<ICombatModel>();
        float attackRange = 1.5f;//攻击范围
        Vector3 attackCenter = transform.position + transform.right * 0.8f;//攻击中心

        Collider2D[] hits= Physics2D.OverlapCircleAll(attackCenter, attackRange);//获取攻击范围内的碰撞体
        foreach(var hit in hits)
        {
            if (hit.gameObject == gameObject) continue; // 排除自己

            var enemy=hit.GetComponent<EnemyController>();//获取敌人的控制器
            if (enemy != null)
            {
                //enemy.TakeDamage(combat.AttackPower.Value);//对敌人造成伤害
                Debug.Log("攻击了敌人");
            }
        }

    }
}
