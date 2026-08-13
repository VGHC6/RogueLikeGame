
using System;
using UnityEngine;

public interface IDoorSystem : ISystem
{

}

public class DoorSystem : AbstractSystem, IDoorSystem
{
    private IDoorModel _doorModel;
    private IMapModel _mapModel;
    private IEnemyModel _enemyModel;

    protected override void OnInit()
    {
        _doorModel=this.GetModel<IDoorModel>();
        _mapModel=this.GetModel<IMapModel>();
        _enemyModel=this.GetModel<IEnemyModel>();
        //注册事件
        this.RegisterEvent<PlayerEnteredRoomEvent>(OnPlayerEnteredRoom);
        this.RegisterEvent<RoomEnemiesClearedEvent>(OnRoomCleared);
        this.RegisterEvent<AllEnemiesDeadEvent>(OnAllEnemiesDead);
        this.RegisterEvent<FloorAdvancedEvent>(OnFloorAdvanced);
        this.RegisterEvent<MapGeneratedEvent>(OnMapGenerated);
    }

    //地板升级事件
    private void OnFloorAdvanced(FloorAdvancedEvent e)
    {
        _doorModel.ClearAll();
        var spwan = this.GetUtility<ISpawnUtility>();
        spwan.CleanupDoors();
    }

    //所有敌人死亡事件s
    private void OnAllEnemiesDead(AllEnemiesDeadEvent e)
    {
        foreach(var door in _doorModel.Doors)
        {
            _doorModel.SetAllDoorOpen(door.RoomIndex, true);
        }
    }

    //清理房间事件
    private void OnRoomCleared(RoomEnemiesClearedEvent e)
    {
        _doorModel.SetDoorOpen(e.RoomIndex, true);
    }

    //进入房间事件
    private void OnPlayerEnteredRoom(PlayerEnteredRoomEvent e)
    {
        if (e.RoomIndex == 0) return;
        if(_enemyModel.IsRoomClear(e.RoomIndex))return;//清理

        _doorModel.SetDoorOpen(e.RoomIndex, false);//进入关门
    }

    //地图生成事件
    void OnMapGenerated(MapGeneratedEvent e)
    {
        // 清理旧门 GameObject
        this.GetUtility<ISpawnUtility>().CleanupDoors();

        // 根据 DoorModel 中的数据重新生成门
        var doorModel = this.GetModel<IDoorModel>();
        foreach (var door in doorModel.Doors)
        {
            this.GetUtility<ISpawnUtility>().SpawnDoor(
                new Vector2(door.Position.x + 0.5f, door.Position.y + 0.5f),
                door.RoomIndex
            );
        }
    }
}