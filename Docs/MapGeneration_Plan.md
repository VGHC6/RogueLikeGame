# 随机地图生成 设计方案

## 目标

每次进入战斗时自动生成一个随机地图，包含房间、墙壁，以及玩家和敌人的合法生成位置。

---

## 一、架构约束（来自 Docs/Rule.txt）

本项目的 MVCS 架构规则，地图生成系统必须遵守：

| 层级 | 可以做什么 | 不能做什么 |
|------|-----------|-----------|
| **Model** (`IModel`) | 获取 Utility、发送 Event | 不能获取 System/Model、不能获取 Command |
| **System** (`ISystem`) | 获取 System/Model/Utility、发送 Event、注册 Event | 不能获取 Controller |
| **ViewController** (`IController`) | 获取 System/Model/Utility、发送 Command、注册 Event | 修改 System/Model 状态必须通过 Command |
| **Utility** (`IUtility`) | 封装第三方/基础设施 API | 不能获取任何架构内对象 |
| **Command** (`ICommand`) | 获取 System/Model/Utility、发送 Event/Command | 不能有状态（字段仅在单次 Execute 内有效） |

**通信方向**：上层 → 下层用方法调用（Controller → 下层须通过 Command）；下层 → 上层用 Event。

这意味着：
- `MapGeneratorSystem`（System 层）可以直接调用 `IMapModel.SetMap()` 写入数据
- `MapGeneratorSystem` 生成完成后通过 `SendEvent(MapGeneratedEvent)` 通知上层
- `MapBuilder`（Controller 层）监听 `MapGeneratedEvent`，读取 Model 数据来绘制 Tilemap
- `SpawnUtility`（Utility 层）只能提供 spawn 方法，不能主动获取 Model——需要由调用者（`EnemyManagerSystem`）传入位置参数

---

## 二、完整运行时流程（精确调用链）

### 从点击"开始"到战斗就绪

```
1. GameStartPanel.OnStartButton()                          [ViewController]
   ↓  
2. this.GetModel<IGameStateModel>().StartGame()             [Model 方法调用]
   ↓
3. _currentPhase.Value = UIPanelType.GamePlay               [BindableProperty setter]
   ↓  触发 mOnValueChanged
4. UISystem.Changepanel(UIPanelType.GamePlay)               [System]
   ↓
5. this.SendEvent(new UIPanelChangeEvent                    [Event 发出]
   {
       OldPanel = UIPanelType.Start,
       NewPanel = UIPanelType.GamePlay
   })
   ↓
   ↓  事件处理器按注册顺序同步执行：
   ↓
6. MapGeneratorSystem.OnPanelChange(e)                      [System — 第1个处理器]
   │  if (e.NewPanel == GamePlay)
   │  {
   │      Generate(60, 40, Random.Range(5, 9));            // 生成地图数据
   │      this.SendEvent(new MapGeneratedEvent());           // 通知渲染层
   │  }
   ↓
7. MapBuilder.OnMapGenerated(e)                             [Controller — 监听 MapGeneratedEvent]
   │  var map = this.GetModel<IMapModel>();
   │  遍历 map.TileGrid → SetTile 地板/墙壁
   │  CameraUtility.SetBounds(...);
   ↓
8. EnemyManagerSystem.OnPanelChange(e)                      [System — 第2个处理器]
   │  if (e.NewPanel == GamePlay)
   │  {
   │      var map = this.GetModel<IMapModel>();
   │      var rooms = map.Rooms;
   │      var playerGo = spwan.SpawnPlayer(rooms[0].Center);   // 玩家在第一个房间
   │      playerGo.GetComponent<PlayerController>().Init();
   │      for (int i = 1; i < rooms.Count; i++)
   │      {
   │          var sd = spwan.SpawnEnemy(rooms[i].Center);      // 敌人在其余房间
   │          var id = enmeyModel.Register(sd.Data);
   │          sd.GO.GetComponent<EnemyView>().Init(id, sd.Data);
   │      }
   │  }
   ↓
9. UIMangager.OnPanelChange(e)                              [Controller — 第3个处理器]
   │  销毁旧面板 → 实例化 GamePlay 面板
   ↓
10. 战斗开始
```

### 关键设计：利用事件注册顺序保证执行顺序

`TypeEventSystem` 的 `Register` 方法用 `Action<T> OnEvent += handler` 将多个处理器串联为委托链。C# 的委托链按 **注册顺序** 依次执行。

所以在 `RogueLikeGame.Init()` 中，注册顺序必须是：

```csharp
// System 注册顺序决定 UIPanelChangeEvent 处理顺序：
this.RegisterSystem<IMapGeneratorSystem>(new MapGeneratorSystem());    // 先注册 → 先执行
this.RegisterSystem<IEnemyManagerSystem>(new EnemyManagerSystem());   // 后注册 → 后执行

// EnemyManagerSystem 执行时 IMapModel.Rooms 已经有数据了
```

### 重新开始时的清理

`GameOver → ReturnToMenu → StartGame` 重新走完整流程：
- `MapGeneratorSystem.OnPanelChange` 调用 `Generate()` 时 `SetMap()` 会**覆盖**旧数据（`int[,]` 是新 new 的，`Rooms` 是新列表）
- `MapBuilder.OnMapGenerated` 在遍历 TileGrid 之前先 `ClearAllTiles()`
- `EnemyManagerSystem.OnPanelChange` 在生成新敌人前，`EnemyModel` 已被 `Clearup()` 清空（通过 `OnPanelChange` 的 `e.OldPanel == GamePlay` 分支，在离开上一局时清理过）

---

## 三、地图生成算法：随机房间 + L 形走廊（完整版）

### 3.1 数据结构

```
TileGrid[x, y] 的取值含义：
  0 = 空地（Empty）    —— 不会渲染，角色不可进入
  1 = 地板（Floor）    —— 渲染地板瓦片，角色可行走
  2 = 墙壁（Wall）     —— 渲染墙壁瓦片，有碰撞体，角色不可穿过
```

