# 存档系统 设计文档（状态机重构版）

## 目标

支持 3 个存档位。在暂停界面中手动保存，在开始界面中选择存档读取。使用 JSON 文件存储。

**核心设计原则：存档面板像其他面板（Start/GamePlay/Pause/GameOver）一样，使用状态机驱动，不依赖调用方。当 Model 修改时自动出现。**

---

## 一、现有 UI 状态机模式（参考）

项目中已有的面板切换流程：

```
GameStateModel._currentPhase (BindableProperty<UIPanelType>)
    ↓ 值变化时触发
UISystem.Changepanel(newType) 
    ↓ 去重后发送事件
UIPanelChangeEvent { OldPanel, NewPanel }
    ↓ UIManager 监听
OnPanelChange() → 隐藏旧面板 → 实例化/显示新面板
```

**关键特征：**
- 面板不直接互相调用，全部通过修改 `GameStateModel._currentPhase` 来驱动
- 面板不知道自己被谁打开、将回到谁——它只读 Model 状态
- 所有面板预制体在 `UIManager` 中统一注册，由 `GetPrefab()` 查找

**现有面板及其流转：**
```
Start ──[新游戏]──→ GamePlay ──[玩家死亡/敌人全灭]──→ GameOver
                      ↑                                  │
                      │──[Esc]──→ Pause                  │
                      ↑            │                      │
                      │            └──[继续]              │
                      │            └──[返回主菜单]──→ Start
                      └──────────────────────────────────┘
```

---

## 二、重构后的存档交互

### 2.1 新增面板类型

在 `UIPanelType` 枚举中新增：

```csharp
public enum UIPanelType
{
    None,
    Start,
    GamePlay,
    Pause,
    GameOver,
    SaveLoad,   // ← 新增：存档/读档面板（共用一个面板，通过模式区分）
}
```

### 2.2 新增 Model 状态

在 `IGameStateModel` 中新增存档面板相关的状态：

```csharp
public interface IGameStateModel : IModel
{
    BindableProperty<UIPanelType> _currentPhase { get; }

    // ---- 新增：存档面板状态 ----
    SavePanelMode SaveMode { get; set; }       // 当前是保存还是读档模式
    UIPanelType SaveReturnPanel { get; set; }  // 关闭存档面板后回到哪个面板

    bool IsWin { get; }
    void StartGame();
    void GameOver(bool isWin);
    void ReturnToMenu();

    // ---- 新增：打开存档面板 ----
    void OpenSavePanel(SavePanelMode mode, UIPanelType returnPanel);
    void CloseSavePanel();  // 关闭存档面板，回到 returnPanel
}
```

```csharp
public class GameStateModel : AbstractModel, IGameStateModel
{
    public BindableProperty<UIPanelType> _currentPhase { get; } = new BindableProperty<UIPanelType>();
    public bool IsWin { get; set; }

    // 存档面板状态
    public SavePanelMode SaveMode { get; set; }
    public UIPanelType SaveReturnPanel { get; set; }

    protected override void OnInit() { }

    public void StartGame()
    {
        _currentPhase.Value = UIPanelType.GamePlay;
    }

    public void ReturnToMenu()
    {
        _currentPhase.Value = UIPanelType.Start;
    }

    public void GameOver(bool isWin)
    {
        IsWin = isWin;
        _currentPhase.Value = UIPanelType.GameOver;
    }

    // ---- 新增 ----
    public void OpenSavePanel(SavePanelMode mode, UIPanelType returnPanel)
    {
        SaveMode = mode;
        SaveReturnPanel = returnPanel;
        _currentPhase.Value = UIPanelType.SaveLoad;
    }

    public void CloseSavePanel()
    {
        _currentPhase.Value = SaveReturnPanel;
    }
}
```

### 2.3 新的流程

