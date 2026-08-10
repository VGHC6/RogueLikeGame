# 存档系统 设计文档（状态机驱动版）

## 目标

3 个存档位，JSON 文件存储。在暂停界面保存/读档，在开始界面读档。

**核心原则：存档面板和其他所有面板（Start/GamePlay/Pause/GameOver）一样，由 `GameStateModel._currentPhase` 驱动。面板自己不知道被谁打开——只管从 Model 读模式，关闭时写回 Model。**

---

## 一、现有状态机机制（直接复用）

项目中已有的面板流转基础设施：

```
GameStateModel._currentPhase (BindableProperty<UIPanelType>)
    │  值变化 → UISystem 注册的 OnValueChanged 回调
    ▼
UISystem.Changepanel(newType)
    │  去重后发送
    ▼
UIPanelChangeEvent { OldPanel, NewPanel }
    │  UIManager 监听
    ▼
OnPanelChange() → 隐藏旧面板 → 实例化/显示新面板 → 启用/禁用 InputUtility
```

**这是纯复用——存档面板只需要 `UIPanelType.SaveLoad` 加进这个链路即可。**

现有流转：

```
Start ──[新游戏]──→ GamePlay ──[死亡/全灭]──→ GameOver ──[返回]──→ Start
                      ↑  │                                       
                      │  └──[Esc]──→ Pause                       
                      │               │                           
                      └──[继续]──────┘                           
                      └──[返回菜单]──→ Start                      
```

---

## 二、存档面板接入状态机

### 2.1 目标流转

```
Start ──[读档]──→ SaveLoad(Load) ──[选槽位]──→ GamePlay
                    │
                    └──[关闭]──→ Start

Pause ──[保存]──→ SaveLoad(Save) ──[选槽位]──→ Pause
Pause ──[读档]──→ SaveLoad(Load) ──[选槽位]──→ GamePlay
Pause ──[关闭存档面板]──→ Pause
```

### 2.2 实现方式

入口面板（Start / Pause）不再直接 `Instantiate` + `Show()` 存档面板。改为：

```csharp
// 打开存档面板（保存模式，关闭后回到 Pause）
this.GetModel<IGameStateModel>().OpenSavePanel(SavePanelMode.Save, UIPanelType.Pause);

// 打开存档面板（读档模式，关闭后回到 Start）
this.GetModel<IGameStateModel>().OpenSavePanel(SavePanelMode.Load, UIPanelType.Start);
```

这两个调用只做一件事：修改 `_currentPhase` 为 `SaveLoad`，同时记录模式和返回面板。后续全部由 UISystem → UIManager 链路自动完成。

---

## 三、改动清单

### 3.1 `UIPanelType.cs` — 已完成，需补全方法

**文件**：`Assets/Scripts/Model/UI/UIPanelType.cs`

枚举已有 `SaveLoad`：

```csharp
public enum UIPanelType
{
    None, Start, GamePlay, Pause, GameOver,
    SaveLoad,   // ← 已添加
}
```

`IGameStateModel` 需补全：

```csharp
public interface IGameStateModel : IModel
{
    BindableProperty<UIPanelType> _currentPhase { get; }
    SavePanelMode SaveMode { get; set; }
    UIPanelType SaveReturnPanel { get; set; }

    bool IsWin { get; }
    void StartGame();
    void GameOver(bool isWin);
    void ReturnToMenu();

    // ★ 新增
    void OpenSavePanel(SavePanelMode mode, UIPanelType returnPanel);
    void CloseSavePanel();
}
```

`GameStateModel` 需补全实现（目前 `SaveReturnPanel` 抛 `NotImplementedException`）：

```csharp
public class GameStateModel : AbstractModel, IGameStateModel
{
    public BindableProperty<UIPanelType> _currentPhase { get; } = new BindableProperty<UIPanelType>();
    public bool IsWin { get; set; }
    public SavePanelMode SaveMode { get; set; }
    public UIPanelType SaveReturnPanel { get; set; }

    protected override void OnInit() { }

    public void StartGame()           => _currentPhase.Value = UIPanelType.GamePlay;
    public void ReturnToMenu()        => _currentPhase.Value = UIPanelType.Start;
    public void GameOver(bool isWin)  { IsWin = isWin; _currentPhase.Value = UIPanelType.GameOver; }

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

### 3.2 `UIManager.cs` — 注册 SaveLoad 预制体

**文件**：`Assets/Scripts/ViewController/UIController/UIManager.cs`

改动点：
1. 新增 `_saveLoadPanelPrefab` 序列化字段
2. `GetPrefab` 新增分支
3. `OnPanelChange` **不用改**（已通过字典+switch 自动支持所有类型）

```csharp
[SerializeField] private GameObject _saveLoadPanelPrefab;   // ← 新增

