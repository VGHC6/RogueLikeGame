using System.Collections.Generic;
using UnityEngine;

public struct DoorData
{
    public int DoorId;
    public Vector2Int Position;
    public bool IsOpen;
    public int RoomIndex;
}


public interface IDoorModel : IModel
{
    List<DoorData> Doors { get; }
    void RegisterDoor(DoorData door);
    void ReMoveDoor(int doorId);
    //操作单个门
    void SetDoorOpen(int doorId, bool isOpen);
    bool IsOpen(int doorId);
    //操作整个房间的门
    void SetAllDoorOpen(int doorId, bool isOpen);
    bool IsAllDoorOpen(int roomIndex);
    //查询
    List<DoorData> GetDoorsByRoom(int roomIndex);
    void ClearAll();
}

public class DoorModel : AbstractModel, IDoorModel
{
    public List<DoorData> Doors { get; } = new List<DoorData>();

    public int _nextIndex = 1;
    protected override void OnInit()
    {

    }
    public void ClearAll()
    {
        Doors.Clear();
        _nextIndex = 1;
    }

    public bool IsAllDoorOpen(int roomIndex)
    {
        foreach (var d in Doors)
            if (d.RoomIndex == roomIndex && !d.IsOpen)
                return false;
        return true;
    }

    public bool IsOpen(int doorId)
    {
        foreach (var d in Doors)
            if (d.DoorId == doorId) return d.IsOpen;
        return true;
    }

    public void RegisterDoor(DoorData door)
    {
        door.DoorId = _nextIndex++;//增加编号
        Doors.Add(door);
    }

    public void SetAllDoorOpen(int doorId, bool isOpen)
    {
        for (int i = 0; i < Doors.Count; i++)
        {
            if (Doors[i].DoorId == doorId)
            {
                var d = Doors[i];
                d.IsOpen = isOpen;
                Doors[i] = d;

                //发送开门事件
                this.SendEvent(new DoorStateChangedEvent
                {
                    DoorId = doorId,
                    RoomIndex = d.RoomIndex,
                    IsOpen = isOpen
                });
            }
        }
    }

    public void SetDoorOpen(int doorId, bool isOpen)
    {
        for (int i = 0; i < Doors.Count; i++)
        {
            if (Doors[i].DoorId == doorId)
            {
                var d = Doors[i];
                d.IsOpen = isOpen;
                Doors[i] = d;

                //发送开门事件
                this.SendEvent(new DoorStateChangedEvent
                {
                    DoorId = doorId,
                    RoomIndex = d.RoomIndex,
                    IsOpen = isOpen
                });
                return;
            }
        }
    }

    public void ReMoveDoor(int doorId)
    {
        Doors.RemoveAll(d => d.DoorId == doorId);
    }

    public List<DoorData> GetDoorsByRoom(int roomIndex)
    {
        var result = new List<DoorData>();
        foreach (var d in Doors)
        {
            if (d.RoomIndex == roomIndex)
            {
                result.Add(d);
            }
        }
        return result;
    }
}