房间用左下角坐标 `(X, Y)` + `Width × Height` 表示，`Center` = 房间几何中心的世界坐标（用于放置角色）。

### 3.2 参数

| 参数 | 建议值 | 说明 |
|------|--------|------|
| `mapWidth` | 60 | 地图总宽（tile 坐标） |
| `mapHeight` | 40 | 地图总高 |
| `roomCount` | 5~8 随机 | 房间数量 |
| `minRoomSize` | 5 | 房间最小宽/高 |
| `maxRoomSize` | 10 | 房间最大宽/高 |
| `roomSpacing` | 1 | 房间之间的最小间距（tile） |
| `maxAttempts` | 200 | 每个房间放置的最大尝试次数 |

### 3.3 生成流程（逐步）

```
Generate(mapWidth, mapHeight, roomCount):

  // 步骤 1：初始化
  int[,] grid = new int[mapWidth, mapHeight]   // 默认全 0（空地）
  List<RoomData> rooms = new List<RoomData>()
  int roomMargin = 2   // 房间距地图边界的边距

  // 步骤 2：尝试放置房间
  for i = 0 .. roomCount-1:
      roomW = Random.Range(minRoomSize, maxRoomSize + 1)
      roomH = Random.Range(minRoomSize, maxRoomSize + 1)
      success = TryPlaceRoom(grid, mapWidth, mapHeight,
                             roomW, roomH, roomMargin, roomSpacing,
                             out roomX, out roomY)
      if success:
          room = new RoomData { X=roomX, Y=roomY, Width=roomW, Height=roomH,
                                Center=new Vector2(roomX+roomW/2f, roomY+roomH/2f) }
          CarveRoom(grid, room)    // 在地图上标记地板和墙壁
          rooms.Add(room)
      else:
          // 这个房间放不下，跳过（尝试下一个）

  // 步骤 3：按 X 排序
  rooms.Sort((a, b) => a.X.CompareTo(b.X))

  // 步骤 4：相邻房间之间挖走廊
  for i = 0 .. rooms.Count-2:
      r1 = rooms[i]
      r2 = rooms[i+1]
      x1 = (int)r1.Center.x
      y1 = (int)r1.Center.y
      x2 = (int)r2.Center.x
      y2 = (int)r2.Center.y
      CarveCorridor(grid, x1, y1, x2, y2)

  // 步骤 5：写入 Model + 发事件
  this.GetModel<IMapModel>().SetMap(grid, rooms)
```

### 3.4 核心算法伪代码

**TryPlaceRoom** — 尝试在随机位置放置房间：

```
TryPlaceRoom(grid, mapW, mapH, roomW, roomH, margin, spacing,
             out roomX, out roomY) → bool:

    // 留 margin 保证不贴地图边缘
    maxX = mapW - roomW - margin
    maxY = mapH - roomH - margin

    if maxX <= margin or maxY <= margin → return false  // 地图太小

    // 随机尝试 maxAttempts 次
    for attempt = 1 .. maxAttempts:
        roomX = Random.Range(margin, maxX + 1)
        roomY = Random.Range(margin, maxY + 1)

        // 检查区域：房间 + spacing 间距内都不能有非空地
        checkMinX = roomX - spacing
        checkMinY = roomY - spacing
        checkMaxX = roomX + roomW + spacing - 1
        checkMaxY = roomY + roomH + spacing - 1

        // 确保检查范围在地图内
        if checkMinX < 0 or checkMinY < 0 → continue
        if checkMaxX >= mapW or checkMaxY >= mapH → continue

        overlap = false
        for x = checkMinX .. checkMaxX:
            for y = checkMinY .. checkMaxY:
                if grid[x, y] != 0:
                    overlap = true; break
            if overlap → break

        if not overlap → return true  // 找到合法位置

    // 所有尝试都失败
    roomX = 0; roomY = 0
    return false
```

**CarveRoom** — 在地图上雕出房间（地板 + 边界墙壁）：

```
CarveRoom(grid, room):

    // 房间内部 → 地板(1)
    for x = room.X .. room.X + room.Width - 1:
        for y = room.Y .. room.Y + room.Height - 1:
            grid[x, y] = 1

    // 房间外一圈 → 墙壁(2)，只覆盖空地格
    for x = room.X - 1 .. room.X + room.Width:
        for y = room.Y - 1 .. room.Y + room.Height:
            if x < 0 or x >= grid.GetLength(0) → continue
            if y < 0 or y >= grid.GetLength(1) → continue
            if grid[x, y] == 0:
                grid[x, y] = 2
```

**CarveCorridor** — L 形走廊（先水平再垂直）：

```
CarveCorridor(grid, x1, y1, x2, y2):

    stepX = x2 > x1 ? 1 : -1

    // 水平段
    for x = x1 .. x2 (含 x2):
        if x >= 0 and x < grid.GetLength(0) and y1 >= 0 and y1 < grid.GetLength(1):
            grid[x, y1] = 1                          // 走廊中心 = 地板
            if y1-1 >= 0 and grid[x, y1-1] == 0:
                grid[x, y1-1] = 2                    // 上方墙壁
            if y1+1 < grid.GetLength(1) and grid[x, y1+1] == 0:
                grid[x, y1+1] = 2                    // 下方墙壁

    stepY = y2 > y1 ? 1 : -1

    // 垂直段
    for y = y1 .. y2 (含 y2):
        if x2 >= 0 and x2 < grid.GetLength(0) and y >= 0 and y < grid.GetLength(1):
            grid[x2, y] = 1                          // 走廊中心 = 地板
            if x2-1 >= 0 and grid[x2-1, y] == 0:
                grid[x2-1, y] = 2                    // 左侧墙壁
            if x2+1 < grid.GetLength(0) and grid[x2+1, y] == 0:
                grid[x2+1, y] = 2                    // 右侧墙壁
```

### 3.5 边界条件处理

