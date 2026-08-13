using System.Collections.Generic;
using UnityEngine;
//��������
public interface IEnemyModel : IModel
{
    int Register(EnemyRuntimeData init);//ע�����
    void Unregister(int id);//ע������
    EnemyRuntimeData Get(int id);//��ȡ����
    bool TryGet(int id, out EnemyRuntimeData data);//���Ի�ȡ����
    IReadOnlyDictionary<int, EnemyRuntimeData> GetAll();//��ȡ���е���
    void SetCurrentHp(int id, int hp);//���õ��˵�ǰѪ��
    void SetState(int id, EnemyActionState state);//���õ���״̬
    void SetMoveDelta(int id, Vector2 delta);//���õ����ƶ�����
    void SetPosition(int id, Vector2 pos);//���õ���λ��
    void SetFacingDir(int id, int dir);//���õ��˳���
    void SetKnockbackVelocity(int id, Vector2 vel);//���õ��˻����ٶ�
    void SetHitChecked(int id, bool c);//���õ����Ƿ񱻻���
    void SetStateTimer(int id, float t);//���õ���״̬��ʱ��

    //����ķ���
    int GetAliveCountInRoom(int roomId);//��ȡ�����ڴ���������
    bool IsRoomClear(int roomId);//�жϷ����Ƿ����
}

public class EnemyModel : AbstractModel, IEnemyModel
{
    private Dictionary<int, EnemyRuntimeData> _enemies = new();//ͳһ�������е���

    private Dictionary<int, int> _alivePerRoom = new();//��¼ÿ�������ڴ����˵�����
    private int _nextId = 1;

    protected override void OnInit() { }

    public int Register(EnemyRuntimeData init)
    {
        init.EnemyId = _nextId;
        _enemies[_nextId] = init;

        int roomId = init.IndexRoom;
        _alivePerRoom.TryGetValue(roomId, out int c);//�鿴�����Ƿ���ڵ���
        _alivePerRoom[roomId] = c + 1;
        return _nextId++;
    }

    public void Unregister(int id)
    {
        if (!_enemies.TryGetValue(id, out var data)) return;
        _enemies.Remove(id);
        int roomIdx = data.IndexRoom;

        //�������������0
        if(_alivePerRoom.TryGetValue(roomIdx,out int c) && c > 0)
        {
            c--;
            _alivePerRoom[roomIdx] = c;

            if(c == 0)
            {
                this.SendEvent(new RoomEnemiesClearedEvent { RoomIndex = roomIdx });//�����¼�
            }
        }
        //���
        if (_enemies.Count == 0)
        {
            this.SendEvent(new AllEnemiesDeadEvent());
        }
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

    //��ѯʣ������
    public int GetAliveCountInRoom(int roomId)
    {
        _alivePerRoom.TryGetValue(roomId, out int c);
        return c;
    }

    //�Ƿ������ɾ�
    public bool IsRoomClear(int roomId)
    {
        return GetAliveCountInRoom(roomId) == 0;
    }
}
