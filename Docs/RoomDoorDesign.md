# 房间门系统 — 设计文档

## 1. 目标

玩家进入房间后，房门关闭；击杀该房间所有敌人后，房门打开，玩家可前往其他房间。

---

## 2. 现有架构回顾（关键复用点）

| 机制 | 位置 | 可复用方式 |
|------|------|-----------|
| ExitPoint 预制体 / 触发推进 | `ViewController/ExitPoint.cs` | Door 采用相同结构：预制体 + Collider2D |
| 全局敌人死亡事件 | `Model/EnemyModel.cs` → `AllEnemiesDeadEvent` | 保持全局事件不变，新增按房间的事件 |
| GameplayPanel 监听全局事件 | `ViewController/UIController/GameplayPanel.cs` | 保持原有逻辑（ExitPoint 生成），不修改 |
| 敌人生成位置 | `System/EnemyManagerSystem.cs:53-58` | 每个敌人出生在某个 `rooms[i].Center`，天然可绑定 RoomIndex |
| `EnemyRuntimeData.IndexRoom` | `Model/EnemyRuntimeData.cs:39` | **已存在**，生成时赋值即可 |
| `DoorModel.cs` | `Model/DoorModel.cs` | **已存在**，结构体 + 接口已定义，需补实现类 + 加 `RoomIndex` |
| 地图/房间数据 | `Model/MapModel` → `List<RoomData>` | 已有房间 bounds，用于"玩家在哪个房间"判定 |
| 走廊生成 | `MapGeneratorSystem.CarveCorridor` | 走廊连接相邻房间，房门放在走廊与房间的交界处 |
| 存档系统 | `Utility/ISaveUtility` + `SaveData` | 门状态需序列化到存档 |

---

## 3. 核心设计决策

### 3.1 敌人→房间 关联

`EnemyRuntimeData.IndexRoom` 字段**已存在**。敌人在哪个房间出生就标记为哪个房间的索引。`EnemyManagerSystem` 生成敌人时赋值，后续敌人死亡时即可查出所属房间。

### 3.2 房间敌人计数 — 放在 EnemyModel 内部

```
架构约束：Model 可以 SendEvent，但不能监听 Event。
因此不能创建独立的 RoomEnemyModel 去监听 EnemyModel 的事件。

方案：在 EnemyModel 内部维护 Dictionary<int, int> _alivePerRoom，
Register 时 +1，Unregister 时 -1。某房间计数归零时发送 RoomEnemiesClearedEvent。

优点：零新增 Model，数据自包含，无跨 Model 协调问题。
```

### 3.3 门的位置

门放在走廊与房间的交界处。对于每对相邻房间，走廊是 L 形（先水平再垂直），门位于走廊穿出/穿入房间边缘的位置。

### 3.4 门的开/闭 触发

由 `DoorSystem` 统一管理：

| 触发条件 | 行为 |
|----------|------|
| 玩家进入房间（`PlayerEnteredRoomEvent`）且房间未清空 | 关闭该房间所有门 |
| 房间敌人清空（`RoomEnemiesClearedEvent`） | 打开该房间所有门 |
| 全局敌人清空（`AllEnemiesDeadEvent`） | 打开所有门（兜底） |
| 楼层推进（`FloorAdvancedEvent`） | 清理旧门 + 重新生成 |

特殊规则：
- 起始房间（RoomIndex=0）无敌人，`_alivePerRoom[0]` 始终为 0，门永不关闭
- 已清空的房间重访时 `IsRoomCleared` 返回 true，不关门

### 3.5 玩家所在房间检测

新建 `RoomDetector` 组件（挂 Player 上），每帧遍历 `IMapModel.Rooms` 判断玩家位置在哪个房间的 bounds 内。房间变化时发送 `PlayerEnteredRoomCommand`（表现层不能发 Event，必须通过 Command）。在走廊中时维持上一个房间状态。

房间数 5~9，每帧 O(N) 遍历无性能影响。

---

## 4. Model 层详细设计

### 4.1 概述

涉及 2 个 Model 的改动 + 1 个存档案结构体：

