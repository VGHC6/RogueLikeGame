# 存档系统 设计文档

## 目标

支持 3 个存档位。在暂停界面（设置面板）中手动保存，在开始界面中选择存档读取。使用 JSON 文件存储。

---

## 一、整体交互

### 1.1 保存流程

```
GamePlay 中按 Esc → Pause 面板弹出
  ├── [继续游戏] 按钮 → 关闭 Pause，回到战斗
  ├── [保存游戏] 按钮 → 显示 3 个存档位
  │     ├── 空位：点击 → 保存到该位 → 提示"保存成功"
  │     └── 已占用：显示时间/HP，点击 → 确认覆盖 → 保存
  └── [返回主菜单] 按钮 → 回到 Start 面板
```

### 1.2 读档流程

```
Start 面板
  ├── [新游戏] 按钮 → 进入 GamePlay（现有流程）
  └── [读取存档] 按钮 → 显示 3 个存档位
        ├── 空位：灰掉，不可点击
        └── 已占用：显示时间/HP，点击 → LoadGameCommand → 进入 GamePlay
```

---

## 二、数据结构

### 2.1 存档内容：`SaveData.cs`

**路径**：`Assets/Scripts/Model/SaveData.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    // ---- 元信息（用于存档位展示） ----
    public string saveTime;       // 保存时间，如 "2026/08/06 14:30"
    public int displayHp;         // 当前 HP（展示用）
    public int displayMaxHp;      // 最大 HP（展示用）
    public string floorName;      // 当前楼层名（暂可为"第1层"）

    // ---- 玩家战斗属性 ----
    public int currentHp;
    public int maxHp;
    public int attackPower;
    public int defensePower;
    public float attackRange;

    // ---- 玩家实体属性 ----
    public float playerPosX;
    public float playerPosY;
    public float moveSpeed;

    // ---- 地图 ----
    public int mapWidth;
    public int mapHeight;
    public int[] tileGridFlat;
    public List<RoomData> rooms;

    // ---- 敌人 ----
    public List<EnemySaveEntry> enemies;

    // ---- 背包 ----
    public List<string> itemNames;
}

[Serializable]
public struct EnemySaveEntry
{
    public int enemyId;
    public int currentHp;
    public int maxHp;
    public int attackPower;
    public int defensePower;
    public float attackRange;
    public float chaseRange;
    public float moveSpeed;
    public float attackDuration;
    public float hitCheckTime;
    public float hurtDuration;
    public float knockbackForce;
    public float knockbackDecay;
    public int state;
    public float posX;
    public float posY;
    public int facingDir;
    public bool isDead;
}
```

### 2.2 文件存储

```
Application.persistentDataPath/
├── slot_0.json
├── slot_1.json
└── slot_2.json
```

每个文件就是一个完整的 `SaveData` JSON，独立读写。

---

## 三、新增文件

### 3.1 `SaveData.cs` — 数据结构

路径：`Assets/Scripts/Model/SaveData.cs`，代码见上文。

### 3.2 `ISaveUtility.cs` — 多存档位读写

**路径**：`Assets/Scripts/Utility/ISaveUtility.cs`

```csharp
using UnityEngine;

public interface ISaveUtility : IUtility
{
    /// <summary>读取指定槽位的元信息（不加载完整数据），空位返回 null</summary>
    SaveSlotInfo GetSlotInfo(int slotIndex);

    /// <summary>保存到指定槽位</summary>
    void SaveToSlot(int slotIndex, SaveData data);

    /// <summary>从指定槽位加载</summary>
    SaveData LoadFromSlot(int slotIndex);

    /// <summary>删除指定槽位</summary>
    void DeleteSlot(int slotIndex);
}

/// <summary>
/// 存档位的展示信息，只含元数据，不含完整游戏数据。
/// 用于 UI 列表显示。
/// </summary>
public class SaveSlotInfo
{
    public int slotIndex;
    public bool isEmpty;
    public string saveTime;
    public int hp;
    public int maxHp;
    public string floorName;
}

public class SaveUtility : ISaveUtility
{
    private const int MaxSlots = 3;

    private string GetPath(int i) =>
        System.IO.Path.Combine(Application.persistentDataPath, $"slot_{i}.json");

    public SaveSlotInfo GetSlotInfo(int slotIndex)
    {
        var path = GetPath(slotIndex);
        if (!System.IO.File.Exists(path))
            return new SaveSlotInfo { slotIndex = slotIndex, isEmpty = true };

        var json = System.IO.File.ReadAllText(path);
        var data = JsonUtility.FromJson<SaveData>(json);
        return new SaveSlotInfo
        {
            slotIndex = slotIndex,
            isEmpty   = false,
            saveTime  = data.saveTime,
            hp        = data.displayHp,
            maxHp     = data.displayMaxHp,
            floorName = data.floorName
        };
    }

    public void SaveToSlot(int slotIndex, SaveData data)
    {
        var json = JsonUtility.ToJson(data, prettyPrint: true);
        System.IO.File.WriteAllText(GetPath(slotIndex), json);
    }

    public SaveData LoadFromSlot(int slotIndex)
    {
        var path = GetPath(slotIndex);
        if (!System.IO.File.Exists(path)) return null;
        return JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(path));
    }

    public void DeleteSlot(int slotIndex)
    {
        var path = GetPath(slotIndex);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }
}
```

