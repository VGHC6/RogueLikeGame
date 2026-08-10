using System.Linq;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;
public class LoadGameCommand : AbstractCommand
{
    public int slotIndex { get; set; }//读取的槽位

    protected override void OnExcute()
    {
        var saveUtil = this.GetUtility<ISaveUtility>();
        var data = saveUtil.LoadFromSlot(slotIndex);//读取数据
        if (data == null) return;

        //清理旧状态
        this.GetUtility<ISpawnUtility>().CleanupAll();//清理生成
        this.GetModel<IEnemyModel>().GetAll().Keys.ToList().ForEach(id => this.GetModel<IEnemyModel>().Unregister(id));
        this.GetModel<IMapModel>().Clearup();

        //生成地图
        var grid = new int[data._mapWidth, data._mapHeight];
        for (int i = 0; i < data._mapWidth; i++)
        {
            for (int j = 0; j < data._mapHeight; j++)
            {
                grid[i, j] = data._tileGrid[i * data._mapHeight + j];//将一维数组转换为二维数组
            }
        }
        this.GetModel<IMapModel>().SetMap(grid, data._room);


        //填充战斗属性
        var combat = this.GetModel<ICombatModel>();
        combat.MaxHp.Value = data._maxHealth;
        combat.CurrentHp.Value = data._currentHealth;
        combat.AttackPower.Value = data._attackPower;
        combat.DefensePower.Value = data._defensePower;
        combat.AttackRange.Value = data._attackRange;
        combat.DefensePower.Value = data._defensePower;

        //恢复实体
        var entity = this.GetModel<IEntityModel>();
        entity.Position = new Vector2(data._playerPosX, data._playerPosY);
        entity.MoveSpeed = data._moveSpeed;

        //敌人
        var enemyModel = this.GetModel<IEnemyModel>();
        foreach (var entry in data._enemyData)
        {
            enemyModel.Register(new EnemyRuntimeData
            {
                EnemyId = entry._emenId,
                CurrentHp = entry._enemCurrentHp,
                MaxHp = entry._enemMaxHp,
                AttackPower = entry._attackPower,
                DefensePower = entry._defensePower,
                AttackRange = entry._attackRange,
                ChaseRange = entry._chaseRange,
                MoveSpeed = entry._moveSpeed,
                AttackDuration = entry._attackDuration,
                HitCheckTime = entry._hitCheckTime,
                HurtDuration = entry._hurtDuration,
                KnockbackForce = entry._knockbackForce,
                KnockbackDecay = entry._knockbackDecay,
                State = (EnemyActionState)entry._state,
                Position = new Vector2(entry._posX, entry._posY),
                FacingDir = entry._facingDir,
                IsDead = entry._isDead,
                MoveDelta = Vector2.zero,
                KnockbackVelocity = Vector2.zero,
                HitChecked = false,
                StateTimer = 0f
            });
        }

        //恢复背包
        var allItems = Resources.LoadAll<ItemConfig>("Config/Items");
        var ItemModel = this.GetModel<IItemModel>();
        ItemModel.Clear();
        foreach (var name in data._packageData)
        {
            var config = allItems.FirstOrDefault(item => item.name == name);//根据名字找到对应的配置
            if (config != null) ItemModel.Add(config);
        }

        //触发游戏开始
        this.GetModel<IGameStateModel>().StartGame();
        this.SendEvent(new MapGeneratedEvent());
    }
}