| Model | 操作 | 职责 |
|-------|------|------|
| `EnemyModel` | 修改 | 新增房间敌人计数；Unregister 时发送房间清空事件 |
| `DoorModel` | 补完 | 已有接口 + 结构体，补实现类，`DoorData` 加 `RoomIndex` |
| `SaveData` | 修改 | 新增门状态字段用于存档 |

不新建 `IRoomEnemyModel` — 房间敌人计数逻辑足够简单，放在 `EnemyModel` 内部更内聚。

---

### 4.2 EnemyModel 改动

#### 现有代码（`Model/EnemyModel.cs`）

```csharp
public interface IEnemyModel : IModel
{
    int Register(EnemyRuntimeData init);
    void Unregister(int id);
    EnemyRuntimeData Get(int id);
    bool TryGet(int id, out EnemyRuntimeData data);
    IReadOnlyDictionary<int, EnemyRuntimeData> GetAll();
    void SetCurrentHp(int id, int hp);
    void SetState(int id, EnemyActionState state);
    // ... 其他 Set 方法
}

public class EnemyModel : AbstractModel, IEnemyModel
{
    private Dictionary<int, EnemyRuntimeData> _enemies = new();
    private int _nextId = 1;

    public int Register(EnemyRuntimeData init)
    {
        init.EnemyId = _nextId;
        _enemies[_nextId] = init;
        return _nextId++;
    }

    public void Unregister(int id)
    {
        _enemies.Remove(id);
        if (_enemies.Count == 0)
        {
            this.SendEvent(new AllEnemiesDeadEvent());
        }
    }
    // ...
}
```

#### 改动后

新增接口方法（红色标注关键新增）：

```csharp
public interface IEnemyModel : IModel
{
    // === 现有方法保持不变 ===
    int Register(EnemyRuntimeData init);
    void Unregister(int id);
    EnemyRuntimeData Get(int id);
    bool TryGet(int id, out EnemyRuntimeData data);
    IReadOnlyDictionary<int, EnemyRuntimeData> GetAll();
    void SetCurrentHp(int id, int hp);
    void SetState(int id, EnemyActionState state);
    void SetMoveDelta(int id, Vector2 delta);
    void SetPosition(int id, Vector2 pos);
    void SetFacingDir(int id, int dir);
    void SetKnockbackVelocity(int id, Vector2 vel);
    void SetHitChecked(int id, bool c);
    void SetStateTimer(int id, float t);

    // === 新增：房间敌人计数 ===
    int GetAliveCountInRoom(int roomIndex);
    bool IsRoomCleared(int roomIndex);
}

public class EnemyModel : AbstractModel, IEnemyModel
{
    private Dictionary<int, EnemyRuntimeData> _enemies = new();
    private int _nextId = 1;

    // 新增：房间存活敌人计数
    private Dictionary<int, int> _alivePerRoom = new();

    protected override void OnInit() { }

    public int Register(EnemyRuntimeData init)
    {
        init.EnemyId = _nextId;
        _enemies[_nextId] = init;

        // 新增：房间计数 +1
        int roomIdx = init.IndexRoom;
        _alivePerRoom.TryGetValue(roomIdx, out int c);
        _alivePerRoom[roomIdx] = c + 1;

        return _nextId++;
    }

    public void Unregister(int id)
    {
        if (!_enemies.TryGetValue(id, out var data))
            return;

        int roomIdx = data.IndexRoom;

        _enemies.Remove(id);

        // 新增：房间计数 -1，归零时发事件
        if (_alivePerRoom.TryGetValue(roomIdx, out int c) && c > 0)
        {
            c--;
            _alivePerRoom[roomIdx] = c;
            if (c == 0)
            {
                this.SendEvent(new RoomEnemiesClearedEvent { RoomIndex = roomIdx });
            }
        }

        // 现有逻辑：全局清空事件
        if (_enemies.Count == 0)
        {
            this.SendEvent(new AllEnemiesDeadEvent());
        }
    }

    // 新增：查询方法
    public int GetAliveCountInRoom(int roomIndex)
    {
        _alivePerRoom.TryGetValue(roomIndex, out int c);
        return c;
    }

    public bool IsRoomCleared(int roomIndex)
    {
        return GetAliveCountInRoom(roomIndex) == 0;
    }

    // === 以下现有方法完全不变 ===
    // Get, TryGet, GetAll, SetCurrentHp, SetState, SetMoveDelta,
    // SetPosition, SetFacingDir, SetKnockbackVelocity, SetHitChecked, SetStateTimer
}
```