```
Start 面板
  ├── [新游戏] → StartGame() → _currentPhase = GamePlay
  └── [读取存档] → OpenSavePanel(Load, Start) → _currentPhase = SaveLoad
                    └── 选择存档 → LoadGameCommand → StartGame()
                    └── 关闭 → CloseSavePanel() → _currentPhase = Start

Pause 面板
  ├── [继续游戏] → _currentPhase = GamePlay
  ├── [保存游戏] → OpenSavePanel(Save, Pause) → _currentPhase = SaveLoad
  │     └── 选择槽位 → SaveGameCommand → CloseSavePanel()
  │     └── 关闭 → CloseSavePanel() → _currentPhase = Pause
  ├── [读取存档] → OpenSavePanel(Load, Pause) → _currentPhase = SaveLoad
  │     └── 选择槽位 → LoadGameCommand → StartGame()
  │     └── 关闭 → CloseSavePanel() → _currentPhase = Pause
  └── [返回主菜单] → ReturnToMenu() → _currentPhase = Start
```

**关键改动：**
- 存档面板不再被 `GameStartPanel` 直接 `Instantiate` + `Show()`
- 而是通过修改 `GameStateModel` 状态，由 `UISystem → UIManager` 自动显示
- 存档面板不知道是谁打开了自己——它只从 Model 读取 `SaveMode` 来决定按钮行为
- 关闭时调用 `CloseSavePanel()`，回到 `SaveReturnPanel` 记录的面板

---

## 三、完整流程图

### 3.1 保存流程

```
GamePlay 中按 Esc
  → UIManager.Update 检测到 Pause 输入
  → _currentPhase = Pause
  → UISystem.Changepanel(Pause) → UIPanelChangeEvent
  → UIManager 显示 Pause 面板

Pause 面板中点击 [保存游戏]
  → OpenSavePanel(Save, Pause)
  → _currentPhase = SaveLoad, SaveMode = Save, SaveReturnPanel = Pause
  → UISystem.Changepanel(SaveLoad) → UIPanelChangeEvent
  → UIManager 显示 SaveLoad 面板
  → SavePanel.OnEnable() 读取 SaveMode = Save，刷新槽位列表

点击槽位 1（空位或确认覆盖）
  → SaveGameCommand { SlotIndex = 0 }
  → CloseSavePanel()
  → _currentPhase = Pause（回到暂停界面）
```

### 3.2 读档流程（从开始界面）

```
Start 面板点击 [读取存档]
  → OpenSavePanel(Load, Start)
  → _currentPhase = SaveLoad, SaveMode = Load, SaveReturnPanel = Start
  → UISystem.Changepanel(SaveLoad) → UIPanelChangeEvent
  → UIManager 显示 SaveLoad 面板
  → SavePanel.OnEnable() 读取 SaveMode = Load，刷新槽位列表

点击已占用的槽位
  → LoadGameCommand { SlotIndex = 0 }
  → 加载数据 → 清理旧状态 → 恢复各 Model → StartGame()
  → _currentPhase = GamePlay
```

---

## 四、数据结构（不变）

### 4.1 存档内容：`SaveData.cs`

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

### 4.2 文件存储（不变）

```
Application.persistentDataPath/
├── slot_0.json
├── slot_1.json
└── slot_2.json
```

---

## 五、新增文件

### 5.1 `SaveData.cs` — 数据结构（不变）

路径：`Assets/Scripts/Model/SaveData.cs`，代码见第四章。

### 5.2 `ISaveUtility.cs` — 多存档位读写（不变）

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

### 5.3 `SaveGameCommand.cs` — 保存到指定槽位（不变）

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

### 5.4 `LoadGameCommand.cs` — 从指定槽位读档（不变）

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

### 5.5 `SaveSlotItem.cs` — 存档位 UI 条目（不变）

**路径**：`Assets/Scripts/ViewController/UIController/SaveSlotItem.cs`

存档列表中的单个条目，显示在该槽位的信息。

```csharp
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotItem : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

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

### 5.6 `SavePanel.cs` — 存档选择界面（★ 重构：去掉 Show/Hide，改用 OnEnable）

**路径**：`Assets/Scripts/ViewController/UIController/SavePanel.cs`

**重构要点：** 
- 不再有 `Show(SavePanelMode)` / `Hide()` 方法
- 改为在 `OnEnable()` 中从 `GameStateModel` 读取当前模式
- 关闭时调用 `CloseSavePanel()` 修改 Model 状态
- 面板本身不知道是谁打开的，只依赖 Model

```csharp
using UnityEngine;