- **地图太小放不下房间**：`maxX <= margin` 时 `TryPlaceRoom` 直接返回 false，不会出现死循环
- **所有房间放置尝试都失败**：最终 `rooms.Count` 可能小于 `roomCount`——可以接受，至少有 1 个房间即可
- **走廊坐标越界**：`CarveCorridor` 中每个格子操作前都检查 `x >= 0 && x < mapWidth`
- **走廊覆盖已有地板**：只写 `grid[x, y1] = 1` 覆盖已有地板也没问题（仍是 1）；墙壁只写在空地(0)上，不会覆盖已有地板
- **房间数量为 0**：如果随机失败导致没有房间——MapBuilder 不会绘制任何东西，SpawnUtility 需要检查 `Rooms.Count > 0`

---

## 四、文件修改/新增详细说明

### 4.1 修复 `Assets/Scripts/Model/IMapModel.cs`

**当前状态（有问题，需修复）**：

```csharp
// 问题1: Weight 拼写错误，应为 Width
public interface IMapModel : IModel
{
    int Weight { get; }         // 拼写错误，且无赋值路径
    int Height { get; }         // 无赋值路径
    List<RoomData> Rooms { get; }       // 返回类型应为 IReadOnlyList
    int[,] TileGrid { get; }    // 从未赋值
    void SetMap(...);
    void Clearup();
}

// 问题2: MapModel 实现不完整
public class MapModel : AbstractModel, IMapModel
{
    public int Weight { get; }             // get-only 自动属性，永远为 0
    public int Height { get; }             // get-only 自动属性，永远为 0
    public List<RoomData> Rooms => new List<RoomData>();   // 每次返回新空列表！
    public int[,] TileGrid { get; }        // get-only 自动属性，永远为 null
    OnInit() → throw NotImplementedException
}
```

**修复后**（参考 `EnemyModel` 的模式 —— 用 private 字段 + 接口方法操作数据）：

```csharp
public interface IMapModel : IModel
{
    int Width { get; }                          // 修正拼写
    int Height { get; }
    IReadOnlyList<RoomData> Rooms { get; }      // 用 IReadOnlyList 暴露，防止外部修改
    int[,] TileGrid { get; }

    void SetMap(int[,] tileGrid, List<RoomData> rooms);
    void Clearup();
}

public class MapModel : AbstractModel, IMapModel
{
    private int _width;
    private int _height;
    private List<RoomData> _rooms = new();
    private int[,] _tileGrid;

    public int Width => _width;
    public int Height => _height;
    public IReadOnlyList<RoomData> Rooms => _rooms;
    public int[,] TileGrid => _tileGrid;

    protected override void OnInit() { }  // 无需特殊初始化

    public void SetMap(int[,] tileGrid, List<RoomData> rooms)
    {
        _tileGrid = tileGrid;
        _width = tileGrid.GetLength(0);
        _height = tileGrid.GetLength(1);
        _rooms = rooms ?? new List<RoomData>();
    }

    public void Clearup()
    {
        _tileGrid = null;
        _width = 0;
        _height = 0;
        _rooms.Clear();
    }
}
```

### 4.2 新增 `Assets/Scripts/Event/MapGeneratedEvent.cs`

参照已有事件（如 `DamageEvent`）的 POCO 模式：

```csharp
/// <summary>
/// 地图生成完成事件。MapGeneratorSystem 写入 IMapModel 后发出，
/// MapBuilder 监听此事件来渲染 Tilemap。
/// </summary>
public class MapGeneratedEvent
{
    // 空事件体 —— 仅作为通知信号
    // 接收方通过 this.GetModel<IMapModel>() 获取具体数据
}
```

### 4.3 新增 `Assets/Scripts/System/MapGeneratorSystem.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

public interface IMapGeneratorSystem : ISystem
{
    void Generate(int mapWidth, int mapHeight, int roomCount);
}

public class MapGeneratorSystem : AbstractSystem, IMapGeneratorSystem
{
    // ========== 可调参数 ==========
    private const int MinRoomSize = 5;
    private const int MaxRoomSize = 10;
    private const int RoomMargin = 2;      // 房间距地图边缘
    private const int RoomSpacing = 1;     // 房间间距
    private const int MaxAttempts = 200;   // 单房间最大尝试次数

    protected override void OnInit()
    {
        // 监听面板切换：进入 GamePlay 时自动生成地图
        this.RegisterEvent<UIPanelChangeEvent>(OnPanelChange);
    }

    void OnPanelChange(UIPanelChangeEvent e)
    {
        if (e.NewPanel == UIPanelType.GamePlay)
        {
            int roomCount = Random.Range(5, 9); // 5~8 个房间
            Generate(60, 40, roomCount);
            this.SendEvent(new MapGeneratedEvent());
        }
    }

    public void Generate(int mapWidth, int mapHeight, int roomCount)
    {
        int[,] grid = new int[mapWidth, mapHeight];
        List<RoomData> rooms = new List<RoomData>();

        // 步骤1：放置房间
        for (int i = 0; i < roomCount; i++)
        {
            int roomW = Random.Range(MinRoomSize, MaxRoomSize + 1);
            int roomH = Random.Range(MinRoomSize, MaxRoomSize + 1);

            if (TryPlaceRoom(grid, mapWidth, mapHeight, roomW, roomH,
                             out int rx, out int ry))
            {
                var room = new RoomData
                {
                    X = rx, Y = ry,
                    Width = roomW, Height = roomH,
                    Center = new Vector2(rx + roomW / 2f, ry + roomH / 2f)
                };
                CarveRoom(grid, room);
                rooms.Add(room);
            }
        }

        // 步骤2：按 X 排序
        rooms.Sort((a, b) => a.X.CompareTo(b.X));

        // 步骤3：相邻房间挖走廊
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            var r1 = rooms[i];
            var r2 = rooms[i + 1];
            int x1 = (int)r1.Center.x;
            int y1 = (int)r1.Center.y;
            int x2 = (int)r2.Center.x;
            int y2 = (int)r2.Center.y;
            CarveCorridor(grid, mapWidth, mapHeight, x1, y1, x2, y2);
        }