### 3.3 `SaveGameCommand.cs` — 保存到指定槽位

**路径**：`Assets/Scripts/Command/SaveGameCommand.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SaveGameCommand : AbstractCommand
{
    /// <summary>要保存到的槽位（0/1/2），调用前设置</summary>
    public int SlotIndex { get; set; }

    protected override void OnExcute()
    {
        var combat   = this.GetModel<ICombatModel>();
        var entity   = this.GetModel<IEntityModel>();
        var map      = this.GetModel<IMapModel>();
        var enemies  = this.GetModel<IEnemyModel>().GetAll();
        var items    = this.GetModel<IItemModel>().Items;
        var saveUtil = this.GetUtility<ISaveUtility>();

        // 1. 展平地图
        var flat = new int[map.Width * map.Height];
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                flat[y * map.Width + x] = map.TileGrid[x, y];

        // 2. 敌人 → List
        var enemyList = new List<EnemySaveEntry>();
        foreach (var kv in enemies)
        {
            var d = kv.Value;
            enemyList.Add(new EnemySaveEntry
            {
                enemyId = d.EnemyId, currentHp = d.CurrentHp, maxHp = d.MaxHp,
                attackPower = d.AttackPower, defensePower = d.DefensePower,
                attackRange = d.AttackRange, chaseRange = d.ChaseRange,
                moveSpeed = d.MoveSpeed, attackDuration = d.AttackDuration,
                hitCheckTime = d.HitCheckTime, hurtDuration = d.HurtDuration,
                knockbackForce = d.KnockbackForce, knockbackDecay = d.KnockbackDecay,
                state = (int)d.State, posX = d.Position.x, posY = d.Position.y,
                facingDir = d.FacingDir, isDead = d.IsDead
            });
        }

        // 3. 道具 → 名字列表
        var names = items.Select(it => it.itemName).ToList();

        // 4. 组装并写入
        var data = new SaveData
        {
            saveTime    = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
            displayHp   = combat.CurrentHp.Value,
            displayMaxHp = combat.MaxHp.Value,
            floorName   = "第1层",

            currentHp    = combat.CurrentHp.Value,
            maxHp        = combat.MaxHp.Value,
            attackPower  = combat.AttackPower.Value,
            defensePower = combat.DefensePower.Value,
            attackRange  = combat.AttackRange.Value,

            playerPosX = entity.Position.x,
            playerPosY = entity.Position.y,
            moveSpeed  = entity.MoveSpeed,

            mapWidth  = map.Width,
            mapHeight = map.Height,
            tileGridFlat = flat,
            rooms     = new List<RoomData>(map.Rooms),

            enemies   = enemyList,
            itemNames = names
        };

        saveUtil.SaveToSlot(SlotIndex, data);
    }
}
```

### 3.4 `LoadGameCommand.cs` — 从指定槽位读档

