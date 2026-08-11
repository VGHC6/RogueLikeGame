using System.Collections.Generic;
using UnityEngine;

public interface IMapGeneratorSystem : ISystem
{
    void GanateMap(int MapWidth, int MapHeight, int MapCount);
}

public class MapGeneratorSystem : AbstractSystem, IMapGeneratorSystem
{
    private const int maxRoomSize = 10;
    private const int minRoomSize = 5;
    private const int RoomMargen = 2;
    private const int RoomSpacing = 1;

    protected override void OnInit()
    {
        this.RegisterEvent<UIPanelChangeEvent>(OnPanelChange);
        this.RegisterEvent<FloorAdvancedEvent>(OnFloorAdvanced);
    }

    void OnPanelChange(UIPanelChangeEvent e)
    {
        if (e.NewPanel == UIPanelType.GamePlay)
        {
            var map = this.GetModel<IMapModel>();
            if (map.Width == 0 || map.Height == 0)
            {
                int roomCount = Random.Range(5, 9);
                GanateMap(60, 40, roomCount);
                this.SendEvent(new MapGeneratedEvent());
            }
        }else if (e.NewPanel == UIPanelType.Start)
        {
            this.GetModel<IMapModel>().Clearup();
        }
    }

    public void GanateMap(int MapWidth, int MapHeight, int MapCount)
    {
        int[,] _grid = new int[MapWidth, MapHeight];
        List<RoomData> _rooms = new List<RoomData>();

        for (int i = 0; i < MapCount; i++)
        {
            int roomH = Random.Range(minRoomSize, maxRoomSize + 1);
            int roomW = Random.Range(minRoomSize, maxRoomSize + 1);

            if (TryPlaceRoom(_grid, MapWidth, MapHeight, roomW, roomH,
                             out int roomX, out int roomY))
            {
                var room = new RoomData
                {
                    X = roomX,
                    Y = roomY,
                    Width = roomW,
                    Height = roomH,
                    Center = new Vector2(roomX + roomW / 2f, roomY + roomH / 2f)
                };
                CarveRoom(_grid, room);
                _rooms.Add(room);
            }
        }

        // 按 X 坐标排序
        _rooms.Sort((a, b) => a.X.CompareTo(b.X));

        // 相邻房间之间挖走廊
        for (int i = 0; i < _rooms.Count - 1; i++)
        {
            var r1 = _rooms[i];
            var r2 = _rooms[i + 1];
            int x1 = (int)r1.Center.x;
            int y1 = (int)r1.Center.y;
            int x2 = (int)r2.Center.x;
            int y2 = (int)r2.Center.y;
            CarveCorridor(_grid, MapWidth, MapHeight, x1, y1, x2, y2);
        }

        this.GetModel<IMapModel>().SetMap(_grid, _rooms);
    }

    bool TryPlaceRoom(int[,] grid, int mapW, int mapH,
                      int roomW, int roomH,
                      out int roomX, out int roomY)
    {
        int maxX = mapW - roomW - RoomMargen;
        int maxY = mapH - roomH - RoomMargen;

        roomX = 0;
        roomY = 0;
        if (maxX < RoomMargen || maxY < RoomMargen) return false;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            roomX = Random.Range(RoomMargen, maxX + 1);
            roomY = Random.Range(RoomMargen, maxY + 1);
            int cx0 = roomX - RoomSpacing;
            int cy0 = roomY - RoomSpacing;
            int cx1 = roomX + roomW + RoomSpacing - 1;
            int cy1 = roomY + roomH + RoomSpacing - 1;

            if (cx0 < 0 || cy0 < 0 || cx1 >= mapW || cy1 >= mapH) continue;

            bool overlap = false;
            for (int x = cx0; x <= cx1 && !overlap; x++)
            {
                for (int y = cy0; y <= cy1 && !overlap; y++)
                {
                    if (grid[x, y] != 0) overlap = true;
                }
            }
            if (!overlap) return true;
        }
        return false;
    }

    void CarveRoom(int[,] grid, RoomData roomData)
    {
        // 地板
        for (int x = 0; x < roomData.Width; x++)
        {
            for (int y = 0; y < roomData.Height; y++)
            {
                grid[roomData.X + x, roomData.Y + y] = 1;
            }
        }

        // 边界墙壁
        int mapW = grid.GetLength(0);
        int mapH = grid.GetLength(1);
        for (int x = roomData.X - 1; x <= roomData.X + roomData.Width; x++)
        {
            for (int y = roomData.Y - 1; y <= roomData.Y + roomData.Height; y++)
            {
                if (x < 0 || x >= mapW || y < 0 || y >= mapH) continue;
                if (grid[x, y] == 0) grid[x, y] = 2;
            }
        }
    }

    void CarveCorridor(int[,] grid, int mapW, int mapH,
                       int x1, int y1, int x2, int y2)
    {
        int stepX = x2 > x1 ? 1 : -1;

        // 水平段：3 格宽地板
        for (int x = x1; x != x2 + stepX; x += stepX)
        {
            if (x < 0 || x >= mapW) continue;
            for (int dy = -1; dy <= 1; dy++)
            {
                int yy = y1 + dy;
                if (yy >= 0 && yy < mapH)
                    grid[x, yy] = 1;
            }
            if (y1 - 2 >= 0 && grid[x, y1 - 2] == 0) grid[x, y1 - 2] = 2;
            if (y1 + 2 < mapH && grid[x, y1 + 2] == 0) grid[x, y1 + 2] = 2;
        }

        int stepY = y2 > y1 ? 1 : -1;

        // 垂直段：3 格宽地板
        for (int y = y1; y != y2 + stepY; y += stepY)
        {
            if (y < 0 || y >= mapH) continue;
            for (int dx = -1; dx <= 1; dx++)
            {
                int xx = x2 + dx;
                if (xx >= 0 && xx < mapW)
                    grid[xx, y] = 1;
            }
            if (x2 - 2 >= 0 && grid[x2 - 2, y] == 0) grid[x2 - 2, y] = 2;
            if (x2 + 2 < mapW && grid[x2 + 2, y] == 0) grid[x2 + 2, y] = 2;
        }
    }


    public void OnFloorAdvanced(FloorAdvancedEvent e)
    {
        var state = this.GetModel<IGameStateModel>();
        int roomCount = Mathf.Max(3, 9 - state._currentFloor);

        GanateMap(60, 40, roomCount);
        this.SendEvent(new MapGeneratedEvent());
    }
}
