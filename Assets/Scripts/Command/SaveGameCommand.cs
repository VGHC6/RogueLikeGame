using System;
using System.Collections.Generic;
using System.Linq;
//保存数据
public class SaveGameCommand : AbstractCommand
{
    public int soltIndex;//保存的槽位
    protected override void OnExcute()
    {
        var combat = this.GetModel<ICombatModel>();
        var playerData = this.GetModel<IEntityModel>();
        var enemyData = this.GetModel<IEnemyModel>();
        var mapData = this.GetModel<IMapModel>();
        var itemData = this.GetModel<IItemModel>().Items;
        var saveUtil = this.GetUtility<ISaveUtility>();

        //将地图展开
        int[] flat = new int[mapData.Width * mapData.Height];
        for (int i = 0; i < mapData.Width; i++)
        {
            for (int j = 0; j < mapData.Height; j++)
            {
                flat[i * mapData.Height + j] = mapData.TileGrid[i, j];
            }
        }

        //将敌人从字典转化成列表
        var listEnmey = new List<EnemySaveData>();
        foreach (var kv in enemyData.GetAll())
        {
            var d = kv.Value;//EnemyData
            listEnmey.Add(new EnemySaveData()
            {
                _emenId = d.EnemyId,
                _enemCurrentHp = d.CurrentHp,
                _enemMaxHp = d.MaxHp,
                _attackPower = d.AttackPower,
                _defensePower = d.DefensePower,
                _attackRange = d.AttackRange,
                _chaseRange = d.ChaseRange,
                _moveSpeed = d.MoveSpeed,
                _attackDuration = d.AttackDuration,
                _hitCheckTime = d.HitCheckTime,
                _hurtDuration = d.HurtDuration,
                _knockbackForce = d.KnockbackForce,
                _knockbackDecay = d.KnockbackDecay,
                _state = (int)d.State,
                _posX = d.Position.x,
                _posY = d.Position.y,
                _facingDir = d.FacingDir,
                _isDead = d.IsDead
            });
        }

        //道具装入
        var names = itemData.Select(it => it.itemName).ToList();

        //写入
        var data=new SaveData
        {
            _detalTime= DateTime.Now.ToString("yyyy/MM/dd HH:mm"),//保存时间
            _displayHp = combat.CurrentHp.Value,
            _displayMaxHp = combat.MaxHp.Value,
            _floorName="第1层",//后续修改

            _currentHealth= combat.CurrentHp.Value,
            _maxHealth= combat.MaxHp.Value,
            _attackPower=combat.AttackPower.Value,
            _attackRange=combat.AttackRange.Value,
            _defensePower = combat.DefensePower.Value,
            _playerPosX = playerData.Position.x,
            _playerPosY= playerData.Position.y,
            _moveSpeed= playerData.MoveSpeed,

            _mapWidth= mapData.Width,
            _mapHeight= mapData.Height,
            _tileGrid= flat,
            _room=mapData.Rooms!=null?new List<RoomData>(mapData.Rooms):new List<RoomData>(),

            _enemyData= listEnmey,

            _packageData = names
        };

        saveUtil.SaveToSolt(soltIndex, data);
    }
}