**路径**：`Assets/Scripts/Command/LoadGameCommand.cs`

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LoadGameCommand : AbstractCommand
{
    /// <summary>要读取的槽位（0/1/2），调用前设置</summary>
    public int SlotIndex { get; set; }

    protected override void OnExcute()
    {
        var saveUtil = this.GetUtility<ISaveUtility>();
        var data = saveUtil.LoadFromSlot(SlotIndex);
        if (data == null) return;

        // 0. 清理旧状态
        this.GetUtility<ISpawnUtility>().CleanupAll();
        this.GetModel<IEnemyModel>().GetAll().Keys.ToList()
            .ForEach(id => this.GetModel<IEnemyModel>().Unregister(id));
        this.GetModel<IMapModel>().Clearup();

        // 1. 恢复地图
        var grid = new int[data.mapWidth, data.mapHeight];
        for (int y = 0; y < data.mapHeight; y++)
            for (int x = 0; x < data.mapWidth; x++)
                grid[x, y] = data.tileGridFlat[y * data.mapWidth + x];
        this.GetModel<IMapModel>().SetMap(grid, data.rooms);

        // 2. 恢复战斗属性
        var combat = this.GetModel<ICombatModel>();
        combat.MaxHp.Value        = data.maxHp;
        combat.CurrentHp.Value    = data.currentHp;
        combat.AttackPower.Value  = data.attackPower;
        combat.DefensePower.Value = data.defensePower;
        combat.AttackRange.Value  = data.attackRange;

        // 3. 恢复实体属性
        var entity = this.GetModel<IEntityModel>();
        entity.Position  = new Vector2(data.playerPosX, data.playerPosY);
        entity.MoveSpeed = data.moveSpeed;

        // 4. 恢复敌人
        var enemyModel = this.GetModel<IEnemyModel>();
        foreach (var entry in data.enemies)
        {
            enemyModel.Register(new EnemyRuntimeData
            {
                EnemyId = entry.enemyId, CurrentHp = entry.currentHp,
                MaxHp = entry.maxHp, AttackPower = entry.attackPower,
                DefensePower = entry.defensePower, AttackRange = entry.attackRange,
                ChaseRange = entry.chaseRange, MoveSpeed = entry.moveSpeed,
                AttackDuration = entry.attackDuration, HitCheckTime = entry.hitCheckTime,
                HurtDuration = entry.hurtDuration, KnockbackForce = entry.knockbackForce,
                KnockbackDecay = entry.knockbackDecay,
                State = (EnemyActionState)entry.state,
                Position = new Vector2(entry.posX, entry.posY),
                FacingDir = entry.facingDir, IsDead = entry.isDead,
                MoveDelta = Vector2.zero, KnockbackVelocity = Vector2.zero,
                HitChecked = false, StateTimer = 0f
            });
        }

        // 5. 恢复背包
        var allConfigs = Resources.LoadAll<ItemConfig>("Config/Items");
        var itemModel = this.GetModel<IItemModel>();
        itemModel.Clear();
        foreach (var name in data.itemNames)
        {
            var cfg = allConfigs.FirstOrDefault(c => c.itemName == name);
            if (cfg != null) itemModel.Add(cfg);
        }

        // 6. 进入游戏（触发 MapBuilder / EnemyManagerSystem 重建视图）
        this.GetModel<IGameStateModel>().StartGame();
    }
}
```

### 3.5 `SaveSlotItem.cs` — 存档位 UI 条目

**路径**：`Assets/Scripts/ViewController/UIController/SaveSlotItem.cs`

存档列表中的单个条目，显示在该槽位的信息。

```csharp
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotItem : MonoBehaviour
{
    [SerializeField] private Text    _labelText;    // "存档 1" / "存档 2" / "存档 3"
    [SerializeField] private Text    _infoText;     // "2026/08/06 14:30  HP:5/6  第1层"
    [SerializeField] private Button  _button;       // 点击触发
    [SerializeField] private Image   _emptyMask;    // 空位时显示灰色遮罩

    public int SlotIndex { get; private set; }
    public bool IsEmpty { get; private set; }

    public void Init(SaveSlotInfo info, System.Action<int> onClick)
    {
        SlotIndex = info.slotIndex;
        IsEmpty   = info.isEmpty;

        _labelText.text = $"存档 {info.slotIndex + 1}";

        if (info.isEmpty)
        {
            _infoText.text = "空";
            _button.interactable = false;
            if (_emptyMask != null) _emptyMask.gameObject.SetActive(true);
        }
        else
        {
            _infoText.text = $"{info.saveTime}  HP:{info.hp}/{info.maxHp}  {info.floorName}";
            _button.interactable = true;
            if (_emptyMask != null) _emptyMask.gameObject.SetActive(false);
        }

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick(SlotIndex));
    }
}
```

### 3.6 `SavePanel.cs` — 存档选择界面

**路径**：`Assets/Scripts/ViewController/UIController/SavePanel.cs`

可复用于保存和读档两种模式。

```csharp
using UnityEngine;

