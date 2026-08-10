# 楼层递进系统 设计文档

## 目标

敌全灭后生成出口，玩家触碰出口进入下一层。每层重新生成地图和敌人，敌人随层数变强。不新增面板类型，整个流程在 `GamePlay` 内部闭环。

---

## 一、架构规则速查

| 层 | GetModel | GetSystem | GetUtility | SendEvent | RegisterEvent | SendCommand |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| Model | ✗ | ✗ | ✓ | ✓ | ✗ | ✗ |
| System | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ |
| Command | ✓ | ✓ | ✓ | ✓ | ✗ | ✗ |
| Controller | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ |

核心约束：
- **Model 不能拿 Model** → `GameStateModel` 无法直接调 `IMapModel.Clearup()`，所以楼层清理逻辑放 Command 或 System
- **Command 能拿一切 + 发事件** → 清理 + 通知 System 重建，放在 `AdvanceFloorCommand`
- **System 能注册事件** → `MapGeneratorSystem`、`EnemyManagerSystem` 各自注册 `FloorAdvancedEvent`

---

## 二、现有可复用逻辑

`EnemyManagerSystem.OnPanelChange` 已有两套生成逻辑：

```
进入 GamePlay:
  ├── 新游戏（Model 里没敌人）
  │     生成玩家到 rooms[0]
  │     生成敌人到 rooms[1..n]（默认属性）
  │
  └── 读档（Model 里已有敌人数据）
        生成玩家到 _playerPos（从存档位置）
        生成敌人到存档位置（恢复属性）
```

楼层递进的生成逻辑 = **新游戏那套 + 敌人属性按楼层缩放**。只需要把房间生成部分提取复用，不写重复代码。

`MapGeneratorSystem.OnPanelChange` 已有地图生成 + 发 `MapGeneratedEvent` 的逻辑，楼层递进完全复用。

---

## 三、新增内容

### 3.1 `FloorAdvancedEvent`（新文件）

**路径**：`Assets/Scripts/Event/FloorAdvancedEvent.cs`

```csharp
public class FloorAdvancedEvent
{
    public int NewFloor;
}
```

纯数据事件，和 `MapGeneratedEvent`、`UIPanelChangeEvent` 同模式。

### 3.2 `AdvanceFloorCommand`（新文件）

**路径**：`Assets/Scripts/Command/AdvanceFloorCommand.cs`

```csharp
public class AdvanceFloorCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var state = this.GetModel<IGameStateModel>();

        // 最终层 → 通关，不递进
        if (state.CurrentFloor >= state.MaxFloor)
        {
            state.GameOver(true);
            return;
        }

        // 1. 清理旧数据（Command 能 GetModel + GetUtility）
        this.GetUtility<ISpawnUtility>().CleanupAll();
        var enemyModel = this.GetModel<IEnemyModel>();
        enemyModel.GetAll().Keys.ToList().ForEach(id => enemyModel.Unregister(id));
        this.GetModel<IMapModel>().Clearup();

        // 2. 楼层 +1
        state.CurrentFloor++;

        // 3. 发事件通知各 System 重建（Command 能 SendEvent）
        this.SendEvent(new FloorAdvancedEvent { NewFloor = state.CurrentFloor });
    }
}
```

架构合规性：
- `CleanupAll()` — Command → Utility ✓
- `GetModel<IMapModel>().Clearup()` — Command → Model ✓
- `SendEvent()` — Command → SendEvent ✓

### 3.3 `ExitPoint`（新文件 + 新 Prefab）

**路径**：`Assets/Scripts/ViewController/ExitPoint.cs`

```csharp
public class ExitPoint : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            this.SendCommand(new AdvanceFloorCommand());
        }
    }
}
```

**Prefab**：`Assets/Resources/Perfabs/ExitPoint.prefab`
- GameObject 挂 `ExitPoint.cs` + `Collider2D`（IsTrigger = true）
- 可用一个楼梯/箭头 Sprite 做视觉

架构合规性：
- `SendCommand()` — Controller → SendCommand ✓

---

## 四、现有文件改动

### 4.1 `UIPanelType.cs` — GameStateModel 加楼层属性

**文件**：`Assets/Scripts/Model/UI/UIPanelType.cs`

`IGameStateModel` 新增：

```csharp
int CurrentFloor { get; set; }
int MaxFloor { get; }
```

`GameStateModel` 实现：