        // 步骤4：写入 Model
        this.GetModel<IMapModel>().SetMap(grid, rooms);
    }

    // ========== 私有方法 ==========

    bool TryPlaceRoom(int[,] grid, int mapW, int mapH,
                      int roomW, int roomH,
                      out int roomX, out int roomY)
    {
        int maxX = mapW - roomW - RoomMargin;
        int maxY = mapH - roomH - RoomMargin;

        roomX = 0; roomY = 0;

        if (maxX <= RoomMargin || maxY <= RoomMargin) return false;

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            roomX = Random.Range(RoomMargin, maxX + 1);
            roomY = Random.Range(RoomMargin, maxY + 1);

            int cx0 = roomX - RoomSpacing;
            int cy0 = roomY - RoomSpacing;
            int cx1 = roomX + roomW + RoomSpacing - 1;
            int cy1 = roomY + roomH + RoomSpacing - 1;

            if (cx0 < 0 || cy0 < 0 || cx1 >= mapW || cy1 >= mapH)
                continue;

            bool overlap = false;
            for (int x = cx0; x <= cx1 && !overlap; x++)
                for (int y = cy0; y <= cy1 && !overlap; y++)
                    if (grid[x, y] != 0)
                        overlap = true;

            if (!overlap) return true;
        }
        return false;
    }

    void CarveRoom(int[,] grid, RoomData room)
    {
        // 地板
        for (int x = room.X; x < room.X + room.Width; x++)
            for (int y = room.Y; y < room.Y + room.Height; y++)
                grid[x, y] = 1;

        // 边界墙壁（只覆盖 0=空地）
        int mapW = grid.GetLength(0);
        int mapH = grid.GetLength(1);
        for (int x = room.X - 1; x <= room.X + room.Width; x++)
        {
            for (int y = room.Y - 1; y <= room.Y + room.Height; y++)
            {
                if (x < 0 || x >= mapW || y < 0 || y >= mapH) continue;
                if (grid[x, y] == 0) grid[x, y] = 2;
            }
        }
    }

    void CarveCorridor(int[,] grid, int mapW, int mapH,
                       int x1, int y1, int x2, int y2)
    {
        // 水平段
        int stepX = x2 > x1 ? 1 : -1;
        for (int x = x1; x != x2 + stepX; x += stepX)
        {
            if (x < 0 || x >= mapW || y1 < 0 || y1 >= mapH) continue;
            grid[x, y1] = 1;
            if (y1 - 1 >= 0 && grid[x, y1 - 1] == 0) grid[x, y1 - 1] = 2;
            if (y1 + 1 < mapH && grid[x, y1 + 1] == 0) grid[x, y1 + 1] = 2;
        }

        // 垂直段
        int stepY = y2 > y1 ? 1 : -1;
        for (int y = y1; y != y2 + stepY; y += stepY)
        {
            if (x2 < 0 || x2 >= mapW || y < 0 || y >= mapH) continue;
            grid[x2, y] = 1;
            if (x2 - 1 >= 0 && grid[x2 - 1, y] == 0) grid[x2 - 1, y] = 2;
            if (x2 + 1 < mapW && grid[x2 + 1, y] == 0) grid[x2 + 1, y] = 2;
        }
    }
}
```

### 4.4 新增 `Assets/Scripts/ViewController/MapBuilder.cs`

```csharp
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapBuilder : MonoBehaviour, IController
{
    [SerializeField] private Tilemap _floorTilemap;
    [SerializeField] private Tilemap _wallTilemap;
    [SerializeField] private TileBase _floorTile;
    [SerializeField] private TileBase _wallTile;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        this.RegisterEvent<MapGeneratedEvent>(OnMapGenerated)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void OnMapGenerated(MapGeneratedEvent e)
    {
        BuildFromModel();
        SetupCamera();
    }

    void BuildFromModel()
    {
        var map = this.GetModel<IMapModel>();

        _floorTilemap.ClearAllTiles();
        _wallTilemap.ClearAllTiles();

        int[,] grid = map.TileGrid;
        if (grid == null) return;

        int w = map.Width;
        int h = map.Height;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                switch (grid[x, y])
                {
                    case 1: _floorTilemap.SetTile(pos, _floorTile); break;
                    case 2: _wallTilemap.SetTile(pos, _wallTile); break;
                }
            }
        }
    }

    void SetupCamera()
    {
        var map = this.GetModel<IMapModel>();

        // 跟随玩家
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            this.GetUtility<ICameraUtility>().Follow(player.transform);
        }

        // 限制摄像范围不超出地图
        this.GetUtility<ICameraUtility>().SetBounds(0, map.Width, 0, map.Height);
    }
}
```

### 4.5 修改 `Assets/Scripts/RogueLikeGame.cs`

```csharp
protected override void Init()
{
    // ========== Model ==========
    this.RegisterModel<IEntityModel>(new PlayerEntityModel());
    this.RegisterModel<ICombatModel>(new PlayerCombatModel());
    this.RegisterModel<IEnemyModel>(new EnemyModel());
    this.RegisterModel<IGameStateModel>(new GameStateModel());
    this.RegisterModel<IMapModel>(new MapModel());           // ★ 新增

    // ========== System ==========
    // ★ MapGeneratorSystem 必须在 EnemyManagerSystem 之前注册
    this.RegisterSystem<IMapGeneratorSystem>(new MapGeneratorSystem());  // ★ 新增
    this.RegisterSystem<ICombatSystem>(new CombatSystem());
    this.RegisterSystem<IEnemyManagerSystem>(new EnemyManagerSystem());
    this.RegisterSystem<IUISystem>(new UISystem());

    // Player FSM
    this.RegisterSystem<FsmIdleState>(new FsmIdleState());
    this.RegisterSystem<FsmMoveState>(new FsmMoveState());
    this.RegisterSystem<FsmAttackState>(new FsmAttackState());
    this.RegisterSystem<FsmHurtState>(new FsmHurtState());
    this.RegisterSystem<IFSMSystem>(new FSMSystem());

    // ========== Utility ==========
    this.RegisterUtility<IInputUtility>(new InputUtility());
    this.RegisterUtility<IHitstopUtility>(new HitstopUtility());
    this.RegisterUtility<ICameraUtility>(new CameraUtility());
    this.RegisterUtility<IAnimationUtility>(new AnimationUtility());
    this.RegisterUtility<ISpawnUtility>(new SpawnUtility());
}
```

### 4.6 修改 `Assets/Scripts/Utility/ISpawnUtility.cs` + `Assets/Scripts/System/EnemyManagerSystem.cs`

#### 4.6.1 设计原则

SpawnUtility 属于 **Utility 层**，根据 Rule.txt 的约束，Utility 层"啥都干不了，只封装第三方/基础设施 API"。它**不能获取 IModel**（不能调用 `this.GetModel<IMapModel>()`）。

因此职责划分如下：

```
EnemyManagerSystem（System 层）           SpawnUtility（Utility 层）
┌─────────────────────────────┐          ┌──────────────────────────┐
│ 获取 IMapModel.Rooms        │          │                          │
│ 遍历房间，决定生成位置      │──位置──→│ GameObject.Instantiate() │
│ 调用 EnemyModel.Register    │←─数据──│ 构建 EnemyRuntimeData    │
│ 调用 EnemyView.Init         │          │                          │
└─────────────────────────────┘          └──────────────────────────┘

