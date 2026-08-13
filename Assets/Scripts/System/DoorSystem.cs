
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
        _doorModel = this.GetModel<IDoorModel>();
        _mapModel = this.GetModel<IMapModel>();
        _enemyModel = this.GetModel<IEnemyModel>();
        //ע���¼�
        this.RegisterEvent<PlayerEnteredRoomEvent>(OnPlayerEnteredRoom);
        this.RegisterEvent<RoomEnemiesClearedEvent>(OnRoomCleared);
        this.RegisterEvent<AllEnemiesDeadEvent>(OnAllEnemiesDead);
        this.RegisterEvent<MapGeneratedEvent>(OnMapGenerated);
    }

    //�ذ������¼�
    private void OnFloorAdvanced(FloorAdvancedEvent e)
    {
        _doorModel.ClearAll();
        var spwan = this.GetUtility<ISpawnUtility>();
        spwan.CleanupDoors();
    }

    //���е��������¼�s
    private void OnAllEnemiesDead(AllEnemiesDeadEvent e)
    {
        foreach (var door in _doorModel.Doors)
        {
            _doorModel.SetAllDoorsOpen(true);
        }
    }

    //���������¼�
    private void OnRoomCleared(RoomEnemiesClearedEvent e)
    {
        _doorModel.SetDoorsInRoomOpen(e.RoomIndex, true);
    }

    //���뷿���¼�
    private void OnPlayerEnteredRoom(PlayerEnteredRoomEvent e)
    {
        if (e.RoomIndex == 0) return;
        if (_enemyModel.IsRoomClear(e.RoomIndex)) return;//����

        _doorModel.SetDoorsInRoomOpen(e.RoomIndex, false);//�������
    }

    //��ͼ�����¼�
    void OnMapGenerated(MapGeneratedEvent e)
    {
        // �������� GameObject
        this.GetUtility<ISpawnUtility>().CleanupDoors();

        // ���� DoorModel �е���������������
        var doorModel = this.GetModel<IDoorModel>();
        foreach (var door in doorModel.Doors)
        {
            float yOffset = door.IsSideWall ? 1.125f : 0.625f;
            this.GetUtility<ISpawnUtility>().SpawnDoor(
                new Vector2(door.Position.x + 0.5f, door.Position.y + yOffset),
                door.RoomIndex,
                door.DoorId
            );
        }
    }
}