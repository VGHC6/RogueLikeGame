using UnityEngine;

public class EnemyController : MonoBehaviour, IController
{
    private EnemyArchitecture _architecture;
    private IFSMSystem _fsmSystem;
    private EnemyEntityModel _entityModel;
    private ICombatModel _combatModel;
    private EnemyAIUtility _enemyAIUtility;
    private Rigidbody2D _rigidbody2D;
    private HitstopUtility _hitstopUtility;
    private CameraUtility _cameraUtility;
    private bool _initialized;//ÊÇ·ñ³õÊ¼»¯

    public IAchitecture GetArchitecture()
    {
        if (!_initialized)
        {
            InitArchitecture();
        }
        return _architecture;
    }

    private void InitArchitecture()
    {
        _architecture = new EnemyArchitecture(RogueLikeGame.Interface);
        _fsmSystem = _architecture.GetSystem<IFSMSystem>();
        _entityModel = _architecture.GetModel<IEntityModel>() as EnemyEntityModel;
        _combatModel = _architecture.GetModel<ICombatModel>();
        _enemyAIUtility = _architecture.GetUtility<IEnemyAIUtility>() as EnemyAIUtility;
        _enemyAIUtility.Awake(transform);
        var animUtil = _architecture.GetUtility<IAnimationUtility>();
        _hitstopUtility = _architecture.GetUtility<IHitstopUtility>()as HitstopUtility;
        _hitstopUtility.Init(this);
        _cameraUtility= _architecture.GetUtility<ICameraUtility>() as CameraUtility;
        _cameraUtility.Init(this);
        animUtil.Init(GetComponent<Animator>());
        _initialized = true;
        this.RegisterEvent<RequestAttackHitCheckEvent>(e =>
        {
            PerformAttackHitCheck();//¹¥»÷ÅÐ¶¨
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }



    public void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    public void OnEnable()
    {
        GetArchitecture();
    }

    public void Update()
    {
        if (!_initialized) return;

        var _currentState = _fsmSystem._currentState.StateType;
        float dist = Vector2.Distance(transform.position, _enemyAIUtility.TargetPosition);
        //Debug.Log($"State:{_currentState} Dist:{dist:F2} AttackR:{_entityModel.AttackRange}ChaseR:{ _entityModel.ChaseRange}");
        if (_currentState != PlayerStateType.Attack && _currentState != PlayerStateType.Hurt)
        {
            if (dist < _entityModel.AttackRange)
            {
                this.SendCommand<TryAttackCommand>();
            }
            else if (dist <= _entityModel.ChaseRange)
            {
                if (_currentState != PlayerStateType.Move)
                    this.SendCommand<TryEnemyMoveCommand>();
            }
            else
            {
                if (_currentState != PlayerStateType.Idle)
                {
                    this.SendCommand<TryIdleCommand>();
                }
            }
        }
        _fsmSystem.Update(Time.deltaTime);
    }


    public void FixedUpdate()
    {
        if (!_initialized) return;

        //var combat = this.GetModel<ICombatModel>();
        //float attackRange = combat.AttackRange.Value;
        //int facingDir = transform.localScale.x > 0 ? 1 : -1;
        //Vector3 attackCenter = transform.position + Vector3.right * facingDir * 0.5f;
        //DrawAttackRangeCircle(attackCenter, attackRange, Color.red);


        _fsmSystem.FixUpdate(Time.fixedDeltaTime);

        _entityModel.Position = transform.position;
        if (Mathf.Abs(_rigidbody2D.velocity.x) > 0.01f && _entityModel._currentState.Value != PlayerStateType.Hurt)
        {
            transform.localScale = new Vector3(_entityModel.MoveDelta.x > 0 ? 1 : -1, 1, 1);
        }
        _rigidbody2D.velocity = _entityModel.MoveDelta;
    }


    public void TakeDamage(int rawDamage,Vector2 knockbackDirection)
    {
        if (_combatModel.IsDead.Value) return;

        var combatSystem = this.GetSystem<ICombatSystem>();
        combatSystem.ApplyDamage(_combatModel, rawDamage);

       _hitstopUtility.Trigger(0.08f);//¿¨Èâ
        _cameraUtility.Shake(0.1f, 0.2f);//¶¶¶¯
        if (!_combatModel.IsDead.Value)
        {
            _entityModel.KnockbackDirection = knockbackDirection;
            this.SendCommand<TryHurtCommand>();
        }
        else
            Die();
    }


    private void Die()
    {
        GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject, 0.2f);
    }

    private void PerformAttackHitCheck()
    {
        var combat = this.GetModel<ICombatModel>();
        float attackRange = combat.AttackRange.Value;
        int facingDir = transform.localScale.x > 0 ? 1 : -1;
        Vector3 attackCenter = transform.position + Vector3.right * facingDir * 0.8f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRange);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var player = hit.GetComponent<PlayerController>();
            if (player != null)
            {
                Vector2 knockbackDir = (player.transform.position - transform.position).normalized;
                player.TakeDamage(combat.AttackPower.Value, knockbackDir);
                break;
            }
        }
    }



    /// <summary>
    /// »æÖÆ¹¥»÷·¶Î§
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