谁拥有"在哪生成"的知识？      →      System 层（读 IMapModel）
谁拥有"怎么生成"的知识？      →      Utility 层（Instantiate + BuildEnemyData）
```

- **EnemyManagerSystem** 负责"决策"：读地图 → 拿到房间坐标 → 决定在哪儿生成
- **SpawnUtility** 负责"执行"：给定一个坐标 → 实例化 Prefab → 构建 RuntimeData → 返回给调用者
- SpawnUtility 不感知地图、房间、列表。每个方法都是**纯函数**——输入坐标，输出 GameObject/数据

#### 4.6.2 当前代码（改造前）

```csharp
// ===== ISpawnUtility.cs =====
public interface ISpawnUtility : IUtility
{
    GameObject SpawnPlayer(Vector2 pos);        // 已改为传位置（上次修改）
    void SpwanEnemy(List<EnemySpawnData> outEnmeyList);  // 无位置参数，内部硬编码坐标
    void CleanupAll();
}

// SpawnUtility 内部：
public void SpwanEnemy(List<EnemySpawnData> outEnmeyList)
{
    foreach (var pos in GetSpawnPositions())    // ← 硬编码坐标！
    {
        var go = Instantiate(perfab, pos, ...);
        outEnmeyList.Add(new EnemySpawnData { GO = go, Data = data });
    }
}

Vector2[] GetSpawnPositions() => new[]          // ← 需要删除
{
    new Vector2(3f, 1f), new Vector2(5f, -1f), new Vector2(7f, 0f)
};

// ===== EnemyManagerSystem.cs =====
// 字段：
private List<EnemySpawnData> _spawnList = new();  // ← 改造后不再需要

// OnPanelChange 进入 GamePlay 分支：
var PlayerGo = spwan.SpawnPlayer();              // 无位置参数（旧）
PlayerGo.GetComponent<PlayerController>().Init();
_playerTransform = PlayerGo.transform;

_spawnList.Clear();
spwan.SpwanEnemy(_spawnList);                    // 无位置参数（旧）
foreach (var sd in _spawnList) { ... }
```

#### 4.6.3 改造后（目标代码）

**SpawnUtility — 纯 Instantiate 工厂**：

```csharp
// ===== ISpawnUtility.cs =====
public interface ISpawnUtility : IUtility
{
    // 在指定位置生成玩家，返回 GameObject（调用者负责 Init）
    GameObject SpawnPlayer(Vector2 atPosition);

    // 在指定位置生成一个敌人，返回包含 GO 和 RuntimeData 的结构体（调用者负责 Register + Init）
    EnemySpawnData SpawnEnemy(Vector2 atPosition);

    // 清理所有已生成的玩家和敌人 GameObject
    void CleanupAll();
}

public class SpawnUtility : ISpawnUtility
{
    private IAchitecture _architecture;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
    }

    public GameObject SpawnPlayer(Vector2 atPosition)
    {
        var perfab = Resources.Load<GameObject>("Perfabs/Player");
        var go = GameObject.Instantiate(perfab, atPosition, Quaternion.identity);
        return go;
    }

    public EnemySpawnData SpawnEnemy(Vector2 atPosition)
    {
        var perfab = Resources.Load<GameObject>("Perfabs/Enemy");
        var go = GameObject.Instantiate(perfab, atPosition, Quaternion.identity);
        var data = BuildEnemyData(atPosition);
        return new EnemySpawnData { GO = go, Data = data };
    }

    public void CleanupAll()
    {
        foreach (var obj in GameObject.FindGameObjectsWithTag("Enemy"))
            GameObject.Destroy(obj);
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) GameObject.Destroy(player);
    }

    private EnemyRuntimeData BuildEnemyData(Vector2 pos) => new EnemyRuntimeData
    {
        MaxHp = 6,
        CurrentHp = 6,
        AttackPower = 1,
        DefensePower = 1,
        AttackRange = 1f,
        ChaseRange = 5f,
        MoveSpeed = 3f,
        AttackDuration = 0.5f,
        HitCheckTime = 0.25f,
        HurtDuration = 0.4f,
        KnockbackForce = 8f,
        KnockbackDecay = 0.85f,
        State = EnemyActionState.Idle,
        Position = pos
    };

    // ★ 删除 GetSpawnPositions() —— 位置由调用者决定
}
```

**接口变化对比**：

| 方法 | 改造前 | 改造后 |
|------|--------|--------|
| `SpawnPlayer` | `SpawnPlayer()` 无参→硬编码 Vector2.zero | `SpawnPlayer(Vector2 atPosition)` |
| `SpwanEnemy` | `SpwanEnemy(List<EnemySpawnData> outEnmeyList)` 无位置→硬编码3个坐标 | `SpawnEnemy(Vector2 atPosition)` 返回单个 EnemySpawnData |
| `GetSpawnPositions` | `Vector2[]` 3个硬编码坐标 | **删除** |
| `_spawnList`（EnemyManager字段） | `List<EnemySpawnData>` 中间列表 | **删除** |

> 注意：`SpwanEnemy` 的拼写也修正为 `SpawnEnemy`（typo 修正）。

**EnemyManagerSystem — 负责决策逻辑**：

```csharp
// ===== EnemyManagerSystem.cs =====
public class EnemyManagerSystem : AbstractSystem, IEnemyManagerSystem
{
    private Transform _playerTransform;
    private List<int> _idSnapshot = new();   // 只保留用于遍历快照的列表
    // ★ 删除 private List<EnemySpawnData> _spawnList = new();

