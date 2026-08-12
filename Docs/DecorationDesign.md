# 地图装饰物系统 — 设计文档

## 1. 目标

在地图的地板格子上随机生成装饰物（草、蘑菇、石头、小花等），随地图生成同步创建，纯视觉表现，无交互逻辑。

---

## 2. 与现有系统的复用

| 机制 | 位置 | 复用方式 |
|------|------|---------|
| 地图数据 | `IMapModel.TileGrid`（0=空, 1=地板, 2=墙） | 只在地板格上放置 |
| 房间数据 | `IMapModel.Rooms` | 每个房间独立随机装饰 |
| 门数据 | `IDoorModel.Doors` | 避开门的坐标 |
| 生成工具 | `ISpawnUtility` | 新增 `SpawnDecoration` 方法 |
| 地图生成完成事件 | `MapGeneratedEvent` | 监听此事件触发装饰生成 |
| 清理 | `ISpawnUtility.CleanupAll` | 切楼层/回主菜单时销毁 |

---

## 3. 架构

```
MapGeneratorSystem.GanateMap
  → SetMap(_grid, _rooms)
  → SendEvent(MapGeneratedEvent)
       ├──→ MapBuilder.OnMapGenerated (渲染 tilemap，现有)
       ├──→ DoorSystem.OnMapGenerated (生成门，新增)
       └──→ DecorationSystem.OnMapGenerated (生成装饰物，新增)
              → ISpawnUtility.SpawnDecoration(prefabName, position)
```

`DecorationSystem` 是 System 层，注册在 `RogueLikeGame.cs`，不需要场景挂载。生命周期完全由架构管理。

---

## 4. DecorationConfig（ScriptableObject）

```csharp
// Model/DecorationConfig.cs (新文件)
[CreateAssetMenu(menuName = "RogueLike/Decoration Config")]
public class DecorationConfig : ScriptableObject
{
    public string decorationName;
    public GameObject prefab;
    [Range(0f, 1f)] public float weight = 1f;  // 生成权重，越大越常见
}
```

在 Unity 中为每种装饰物创建 `.asset` 文件，放到 `Resources/Config/Decorations/`。

---

## 5. ISpawnUtility 新增

```csharp
// Utility/ISpawnUtility.cs 接口新增
public interface ISpawnUtility : IUtility
{
    // ... 现有方法不变 ...

    // 新增
    void SpawnDecoration(GameObject prefab, Vector2 atPosition);
    void CleanupDecorations();
}

// SpawnUtility 实现类新增
public void SpawnDecoration(GameObject prefab, Vector2 atPosition)
{
    var go = GameObject.Instantiate(prefab, atPosition, Quaternion.identity);
    go.tag = "Decoration";
}

public void CleanupDecorations()
{
    var objs = GameObject.FindGameObjectsWithTag("Decoration");
    foreach (var obj in objs) GameObject.Destroy(obj);
}
```

同时在 `CleanupAll` 末尾加一行：

```csharp
public void CleanupAll()
{
    foreach (var obj in GameObject.FindGameObjectsWithTag("Enemy")) GameObject.Destroy(obj);
    var player = GameObject.FindGameObjectWithTag("Player");
    if (player != null) GameObject.Destroy(player);
    var exits = GameObject.FindGameObjectsWithTag("ExitPoint");
    foreach (var exit in exits) GameObject.Destroy(exit);

    // 新增
    CleanupDecorations();
}
```

---

## 6. DecorationSystem