GameObject GetPrefab(UIPanelType type) => type switch
{
    UIPanelType.Start    => _gameStartPanelPrefab,
    UIPanelType.GamePlay => _gameplayPanelPrefab,
    UIPanelType.GameOver => _gameOverPanelPrefab,
    UIPanelType.Pause    => _pausePanelPrefab,
    UIPanelType.SaveLoad => _saveLoadPanelPrefab,           // ← 新增
    _ => null
};
```

### 3.3 `PausePanel.cs` — 改为修改 Model，不再直接调 SavePanel

**文件**：`Assets/Scripts/ViewController/UIController/PausePanel.cs`

**现在（旧方式）**：
```csharp
[SerializeField] private SavePanel _savePanel;  // 直接引用

public void OnSaveButton()
{
    _savePanel.Show(SavePanelMode.Save);         // 绕过状态机
}
```

**改后（新方式）**：
```csharp
// 不需要 _savePanel 字段

public void OnSaveButton()
{
    this.GetModel<IGameStateModel>().OpenSavePanel(SavePanelMode.Save, UIPanelType.Pause);
}

public void OnLoadButton()
{
    this.GetModel<IGameStateModel>().OpenSavePanel(SavePanelMode.Load, UIPanelType.Pause);
}
```

### 3.4 `GameStartPanel.cs` — 同样改为写 Model

**文件**：`Assets/Scripts/ViewController/UIController/GameStartPanel.cs`

**现在（旧方式）**：
```csharp
[SerializeField] private GameObject _savePanel;  // 预制体引用

public void OnLoadButton()
{
    var go = Instantiate(_savePanel, this.transform);  // 绕开 UIManager
    go.GetComponent<SavePanel>().Show(SavePanelMode.Load);
}
```

**改后（新方式）**：
```csharp
// 不需要 _savePanel 字段，不需要 Instantiate

public void OnLoadButton()
{
    this.GetModel<IGameStateModel>().OpenSavePanel(SavePanelMode.Load, UIPanelType.Start);
}
```

### 3.5 `SavePanel.cs` — 用 OnEnable 自驱初始化

**文件**：`Assets/Scripts/ViewController/UIController/SavePanel.cs`

**核心变化**：去掉 `Show()`/`Hide()`，改为 `OnEnable` 从 Model 读取状态。面板激活由 UIManager 的 `SetActive(true)` 触发。

```csharp
using UnityEngine;

public enum SavePanelMode { Save, Load }

public class SavePanel : MonoBehaviour, IController
{
    [SerializeField] private SaveSlotItem[] _saveSlotItemPrefab;
    [SerializeField] private GameObject _panelRoot;          // ← 保留作为面板根节点
    [SerializeField] private GameObject _titleForSave;       // ← 可选，"保存游戏"标题
    [SerializeField] private GameObject _titleForLoad;       // ← 可选，"读取存档"标题

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    /// <summary>
    /// UIManager SetActive(true) 时自动触发。从 GameStateModel 读取模式。
    /// </summary>
    private void OnEnable()
    {
        var state = this.GetModel<IGameStateModel>();
        var mode = state.SaveMode;

        // 根据模式切换 UI
        if (_titleForSave != null) _titleForSave.SetActive(mode == SavePanelMode.Save);
        if (_titleForLoad != null) _titleForLoad.SetActive(mode == SavePanelMode.Load);

        RefreshSlots();
    }

    void RefreshSlots()
    {
        var saveUtil = this.GetUtility<ISaveUtility>();
        for (int i = 0; i < _saveSlotItemPrefab.Length; i++)
        {
            var info = saveUtil.GetSlotinfo(i);
            _saveSlotItemPrefab[i].Init(info, OnSlotClicked);
        }
    }

    void OnSlotClicked(int index)
    {
        var state = this.GetModel<IGameStateModel>();

        if (state.SaveMode == SavePanelMode.Save)
        {
            this.SendCommand(new SaveGameCommand { soltIndex = index });
            state.CloseSavePanel();  // 保存完回到 Pause
        }
        else
        {
            this.SendCommand(new LoadGameCommand { slotIndex = index });
            // LoadGameCommand 最终调用 StartGame() → _currentPhase = GamePlay
            // 不需要手动 CloseSavePanel
        }
    }

