using UnityEngine;

public class PlayerController : MonoBehaviour, IController
{
    private IInputUtility _inputUtility;
    private IFSMSystem _fsmSystem;
    private IEntityModel _playerModel;
    private IAnimationUtility _animationUtility;
    private Rigidbody2D _rigidbody2D;
    private bool _prevAttack;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;


    public void Init()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _inputUtility = this.GetUtility<IInputUtility>();
        _fsmSystem = this.GetSystem<IFSMSystem>();
        _playerModel = this.GetModel<IEntityModel>();
        _animationUtility = this.GetUtility<IAnimationUtility>();
        _animationUtility.Init(GetComponent<Animator>());

        this.RegisterEvent<RequestAttackHitCheckEvent>(e =>
        {
            PerformAttackHitCheck();
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }


    public void Update()
    {
        var input = _inputUtility;
        var currentState = _fsmSystem._currentState.StateType;
        bool attackPressed = input.Attack && !_prevAttack;
        bool hasMoveInput = Mathf.Abs(input.Move.x) > 0.1f || Mathf.Abs(input.Move.y) > 0.1f;

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

        _fsmSystem.Update(Time.deltaTime);
    }

    public void FixedUpdate()
    {
        _fsmSystem.FixUpdate(Time.fixedDeltaTime);

        _playerModel.Position = transform.position;
        if (Mathf.Abs(_playerModel.MoveDelta.x) > 0.01f && _playerModel._currentState.Value != PlayerStateType.Hurt)
        {
            transform.localScale = new Vector3(_playerModel.MoveDelta.x > 0 ? 1 : -1, 1, 1);
        }

        _rigidbody2D.velocity = _playerModel.MoveDelta;
    }
    public void TakeDamage(int rawDamage, Vector2 knockbackDirection)
    {
        var combat = this.GetModel<ICombatModel>();
        if (combat.IsDead.Value) return;

        var combatSystem = this.GetSystem<ICombatSystem>();
        combatSystem.ApplyDamage(combat, rawDamage);//�۳�����ֵ

        if (!combat.IsDead.Value)
        {
            _playerModel.KnockbackDirection = knockbackDirection;
            this.SendCommand<TryHurtCommand>();//�л�Ϊ����״̬
        }
    }

    /// <summary>
    /// 执行攻击检测
    /// </summary>
    private void PerformAttackHitCheck()
    {
        var combat = this.GetModel<ICombatModel>();
        float attackRange = combat.AttackRange.Value;

        int facingDir = transform.localScale.x > 0 ? 1 : -1;
        Vector3 attackCenter = transform.position + Vector3.right * facingDir * 0.5f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRange);//������Χ���
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var enemy = hit.GetComponent<EnemyView>();
            if (enemy != null)
            {
                Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
               // Debug.Log($"Hit enemy! knockbackDir={knockbackDir}");
                enemy.TakeDamage(combat.AttackPower.Value, knockbackDir);//�۳���������ֵ
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
            //Debug.DrawLine(prevPoint, nextPoint, color);
            prevPoint = nextPoint;
        }
    }
}
