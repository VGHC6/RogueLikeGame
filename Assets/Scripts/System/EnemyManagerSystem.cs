using System.Collections.Generic;
using Unity.VisualScripting;
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

        this.RegisterEvent<UIPanelChangeEvent>(OnPanelChange);

        this.RegisterEvent<FloorAdvancedEvent>(OnChangeFloor);
    }

    void OnPanelChange(UIPanelChangeEvent e)
    {
        var spwan = this.GetUtility<ISpawnUtility>();//获取生成器
        var enmeyModel = this.GetModel<IEnemyModel>();//获取敌人模型
        if (e.NewPanel == UIPanelType.GamePlay)
        {
            var enemyModel = this.GetModel<IEnemyModel>();//敌人模型
            if (enemyModel.GetAll().Count == 0)
            {
                var map = this.GetModel<IMapModel>();//地图模型
                var rooms = map.Rooms;//房间列表

                //玩家第一个生成
                if (rooms.Count > 0)
                {
                    var playerGo = spwan.SpawnPlayer(rooms[0].Center);//生成玩家
                    playerGo.GetComponent<PlayerController>().Init();//初始化玩家控制器
                    _playerTransform = playerGo.transform;//设置玩家位置
                }

                //敌人生成
                for (int i = 1; i < rooms.Count; i++)
                {
                    var sd = spwan.SpwanEnemy(rooms[i].Center);//生成敌人
                    var id = enmeyModel.Register(sd.Data);//注册敌人
                    sd.GO.GetComponent<EnemyView>().Init(id, sd.Data);//初始化敌人控制器
                }
            }
            else
            {
                var entity = this.GetModel<IEntityModel>();//实体模型
                var playerGo = spwan.SpawnPlayer(entity.Position);//生成玩家
                playerGo.GetComponent<PlayerController>().Init();//初始化玩家控制器
                _playerTransform = playerGo.transform;//设置玩家位置

                var prefab = Resources.Load<GameObject>("Perfabs/Enemy");//加载敌人预制体
                foreach (var kv in enmeyModel.GetAll())
                {
                    var id = kv.Key;
                    var data = kv.Value;
                    var go = GameObject.Instantiate(prefab, data.Position, Quaternion.identity);//生成敌人
                    go.GetComponent<EnemyView>().Init(id, data);//初始化敌人控制器
                }
            }
        }
        else if (e.NewPanel == UIPanelType.Start || e.NewPanel == UIPanelType.GameOver)
        {
            //销毁敌人
            _idSnapshot.Clear();
            foreach (var kv in enmeyModel.GetAll())
            {
                _idSnapshot.Add(kv.Key);
            }
            foreach (var id in _idSnapshot)
            {
                enmeyModel.Unregister(id);
            }
            _playerTransform = null;
            spwan.CleanupAll();
        }
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

    //通用生成代码
    public void SpawnFromRooms(float enemyScale = 1f)
    {
        var spwan = this.GetUtility<ISpawnUtility>();
        var enmeyModel = this.GetModel<IEnemyModel>();
        var map = this.GetModel<IMapModel>();
        var rooms = map.Rooms;

        if (rooms.Count == 0) return;

        // 玩家
        var playerGo = spwan.SpawnPlayer(rooms[0].Center);
        playerGo.GetComponent<PlayerController>().Init();
        _playerTransform = playerGo.transform;

        // 敌人
        for (int i = 1; i < rooms.Count; i++)
        {
            var sd = spwan.SpwanEnemy(rooms[i].Center);
            if (enemyScale != 1f)
            {
                sd.Data.MaxHp = (int)(sd.Data.MaxHp * enemyScale);
                sd.Data.CurrentHp = sd.Data.MaxHp;
                sd.Data.AttackPower = (int)(sd.Data.AttackPower * enemyScale);
                sd.Data.DefensePower = (int)(sd.Data.DefensePower * enemyScale);
            }
            var id = enmeyModel.Register(sd.Data);
            sd.GO.GetComponent<EnemyView>().Init(id, sd.Data);
        }
    }

    public void OnChangeFloor(FloorAdvancedEvent e)
    {
        var state = this.GetModel<IGameStateModel>();
        float scale = 1f + (state._currentFloor - 1) * 0.3f;
        SpawnFromRooms(scale);
    }
}
