using UnityEditor;
using UnityEngine;

public class PlayerController : MonoBehaviour, IController
{
    private PlayerArchitecture _architecture;//架构

    private IInputUtility _inputUtility;//输入管理器
    private IFSMSystem _fsmSystem;
    private IEntityModel _playerModel;
    private IAnimationUtility _animationUtility;
    private Rigidbody2D _rigidbody2D;
    private bool _prevAttack;

    private bool _initialized;//是否初始化


    public IAchitecture GetArchitecture()
    {
        if (!_initialized) InitArchitecture();
        return _architecture;
    }

    private void InitArchitecture()
    {
        _architecture = new PlayerArchitecture(RogueLikeGame.Interface);
        _inputUtility = _architecture.GetUtility<IInputUtility>();
        _fsmSystem = _architecture.GetSystem<IFSMSystem>();
        _playerModel = _architecture.GetModel<IEntityModel>();
        _animationUtility = _architecture.GetUtility<IAnimationUtility>();
        _animationUtility.Init(GetComponent<Animator>());
        _inputUtility.Awake();

        _initialized = true;

        _architecture.RegisterEvent<RequestAttackHitCheckEvent>(e =>
        {
            PerformAttackHitCheck();
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    public void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    public void OnEnable()
    {
        GetArchitecture();
        _inputUtility.Enable();
    }

    public void Update()
    {
        if (!_initialized) return;

        var input = _inputUtility;
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
        if (!_initialized) return;


        _fsmSystem.FixUpdate(Time.fixedDeltaTime);
        // Debug.Log("移动量：" + _playerModel.MoveDelta);
        _playerModel.Position = transform.position;
        if (Mathf.Abs(_playerModel.MoveDelta.x) > 0.01f && _playerModel._currentState.Value != PlayerStateType.Hurt)
        {
            transform.localScale = new Vector3(_playerModel.MoveDelta.x > 0 ? 1 : -1, 1, 1);
        }

        _rigidbody2D.velocity = _playerModel.MoveDelta;
    }

    public void OnDisable()
    {
        if (!_initialized) return;
        _inputUtility.Disable();
    }


    /// <summary>
    /// 受到伤害
    /// </summary>
    /// <param name="rawDamage"></param>
    public void TakeDamage(int rawDamage, Vector2 knockbackDirection)
    {
        var combat = this.GetModel<ICombatModel>();
        if (combat.IsDead.Value) return;

        var combatSystem = this.GetSystem<ICombatSystem>();
        combatSystem.ApplyDamage(combat, rawDamage);

        if (!combat.IsDead.Value)
        {
            _playerModel.KnockbackDirection = knockbackDirection;
            this.SendCommand<TryHurtCommand>();
        }
    }

    /// <summary>
    /// 攻击判定
    /// </summary>
    private void PerformAttackHitCheck()
    {
        var combat = this.GetModel<ICombatModel>();
        float attackRange =combat.AttackRange.Value;

        int facingDir = transform.localScale.x > 0 ? 1 : -1;//朝向方向
        Vector3 attackCenter = transform.position + Vector3.right * facingDir * 0.5f;//攻击中心


        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRange);//获取攻击范围内的碰撞体
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue; // 排除自己

            var enemy = hit.GetComponent<EnemyController>();//获取敌人的控制器
            if (enemy != null)
            {
                Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
                enemy.TakeDamage(combat.AttackPower.Value, knockbackDir);//对敌人造成伤害
                //Debug.Log("攻击了敌人");
                break;
            }
        }
    }


    /// <summary>
    /// 绘制攻击范围
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    /// <param name="color"></param>
    void DrawAttackRangeCircle(Vector3 center, float radius, Color color)
    {
        int segments = 32;
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            Debug.DrawLine(prevPoint, nextPoint, color);
            prevPoint = nextPoint;
        }
    }
}