**关键设计点：**

1. `Register` 时 `EnemyRuntimeData.IndexRoom` 必须已被赋值（由 `EnemyManagerSystem` 在调用前设置）
2. `Unregister` 在 `_enemies.Remove` **之前**读取 `data.IndexRoom`，否则 Remove 后数据丢失
3. 房间计数归零 → 发 `RoomEnemiesClearedEvent` → `DoorSystem` 监听到后开门
4. 全局计数归零 → 发 `AllEnemiesDeadEvent` → 原有逻辑不变（`GameplayPanel` 生成 ExitPoint）
5. `IsRoomCleared(roomIndex)` 初始即为 true（无 key 时 `TryGetValue` 返回 0），所以起始房间（Room 0）永不会被关门

---

### 4.3 DoorModel 补完

#### 现有代码（`Model/DoorModel.cs`）

```csharp
public struct DoorData
{
    public int DoorId;
    public Vector2Int Position;
    public bool IsOpen;
    //public       ← 未完成的注释
}

public interface IDoorModel : IModel
{
    List<DoorData> Doors { get; }
    void RegisterDoor(DoorData door);
    void SetDoorOpen(int doorId, bool isOpen);
    void AllDoorsInRoomOpen(int roomId);
    void ClearAll();
}
// 缺少实现类！
```

**问题分析：**
- `DoorData` 缺少 `RoomIndex`，但 `AllDoorsInRoomOpen(int roomId)` 又需要按房间操作，两者矛盾
- 缺少 `DoorModel` 实现类（`AbstractModel` 子类）
- `AllDoorsInRoomOpen` 语义不够通用（只能全部打开，不能全部关闭）

#### 改动后

```csharp
using System.Collections.Generic;
using UnityEngine;

// 门数据结构
public struct DoorData
{
    public int DoorId;            // 门唯一 ID
    public int RoomIndex;         // 属于哪个房间
    public Vector2Int Position;   // 门的世界坐标（格子）
    public bool IsOpen;           // 当前是否打开
}

public interface IDoorModel : IModel
{
    List<DoorData> Doors { get; }

    void RegisterDoor(DoorData door);
    void RemoveDoor(int doorId);

    // 操作单个门
    void SetDoorOpen(int doorId, bool isOpen);
    bool IsDoorOpen(int doorId);

    // 操作整个房间的门
    void SetDoorsInRoomOpen(int roomIndex, bool isOpen);
    bool AreAllDoorsInRoomOpen(int roomIndex);

    // 查询
    List<DoorData> GetDoorsByRoom(int roomIndex);

    // 全量清理（切楼层/回主菜单时调用）
    void ClearAll();
}

public class DoorModel : AbstractModel, IDoorModel
{
    private List<DoorData> _doors = new();
    private int _nextDoorId = 1;

    public List<DoorData> Doors => _doors;

    protected override void OnInit() { }

    public void RegisterDoor(DoorData door)
    {
        door.DoorId = _nextDoorId++;
        _doors.Add(door);
    }

    public void RemoveDoor(int doorId)
    {
        _doors.RemoveAll(d => d.DoorId == doorId);
    }

    public void SetDoorOpen(int doorId, bool isOpen)
    {
        for (int i = 0; i < _doors.Count; i++)
        {
            if (_doors[i].DoorId == doorId)
            {
                var d = _doors[i];
                d.IsOpen = isOpen;
                _doors[i] = d;

                // 发送事件通知 View 更新表现（动画/collider）
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

    public bool IsDoorOpen(int doorId)
    {
        foreach (var d in _doors)
            if (d.DoorId == doorId) return d.IsOpen;
        return true; // 不存在的门视为打开（安全默认）
    }

    public void SetDoorsInRoomOpen(int roomIndex, bool isOpen)
    {
        for (int i = 0; i < _doors.Count; i++)
        {
            if (_doors[i].RoomIndex == roomIndex)
            {
                var d = _doors[i];
                d.IsOpen = isOpen;
                _doors[i] = d;

                this.SendEvent(new DoorStateChangedEvent
                {
                    DoorId = d.DoorId,
                    RoomIndex = roomIndex,
                    IsOpen = isOpen
                });
            }
        }
    }

    public bool AreAllDoorsInRoomOpen(int roomIndex)
    {
        foreach (var d in _doors)
            if (d.RoomIndex == roomIndex && !d.IsOpen)
                return false;
        return true;
    }

    public List<DoorData> GetDoorsByRoom(int roomIndex)
    {
        var result = new List<DoorData>();
        foreach (var d in _doors)
            if (d.RoomIndex == roomIndex)
                result.Add(d);
        return result;
    }

    public void ClearAll()
    {
        _doors.Clear();
        _nextDoorId = 1;
    }
}
```