    /// <summary>关闭按钮回调，Inspector 绑定</summary>
    public void OnCloseButton()
    {
        this.GetModel<IGameStateModel>().CloseSavePanel();
    }
}
```

### 3.6 道具存档（SaveGameCommand / LoadGameCommand 修复）

#### 设计思路

道具效果（`PickupItemCommand.ApplyEffect`）会修改以下属性：

| 道具类型 | 影响的属性 |
|---------|-----------|
| Heal | `CombatModel.CurrentHp` |
| AtkUp | `CombatModel.AttackPower` |
| DefUp | `CombatModel.DefensePower` |
| SpeedUp | `EntityModel.MoveSpeed` |
| MaxHpUp | `CombatModel.MaxHp` |

**存档策略**：直接保存这些属性的"最终值"（已包含所有道具加成）。读档时恢复最终值即可，不需要重新执行 `ApplyEffect`。道具列表本身也需要保存和恢复，供 UI 展示。

#### 当前 Bug

`SaveGameCommand` 第 68-69 行**漏了 `_defensePower`**：

```
// 当前（漏了 defense）
_attackPower = combat.AttackPower.Value,
_attackRange  = combat.AttackRange.Value,
// ← 没有 _defensePower

// 应该
_attackPower  = combat.AttackPower.Value,
_defensePower = combat.DefensePower.Value,   // ← 加这行
_attackRange  = combat.AttackRange.Value,
```

`LoadGameCommand` 第 35-38 行**设了两次 AttackPower，没设 DefensePower**：

```
// 当前（有 bug）
combat.AttackPower.Value = data._attackPower;
combat.AttackPower.Value = data._attackPower;   // ← 重复了，应该是 DefensePower
combat.AttackRange.Value = data._attackRange;
combat.DefensePower.Value = data._defensePower; // ← 但 _defensePower 没被保存

// 应该
combat.AttackPower.Value  = data._attackPower;
combat.DefensePower.Value = data._defensePower;  // ← 修正确
combat.AttackRange.Value  = data._attackRange;
```

`LoadGameCommand` 加载道具的 Resources 路径**写错了**：

```
// 当前（错误路径）
var allItems = Resources.LoadAll<ItemConfig>("Prefabs/Items");

// 应该（正确路径，相对于 Resources 文件夹）
var allItems = Resources.LoadAll<ItemConfig>("Config/Items");
```

> 注：`ItemConfig` 文件实际在 `Assets/Resources/Config/Items/`，`Resources.LoadAll` 的路径是相对于 `Resources` 文件夹的，所以正确路径是 `"Config/Items"`。写成 `"Prefabs/Items"` 会返回空数组，导致所有道具加载失败。

#### 修复后的完整保存逻辑

```
// SaveGameCommand.OnExcute() —— 保存战斗属性 + 道具

var data = new SaveData
{
    // ...其他字段...

    // 战斗属性（已是包含道具加成的最终值）
    _currentHealth = combat.CurrentHp.Value,
    _maxHealth     = combat.MaxHp.Value,
    _attackPower   = combat.AttackPower.Value,
    _defensePower  = combat.DefensePower.Value,   // ← 补上
    _attackRange   = combat.AttackRange.Value,

    _playerPosX = entity.Position.x,
    _playerPosY = entity.Position.y,
    _moveSpeed  = entity.MoveSpeed,               // ← 已包含 SpeedUp 效果

    // 道具列表（只存名字，ItemConfig 是 ScriptableObject 不序列化）
    _packageData = items.Select(it => it.itemName).ToList(),
};
```

#### 修复后的完整加载逻辑

```
// LoadGameCommand.OnExcute() —— 恢复战斗属性 + 道具

// 1. 恢复战斗属性（最终值，不需要重新 ApplyEffect）
combat.MaxHp.Value        = data._maxHealth;
combat.CurrentHp.Value    = data._currentHealth;
combat.AttackPower.Value  = data._attackPower;
combat.DefensePower.Value = data._defensePower;   // ← 修复
combat.AttackRange.Value  = data._attackRange;

entity.Position  = new Vector2(data._playerPosX, data._playerPosY);
entity.MoveSpeed = data._moveSpeed;               // ← SpeedUp 效果直接恢复