    private const float AttackDuration = 0.5f;
    private const float HitCheckTime = 0.25f;
    private const float HurtDuration = 0.4f;
    private const float KnockbackDecay = 0.85f;

    protected override void OnInit()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) _playerTransform = player.transform;
        this.RegisterEvent<UIPanelChangeEvent>(OnPanelChange);
    }

    void OnPanelChange(UIPanelChangeEvent e)
    {
        var spwan = this.GetUtility<ISpawnUtility>();
        var enmeyModel = this.GetModel<IEnemyModel>();

        if (e.NewPanel == UIPanelType.GamePlay)
        {
            // ★ 改造后：从 IMapModel.Rooms 读位置
            var map = this.GetModel<IMapModel>();
            var rooms = map.Rooms;

            // 玩家在第一个房间中心
            if (rooms.Count > 0)
            {
                var playerGo = spwan.SpawnPlayer(rooms[0].Center);
                playerGo.GetComponent<PlayerController>().Init();
                _playerTransform = playerGo.transform;
            }

            // 敌人在其余房间中心（逐个生成）
            for (int i = 1; i < rooms.Count; i++)
            {
                var sd = spwan.SpawnEnemy(rooms[i].Center);
                var id = enmeyModel.Register(sd.Data);
                sd.GO.GetComponent<EnemyView>().Init(id, sd.Data);
            }
        }
        else if (e.OldPanel == UIPanelType.GamePlay)
        {
            _idSnapshot.Clear();
            foreach (var kv in enmeyModel.GetAll())
                _idSnapshot.Add(kv.Key);
            foreach (var id in _idSnapshot)
                enmeyModel.Unregister(id);

            _playerTransform = null;
            spwan.CleanupAll();
        }
    }

    // Update / ChangeState / OnEnemyDamaged 不变
}
```

#### 4.6.4 对比：改造前 vs 改造后

**调用流程对比**：

```
改造前（硬编码）：
  EnemyManagerSystem.OnPanelChange
    ├── spwan.SpawnPlayer()                    // 不知道在哪生成，内部写死 Vector2.zero
    ├── _spawnList.Clear()                     // 准备中间列表
    ├── spwan.SpwanEnemy(_spawnList)           // 不知道在哪生成，内部写死3个坐标
    └── foreach _spawnList → Register + Init   // 遍历中间列表

改造后（数据驱动）：
  EnemyManagerSystem.OnPanelChange
    ├── map = GetModel<IMapModel>()            // 获取地图数据
    ├── rooms = map.Rooms                      // 获取房间列表（含坐标）
    ├── spwan.SpawnPlayer(rooms[0].Center)     // 明确告诉 SpawnUtility 在哪个位置
    └── for i = 1..rooms.Count:
          sd = spwan.SpawnEnemy(rooms[i].Center) // 每个敌人独立调用，位置明确
          Register(sd.Data) + Init(sd.GO)        // 立即注册和初始化
```

**架构合规性**：

| 检查项 | 改造前 | 改造后 |
|--------|--------|--------|
| Utility 层是否获取了 IModel？ | 间接：`GetSpawnPositions()` 硬编码坐标，不需要 Model，但不是数据驱动 | **合规**：SpawnUtility 接收位置参数，不获取 Model |
| "在哪儿生成"的知识在哪？ | 分散在 SpawnUtility 硬编码数组 | **集中在 EnemyManagerSystem**，读取 IMapModel |
| System 层是否承担了不该有的职责？ | EnemyManagerSystem 只做中间传递 | **合规**：EnemyManagerSystem 是 System 层，有权获取 Model |
| 中间数据 | `_spawnList` 字段，List 积累后批量处理 | **消除**：逐个处理，不需要中间列表 |

#### 4.6.5 为什么改成"逐个生成"而不是传入数组？

```csharp
// 方案 A（逐个）：SpawnEnemy(Vector2) → EnemySpawnData
for (int i = 1; i < rooms.Count; i++)
{
    var sd = spwan.SpawnEnemy(rooms[i].Center);  // 单个位置进，单个数据出
    var id = enmeyModel.Register(sd.Data);
    sd.GO.GetComponent<EnemyView>().Init(id, sd.Data);
}

// 方案 B（数组）：SpwanEnemy(List<EnemySpawnData>, Vector2[])
var positions = new Vector2[rooms.Count - 1];
for (int i = 1; i < rooms.Count; i++)
    positions[i - 1] = rooms[i].Center;