public enum SavePanelMode
{
    Save,  // 保存模式：点击槽位执行保存，空位也可点击
    Load   // 读档模式：只能点已有档的槽位
}

public class SavePanel : MonoBehaviour, IController
{
    [SerializeField] private SaveSlotItem[] _slots;   // 3 个槽位条目

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    /// <summary>
    /// 面板激活时自动从 Model 读取模式并刷新。
    /// 由 UIManager 在收到 UIPanelChangeEvent 后 SetActive(true) 触发。
    /// </summary>
    private void OnEnable()
    {
        RefreshSlots();
    }

    void RefreshSlots()
    {
        var saveUtil = this.GetUtility<ISaveUtility>();
        for (int i = 0; i < _slots.Length && i < 3; i++)
        {
            var info = saveUtil.GetSlotInfo(i);
            _slots[i].Init(info, OnSlotClicked);
        }
    }

    void OnSlotClicked(int slotIndex)
    {
        var state = this.GetModel<IGameStateModel>();
        var mode = state.SaveMode;

        if (mode == SavePanelMode.Save)
        {
            // 如果已有存档，可以在这加确认覆盖弹窗
            this.SendCommand(new SaveGameCommand { SlotIndex = slotIndex });
            state.CloseSavePanel();  // 保存后回到 Pause
        }
        else // Load
        {
            this.SendCommand(new LoadGameCommand { SlotIndex = slotIndex });
            // 注意：LoadGameCommand 最后会调用 StartGame()，
            // 那会把 _currentPhase 设为 GamePlay，不需要手动 CloseSavePanel
        }
    }

    /// <summary>
    /// 关闭按钮的点击回调（在 Inspector 中绑定）。
    /// 关闭存档面板，回到打开之前的那个面板。
    /// </summary>
    public void OnCloseButton()
    {
        this.GetModel<IGameStateModel>().CloseSavePanel();
    }
}
```

---

## 六、现有文件改动

### 6.1 `UIPanelType.cs`（即 `Assets/Scripts/Model/UI/UIPanelType.cs`）— 新增枚举值

```csharp
public enum UIPanelType
{
    None,
    Start,
    GamePlay,
    Pause,
    GameOver,
    SaveLoad,   // ← 新增
}
```

同时修改 `IGameStateModel` 和 `GameStateModel`（详见 2.2 节）。

### 6.2 `UIManager.cs` — 注册 SaveLoad 面板

增加序列化字段：

```csharp
[SerializeField] private GameObject _saveLoadPanelPrefab;   // ← 新增
```

`GetPrefab` 的 switch 里增加 SaveLoad 分支：

```csharp
GameObject GetPrefab(UIPanelType type) => type switch
{
    UIPanelType.Start    => _gameStartPanelPrefab,
    UIPanelType.GamePlay => _gameplayPanelPrefab,
    UIPanelType.GameOver => _gameOverPanelPrefab,
    UIPanelType.Pause    => _pausePanelPrefab,
    UIPanelType.SaveLoad => _saveLoadPanelPrefab,   // ← 新增
    _ => null
};
```

**注意：** `OnPanelChange` 方法不需要修改——它已经通过 switch 和字典自动支持所有面板类型。存档面板会像其他面板一样被自动 SetActive(true)/false。

### 6.3 `GameStartPanel.cs` — 改为修改 Model 而非直接实例化面板

**重构前（旧代码）：**
```csharp
public void OnLoadButton()
{
    var go = Instantiate(_savePanel, this.transform);
    go.GetComponent<SavePanel>().Show(SavePanelMode.Load);
}
```

**重构后（新代码）：**
```csharp
using UnityEngine;

public class GameStartPanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void OnStartButton()
    {
        this.GetModel<IGameStateModel>().StartGame();
    }

    public void OnLoadButton()
    {
        // ★ 不再直接操作 SavePanel，改为修改 Model
        // UISystem 会检测到 _currentPhase 变化，自动通过 UIManager 显示存档面板
        this.GetModel<IGameStateModel>().OpenSavePanel(SavePanelMode.Load, UIPanelType.Start);
    }
}
```

**变化：**
- 删除了 `[SerializeField] private GameObject _savePanel;` 字段
- 不再需要 `Instantiate` 存档面板预制体
- 改为调用 `OpenSavePanel(mode, returnPanel)` 修改 Model 状态

### 6.4 `PausePanel.cs` — 新增暂停面板脚本

**路径**：`Assets/Scripts/ViewController/UIController/PausePanel.cs`

```csharp
using UnityEngine;