**关键设计点：**

1. `DoorData` 增加 `RoomIndex`，与 `SetDoorsInRoomOpen(roomIndex)` 语义一致
2. 新增 `DoorStateChangedEvent` — Model 通过事件通知 View（`DoorView`）更新 collider/动画
3. `SetDoorsInRoomOpen` 比旧接口 `AllDoorsInRoomOpen` 更通用（既能全开也能全关）
4. `ClearAll` 在切楼层和返回主菜单时调用（由 `DoorSystem` 触发）
5. 门状态变更通过 Event 向上通知 View 层，符合"Model → Event → View"的数据流向

#### 新增事件

```csharp
// Event/DoorStateChangedEvent.cs (新文件)
public class DoorStateChangedEvent
{
    public int DoorId;
    public int RoomIndex;
    public bool IsOpen;
}
```

---

### 4.4 SaveData 改动（存档）

```csharp
// Model/SaveData.cs

[Serializable]
public class SaveData
{
    // === 现有字段保持不变 ===
    public string _detalTime;
    public int _displayHp;
    public int _displayMaxHp;
    public string _floorName;
    public int _currentHealth;
    public int _maxHealth;
    public int _attackPower;
    public float _attackRange;
    public int _defensePower;
    public float _playerPosX;
    public float _playerPosY;
    public float _moveSpeed;
    public int _mapWidth;
    public int _mapHeight;
    public int[] _tileGrid;
    public List<RoomData> _room;
    public List<EnemySaveData> _enemyData;
    public List<string> _packageData;

    // === 新增：门状态 ===
    public List<DoorSaveData> _doorData;
}

[Serializable]
public class DoorSaveData
{
    public int _roomIndex;    // 所属房间
    public bool _isOpen;      // 门是否打开
    // 注：Position 不需要存，读档时从地图重新计算
}
```

存/读档的改动在 `SaveGameCommand` / `LoadGameCommand` 中，见第 6 节（非 Model 层，此处不展开）。

---

### 4.5 Model 层注册

`RogueLikeGame.cs` 中新增一行：

```csharp
this.RegisterModel<IDoorModel>(new DoorModel());
```

不需要注册新的 `IEnemyModel`（已存在），也不需要新建 `IRoomEnemyModel`。

---

### 4.6 Model 层数据流总结