public enum SavePanelMode
{
    Save,  // 保存模式：点击槽位执行保存
    Load   // 读档模式：只能点已有档的槽位
}

public class SavePanel : MonoBehaviour, IController
{
    [SerializeField] private SaveSlotItem[] _slots;   // 3 个槽位条目
    [SerializeField] private GameObject     _panelRoot;

    private SavePanelMode _mode;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void Show(SavePanelMode mode)
    {
        _mode = mode;
        RefreshSlots();
        _panelRoot.SetActive(true);
    }

    public void Hide()
    {
        _panelRoot.SetActive(false);
    }

    void RefreshSlots()
    {
        var saveUtil = this.GetUtility<ISaveUtility>();
        for (int i = 0; i < 3; i++)
        {
            var info = saveUtil.GetSlotInfo(i);
            _slots[i].Init(info, OnSlotClicked);
        }
    }

    void OnSlotClicked(int slotIndex)
    {
        if (_mode == SavePanelMode.Save)
        {
            // 如果已有存档，可以在这加确认覆盖弹窗
            this.SendCommand(new SaveGameCommand { SlotIndex = slotIndex });
            Hide();
        }
        else // Load
        {
            this.SendCommand(new LoadGameCommand { SlotIndex = slotIndex });
            Hide();
        }
    }
}
```

---

## 四、现有文件改动

### 4.1 `RogueLikeGame.cs` — 注册

```csharp
// Utility
this.RegisterUtility<ISaveUtility>(new SaveUtility());   // 新增
```

### 4.2 `UIManager.cs` — 支持 Pause 面板

`GetPrefab` 的 switch 里增加 Pause 分支：

```csharp
GameObject GetPrefab(UIPanelType type) => type switch
{
    UIPanelType.Start    => _gameStartPanelPrefab,
    UIPanelType.GamePlay => _gameplayPanelPrefab,
    UIPanelType.GameOver => _gameOverPanelPrefab,
    UIPanelType.Pause    => _pausePanelPrefab,   // ← 新增
    _ => null
};
```

并增加序列化字段：

```csharp
[SerializeField] private GameObject _pausePanelPrefab;   // ← 新增
```

### 4.3 开始面板脚本（如 `GameStartPanel.cs`）

增加"读取存档"按钮：

```
Start 面板 UI 结构：
├── [新游戏] 按钮 → StartGame()（现有）
├── [读取存档] 按钮 → 激活 SavePanel，mode = Load
└── SavePanel（初始隐藏，激活后显示）
```

### 4.4 新增 Pause 面板 Prefab

```
Pause 面板 UI 结构：
├── 半透明背景遮罩
├── [继续游戏] 按钮 → 关闭 Pause（回到 GamePlay）
├── [保存游戏] 按钮 → 激活 SavePanel，mode = Save
├── [读取存档] 按钮 → 激活 SavePanel，mode = Load
├── [返回主菜单] 按钮 → ReturnToMenu()
└── SavePanel（初始隐藏，激活后显示）
```

---

## 五、Unity Editor 操作清单

### 5.1 创建 Pause 面板 Prefab

1. Hierarchy 右键 `UI → Panel`，命名 `PausePanel`
2. 添加半透明背景 Image（黑色，alpha 约 0.7）
3. 添加按钮：继续游戏、保存游戏、读取存档、返回主菜单
4. 添加 SavePanel 子节点（见 5.3）
5. 编写 `PausePanel.cs` 脚本挂在根节点上（处理各按钮点击事件）
6. 保存为 `Assets/Resources/Perfabs/PausePanel.prefab`
7. 拖入 UIManager 的 `_pausePanelPrefab` 字段

### 5.2 修改 Start 面板

1. 打开 `GameStart.prefab`
2. 增加"读取存档"按钮
3. 添加 SavePanel 子节点（初始隐藏）
4. 在 `GameStartPanel.cs` 中增加"读取存档"按钮的逻辑

### 5.3 创建 SavePanel 子结构

```
SavePanel（Panel，初始 SetActive(false)）
├── Title Text（"保存游戏" / "读取存档"，根据模式切换）
├── SaveSlot_0（Image + Button + [Label Text + Info Text]）
├── SaveSlot_1
├── SaveSlot_2
└── [关闭] 按钮 → Hide()
```

每个 `SaveSlot` 节点上挂 `SaveSlotItem.cs`，拖入对应的子组件引用。

### 5.4 触发 Pause 面板

在 `GamePlayPanel` 或 `PlayerController` 中监听 Esc 键：

```csharp
void Update()
{
    if (Input.GetKeyDown(KeyCode.Escape))
    {
        var state = this.GetModel<IGameStateModel>();
        if (state._currentPhase.Value == UIPanelType.GamePlay)
            state._currentPhase.Value = UIPanelType.Pause;   // 打开暂停
        else if (state._currentPhase.Value == UIPanelType.Pause)
            state._currentPhase.Value = UIPanelType.GamePlay; // 关闭暂停回到游戏
    }
}
```

---

## 六、完整流程

### 保存流程

```
Esc → Pause 面板弹出 → 点击 [保存游戏]
  → SavePanel.Show(Save)
  → 扫描 slot_0.json / slot_1.json / slot_2.json → 刷新 3 个 SaveSlotItem
  → 点击槽位 1
    → SaveGameCommand { SlotIndex = 0 }
      → 从 6 个 Model 收集数据 → 组装 SaveData
      → SaveUtility.SaveToSlot(0, data) → JsonUtility.ToJson → File.WriteAllText