public class PausePanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    /// <summary>继续游戏，回到 GamePlay</summary>
    public void OnContinueButton()
    {
        this.GetModel<IGameStateModel>()._currentPhase.Value = UIPanelType.GamePlay;
    }

    /// <summary>打开存档面板（保存模式）</summary>
    public void OnSaveButton()
    {
        this.GetModel<IGameStateModel>().OpenSavePanel(SavePanelMode.Save, UIPanelType.Pause);
    }

    /// <summary>打开存档面板（读档模式）</summary>
    public void OnLoadButton()
    {
        this.GetModel<IGameStateModel>().OpenSavePanel(SavePanelMode.Load, UIPanelType.Pause);
    }

    /// <summary>返回主菜单</summary>
    public void OnReturnToMenuButton()
    {
        this.GetModel<IGameStateModel>().ReturnToMenu();
    }
}
```

### 6.5 `UIManager.Update()` — Pause 切换逻辑（不变，已实现）

```csharp
void Update()
{
    if (this.GetUtility<IInputUtility>().Pause)
    {
        var state = this.GetModel<IGameStateModel>();
        if (state._currentPhase.Value == UIPanelType.GamePlay)
            state._currentPhase.Value = UIPanelType.Pause;
        else if (state._currentPhase.Value == UIPanelType.Pause)
            state._currentPhase.Value = UIPanelType.GamePlay;
    }
}
```

---

## 七、对比：旧设计 vs 新设计

### 旧设计的问题

```
GameStartPanel.OnLoadButton()
  → Instantiate(_savePanel)           // 直接实例化，绕开 UIManager
  → SavePanel.Show(SavePanelMode.Load) // 手动传参
  → SavePanel 自己管理生命周期
```

1. **绕过了状态机**：存档面板不受 `UIManager` 管理，不受 `UIPanelChangeEvent` 驱动
2. **依赖调用方**：`GameStartPanel` 必须持有 `_savePanel` 引用并知道 `SavePanelMode` 枚举
3. **不一致**：其他面板通过 `_currentPhase` 变化自动出现，存档面板是手动 `Instantiate`
4. **生命周期混乱**：SavePanel 被创建在 GameStartPanel 的 transform 下，而不是 UIManager 下
5. **难以扩展**：如果 Pause 面板也需要打开存档面板，需要在 PausePanel 中也写一遍 `Instantiate` + `Show()`

### 新设计的改进

```
GameStartPanel.OnLoadButton()
  → OpenSavePanel(Load, Start)        // 只修改 Model
  → _currentPhase = SaveLoad
  → UISystem.Changepanel(SaveLoad)    // 自动触发
  → UIPanelChangeEvent                // 自动发送
  → UIManager.OnPanelChange()         // 自动响应
  → SavePanel 被 SetActive(true)      // 自动显示
  → SavePanel.OnEnable() 读 Model     // 自驱初始化