```
EnemyManagerSystem                DoorSystem                     RoomDetector (View)
(生成敌人时赋值 IndexRoom)         (监听事件，调 DoorModel 开关门)    (检测玩家房间变化)
       │                                  │                           │
       ▼                                  │                    SendCommand(
EnemyModel.Register()                      │                      PlayerEnteredRoomCommand)
  _alivePerRoom[roomIdx]++                 │                           │
       │                                   │                           ▼
       ▼                                   │                    Command.OnExcute()
EnemyModel.Unregister()                    │                      SendEvent(
  _alivePerRoom[roomIdx]--                 │                        PlayerEnteredRoomEvent)
  if (count==0) → SendEvent(               │                           │
    RoomEnemiesClearedEvent) ──────────→ DoorSystem                    │
  if (total==0) → SendEvent(         OnRoomCleared() ←────────────────┘
    AllEnemiesDeadEvent)                   │
       │                                   ▼
       │                            DoorModel.SetDoorsInRoomOpen()
       │                              _doors[i].IsOpen = true/false
       │                                   │
       │                            SendEvent(DoorStateChangedEvent)
       │                                   │
       │                                   ▼
       │                            DoorView.OnDoorStateChanged()
       │                              Collider2D.enabled = !isOpen
       │
       └──→ GameplayPanel 监听 AllEnemiesDeadEvent → 生成 ExitPoint（不变）
```

---

## 5. 事件层与 Command 层

### 5.1 事件

| 事件 | 发送者（层） | 监听者（层） | 用途 |
|------|------------|------------|------|
| `RoomEnemiesClearedEvent` | `EnemyModel` (Model) | `DoorSystem` (System) | 房间清空 → 开门 |
| `DoorStateChangedEvent` | `DoorModel` (Model) | `DoorView` (ViewController) | 门状态变更 → 更新表现 |
| `AllEnemiesDeadEvent` | `EnemyModel` (Model) | `GameplayPanel` (ViewController), `DoorSystem` (System) | 全局清空 → 生成 ExitPoint + 全部门打开（兜底） |
| `FloorAdvancedEvent` | 现有逻辑 | `DoorSystem` (System) | 楼层推进 → 清理重建门 |

### 5.2 Command

| Command | 发送者（层） | 内部行为 | 用途 |
|---------|------------|---------|------|
| `PlayerEnteredRoomCommand` | `RoomDetector` (ViewController) | 发送 `PlayerEnteredRoomEvent` | 表现层不能发 Event，通过 Command 桥接 |

```
RoomDetector (ViewController)
  → SendCommand(PlayerEnteredRoomCommand)
    → Command.OnExcute() → SendEvent(PlayerEnteredRoomEvent)
      → DoorSystem.OnPlayerEnteredRoom() → 关门
```

---

## 6. 系统层 (DoorSystem)

```csharp
// System/DoorSystem.cs (新文件)
public interface IDoorSystem : ISystem { }

public class DoorSystem : AbstractSystem, IDoorSystem
{
    private IDoorModel _doorModel;
    private IEnemyModel _enemyModel;
    private IMapModel _mapModel;

    protected override void OnInit()
    {
        _doorModel = this.GetModel<IDoorModel>();
        _enemyModel = this.GetModel<IEnemyModel>();
        _mapModel = this.GetModel<IMapModel>();

        // 监听事件
        this.RegisterEvent<PlayerEnteredRoomEvent>(OnPlayerEnteredRoom);
        this.RegisterEvent<RoomEnemiesClearedEvent>(OnRoomCleared);
        this.RegisterEvent<AllEnemiesDeadEvent>(OnAllEnemiesDead);
        this.RegisterEvent<FloorAdvancedEvent>(OnFloorAdvanced);
        this.RegisterEvent<MapGeneratedEvent>(OnMapGenerated);
    }

    void OnPlayerEnteredRoom(PlayerEnteredRoomEvent e)
    {
        // 起始房间不管，已清空的房间不管
        if (e.RoomIndex == 0) return;
        if (_enemyModel.IsRoomCleared(e.RoomIndex)) return;

        _doorModel.SetDoorsInRoomOpen(e.RoomIndex, false);
    }

    void OnRoomCleared(RoomEnemiesClearedEvent e)
    {
        _doorModel.SetDoorsInRoomOpen(e.RoomIndex, true);
    }

    void OnAllEnemiesDead(AllEnemiesDeadEvent e)
    {
        // 兜底：所有门打开
        foreach (var door in _doorModel.Doors)
        {
            _doorModel.SetDoorOpen(door.DoorId, true);
        }
    }

    void OnFloorAdvanced(FloorAdvancedEvent e)
    {
        _doorModel.ClearAll();
        var spwan = this.GetUtility<ISpawnUtility>();
        spwan.CleanupDoors();
        // 注：MapGeneratedEvent 随后由 MapGeneratorSystem 发送，
        // OnMapGenerated 会重新生成门的 GameObject
    }

    void OnMapGenerated(MapGeneratedEvent e)
    {
        // 清理旧门 GameObject
        this.GetUtility<ISpawnUtility>().CleanupDoors();

        // 根据 DoorModel 数据生成门
        foreach (var door in _doorModel.Doors)
        {
            this.GetUtility<ISpawnUtility>().SpawnDoor(
                new Vector2(door.Position.x, door.Position.y),
                door.RoomIndex
            );
        }
    }
}
```

