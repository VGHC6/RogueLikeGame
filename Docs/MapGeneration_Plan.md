# 自动生成地图 设计方案

## 目标

每次进入战斗时自动生成一个随机地图，包含房间、墙壁，以及玩家和敌人的合法生成位置。

---

## 一、整体流程

```
开始界面 → [点击开始]
  ↓
1. 生成地图数据（MapGenerator / ISystem）
  ↓
2. 根据数据绘制 Tilemap（MapBuilder / MonoBehaviour）
  ↓
3. 从地图数据中获取合法生成位置
  ↓
4. 在对应位置生成玩家 + 敌人
  ↓
5. 开始战斗
```

每次重新开始都要：
1. 清除旧的 Tilemap 瓦片
2. 清除旧的敌人
3. 重新生成地图
4. 重新放置玩家和敌人

---

## 二、选用什么地图生成算法

Roguelike 常用三种：

| 算法 | 效果 | 复杂度 |
|------|------|--------|
| **BSP（二分空间分割）** | 矩形房间 + 走廊，规整 | 中 |
| **随机房间 + 走廊连接** | 房间大小不一，自然 | 低 |
| **元胞自动机** | 洞穴风格，不规则 | 低 |

**建议用「随机房间 + 走廊连接」**，效果最像传统 Roguelike，实现也最直观：

1. 在一个矩形区域内随机放置 N 个房间（位置随机、大小在一定范围内随机）
2. 房间之间不能重叠（或允许少量重叠）
3. 用走廊连接相邻房间（水平 + 垂直的 L 形走廊）
4. 墙壁沿房间边界放置

---

## 三、地图数据结构（Model 层）

### 新建 `IMapModel`

```csharp
// 单个房间
public struct RoomData
{
    public int X, Y;          // 左下角坐标（tile 坐标）
    public int Width, Height; // 宽高（tile 数）
    public Vector2 Center;    // 房间中心（世界坐标），用于生成角色
}

// 地图数据
public interface IMapModel : IModel
{
    int MapWidth { get; }            // 地图总宽（tile 数）
    int MapHeight { get; }           // 地图总高
    IReadOnlyList<RoomData> Rooms { get; }
    int[,] TileGrid { get; }         // 0=空地, 1=地板, 2=墙壁

    void SetMap(int[,] grid, List<RoomData> rooms);
    void Clear();
}
```

注册到 `RogueLikeGame.Init()`：
```csharp
this.RegisterModel<IMapModel>(new MapModel());
```

---

## 四、地图生成逻辑（System 层）

### 新建 `IMapGeneratorSystem`

```csharp
public interface IMapGeneratorSystem : ISystem
{
    void Generate(int mapWidth, int mapHeight, int roomCount);
}
```

### `MapGeneratorSystem` 生成流程

```
1. 创建一个 mapWidth × mapHeight 的全 0 数组
2. 循环 roomCount 次：
   a. 随机房间大小（如 5~10 tile）
   b. 随机房间位置（在地图范围内）
   c. 检查是否与已有房间重叠（加一点间距）
   d. 不重叠 → 在数组中标记地板(1)，边界标记墙壁(2)
   e. 记录房间数据到列表
3. 对房间列表按 X 坐标排序
4. 相邻房间之间挖走廊：
   a. 从前一个房间中心 → 水平走到下一个房间的 X → 垂直走到下一个房间中心
   b. 走廊经过的 tile 标记为地板(1)，走廊两侧标记墙壁(2)
5. 把结果写入 IMapModel
```

伪代码核心部分：

```csharp
// 尝试放置房间
bool TryPlaceRoom(int[,] grid, int mapW, int mapH, 
                   int roomW, int roomH, 
                   out int roomX, out int roomY)
{
    roomX = Random.Range(2, mapW - roomW - 2);
    roomY = Random.Range(2, mapH - roomH - 2);
    
    // 检查区域是否为空（留 1 格间距）
    for (int x = roomX - 1; x < roomX + roomW + 1; x++)
        for (int y = roomY - 1; y < roomY + roomH + 1; y++)
            if (grid[x, y] != 0) return false;
    
    return true;
}

// 在地图上填充房间
void CarveRoom(int[,] grid, RoomData room)
{
    // 地板
    for (int x = room.X; x < room.X + room.Width; x++)
        for (int y = room.Y; y < room.Y + room.Height; y++)
            grid[x, y] = 1;
    
    // 墙壁（地板周围一圈）
    for (int x = room.X - 1; x <= room.X + room.Width; x++)
        for (int y = room.Y - 1; y <= room.Y + room.Height; y++)
            if (grid[x, y] == 0)
                grid[x, y] = 2;
}

// 挖 L 形走廊
void CarveCorridor(int[,] grid, int x1, int y1, int x2, int y2)
{
    // 先水平走
    int dx = x2 > x1 ? 1 : -1;
    for (int x = x1; x != x2 + dx; x += dx)
    {
        grid[x, y1] = 1;           // 走廊地板
        if (grid[x, y1 - 1] == 0) grid[x, y1 - 1] = 2; // 上墙
        if (grid[x, y1 + 1] == 0) grid[x, y1 + 1] = 2; // 下墙
    }
    // 再垂直走
    int dy = y2 > y1 ? 1 : -1;
    for (int y = y1; y != y2 + dy; y += dy)
    {
        grid[x2, y] = 1;
        if (grid[x2 - 1, y] == 0) grid[x2 - 1, y] = 2;
        if (grid[x2 + 1, y] == 0) grid[x2 + 1, y] = 2;
    }
}
```

---

## 五、Tilemap 渲染（ViewController 层）

### 新建 `MapBuilder`

