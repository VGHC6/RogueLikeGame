using System.Collections.Generic;
using UnityEngine;

public interface IMapGeneratorSystem : ISystem
{
    void GanateMap(int MapWidth,int MapHeight, int MapCount);
}

public class MapGeneratorSystem : AbstractSystem, IMapGeneratorSystem
{
    private const int maxRoomSize = 10;//房间最大大小
    private const int minRoomSize = 5;//房价最小大小
    private const int RoomMargen = 2;//摄像机边缘
    private const int RoomSpacing = 1;//房间间距

    protected override void OnInit()
    {
        this.RegisterEvent<UIPanelChangeEvent>(OnPanelChange);
    }

    //注册事件，判断是否进入游戏
    void OnPanelChange(UIPanelChangeEvent e)
    {
        if (e.NewPanel == UIPanelType.GamePlay)
        {
            int roomCount = Random.Range(5, 9);//随机生成5-9个房间
            GanateMap(60, 40, roomCount);
            this.SendEvent(new MapGeneratedEvent());//发送地图生成事件
        }
    }

    /// <summary>
    /// 生成地图
    /// </summary>
    /// <param name="MapWidth"></param>
    /// <param name="Height"></param>
    /// <param name="MapCount"></param>
    public void GanateMap(int MapWidth, int MapHeight, int MapCount)
    {
        int[,] _grid= new int[MapWidth, MapHeight];//创建一个二维数组
        List<RoomData> _rooms = new List<RoomData>();//创建一个房间列表

        for(int i=0;i< MapCount; i++)
        {
            int roomH= Random.Range(minRoomSize, maxRoomSize+1);
            int roomW = Random.Range(minRoomSize, maxRoomSize+1);

            if(TryPlaceRoom(_grid, MapWidth, MapHeight, roomW,roomH,out int roomX,out int roomY))
            {
                var room = new RoomData
                {
                    X = roomX,
                    Y = roomY,
                    Width = roomW,
                    Height = roomH,
                    Center = new Vector2(roomX + roomW / 2, roomY + roomH / 2)//房间中心坐标
                };
                CarveRoom(_grid, room);//填充房间
                _rooms.Add(room);//将房间添加到房间列表
            }
        }

        _rooms.Sort((a, b) => a.X.CompareTo(b.X));//按X坐标排序
        //走廊
        for(int i=0; i< _rooms.Count-1; i++)
        {
            var r1= _rooms[i];
            var r2= _rooms[i + 1];
            int x1 = (int)r1.Center.x;//房间1中心X坐标
            int y1 = (int)r1.Center.y;
            int x2 = (int)r2.Center.x;//房间2中心X坐标
            int y2 = (int)r2.Center.y;
            CarveCorridor(_grid, MapWidth, MapHeight, x1, y1, x2, y2);//填充走廊
        }
        //写入Model
        this.GetModel<IMapModel>().SetMap(_grid, _rooms);
    }

    /// <summary>
    /// 尝试放置房间
    /// </summary>
    /// <param name="grid"></param>
    /// <param name="mapW"></param>
    /// <param name="mapH"></param>
    /// <param name="roomW"></param>
    /// <param name="roomH"></param>
    /// <param name="roomX"></param>
    /// <param name="roomY"></param>
    /// <returns></returns>
    bool TryPlaceRoom(int[,] grid, int mapW, int mapH,int roomW, int roomH,out int roomX, out int roomY)
    {
        int maxX = mapW - roomW - RoomMargen;//房间最大X坐标
        int maxY= mapH - roomH - RoomMargen;//房间最大Y坐标

        roomX = 0;
        roomY= 0;
        if (maxX < RoomMargen || maxY < RoomMargen) return false;//如果房间最大坐标小于边缘，则返回false

        //尝试放置
        for(int attempt = 0; attempt< 10; attempt++)
        {
            roomX = Random.Range(RoomMargen, maxX+1);
            roomY = Random.Range(RoomMargen, maxY+1);
            int cx0 = roomX - RoomSpacing;//房间左上角X坐标
            int cy0 = roomY - RoomSpacing;//房间左上角Y坐标
            int cx1 = roomX + roomW + RoomSpacing - 1;//房间右下角X坐标
            int cy1 = roomY + roomH + RoomSpacing - 1;//房间右下角Y坐标

            if(cx0<0||cy0<0||cx1>=mapW||cy1>=mapH) continue;//如果房间坐标超出地图范围，则跳过

            bool overlap = false;//重叠标志
            for(int x = cx0; x<= cx1&& !overlap; x++)
            {
                for (int y = cy0; y <= cy1 && !overlap; y++)
                {
                    if(grid[x,y]!=0) overlap = true;//如果房间坐标有重叠，则设置重叠标志为true
                }
            }
            if (!overlap) return true;//如果没有重叠，则返回true
        }
        return false;//如果尝试次数超过10次，则返回false
    }

    /// <summary>
    /// 填充数组
    /// </summary>
    /// <param name="grid"></param>
    /// <param name="roomData"></param>
    void CarveRoom(int[,] grid,RoomData roomData)
    {
        //地板填充
        for(int x=0; x< roomData.Width; x++)
        {
            for(int y=0; y< roomData.Height; y++)
            {
                grid[roomData.X + x, roomData.Y + y] = 1;
            }
        }

        int mapW = grid.GetLength(0);//获取地图宽度
        int mapH = grid.GetLength(1);//获取地图高度
        for(int x= roomData .X- 1; x<=roomData.X+roomData.Width; x++)
        {
            for(int y= roomData.Y- 1; y<=roomData.Y+roomData.Height; y++)
            {
                if(x < 0 || x >= mapW || y < 0 || y >= mapH) continue;//如果坐标超出地图范围，则跳过
                if (grid[x, y] == 0) grid[x, y] = 2;//填充数组
            }
        }
    }


    /// <summary>
    /// 生成走廊
    /// </summary>
    /// <param name="grid"></param>
    /// <param name="mapW"></param>
    /// <param name="mapH"></param>
    /// <param name="x1"></param>
    /// <param name="y1"></param>
    /// <param name="x2"></param>
    /// <param name="y2"></param>
    void CarveCorridor(int[,] grid,int mapW, int mapH, int x1, int y1, int x2, int y2)
    {
        int stepX=x2 - x1 > 0 ? 1 : -1;//计算x方向步长
        for(int x= x1; x!=x2; x += stepX)
        {
            if (x < 0 || x >= mapW || y1 < 0 || y1 >= mapH) continue;
            grid[x, y1] = 1;//填充数组
            if (y1 - 1 >= 0 && grid[x, y1 - 1] == 0)grid[x, y1 - 1] = 2;//填充数组
            if (y1 + 1 < mapH && grid[x, y1 + 1] == 0) grid[x, y1 + 1] = 2;//填充数组
        }

        
        int stepY = y2 - y1 > 0 ? 1 : -1;//计算y方向步长
        for (int y = y1; y != y2; y += stepY)
        {
            if (x2 < 0 || x2 >= mapW || y < 0 || y >= mapH) continue;
            grid[x2, y] = 1;//填充数组
            if (x2 - 1 >= 0 && grid[x2 - 1, y] == 0) grid[x2 - 1, y] = 2;//填充数组
            if (x2 + 1 < mapW && grid[x2 + 1, y] == 0) grid[x2 + 1, y] = 2;//填充数组
        }
    }
}