// 2. 恢复道具列表（从 Resources 按名字找回 ItemConfig）
var allConfigs = Resources.LoadAll<ItemConfig>("Config/Items");  // ← 修复路径
var itemModel = this.GetModel<IItemModel>();
itemModel.Clear();
foreach (var name in data._packageData)
{
    var cfg = allConfigs.FirstOrDefault(c => c.itemName == name);
    if (cfg != null) itemModel.Add(cfg);
}
```

#### 为什么不需要重新 ApplyEffect

```
拾取 AtkUp(+2) → AttackPower 从 1 变成 3 → 保存 AttackPower=3
读档 → 恢复 AttackPower=3 → 道具列表里有 AtkUp（只用于 UI 展示）
                                        ↑
                              效果已在 3 里了，无需重新加
```

这比"保存基础值 + 重放道具效果"更简单，也不会因为效果叠加顺序产生差异。

### 3.7 不需改动的文件

以下文件已经是状态机友好的，不需要修改：

| 文件 | 原因 |
|------|------|
| `UISystem.cs` | 已在 `OnInit` 注册 `_currentPhase` 变化回调，SaveLoad 枚举加入后自动生效 |
| `GameplayPanel.cs` | 只处理战斗结束判定，不涉及存档 |
| `GameOverPanel.cs` | 只处理 GameOver 展示，不涉及存档 |
| `RogueLikeGame.cs` | 已注册 `ISaveUtility` |
| `SaveSlotItem.cs` | 只负责单个槽位展示，不关心打开方式 |
| `ISaveUtility.cs` / `SaveUtility.cs` | 纯 IO 工具，无 UI 依赖 |
| `SaveData.cs` / `EnemySaveData` | 纯数据结构（字段已存在，无需新增） |
| `PickupItemCommand.cs` | 拾取时已 ApplyEffect，存档保存的是最终值 |

---

## 四、Unity Editor 挂载指南

以下逐一说明每个预制体和场景对象的当前状态，以及接入状态机后需要做的 Inspector 操作。

### 4.1 整体架构图

```
SampleScene
  └── UIManager (挂 UIManager.cs)
        ├── _gameStartPanelPrefab   → GameStart.prefab     ← 已挂 ✓
        ├── _gameplayPanelPrefab    → GamePlay.prefab      ← 已挂 ✓
        ├── _gameOverPanelPrefab    → GameOver.prefab      ← 已挂 ✓
        ├── _pausePanelPrefab       → Pause.prefab         ← 需挂载
        └── _saveLoadPanelPrefab    → SavePanel.prefab     ← 需挂载 (新增字段)

Pause.prefab
  └── PausePanel.cs
        ├── 按钮: [继续] → OnContinueButton()             ← 已绑定 ✓
        ├── 按钮: [保存] → OnSaveButton()                 ← 已绑定 ✓，需改代码
        ├── 按钮: [读档] → OnLoadButton()                 ← 已绑定 ✓，需改代码
        ├── 按钮: [返回] → OnReturnToMenuButton()         ← 已绑定 ✓
        └── _savePanel 字段                                ← 需删除 (代码改后无用)

GameStart.prefab
  └── GameStartPanel.cs
        ├── 按钮: [新游戏] → OnStartButton()               ← 已绑定 ✓
        ├── 按钮: [读档]   → OnLoadButton()               ← 已绑定 ✓，需改代码
        └── _savePanel 字段                                ← 需删除 (代码改后无用)

SavePanel.prefab
  └── SavePanel.cs
        ├── _saveSlotItemPrefab[0] → SaveSlot_0           ← 已挂 ✓
        ├── _saveSlotItemPrefab[1] → SaveSlot_1           ← 已挂 ✓
        ├── _saveSlotItemPrefab[2] → SaveSlot_2           ← 已挂 ✓
        ├── _panelRoot                                     ← 目前为空，需挂或删
        └── [关闭] 按钮 → OnCloseButton()                  ← 需绑定