spwan.SpwanEnemy(_spawnList, positions);
foreach (var sd in _spawnList) { ... }
```

选择方案 A 的理由：

1. **职责更纯粹**：SpawnUtility 的方法变成"给我一个坐标，还你一个敌人"的纯函数，不需要理解"列表"或"批量"的概念
2. **消除中间字段**：`_spawnList` 字段不再需要，`EnemyManagerSystem` 少了一个状态
3. **更灵活**：如果后续想在不同房间生成不同类型的敌人（Boss房/普通房），只需在 for 循环中加判断，无需改 SpawnUtility
4. **调用者一眼能看懂**：`spwan.SpawnEnemy(rooms[i].Center)` 读起来就是"在房间i的中心生成一个敌人"

### 4.7 修改 `Assets/Scripts/Utility/ICameraUtility.cs`

```csharp
public interface ICameraUtility : IUtility
{
    void Init(MonoBehaviour runner);
    void Shake(float intensity, float duration);

    // ★ 新增
    void Follow(Transform target);
    void SetBounds(float minX, float maxX, float minY, float maxY);
}

public class CameraUtility : ICameraUtility
{
    // ... 已有代码保持不变 ...

    private Transform _followTarget;
    private float _minX, _maxX, _minY, _maxY;
    private bool _hasBounds;

    public void Follow(Transform target)
    {
        _followTarget = target;
    }

    public void SetBounds(float minX, float maxX, float minY, float maxY)
    {
        float camHalfH = _camera.orthographicSize;
        float camHalfW = camHalfH * _camera.aspect;
        _minX = minX + camHalfW;
        _maxX = maxX - camHalfW;
        _minY = minY + camHalfH;
        _maxY = maxY - camHalfH;
        _hasBounds = true;
    }

    // 需要在 Shake 协程之外处理跟随逻辑
    // 推荐在 MapBuilder 或一个专门的 LateUpdate 中调用如下方法：
    public void LateTick()
    {
        if (_followTarget == null || _camera == null) return;

        Vector3 targetPos = _followTarget.position;
        targetPos.z = _camera.transform.position.z;

        if (_hasBounds)
        {
            targetPos.x = Mathf.Clamp(targetPos.x, _minX, _maxX);
            targetPos.y = Mathf.Clamp(targetPos.y, _minY, _maxY);
        }

        _camera.transform.position = targetPos;
    }
}
```

---

## 五、Unity 编辑器设置步骤

### 5.1 创建 Tilemap 层级结构

在 `SampleScene` 场景中：

```
场景 Hierarchy：
  Grid (GameObject)
  ├── Floor (Tilemap)          ← Tilemap Renderer，Order in Layer = 0
  ├── Wall (Tilemap)           ← Tilemap Renderer + TilemapCollider2D + CompositeCollider2D
  └── MapBuilder (GameObject)  ← 挂 MapBuilder.cs