---

## 7. 视图层

### 7.1 RoomDetector

```csharp
// ViewController/RoomDetector.cs (新文件)
public class RoomDetector : MonoBehaviour, IController
{
    private int _currentRoom = -1;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Update()
    {
        var map = this.GetModel<IMapModel>();
        if (map.Rooms == null || map.Rooms.Count == 0) return;

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
            // 表现层不能发 Event，通过 Command 桥接
            this.SendCommand(new PlayerEnteredRoomCommand { RoomIndex = newRoom });
        }
        // newRoom == -1 时在走廊中，维持上一个房间，不发任何事件
    }
}
```

### 7.2 PlayerEnteredRoomCommand

```csharp
// Command/PlayerEnteredRoomCommand.cs (新文件)
public class PlayerEnteredRoomCommand : AbstractCommand
{
    public int RoomIndex;

    protected override void OnExcute()
    {
        // Command 可以发送 Event（Rule.txt: Command 可以发送 Event）
        this.SendEvent(new PlayerEnteredRoomEvent { RoomIndex = RoomIndex });
    }
}
```

> ViewController 不能直接发 Event，必须通过 Command 桥接。Command 是表现层修改系统层状态的唯一通道（符合 Rule.txt 规则）。

### 7.3 DoorView

```csharp
// ViewController/DoorView.cs (新文件)
public class DoorView : MonoBehaviour, IController
{
    [SerializeField] private int _doorId;
    [SerializeField] private int _roomIndex;

    private Collider2D _col;
    private SpriteRenderer _sprite;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        _sprite = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        // 注册到 Model
        var model = this.GetModel<IDoorModel>();
        model.RegisterDoor(new DoorData
        {
            RoomIndex = _roomIndex,
            Position = Vector2Int.RoundToInt(transform.position),
            IsOpen = true  // 初始默认打开
        });
        // 记录 DoorId 用于后续事件匹配
        _doorId = model.Doors[model.Doors.Count - 1].DoorId;

        // 监听门状态变化
        this.RegisterEvent<DoorStateChangedEvent>(OnDoorStateChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void OnDoorStateChanged(DoorStateChangedEvent e)
    {
        if (e.DoorId != _doorId) return;

        _col.enabled = !e.IsOpen;        // 关门=启用碰撞
        _sprite.enabled = !e.IsOpen;      // 开门=隐藏（或换成打开状态动画）
    }

    void OnDestroy()
    {
        var model = this.GetModel<IDoorModel>();
        model?.RemoveDoor(_doorId);
    }
}
```

---

## 8. 门位置计算（MapGeneratorSystem 新增）

在 `GanateMap` 末尾，`SetMap` 之后调用：

```csharp
// GanateMap 方法末尾，SetMap 之后新增：

// 计算门位置（纯数据），存入 DoorModel
var placements = CalculateDoorPositions(_rooms);
var doorModel = this.GetModel<IDoorModel>();
doorModel.ClearAll();
foreach (var p in placements)
{
    doorModel.RegisterDoor(new DoorData
    {
        RoomIndex = p.RoomIndex,
        Position = p.TilePosition,
        IsOpen = true   // 初始默认打开
    });
}
```

**为什么分两步？** `GanateMap` 运行在 `MapGeneratorSystem`（System 层），只负责数据。门 GameObject 的实际生成由 `DoorSystem` 在收到 `MapGeneratedEvent` 后通过 `ISpawnUtility` 完成：

