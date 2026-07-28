using UnityEngine;

public class EnemyController : MonoBehaviour, IController
{
    private EnemyArchitecture _architecture;
    private IFSMSystem _fsmSystem;
    private IEntityModel _entityModel;
    private ICombatModel _combatModel;
    private Rigidbody2D _rigidbody2D;
    public IAchitecture GetArchitecture() => _architecture;

    public void Awake()
    {
        _architecture = new EnemyArchitecture(RogueLikeGame.Interface);

        _fsmSystem = _architecture.GetSystem<IFSMSystem>();
        _entityModel = _architecture.GetModel<IEntityModel>();
        _combatModel = _architecture.GetModel<ICombatModel>();

        _rigidbody2D = GetComponent<Rigidbody2D>();

        this.RegisterEvent<RequestAttackHitCheckEvent>(e =>
        {
            PerformAttackHitCheck();
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }


    public void TakeDamage(int rawDamage)
    {
        if (_combatModel.IsDead.Value) return;

        var combatSystem = this.GetSystem<ICombatSystem>();
        combatSystem.ApplyDamage(_combatModel, rawDamage);

        if (!_combatModel.IsDead.Value)
            this.SendCommand<TryHurtCommand>();
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
        float attackRange = 1.5f;
        int facingDir = transform.localScale.x > 0 ? 1 : -1;
        Vector3 attackCenter = transform.position + Vector3.right * facingDir * 0.8f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRange);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var player = hit.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(combat.AttackPower.Value);
                break;
            }
        }
    }
}