```

**创建方式**：
1. 右键 Hierarchy → 2D Object → Tilemap → **创建 Grid + Tilemap**
2. 删除默认的 Tilemap，重新创建两个：
   - 右键 Grid → 2D Object → Tilemap，命名为 `Floor`
   - 右键 Grid → 2D Object → Tilemap，命名为 `Wall`

**Wall 碰撞设置**：
- 选中 `Wall` GameObject
- Add Component → `TilemapCollider2D`
- 在 `TilemapCollider2D` 上勾选 `Used By Composite`
- Add Component → `CompositeCollider2D`
- `CompositeCollider2D` 的 `Geometry Type` 选 `Outlines`

**注意事项**：
- `Wall` 所在的 Rigidbody2D（CompositeCollider2D 会自动添加）的 `Body Type` 会自动设为 `Static`
- 角色已有的 `Rigidbody2D` 需确保 `Body Type` 不是 Static，碰撞即可自动生效
- Tilemap Renderer 的 `Order in Layer`：Floor 设 0，Wall 设 0（两者在同一层即可，Floor 瓦片和 Wall 瓦片不会重叠）

### 5.2 创建 Tile 资产

1. Window → 2D → **Tile Palette** 打开调色板窗口
2. Create New Palette → 命名为 `RoguelikeTiles`，保存到 `Assets/TilePalettes/`
3. 将 `Tilemap_Flat.png`（或项目中现有的 Terrain 素材）拖入 Palette 窗口
4. 创建两个 Tile 资产：
   - 右键 Project → Create → 2D → Tiles → **Rule Tile**（或直接创建 Tile），命名为 `FloorTile`
   - 同样创建 `WallTile`
   - 或者用代码创建：选中 Palette 中的 Sprite → 自动生成 Tile 资产

### 5.3 挂载 MapBuilder

1. 选中 `MapBuilder` GameObject
2. Add Component → `MapBuilder`
3. 拖入引用：
   - `_floorTilemap` → 场景中的 `Floor` Tilemap
   - `_wallTilemap` → 场景中的 `Wall` Tilemap
   - `_floorTile` → Project 中的 `FloorTile` 资产
   - `_wallTile` → Project 中的 `WallTile` 资产

### 5.4 摄像机跟随逻辑

在 `MapBuilder` 上或在已有的 `EnemyManagerDriver` 上加 `LateUpdate`：

```csharp
// 在 EnemyManagerDriver 或 MapBuilder 中添加
void LateUpdate()
{
    this.GetUtility<ICameraUtility>().LateTick();
}
```

或者单独创建一个 `CameraDriver : MonoBehaviour, IController`。

---

## 六、与现有代码的交互点（完整清单）

### 6.1 与 UIPanelChangeEvent 的交互

`UIPanelChangeEvent` 当前被三个地方监听：

| 监听者 | 层级 | 作用 | 变更 |
|--------|------|------|------|
| `MapGeneratorSystem` | System | ★ 新增：生成地图数据 | 无需改 |
| `EnemyManagerSystem` | System | 已有：生成角色 | **需改**：改为从 IMapModel.Rooms 读位置（见 §4.6） |
| `UIMangager` | Controller | 已有：切换面板 UI | 无需改 |

### 6.2 与 EnemyManagerSystem 的交互（详细对比）

**改造前的 `OnPanelChange`（硬编码版本）**：

```csharp
if (e.NewPanel == UIPanelType.GamePlay)
{
    // 生成玩家 —— 位置写死在 SpawnPlayer 内部（Vector2.zero）
    var PlayerGo = spwan.SpawnPlayer();
    PlayerGo.GetComponent<PlayerController>().Init();
    _playerTransform = PlayerGo.transform;

    // 生成敌人 —— 位置写死在 GetSpawnPositions() 里
    _spawnList.Clear();
    spwan.SpwanEnemy(_spawnList);          // 不知道会生成几个、在哪
    foreach (var sd in _spawnList)
    {
        var id = enmeyModel.Register(sd.Data);
        sd.GO.GetComponent<EnemyView>().Init(id, sd.Data);
    }
}
```

**改造后的 `OnPanelChange`（数据驱动版本）**：

```csharp
if (e.NewPanel == UIPanelType.GamePlay)
{
    // 1. 读取地图数据（此时 MapGeneratorSystem 已生成完毕）
    var map = this.GetModel<IMapModel>();
    var rooms = map.Rooms;

    // 2. 玩家在第一个房间中心
    if (rooms.Count > 0)
    {
        var playerGo = spwan.SpawnPlayer(rooms[0].Center);
        playerGo.GetComponent<PlayerController>().Init();
        _playerTransform = playerGo.transform;
    }

    // 3. 敌人在其余房间中心 —— 逐个生成
    for (int i = 1; i < rooms.Count; i++)
    {
        var sd = spwan.SpawnEnemy(rooms[i].Center);
        var id = enmeyModel.Register(sd.Data);
        sd.GO.GetComponent<EnemyView>().Init(id, sd.Data);
    }
}
```

**关键变化总结**：

| 变化点 | 改造前 | 改造后 |
|--------|--------|--------|
| 玩家位置来源 | `Vector2.zero`（硬编码） | `rooms[0].Center`（地图数据） |
| 敌人位置来源 | `GetSpawnPositions()` 3个硬编码坐标 | `rooms[1..].Center`（地图数据） |
| `_spawnList` 字段 | 需要（积累中间列表） | **删除**（逐个处理） |
| 敌人数量 | 固定3个 | 由地图房间数决定（1~7个） |
| 房间数 ≤ 1 时 | 可能生成在地图外 | `rooms.Count > 1` 守卫，安全 |

### 6.3 与 EnemyModel 的交互

不需要改动。`Register` / `Unregister` 接口不变。

---

## 七、异常与边界情况

| 情况 | 处理方式 |
|------|---------|
| 所有房间放置失败（rooms.Count == 0） | `EnemyManagerSystem` 检查 `rooms.Count > 0`，不生成任何角色。`MapBuilder` 不做任何 Tile 设置。 |
| 只有1个房间（rooms.Count == 1） | 玩家生成在该房间，敌人不生成。`GameplayPanel` 检测到敌人数量为 0 → 立即触发胜利。 |
| corridor 路径越界 | `CarveCorridor` 中每步都做边界检查 |
| `IMapModel` 中 `TileGrid` 为 null | `MapBuilder.BuildFromModel()` 检查 `grid == null` 直接 return |
| `_playerTransform` 为 null | `EnemyManagerSystem.Update()` 已有检查，直接 return |
| 房间重叠检测 | `TryPlaceRoom` 检查 `RoomSpacing` 间距内的所有格子 |

---

## 八、文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| **修复** | `Assets/Scripts/Model/IMapModel.cs` | 修正 Weight→Width、实现 MapModel 数据存储、修复 Rooms/TileGrid getter |
| **新增** | `Assets/Scripts/System/MapGeneratorSystem.cs` | IMapGeneratorSystem 接口 + MapGeneratorSystem（含完整生成算法） |
| **新增** | `Assets/Scripts/ViewController/MapBuilder.cs` | Tilemap 绘制的 MonoBehaviour |
| **新增** | `Assets/Scripts/Event/MapGeneratedEvent.cs` | 空事件类（通知 MapBuilder 渲染） |
| **修改** | `Assets/Scripts/RogueLikeGame.cs` | 注册 IMapModel + IMapGeneratorSystem（注册顺序见 §2） |
| **修改** | `Assets/Scripts/Utility/ISpawnUtility.cs` | `SpawnPlayer` 加位置参数；`SpwanEnemy`→`SpawnEnemy(Vector2)` 改为逐个体生成并返回 `EnemySpawnData`；删除 `GetSpawnPositions()` |
| **修改** | `Assets/Scripts/System/EnemyManagerSystem.cs` | `OnPanelChange` 改为从 `IMapModel.Rooms` 读位置传给 SpawnUtility；删除 `_spawnList` 字段 |
| **修改** | `Assets/Scripts/Utility/ICameraUtility.cs` | 加 `Follow(Transform)` + `SetBounds(float,float,float,float)` + `LateTick()` |

**Unity 编辑器操作**：
- 创建 Grid/Floor/Wall Tilemap 层级
- 设置 Wall 碰撞（TilemapCollider2D + CompositeCollider2D）
- 创建地板/墙壁 Tile 资产
- 创建 MapBuilder GameObject，挂脚本，拖入引用

---

## 九、进阶扩展（后续可做）

- **房间类型**：`RoomData` 加 `RoomType` 枚举（Normal/Treasure/Shop/Boss），不同的 `RoomType` 在 `SpawnUtility.SpwanEnemy` 中生成不同难度的敌人
- **多层级地图**：`IMapModel` 加 `int CurrentFloor` + `List<int[,]> FloorGrids`，楼梯瓦片触发 `MapGeneratorSystem.GenerateNextFloor()`
- **迷雾/视野**：加一个 `FogOfWar` Tilemap（Overlay 层），根据玩家位置计算可见 tile，动态 SetTile/SetColor
- **最小生成树走廊**：用 Prim 算法计算房间之间的最小生成树，只保留树中的边——走廊更自然，避免冗余连接
- **种子控制**：`MapGeneratorSystem` 用 `System.Random(seed)` 代替 `UnityEngine.Random`，支持同一地图重玩和分享
- **BSP 算法变体**：二分空间分割生成更规整的矩形房间布局，适合传统 Rogue 风格