```csharp
// DoorSystem 中新增监听：
void OnMapGenerated(MapGeneratedEvent e)
{
    // 清理旧门 GameObject
    this.GetUtility<ISpawnUtility>().CleanupDoors();

    // 根据 DoorModel 中的数据重新生成门
    var doorModel = this.GetModel<IDoorModel>();
    foreach (var door in doorModel.Doors)
    {
        this.GetUtility<ISpawnUtility>().SpawnDoor(
            new Vector2(door.Position.x, door.Position.y),
            door.RoomIndex
        );
    }
}
```

这样数据流是：

```
GanateMap → DoorModel (数据) → MapGeneratedEvent
  → DoorSystem → ISpawnUtility.SpawnDoor() → DoorView (GameObject)
```

**注意**：`MapGeneratedEvent` 在 `OnPanelChange` 和 `OnFloorAdvanced` 中 `GanateMap` 之后已发送（现有代码 `MapGeneratorSystem.cs:39` 和 `:196`），无需新增。

`CalculateDoorPositions` 方法已存在于 `MapGeneratorSystem.cs:200-226`，无需改动。

---

## 9. 边界情况处理

| 边界情况 | 处理方式 |
|----------|---------|
| **起始房间（Room 0）** | `_alivePerRoom` 中无记录，`GetAliveCount(0)` 返回 0，`IsRoomCleared(0)` 永为 true；`DoorSystem.OnPlayerEnteredRoom` 显式跳过 `RoomIndex == 0` |
| **玩家在走廊中** | `RoomDetector` 找不到房间返回 -1，维持 `_currentRoom` 不变，不发送事件 |
| **房间无敌人（如宝藏房）** | `IsRoomCleared` 返回 true，`DoorSystem` 不关门 |
| **玩家重访已清空房间** | `IsRoomCleared` = true，不关门 |
| **楼层推进** | `FloorAdvancedEvent` → `DoorSystem` 调 `DoorModel.ClearAll()` + `ISpawnUtility.CleanupDoors()` |
| **返回主菜单** | `EnemyManagerSystem.OnPanelChange` 清理敌人 → 触发 Unregister → 可能不触发（切换到Start时敌人已被清理），需要 `ISpawnUtility` 同时清理门对象 |
| **存档/读档** | `SaveGameCommand` 从 `DoorModel.Doors` 读取状态写入 `SaveData._doorData`；`LoadGameCommand` 读回并恢复 |
| **全局敌人清空** | `DoorSystem` 额外监听 `AllEnemiesDeadEvent`，确保全部门打开（兜底） |

---

## 10. ISpawnUtility 改动

```csharp
public interface ISpawnUtility : IUtility
{
    GameObject SpawnPlayer(Vector2 pos);
    EnemySpawnData SpwanEnemy(Vector2 atPosition);
    GameObject SpawnPickup(ItemConfig config, Vector2 atPosition);
    void CleanupAll();

    // 新增
    DoorData SpawnDoor(Vector2 atPosition, int roomIndex);
    void CleanupDoors();
}

// 实现类 SpawnUtility 新增方法：
public DoorData SpawnDoor(Vector2 atPosition, int roomIndex)
{
    var prefab = Resources.Load<GameObject>("Perfabs/Door");
    var go = GameObject.Instantiate(prefab, atPosition, Quaternion.identity);
    var view = go.GetComponent<DoorView>();
    return new DoorData
    {
        RoomIndex = roomIndex,
        Position = Vector2Int.RoundToInt(atPosition),
        IsOpen = true
    };
}

public void CleanupDoors()
{
    var doors = GameObject.FindGameObjectsWithTag("Door");
    foreach (var d in doors) GameObject.Destroy(d);
}
```

注意：`CleanupAll` 中也需要加 `CleanupDoors()` 调用（或通过 tag 统一清理）。

---

## 11. 文件变更清单