```csharp
// System/DecorationSystem.cs (新文件)
public interface IDecorationSystem : ISystem { }

public class DecorationSystem : AbstractSystem, IDecorationSystem
{
    protected override void OnInit()
    {
        this.RegisterEvent<MapGeneratedEvent>(OnMapGenerated);
    }

    void OnMapGenerated(MapGeneratedEvent e)
    {
        SpawnDecorations();
    }

    void SpawnDecorations()
    {
        var map = this.GetModel<IMapModel>();
        var doorModel = this.GetModel<IDoorModel>();
        var spwan = this.GetUtility<ISpawnUtility>();

        // 加载所有装饰物配置
        var configs = Resources.LoadAll<DecorationConfig>("Config/Decorations");
        if (configs.Length == 0) return;

        // 累积权重
        float totalWeight = 0f;
        foreach (var c in configs) totalWeight += c.weight;

        // 收集已占用的格（门 + 已放装饰物）
        var occupied = new HashSet<Vector2Int>();
        if (doorModel != null)
        {
            foreach (var d in doorModel.Doors)
                occupied.Add(d.Position);
        }

        foreach (var room in map.Rooms)
        {
            int area = room.Width * room.Height;
            int count = Mathf.FloorToInt(area * Random.Range(0.15f, 0.25f));

            for (int n = 0; n < count; n++)
            {
                if (!TryPickTile(map, room, occupied, out int gx, out int gy))
                    continue;

                var config = PickWeighted(configs, totalWeight);
                if (config == null) continue;

                var pos = new Vector2(gx + 0.5f, gy + 0.5f);
                spwan.SpawnDecoration(config.prefab, pos);

                occupied.Add(new Vector2Int(gx, gy));
            }
        }
    }

    // 在房间内随机选一个可放置的地板格
    bool TryPickTile(IMapModel map, RoomData room,
                     HashSet<Vector2Int> blocked,
                     out int gx, out int gy)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            gx = Random.Range(room.X, room.X + room.Width);
            gy = Random.Range(room.Y, room.Y + room.Height);

            if (gx < 0 || gx >= map.Width || gy < 0 || gy >= map.Height)
                continue;

            // 必须是地板
            if (map.TileGrid[gx, gy] != 1) continue;

            // 避开已占用格
            if (blocked.Contains(new Vector2Int(gx, gy))) continue;

            // 避开房间中心 2x2（敌人/玩家出生点）
            int cx = (int)room.Center.x;
            int cy = (int)room.Center.y;
            if (gx >= cx - 1 && gx <= cx && gy >= cy - 1 && gy <= cy)
                continue;

            return true;
        }

        gx = gy = 0;
        return false;
    }

    DecorationConfig PickWeighted(DecorationConfig[] configs, float totalWeight)
    {
        float roll = Random.Range(0f, totalWeight);
        float acc = 0f;
        foreach (var c in configs)
        {
            acc += c.weight;
            if (roll <= acc) return c;
        }
        return configs[configs.Length - 1];
    }
}
```

---

## 7. 注册

```csharp
// RogueLikeGame.cs Init() 中新增一行：
this.RegisterSystem<IDecorationSystem>(new DecorationSystem());
```

放在 `IDoorSystem` 之后注册即可，没有顺序依赖。

---

## 8. 文件变更清单

| 操作 | 文件 | 说明 |
|------|------|------|
| **新建** | `Model/DecorationConfig.cs` | ScriptableObject 配置（prefab + 权重） |
| **新建** | `System/DecorationSystem.cs` | 监听 MapGeneratedEvent，生成装饰物 |
| **新建** | `Resources/Config/Decorations/*.asset` | 每种装饰物的 DecorationConfig 配置资产 |
| **新建** | `Resources/Perfabs/Decorations/*.prefab` | 装饰物 prefab（草、蘑菇等） |
| **修改** | `Utility/ISpawnUtility.cs` | 新增 `SpawnDecoration` / `CleanupDecorations`；`CleanupAll` 末尾加调用 |
| **修改** | `RogueLikeGame.cs` | 注册 `IDecorationSystem` |

**不需要的场景操作：** 不挂任何 GameObject，不修改 prefab，不修改 `SampleScene.unity`。

---

## 9. 放置规则总结

| 规则 | 原因 |
|------|------|
| `TileGrid == 1`（地板格） | 墙和空地上不放 |
| 避开 `DoorData.Position` | 不挡门（视觉上） |
| 避开房间中心 2x2 | 不盖住敌人/玩家出生点 |
| 同一格不重复放 | `occupied` HashSet 去重 |
| 房间面积 × 15%~25% | 不铺满，留白才自然 |
| 走廊不放置 | 房间内才随机（`room.X~room.X+Width` 范围） |