```

### 读档流程

```
Start 面板 → 点击 [读取存档]
  → SavePanel.Show(Load)
  → 点击已占用的槽位 1
    → LoadGameCommand { SlotIndex = 0 }
      → SaveUtility.LoadFromSlot(0) → File.ReadAllText → JsonUtility.FromJson
      → CleanupAll + 清空 EnemyModel + MapModel.Clearup
      → 依次恢复 MapModel / CombatModel / EntityModel / EnemyModel / ItemModel
      → StartGame() → MapGeneratedEvent + UIPanelChangeEvent
        → MapBuilder 重绘地图
        → EnemyManagerSystem 生成 EnemyView
```

---

## 七、文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| **新增** | `Assets/Scripts/Model/SaveData.cs` | SaveData + EnemySaveEntry |
| **新增** | `Assets/Scripts/Utility/ISaveUtility.cs` | 多槽位读写工具 |
| **新增** | `Assets/Scripts/Command/SaveGameCommand.cs` | 保存命令（含 SlotIndex） |
| **新增** | `Assets/Scripts/Command/LoadGameCommand.cs` | 读档命令（含 SlotIndex） |
| **新增** | `Assets/Scripts/ViewController/UIController/SaveSlotItem.cs` | 槽位 UI 条目 |
| **新增** | `Assets/Scripts/ViewController/UIController/SavePanel.cs` | 存档选择面板 |
| **新增** | `Assets/Scripts/ViewController/UIController/PausePanel.cs` | 暂停面板逻辑 |
| **修改** | `Assets/Scripts/RogueLikeGame.cs` | 注册 `ISaveUtility` |
| **修改** | `Assets/Scripts/ViewController/UIController/UIManager.cs` | 支持 Pause 面板 |
| **修改** | `Assets/Scripts/ViewController/UIController/GameStartPanel.cs` | 增加"读取存档"按钮逻辑 |
| **修改** | 某 Controller（如 `GameplayPanel.cs`）| Esc 键切换 Pause |
| **新增** | `Assets/Resources/Perfabs/PausePanel.prefab` | Pause 面板预制体 |
| **修改** | `Assets/Resources/Perfabs/GameStart.prefab` | 增加 SavePanel |

---

## 八、扩展建议

1. **确认覆盖弹窗**：保存时若槽位已有存档，弹出"确定覆盖此存档？"确认框
2. **删除存档**：Load 模式下给已占用的槽位加一个 × 按钮
3. **快速存档**：加一个 F5 快速保存到最近使用的槽位
4. **自动存档**：进入新楼层时自动保存到 slot_0
