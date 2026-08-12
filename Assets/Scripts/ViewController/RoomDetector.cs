using UnityEngine;

public class RoomDetector : MonoBehaviour, IController
{
    private int _currentRoom = -1;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Update()
    {
        var map = this.GetModel<IMapModel>();
        if (map.Rooms == null && map.Rooms.Count == 0) return;

        Vector2 pos = transform.position;
        int newRoom = -1;

        for (int i = 0; i < map.Rooms.Count; i++)
        {
            var r = map.Rooms[i];
            if (pos.x >= r.X && pos.x <= r.X + r.Width &&
                pos.y >= r.Y && pos.y <= r.Y + r.Height)
            {
                newRoom = i;
                break;
            }
        }

        if (newRoom != -1 && newRoom != _currentRoom)
        {
            _currentRoom = newRoom;
            this.SendCommand(new PlayerEnteredRoomCommand { RoomIndex = newRoom });
        }
    }
}