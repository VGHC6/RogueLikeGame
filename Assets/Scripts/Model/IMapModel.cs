using System.Collections.Generic;
using UnityEngine;
//随机生成地图
//单个房间的数据
public struct RoomData
{
    public int X, Y;//坐标
    public int Width, Height;//宽高
    public Vector2 Center;//中心点
}

public interface IMapModel : IModel
{
    int Width { get; }//总宽
    int Height { get; }//总高
    List<RoomData> Rooms { get; }//房间列表,只读
    int[,] TileGrid { get; }//0=空地, 1=地板, 2=墙壁,矩阵

    void SetMap(int[,] tileGrid, List<RoomData> rooms);//设置地图
    void Clearup();//地图清理
}


public class MapModel : AbstractModel, IMapModel
{
    int _width;
    int _height;
    List<RoomData> _rooms;//房间列表
    private int[,] _tileGrid;//地图矩阵

    public int Width => _width;//总宽
    public int Height =>_height;//总高

    public List<RoomData> Rooms => _rooms;

    public int[,] TileGrid => _tileGrid;

    protected override void OnInit()
    {
    }
    public void SetMap(int[,] tileGrid, List<RoomData> rooms)
    {
        _tileGrid = tileGrid;
        _width= tileGrid.GetLength(0);//获取矩阵的宽
        _height = tileGrid.GetLength(1);//获取矩阵的高
        _rooms = rooms;
    }

    public void Clearup()
    {
        _tileGrid = null;
        _width = 0;
        _height = 0;
        _rooms.Clear();
    }
}