```csharp
public int CurrentFloor { get; set; } = 1;
public int MaxFloor { get; private set; } = 5;

// StartGame 中重置
public void StartGame()
{
    CurrentFloor = 1;
    _currentPhase.Value = UIPanelType.GamePlay;
}
```

Model 不能拿外部东西，但存自己的 int 属性没问题。

### 4.2 `EnemyManagerSystem` — 提取公共方法 + 注册楼层事件

**文件**：`Assets/Scripts/System/EnemyManagerSystem.cs`

**改动 1**：`OnInit` 中多注册一个事件：

```csharp
protected override void OnInit()
{
    var player = GameObject.FindWithTag("Player");
    if (player != null) _playerTransform = player.transform;
    this.RegisterEvent<UIPanelChangeEvent>(OnPanelChange);
    this.RegisterEvent<FloorAdvancedEvent>(OnFloorAdvanced);   // ← 新增
}
```

**改动 2**：提取公共方法，新游戏和楼层递进共用：

将 `OnPanelChange` 中"从房间生成"的代码抽出来：

```csharp
/// <summary>
/// 从房间列表生成玩家和敌人。新游戏和楼层递进共用。
/// </summary>
void SpawnFromRooms(float enemyScale = 1f)
{
    var spwan = this.GetUtility<ISpawnUtility>();
    var enmeyModel = this.GetModel<IEnemyModel>();
    var map = this.GetModel<IMapModel>();
    var rooms = map.Rooms;

    if (rooms.Count == 0) return;

    // 玩家
    var playerGo = spwan.SpawnPlayer(rooms[0].Center);
    playerGo.GetComponent<PlayerController>().Init();
    _playerTransform = playerGo.transform;

    // 敌人
    for (int i = 1; i < rooms.Count; i++)
    {
        var sd = spwan.SpwanEnemy(rooms[i].Center);
        if (enemyScale != 1f)
        {
            sd.Data.MaxHp        = (int)(sd.Data.MaxHp * enemyScale);
            sd.Data.CurrentHp    = sd.Data.MaxHp;
            sd.Data.AttackPower  = (int)(sd.Data.AttackPower * enemyScale);
            sd.Data.DefensePower = (int)(sd.Data.DefensePower * enemyScale);
        }
        var id = enmeyModel.Register(sd.Data);
        sd.GO.GetComponent<EnemyView>().Init(id, sd.Data);
    }
}
```

`OnPanelChange` 的新游戏分支改为调用 `SpawnFromRooms(1f)`（无缩放）。

**改动 3**：新增 `OnFloorAdvanced` 处理器：

```csharp
void OnFloorAdvanced(FloorAdvancedEvent e)
{
    var state = this.GetModel<IGameStateModel>();
    float scale = 1f + (state.CurrentFloor - 1) * 0.3f;   // 每层 +30%
    SpawnFromRooms(scale);
}
```

复用效果：
```
OnPanelChange(GamePlay, new game)  → SpawnFromRooms(1f)
OnFloorAdvanced                    → SpawnFromRooms(1.3f / 1.6f / ...)
OnPanelChange(GamePlay, load game) → 不受影响（走 else 分支）
```

### 4.3 `MapGeneratorSystem` — 注册楼层事件

**文件**：`Assets/Scripts/System/MapGeneratorSystem.cs`

**改动 1**：`OnInit` 中多注册一个事件：

```csharp
protected override void OnInit()
{
    this.RegisterEvent<UIPanelChangeEvent>(OnPanelChange);
    this.RegisterEvent<FloorAdvancedEvent>(OnFloorAdvanced);   // ← 新增
}
```

**改动 2**：新增 `OnFloorAdvanced` 处理器，复用现有 `GanateMap`：

```csharp
void OnFloorAdvanced(FloorAdvancedEvent e)
{
    var state = this.GetModel<IGameStateModel>();
    // 楼层越高房间越少
    int roomCount = Mathf.Max(3, 9 - state.CurrentFloor);
    GanateMap(60, 40, roomCount);           // ← 复用已有方法
    this.SendEvent(new MapGeneratedEvent()); // ← MapBuilder 重建 Tilemap
}
```

架构合规性：
- System 注册事件 ✓
- System 拿 Model ✓
- System 发事件 ✓

### 4.4 `GameplayPanel` — 敌全灭后分支

**文件**：`Assets/Scripts/ViewController/UIController/GameplayPanel.cs`