| 操作 | 文件 | 说明 |
|------|------|------|
| **新建** | `Event/RoomEnemiesClearedEvent.cs` | 房间清空事件 |
| **新建** | `Event/PlayerEnteredRoomEvent.cs` | 玩家进入房间事件 |
| **新建** | `Event/DoorStateChangedEvent.cs` | 门状态变更事件 |
| **新建** | `Command/PlayerEnteredRoomCommand.cs` | 玩家进入房间 Command（桥接表现层→事件） |
| **新建** | `System/DoorSystem.cs` | 门开关逻辑 |
| **新建** | `ViewController/DoorView.cs` | 门 MonoBehaviour |
| **新建** | `ViewController/RoomDetector.cs` | 玩家房间检测 |
| **新建** | `Resources/Perfabs/Door.prefab` | 门预制体（需 Unity 制作） |
| **修改** | `Model/DoorModel.cs` | ~~新建~~ → 补完：`DoorData` 加 `RoomIndex`，写 `DoorModel` 实现类，接口加 `SetDoorsInRoomOpen` 等方法 |
| **修改** | `Model/EnemyModel.cs` | `Register`/`Unregister` 增加 `_alivePerRoom` 计数；接口加 `GetAliveCountInRoom`/`IsRoomCleared` |
| **修改** | `Model/SaveData.cs` | 新增 `DoorSaveData` 结构体 + `_doorData` 字段 |
| **修改** | `System/EnemyManagerSystem.cs` | 生成敌人时给 `EnemyRuntimeData.IndexRoom` 赋值 |
| **修改** | `System/MapGeneratorSystem.cs` | 生成地图后计算门位置 → 调 `SpawnUtility.SpawnDoor` |
| **修改** | `Command/SaveGameCommand.cs` | 序列化 `DoorModel.Doors` |
| **修改** | `Command/LoadGameCommand.cs` | 反序列化恢复门状态 |
| **修改** | `Utility/ISpawnUtility.cs` | 新增 `SpawnDoor` / `CleanupDoors` 方法 |
| **修改** | `RogueLikeGame.cs` | 注册 `IDoorModel` / `IDoorSystem` |

---

## 12. 实现顺序

1. **Event**：`RoomEnemiesClearedEvent`、`PlayerEnteredRoomEvent`、`DoorStateChangedEvent`
2. **Model**：补完 `DoorModel.cs`（实现类 + 接口完善）、改 `EnemyModel`（房间计数）、改 `SaveData`（门存档）
3. **注册**：`RogueLikeGame.cs` 注册 `IDoorModel` + `IDoorSystem`
4. **Utility**：`ISpawnUtility` 加 `SpawnDoor` / `CleanupDoors`
5. **EnemyManagerSystem**：赋值 `IndexRoom` 到 `EnemyRuntimeData`
6. **DoorSystem**：核心逻辑（监听事件 → 开关门）
7. **DoorView + Door.prefab**：门 GameObject
8. **RoomDetector + Command**：挂 Player 上，发送 `PlayerEnteredRoomCommand`（Command 内部发送 Event）
9. **MapGeneratorSystem**：计算门位置 + 生成 Door 实例
10. **存档**：`SaveGameCommand` / `LoadGameCommand` 增加门状态读写

---

## 13. 架构合规说明

- **Model 层**：`DoorData` 为纯数据结构，`DoorModel`/`EnemyModel` 不依赖 Unity 类型。Model 通过 `SendEvent` 向上通知（符合"数据层可以发送 Event"规则）
- **System 层**：`DoorSystem` 监听 Event，操作 Model，不直接操作 GameObject（符合"系统层可以获取 Model，监听/发送 Event"规则）
- **ViewController 层**：`DoorView`/`RoomDetector` 操作 GameObject。`RoomDetector` 通过 Command 桥接事件（表现层规则：不能直接发 Event，只能发 Command）。`DoorView` 监听 Model 发出的 Event 更新表现
- **Command 层**：`PlayerEnteredRoomCommand` — 表现层→事件层的唯一通道。Command 无状态，只做转发
- **新增 1 个 Command**：`PlayerEnteredRoomCommand`（表现层桥接必须，Rule.txt 规定 IController 更改状态必须用 Command）
- **门生成**通过 `ISpawnUtility`，与现有的 Player/Enemy/ExitPoint 生成模式完全一致
