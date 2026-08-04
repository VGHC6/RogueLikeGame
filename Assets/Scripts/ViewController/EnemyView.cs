using System.Globalization;
using UnityEngine;
/// <summary>
/// 敌人的视图
/// </summary>
public class EnemyView : MonoBehaviour, IController
{
    [SerializeField] private int _enemyId;//敌人的id

    private Rigidbody2D _rb;//刚体
    private Animator _anim;
    private Collider2D _col;//碰撞体

    public int EnemyId => _enemyId;//获取敌人的id

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void Init(int enemyId,EnemyRuntimeData data)
    {
        _enemyId = enemyId;
        RegisterEvents();
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
        _col = GetComponent<Collider2D>();
    }


    void OnDisable()
    {
        this.UnRegisterEvent<EnemyRequestHitCheckEvent>(OnRequestHitCheck);
        this.UnRegisterEvent<EnemyStateChangedEvent>(OnStateChanged);
        this.UnRegisterEvent<EnemyDeadEvent>(OnDead);
    }

    private void RegisterEvents()//注册事件
    {
        this.RegisterEvent<EnemyRequestHitCheckEvent>(OnRequestHitCheck)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<EnemyStateChangedEvent>(OnStateChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<EnemyDeadEvent>(OnDead)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void FixedUpdate()
    {

        var model = this.GetModel<IEnemyModel>();
        if (!model.TryGet(_enemyId, out var data)) return;//如果敌人的数据为空，则返回
       // Debug.Log($"[FixedUpdate] state={data.State} md={data.MoveDelta} kv={data.KnockbackVelocity}");
        _rb.velocity = data.MoveDelta;
        if (Mathf.Abs(data.MoveDelta.x) > 0.01f && data.State != EnemyActionState.Hurt)
        {
            transform.localScale = new Vector3(data.MoveDelta.x > 0 ? 1 : -1, 1, 1);
        }

        model.SetPosition(_enemyId, transform.position);//设置敌人的位置
    }

    public void TakeDamage(int rawDamage, Vector2 knockbackDirection)
    {
        var model = this.GetModel<IEnemyModel>();
        if (!model.TryGet(_enemyId, out var data)) return; 
        if (data.IsDead) return;
        var result=this.GetSystem<ICombatSystem>().ApplyEnemyDamage(_enemyId, rawDamage); //计算敌人的伤害
        if (!result.IsDead)
        {
            this.GetSystem<IEnemyManagerSystem>().OnEnemyDamaged(_enemyId, knockbackDirection);//发送命令，敌人受伤
        }
        this.SendCommand(new AddOtherHitParticel{});//发送命令，添加其他击中效果
    }

    private void OnRequestHitCheck(EnemyRequestHitCheckEvent e)//请求击打事件
    {
        if (e.EnemyId != _enemyId) return;
        PerformAttackHitCheck();//执行攻击检测
    }

    private void OnStateChanged(EnemyStateChangedEvent e)
    {
        if (e.EnemyId != _enemyId) return;
        string animName = e.NewState switch
        {
            EnemyActionState.Idle => "Idle",
            EnemyActionState.Chase => "Move",
            EnemyActionState.Attack => "Attack",
            EnemyActionState.Hurt => "Hurt",
            _ => "Idle"
        };
        _anim.CrossFade(animName, 0.1f);
    }

    private void OnDead(EnemyDeadEvent e)
    {
        if (e.EnemyId != _enemyId) return;
        _col.enabled = false;
        var model = this.GetModel<IEnemyModel>();
        model.Unregister(_enemyId);
        Destroy(gameObject, 0.2f);
    }

    private void PerformAttackHitCheck()//执行攻击检测
    {
        var model = this.GetModel<IEnemyModel>();
        var data = model.Get(_enemyId);
        int facingDir = transform.localScale.x > 0 ? 1 : -1;
        Vector3 attackCenter = transform.position + Vector3.right * facingDir * 0.8f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, data.AttackRange);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var player = hit.GetComponent<PlayerController>();
            if (player != null)
            {
                Vector2 knockbackDir = (player.transform.position - transform.position).normalized;
                player.TakeDamage(data.AttackPower, knockbackDir);
                break;
            }
        }
    }
}