这是一个 MonoBehaviour，挂在场景里的 MapBuilder GameObject 上。
持有 Tilemap 引用和 TileBase 资产的引用。

```csharp
public class MapBuilder : MonoBehaviour, IController
{
    [SerializeField] private Tilemap _floorTilemap;   // 地板层
    [SerializeField] private Tilemap _wallTilemap;     // 墙壁层
    [SerializeField] private TileBase _floorTile;      // 地板瓦片
    [SerializeField] private TileBase _wallTile;       // 墙壁瓦片

    public void BuildFromModel()
    {
        var map = this.GetModel<IMapModel>();
        
        _floorTilemap.ClearAllTiles();
        _wallTilemap.ClearAllTiles();

        for (int x = 0; x < map.MapWidth; x++)
        {
            for (int y = 0; y < map.MapHeight; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                switch (map.TileGrid[x, y])
                {
                    case 1: _floorTilemap.SetTile(pos, _floorTile); break;
                    case 2: _wallTilemap.SetTile(pos, _wallTile); break;
                }
            }
        }
    }
}
```

### 与生成流程的衔接

`MapGeneratorSystem.Generate()` 生成数据写入 `IMapModel` 后，发一个事件：

```csharp
this.SendEvent(new MapGeneratedEvent());
```

`MapBuilder` 监听这个事件，调用 `BuildFromModel()` 绘制 Tilemap。

---

## 六、与敌人生成系统的衔接

地图生成后，`EnemySpawner` 需要从 `IMapModel` 获取合法生成位置：

```csharp
void SpawnEnemies()
{
    var map = this.GetModel<IMapModel>();
    var rooms = map.Rooms;

    // 玩家放在第一个房间中心
    _player.transform.position = rooms[0].Center;

    // 敌人放在其余房间中心
    for (int i = 1; i < rooms.Count; i++)
    {
        var go = Instantiate(_enemyPrefab, rooms[i].Center, Quaternion.identity);
        var enemyView = go.GetComponent<EnemyView>();
        var data = BuildEnemyData(rooms[i].Center);
        int id = this.GetModel<IEnemyModel>().Register(data);
        enemyView.Init(id, data);
    }
}
```

这样敌人的生成位置完全由地图决定，不需要手动设置生成点。

---

## 七、墙壁碰撞

生成 Tilemap 后需要让角色不能穿过墙壁。

**最简单的方案**：给 `_wallTilemap` 所在的 GameObject 加一个 `TilemapCollider2D` + `CompositeCollider2D`（需要设置 `Used By Composite`）。墙壁自动拥有碰撞体。

如果角色（Player/Enemy）有 `Collider2D` + `Rigidbody2D`，物理碰撞就会自动生效。

或者自己加代码层过滤也行，但直接用 Unity 物理是最省事的。

---

## 八、摄像机跟随

地图比屏幕大时，摄像机需要跟随玩家。

项目中已有 `ICameraUtility`，可以扩展：

```csharp
public interface ICameraUtility : IUtility
{
    void Init(MonoBehaviour runner);
    void Shake(float intensity, float duration);
    void Follow(Transform target);                    // 新增
    void SetBounds(float minX, float maxX, 
                   float minY, float maxY);           // 新增，限制在地图内
}
```

在 `MapBuilder.BuildFromModel()` 或 `MapGeneratorSystem.Generate()` 里设置摄像机边界。

---

## 九、和已有方案的整合点

结合开始/结束界面文档，整个启动流程变为：

```
StartPanel.OnStartButton
  ↓
Changepanel(GamePlay)
  ↓
MapGeneratorSystem.Generate()     // 生成地图数据
  ↓
MapBuilder.BuildFromModel()       // 绘制 Tilemap
  ↓
SetCameraBounds()                 // 设置摄像机边界
  ↓
SpawnPlayer()                     // 把玩家移到第一个房间
  ↓
SpawnEnemies()                    // 在其他房间生成敌人
  ↓
战斗开始
```

---

## 十、文件清单

| 操作 | 文件路径 | 说明 |
|------|---------|------|
| 新建 | `Assets/Scripts/Model/MapModel.cs` | IMapModel + MapModel |
| 新建 | `Assets/Scripts/System/MapGeneratorSystem.cs` | IMapGeneratorSystem + 实现 |
| 新建 | `Assets/Scripts/ViewController/MapBuilder.cs` | Tilemap 绘制 |
| 新建 | `Assets/Scripts/Event/MapGeneratedEvent.cs` | 地图生成完成事件 |
| 修改 | `Assets/Scripts/RogueLikeGame.cs` | 注册 MapModel + MapGeneratorSystem |
| 修改 | `Assets/Scripts/Utility/ICameraUtility.cs` | 加 Follow + SetBounds |

### Unity 编辑器操作

1. 在场景 Game 下创建 `Grid` GameObject
2. 在 Grid 下创建 `Floor` Tilemap（放地板）+ `Wall` Tilemap（放墙壁），挂 `TilemapCollider2D`
3. 创建 `MapBuilder` GameObject，挂 `MapBuilder` 脚本，拖入引用
4. 在 Tile Palette 窗口用 `Tilemap_Flat.png` 创建 Floor Tile 和 Wall Tile
5. 把它们拖到 `MapBuilder` 的 `_floorTile` / `_wallTile` 字段

---

## 十一、进阶扩展（后续可做）

- **房间类型**：宝箱房、商店房、Boss 房
- **多层级地图**：上下楼梯到新的楼层
- **迷雾/视野**：用 Tilemap 的 alpha 或额外 Overlay 层做视野遮挡
- **最小生成树走廊算法**：连接更自然（避免环形连接）
- **种子控制**：用 `System.Random` + seed 支持同一地图重玩
