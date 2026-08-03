using System.Collections.Generic;
using UnityEngine;

public interface IEnemyManagerSystem : ISystem
{
    void Update(float dt);//更新
    void ChangeState(int enemyId, EnemyActionState newState);//改变状态
    void OnEnemyDamaged(int enemyId, Vector2 knockbackDir);//敌人受到伤害
}

public class EnemyManagerSystem : AbstractSystem, IEnemyManagerSystem
{
    private Transform _playerTransform;//玩家位置，用来计算击退
    private List<int> _idSnapshot = new();//敌人id快照

    private const float AttackDuration = 0.5f;//攻击持续时间
    private const float HitCheckTime = 0.25f;//攻击检测时间
    private const float HurtDuration = 0.4f;//受伤持续时间
    private const float KnockbackDecay = 0.85f;//击退衰减

    protected override void OnInit()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) _playerTransform = player.transform;
    }

    public void Update(float dt)
    {
        if (_playerTransform == null) return;

        var model = this.GetModel<IEnemyModel>();
        _idSnapshot.Clear();//清空快照
        foreach (var kv in model.GetAll())
        {
            _idSnapshot.Add(kv.Key);
        }

        for (int i = 0; i < _idSnapshot.Count; i++)
        {
            int id = _idSnapshot[i];
            if (!model.TryGet(id, out var data)) continue;
            if (data.IsDead) continue;

            Vector2 toPlayer = (Vector2)_playerTransform.position - data.Position;
            float dist = toPlayer.magnitude;

            switch (data.State)
            {
                case EnemyActionState.Idle:
                    if (dist < data.AttackRange)
                        ChangeState(id, EnemyActionState.Attack);
                    else if (dist <= data.ChaseRange)
                        ChangeState(id, EnemyActionState.Chase);
                    break;

                case EnemyActionState.Chase:
                    if (dist < data.AttackRange)
                        ChangeState(id, EnemyActionState.Attack);
                    else if (dist > data.ChaseRange)
                        ChangeState(id, EnemyActionState.Idle);
                    else
                        model.SetMoveDelta(id, toPlayer.normalized);
                    break;

                case EnemyActionState.Attack:
                    {
                        float timer = data.StateTimer + dt;
                        if (!data.HitChecked && timer >= HitCheckTime)
                        {
                            model.SetHitChecked(id, true);
                            this.SendEvent(new EnemyRequestHitCheckEvent { EnemyId = id });
                        }
                        if (timer >= AttackDuration)
                            ChangeState(id, EnemyActionState.Idle);
                        else
                            model.SetStateTimer(id, timer);
                        break;
                    }

                case EnemyActionState.Hurt:
                    {
                       //Debug.Log($"[HurtUpdate] id={id} kv={data.KnockbackVelocity} timer={data.StateTimer:F3}");
                        float timer = data.StateTimer + dt;
                        Vector2 kvDecay = data.KnockbackVelocity * KnockbackDecay;
                        Vector2 moveDelta = kvDecay.magnitude < 0.1f ? Vector2.zero : kvDecay;
                        model.SetKnockbackVelocity(id, kvDecay);
                        model.SetMoveDelta(id, moveDelta);

                        if (timer >= HurtDuration)
                            ChangeState(id, data.IsDead ? EnemyActionState.Dead : EnemyActionState.Idle);
                        else
                            model.SetStateTimer(id, timer);
                        break;
                    }

                case EnemyActionState.Dead:
                    break;
            }
        }
    }

    public void ChangeState(int enemyId, EnemyActionState newState)
    {
        var model = this.GetModel<IEnemyModel>();
        model.SetState(enemyId, newState);
    }

    public void OnEnemyDamaged(int enemyId, Vector2 knockbackDir)
    {
        var model = this.GetModel<IEnemyModel>();
        if (!model.TryGet(enemyId, out var data)) return;
        model.SetKnockbackVelocity(enemyId, knockbackDir * data.KnockbackForce);//击退
        model.SetMoveDelta(enemyId, knockbackDir * data.KnockbackForce);
        ChangeState(enemyId, EnemyActionState.Hurt);
    }
}
