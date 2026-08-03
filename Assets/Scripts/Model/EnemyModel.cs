using System.Collections.Generic;
using UnityEngine;
//敌人属性
public interface IEnemyModel : IModel
{
    int Register(EnemyRuntimeData init);//注册敌人
    void Unregister(int id);//注销敌人
    EnemyRuntimeData Get(int id);//获取敌人
    bool TryGet(int id, out EnemyRuntimeData data);//尝试获取敌人
    IReadOnlyDictionary<int, EnemyRuntimeData> GetAll();//获取所有敌人
    void SetCurrentHp(int id, int hp);//设置敌人当前血量
    void SetState(int id, EnemyActionState state);//设置敌人状态
    void SetMoveDelta(int id, Vector2 delta);//设置敌人移动方向
    void SetPosition(int id, Vector2 pos);//设置敌人位置
    void SetFacingDir(int id, int dir);//设置敌人朝向
    void SetKnockbackVelocity(int id, Vector2 vel);//设置敌人击退速度
    void SetHitChecked(int id, bool c);//设置敌人是否被击中
    void SetStateTimer(int id, float t);//设置敌人状态计时器
}

public class EnemyModel : AbstractModel, IEnemyModel
{
    private Dictionary<int, EnemyRuntimeData> _enemies = new();//统一管理所有敌人
    private int _nextId = 1;

    protected override void OnInit() { }

    public int Register(EnemyRuntimeData init)
    {
        init.EnemyId = _nextId;
        _enemies[_nextId] = init;
        return _nextId++;
    }

    public void Unregister(int id)
    {
        _enemies.Remove(id);
    }

    public EnemyRuntimeData Get(int id)
    {
        return _enemies[id];
    }

    public bool TryGet(int id, out EnemyRuntimeData data)
    {
        return _enemies.TryGetValue(id, out data);
    }

    public IReadOnlyDictionary<int, EnemyRuntimeData> GetAll()
    {
        return _enemies;
    }

    public void SetCurrentHp(int id, int hp)
    {
        if (!_enemies.TryGetValue(id, out var data)) return;
        data.CurrentHp = Mathf.Max(0, hp);
        data.IsDead = data.CurrentHp <= 0;
        _enemies[id] = data;

        this.SendEvent(new EnemyHpChangedEvent
        {
            EnemyId = id,
            CurrentHp = data.CurrentHp,
            MaxHp = data.MaxHp,
            IsDead = data.IsDead
        });

        if (data.IsDead)
        {
            this.SendEvent(new EnemyDeadEvent { EnemyId = id });
        }
    }

    public void SetState(int id, EnemyActionState state)
    {
        if (!_enemies.TryGetValue(id, out var data)) return;
        data.State = state;
        data.StateTimer = 0f;
        data.HitChecked = false;
        _enemies[id] = data;

        this.SendEvent(new EnemyStateChangedEvent { EnemyId = id, NewState = state });
    }

    public void SetMoveDelta(int id, Vector2 delta)
    {
        if (!_enemies.TryGetValue(id, out var data)) return;
        data.MoveDelta = delta;
        _enemies[id] = data;
    }

    public void SetPosition(int id, Vector2 pos)
    {
        if (!_enemies.TryGetValue(id, out var data)) return;
        data.Position = pos;
        _enemies[id] = data;
    }

    public void SetFacingDir(int id, int dir)
    {
        if (!_enemies.TryGetValue(id, out var data)) return;
        data.FacingDir = dir;
        _enemies[id] = data;
    }

    public void SetKnockbackVelocity(int id, Vector2 vel)
    {
        if (!_enemies.TryGetValue(id, out var data)) return;
        data.KnockbackVelocity = vel;
        _enemies[id] = data;
    }

    public void SetHitChecked(int id, bool c)
    {
        if (!_enemies.TryGetValue(id, out var data)) return;
        data.HitChecked = c;
        _enemies[id] = data;
    }

    public void SetStateTimer(int id, float t)
    {
        if (!_enemies.TryGetValue(id, out var data)) return;
        data.StateTimer = t;
        _enemies[id] = data;
    }
}