```csharp
void OnEnemyDead(EnemyDeadEvent e)
{
    if (this.GetModel<IEnemyModel>().GetAll().Count == 0)
    {
        var state = this.GetModel<IGameStateModel>();
        if (state.CurrentFloor >= state.MaxFloor)
        {
            state.GameOver(true);   // 最终层 → 通关
        }
        else
        {
            // 在最后一个房间生成出口
            var rooms = this.GetModel<IMapModel>().Rooms;
            var exitPrefab = Resources.Load<GameObject>("Perfabs/ExitPoint");
            Instantiate(exitPrefab, rooms[rooms.Count - 1].Center, Quaternion.identity);
        }
    }
}
```

架构合规性：
- Controller 拿 Model ✓
- Controller 拿 Model 调 `GameOver()` ✓（Model 方法只是改自己的值）
- `Instantiate` 是 MonoBehaviour 原生能力，不涉及架构层

---

## 五、改动清单

| 操作 | 文件 | 说明 |
|------|------|------|
| **新增** | `Assets/Scripts/Event/FloorAdvancedEvent.cs` | 纯数据事件，2 行 |
| **新增** | `Assets/Scripts/Command/AdvanceFloorCommand.cs` | 清理 + 递进 + 发事件 |
| **新增** | `Assets/Scripts/ViewController/ExitPoint.cs` | 触发器，触碰发 Command |
| **新增** | `Assets/Resources/Perfabs/ExitPoint.prefab` | 出口预制体 |
| **修改** | `.../Model/UI/UIPanelType.cs` | `IGameStateModel` + `CurrentFloor`/`MaxFloor`，`StartGame` 重置 |
| **修改** | `.../System/EnemyManagerSystem.cs` | 提取 `SpawnFromRooms(scale)`，注册 `FloorAdvancedEvent` |
| **修改** | `.../System/MapGeneratorSystem.cs` | 注册 `FloorAdvancedEvent`，回调复用 `GanateMap` |
| **修改** | `.../UIController/GameplayPanel.cs` | 敌全灭后：最终层→通关 / 非最终层→生成出口 |

---

## 六、信号链路

### 进入下一层

```
敌全灭 → GameplayPanel.OnEnemyDead
  → IsFinalFloor? No
  → Instantiate(ExitPoint, 最后一个房间)

玩家碰 ExitPoint
  → ExitPoint.OnTriggerEnter2D
  → SendCommand<AdvanceFloorCommand>
      ├── IsFinalFloor? No
      ├── CleanupAll() + 清 EnemyModel + Clearup()
      ├── CurrentFloor++
      └── SendEvent(FloorAdvancedEvent { NewFloor })

FloorAdvancedEvent:
  ├── MapGeneratorSystem.OnFloorAdvanced
  │     → GanateMap(60, 40, roomCount)       ← 复用现有方法
  │     → SendEvent(MapGeneratedEvent)        ← MapBuilder 重建
  │
  └── EnemyManagerSystem.OnFloorAdvanced
        → SpawnFromRooms(1 + floor*0.3)      ← 复用提取的方法
             ├── 玩家 → rooms[0]
             └── 敌人 → rooms[1..n]，属性缩放
```

### 最终层通关（不变）

```
敌全灭 → GameplayPanel.OnEnemyDead
  → IsFinalFloor? Yes
  → GameOver(true) → "You Win!"
```

---

## 七、与存档系统的关系

`SaveData` 已有的 `_floorName` 和新增的楼层字段：

| 字段 | 存 | 读 |
|------|----|----|
| `_floorName`（已有） | `SaveGameCommand` 写入 `$"第{CurrentFloor}层"` | 仅展示 |
| `_currentFloor`（需新增到 SaveData） | `SaveGameCommand` 写入 `state.CurrentFloor` | `LoadGameCommand` 恢复 `state.CurrentFloor` |

`SaveData` 加一行：

```csharp
public int _currentFloor;
```

`SaveGameCommand` 加一行：

```csharp
_currentFloor = state.CurrentFloor,
```

`LoadGameCommand` 加一行（在 `MapGeneratedEvent` 之前）：

```csharp
state.CurrentFloor = data._currentFloor;
```

---

## 八、与状态机的关系

楼层递进**不经过面板切换**，保持在 `GamePlay` 内：

```
面板状态机:  Start → GamePlay → ... → GameOver
                           │
             楼层递进在此内部完成（不切面板）
             AdvanceFloorCommand → FloorAdvancedEvent → System 自行重建
```

这和"继续游戏"（Pause → GamePlay 不重新生成）是对称设计——面板不动的场景，内部通过事件驱动。面板要动的场景，通过 `_currentPhase` 驱动。两类机制互不干扰。