```

### 4.2 操作步骤

按以下顺序操作：

#### 步骤 1：补全 `GameStateModel`

代码补全 `OpenSavePanel()` 和 `CloseSavePanel()`（详见 3.1 节），无需 Editor 操作。

#### 步骤 2：修改 `SavePanel.prefab`

当前预制体结构：
```
SavePanel (根节点，挂 SavePanel.cs, Canvas, CanvasScaler, GraphicRaycaster)
├── SaveSlot_0 (挂 SaveSlotItem.cs)
├── SaveSlot_1 (挂 SaveSlotItem.cs)
├── SaveSlot_2 (挂 SaveSlotItem.cs)
└── ...其他子节点...
```

**操作：**

1. **`_panelRoot` 字段处理**：当前该字段为空。有两个选择：
   - **推荐**：代码改为 OnEnable 驱动后，整个面板的激活由 UIManager 的 `SetActive(true/false)` 控制根 GameObject，不再需要 `_panelRoot`。直接在代码中删掉 `_panelRoot` 字段，此处无需操作。
   - 备选：将根节点上的 Panel/背景 GameObject 拖入 `_panelRoot`。

2. **添加关闭按钮**：在面板上找一个返回/关闭按钮，在 Inspector 的 Button → OnClick 中添加：
   - Target：拖入根节点（挂 SavePanel.cs 的 GameObject）
   - Method：`SavePanel.OnCloseButton`

3. **可选——标题切换**：如果需要在 Save/Load 两种模式下显示不同标题文字：
   - 添加两个 Text 子节点（如 "保存游戏" 和 "读取存档"）
   - 拖入 `_titleForSave` 和 `_titleForLoad` 字段

#### 步骤 3：修改 `Pause.prefab`

当前状态：
- `PausePanel.cs` 已挂载
- `_savePanel` 字段指向 SavePanel.prefab
- 四个按钮均已绑定

**操作：**

1. **删除 `_savePanel` 引用**：代码改为通过 Model 驱动后，PausePanel 不再需要直接持有 SavePanel 引用。在 PausePanel 组件的 Inspector 中把 `_savePanel` 字段清空（设为 None）。

2. **按钮绑定保持不变**：`OnSaveButton()`、`OnLoadButton()` 等方法名不变，仅方法内部实现改变（从 `_savePanel.Show()` 变为 `OpenSavePanel()`），Inspector 绑定无需操作。

#### 步骤 4：修改 `GameStart.prefab`

当前状态：
- `GameStartPanel.cs` 已挂载
- `_savePanel` 字段指向 SavePanel.prefab
- [新游戏] 和 [读档] 按钮已绑定

**操作：**

1. **删除 `_savePanel` 引用**：在 GameStartPanel 组件的 Inspector 中把 `_savePanel` 字段清空（设为 None）。代码改为调用 `OpenSavePanel()` 后不再需要此引用。

2. **按钮绑定保持不变**：同上，只改内部实现，Inspector 不动。

#### 步骤 5：配置 `UIManager`（SampleScene 中）

当前状态：
```
UIManager 组件:
  _gameStartPanelPrefab  → GameStart.prefab   ✓
  _gameplayPanelPrefab   → GamePlay.prefab    ✓
  _gameOverPanelPrefab   → GameOver.prefab    ✓
  _pausePanelPrefab      → (空)               ✗ 需要挂
  _saveLoadPanelPrefab   → (字段不存在)        ✗ 代码加字段后挂
```

**操作：**

1. 在 `Assets/Resources/Perfabs/` 中找到 **Pause.prefab**，拖入 `_pausePanelPrefab` 字段。

2. 代码中添加 `_saveLoadPanelPrefab` 字段后，在 `Assets/Resources/Perfabs/` 中找到 **SavePanel.prefab**，拖入 `_saveLoadPanelPrefab` 字段。**注意：这里的 SavePanel 和之前暴露给 PausePanel/GameStartPanel 的 `_savePanel` 是同一个预制体、不同的引用方式——之前是直接引用（旧方式），现在是通过 UIManager 统一管理。**

### 4.3 挂载前后对比

```
挂载前（旧方式）:
  GameStart ──直接引用──→ SavePanel.prefab    (Instantiate 到 GameStart 下)
  Pause     ──直接引用──→ SavePanel.prefab    (通过 _savePanel 字段)
  UIManager ──────────→ 没有 SavePanel 的注册  (GetPrefab 不处理 SaveLoad)

挂载后（新方式）:
  GameStart ──没有 SavePanel 引用──→ 只调 OpenSavePanel()
  Pause     ──没有 SavePanel 引用──→ 只调 OpenSavePanel()
  UIManager ──_saveLoadPanelPrefab──→ SavePanel.prefab  (统一管理，类似其他面板)
