using System.Collections.Generic;
using UnityEngine;

public struct DoorData
{
    public int DoorId;
    public Vector2Int Position;
    public bool IsOpen;
    public int RoomIndex;
    public bool IsSideWall; // true=东/西墙(左右的门), false=北/南墙(上下的门)
}


public interface IDoorModel : IModel
{
    List<DoorData> Doors { get; }
    void RegisterDoor(DoorData door);
    void ReMoveDoor(int doorId);
    //����������
    void SetDoorsInRoomOpen(int doorId, bool isOpen);
    public void SetAllDoorsOpen(bool isOpen);
    bool IsOpen(int doorId);
    //���������������
    bool IsAllDoorOpen(int roomIndex);
    //��ѯ
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

    public void SetDoorsInRoomOpen(int doorId, bool isOpen)
    {
        for(int i = 0; i < Doors.Count; i++)
        {
            if (Doors[i].RoomIndex != doorId) continue;
            if (Doors[i].IsOpen == isOpen) continue;
            var d = Doors[i];
            d.IsOpen = isOpen;
            Doors[i] = d;
            this.SendEvent(new DoorStateChangedEvent
            {
                DoorId = d.DoorId,
                RoomIndex=doorId,
                IsOpen=isOpen
            });
        }
    }

    /// <summary>
    /// ����������
    /// </summary>
    /// <param name="isOpen"></param>
    /// <exception cref="System.NotImplementedException"></exception>
    public void SetAllDoorsOpen(bool isOpen)
    {
        for (int i = 0; i < Doors.Count; i++)
        {
            if (Doors[i].IsOpen == isOpen) continue;
            var d = Doors[i]; d.IsOpen = isOpen; Doors[i] = d;
            this.SendEvent(new DoorStateChangedEvent { DoorId = d.DoorId, IsOpen = isOpen });
        }
    }

    public void RegisterDoor(DoorData door)
    {
        door.DoorId = _nextIndex++;//���ӱ��
        Doors.Add(door);
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