```

1. **状态机驱动**：与 Start/GamePlay/Pause/GameOver 完全一致的流程
2. **不依赖调用方**：SavePanel 不知道谁打开的，只从 Model 读 `SaveMode`
3. **统一管理**：所有面板都在 UIManager 下生命周期统一
4. **任意入口**：Start 面板、Pause 面板、甚至 GameOver 面板都可以通过 `OpenSavePanel()` 打开存档面板，无需重复代码

---

## 八、Unity Editor 操作清单

### 8.1 创建 SaveLoad 面板 Prefab

```
SaveLoadPanel（Panel/Canvas，挂载 SavePanel.cs）
├── Title Text（"保存游戏" / "读取存档"，根据模式切换——可在 OnEnable 中设置）
├── SaveSlot_0（挂载 SaveSlotItem.cs）
├── SaveSlot_1（挂载 SaveSlotItem.cs）
├── SaveSlot_2（挂载 SaveSlotItem.cs）
└── [关闭] 按钮 → OnClick 绑定 SavePanel.OnCloseButton()
```

保存为 `Assets/Resources/Perfabs/SaveLoadPanel.prefab`。

### 8.2 创建 Pause 面板 Prefab

```
PausePanel（Panel，挂载 PausePanel.cs）
├── 半透明背景遮罩
├── [继续游戏] 按钮 → OnClick 绑定 PausePanel.OnContinueButton()
├── [保存游戏] 按钮 → OnClick 绑定 PausePanel.OnSaveButton()
├── [读取存档] 按钮 → OnClick 绑定 PausePanel.OnLoadButton()
└── [返回主菜单] 按钮 → OnClick 绑定 PausePanel.OnReturnToMenuButton()
```

保存为 `Assets/Resources/Perfabs/PausePanel.prefab`。

### 8.3 修改 UIManager

1. 在场景中找到挂载 `UIManager` 的 GameObject
2. 将 `SaveLoadPanel.prefab` 拖入 `_saveLoadPanelPrefab` 字段
3. 将 `PausePanel.prefab` 拖入 `_pausePanelPrefab` 字段（如果还没有）

### 8.4 修改 GameStart Prefab

1. 打开 `GameStart.prefab`
2. **删除**之前挂载的 SavePanel 子节点（如果有的话）
3. **删除** `GameStartPanel` 上的 `_savePanel` 字段引用（这个字段已从代码中移除）
4. 保留 "Read" 按钮，确保 OnClick 绑定 `GameStartPanel.OnLoadButton()`

---

## 九、文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| **新增** | `Assets/Scripts/Model/SaveData.cs` | SaveData + EnemySaveEntry |
| **新增** | `Assets/Scripts/Utility/ISaveUtility.cs` | 多槽位读写工具 |
| **新增** | `Assets/Scripts/Command/SaveGameCommand.cs` | 保存命令（含 SlotIndex） |
| **新增** | `Assets/Scripts/Command/LoadGameCommand.cs` | 读档命令（含 SlotIndex） |
| **新增** | `Assets/Scripts/ViewController/UIController/SaveSlotItem.cs` | 槽位 UI 条目 |
| **新增** | `Assets/Scripts/ViewController/UIController/SavePanel.cs` | 存档选择面板（★ 重构：状态机驱动） |
| **新增** | `Assets/Scripts/ViewController/UIController/PausePanel.cs` | 暂停面板逻辑 |
| **修改** | `Assets/Scripts/Model/UI/UIPanelType.cs` | 新增 `SaveLoad` 枚举值 + 扩展 `IGameStateModel`/`GameStateModel` |
| **修改** | `Assets/Scripts/RogueLikeGame.cs` | 注册 `ISaveUtility` |
| **修改** | `Assets/Scripts/ViewController/UIController/UIManager.cs` | 新增 `_saveLoadPanelPrefab` 字段 + `GetPrefab` 中新增 `SaveLoad` 分支 |
| **修改** | `Assets/Scripts/ViewController/UIController/GameStartPanel.cs` | `OnLoadButton` 改为调用 `OpenSavePanel()` |
| **新增** | `Assets/Resources/Perfabs/SaveLoadPanel.prefab` | 存档面板预制体 |
| **新增** | `Assets/Resources/Perfabs/PausePanel.prefab` | Pause 面板预制体 |

---

## 十、扩展建议

1. **确认覆盖弹窗**：保存时若槽位已有存档，弹出"确定覆盖此存档？"确认框（可通过新增一个 `ConfirmDialog` 面板，同样用状态机管理）
2. **删除存档**：Load 模式下给已占用的槽位加一个 × 按钮
3. **快速存档**：加一个 F5 快速保存到最近使用的槽位（直接发 `SaveGameCommand`，不打开面板）
4. **自动存档**：进入新楼层时自动保存到 slot_0（直接发 `SaveGameCommand`）
5. **模式标题切换**：在 `SavePanel.OnEnable()` 中根据 `SaveMode` 切换 Title 文字（"保存游戏" / "读取存档"）