```

### 4.4 验证清单

全部挂载完成后，逐项验证：

| # | 验证项 | 预期行为 |
|---|--------|---------|
| 1 | 启动游戏 | 显示 Start 面板 |
| 2 | Start → 点击 "读档" | 切换到 SavePanel，槽位列表刷新，标题/按钮为读档模式 |
| 3 | SavePanel → 点击关闭 | 回到 Start 面板 |
| 4 | 新游戏 → Esc | 显示 Pause 面板 |
| 5 | Pause → 点击 "保存" | 切换到 SavePanel，槽位可点击（包括空位） |
| 6 | 点击空槽位 | 保存成功，自动回到 Pause 面板 |
| 7 | Pause → 点击 "读档" | 切换到 SavePanel，空位不可点击 |
| 8 | 点击有数据的槽位 | 加载成功，进入 GamePlay |
| 9 | SavePanel 的 InputUtility | 在 SavePanel 显示时 InputUtility 应禁用（由 UIManager.OnPanelChange 自动处理） |
| 10 | Pause → Esc | 回到 GamePlay |

---

## 五、完整信号链路

### 保存流程

```
PausePanel.OnSaveButton()
  → OpenSavePanel(Save, Pause)
  → _currentPhase = SaveLoad (SaveMode=Save, SaveReturnPanel=Pause)
  → UISystem.Changepanel(SaveLoad)
  → UIPanelChangeEvent { Old=Pause, New=SaveLoad }
  → UIManager.OnPanelChange: 隐藏 Pause, 显示 SavePanel
  → SavePanel.OnEnable(): 读 SaveMode=Save → 标题切"保存" → RefreshSlots()

点击槽位 0:
  → SaveGameCommand { soltIndex=0 } 执行保存
  → CloseSavePanel() → _currentPhase = Pause
  → ... UI 链路自动回到 Pause
```

### 读档流程（从暂停）

```
PausePanel.OnLoadButton()
  → OpenSavePanel(Load, Pause)
  → _currentPhase = SaveLoad (SaveMode=Load, SaveReturnPanel=Pause)
  → ... 同上 ...

点击槽位 0:
  → LoadGameCommand { slotIndex=0 } 恢复全部状态
  → StartGame() → _currentPhase = GamePlay
  → ... UI 链路自动进入游戏
```

### 读档流程（从主菜单）

```
GameStartPanel.OnLoadButton()
  → OpenSavePanel(Load, Start)
  → _currentPhase = SaveLoad (SaveMode=Load, SaveReturnPanel=Start)
  → ... 同上 ...

点击槽位 0:
  → LoadGameCommand → StartGame() → GamePlay

点击关闭:
  → CloseSavePanel() → _currentPhase = Start
```

---

## 六、复用总结

一切已有基础设施直接复用：

| 组件 | 做了什么 | 存档面板要做的事 |
|------|---------|----------------|
| `BindableProperty<UIPanelType>` | 值变化自动通知 | 无——直接写入 |
| `UISystem.Changepanel()` | 去重 + 发 `UIPanelChangeEvent` | 无——自动触发 |
| `UIManager.OnPanelChange()` | 切换 GameObject 激活状态 + InputUtility | 只在 `GetPrefab` 加一个 case |
| `SavePanel.OnEnable()` | — | 从 Model 读 `SaveMode`，初始化自身 |
| `GameStateModel` | 持有 `_currentPhase` | 加 `SaveMode` / `SaveReturnPanel` + 两个方法 |

**每个入口面板只需要一行调用 `OpenSavePanel(mode, returnPanel)`，不再需要持有 SavePanel 引用、不再 Instantiate、不再手动管理生命周期。**

---

## 七、与旧方式的对比

```
旧：GameStartPanel → Instantiate(SavePanel) → .Show(Load)
                     ↑ 绕开状态机，SavePanel 挂在 GameStartPanel 下
旧：PausePanel      → _savePanel.Show(Save)
                     ↑ 直接引用，必须拖拽绑定

新：任意入口        → OpenSavePanel(mode, returnPanel)
                     ↑ 一行代码，Model 驱动，UIManager 统一管理生命周期
```

旧方式的问题：
1. SavePanel 的生命周期不统一（有时在 Start 下，有时在 Pause 下）
2. 每个需要存档面板的地方都要重复写 Instantiate + Show
3. 存档面板不在 UIManager 的管理范围内，关闭其他面板的逻辑对它无效

新方式的收益：
1. 面板管理和所有其他面板一致——UIManager 统一控制
2. 入口只写 Model，不需要知道 SavePanel 存在
3. SavePanel 自己从 Model 读配置，不依赖调用方
4. 新入口（如 GameOver 面板想加个"加载存档"按钮）只需一行 `OpenSavePanel()`
