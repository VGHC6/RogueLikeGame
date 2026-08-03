# UI 架构文档

## 架构总览

```
UIManager (MonoBehaviour, IController)  ← 集中工厂：持有所有 Panel Prefab，管生成/显隐
  │
  ├─ 调用 IUISystem.ChangePanel(type) 切换状态
  │
  └─ 订阅 UIPanelStateChangedEvent → 隐藏旧 Panel → 显示/实例化新 Panel
       │
       ├── StartPanel Prefab      (StartPanel.cs 挂上面)
       ├── GameplayHUD Prefab     (内有 PlayerHpView.cs)
       ├── PausePanel Prefab      (PausePanel.cs 挂上面)
       └── …后面新增的 Panel…

EnemyHpBarView Prefab → 挂在敌人 Prefab 上，World Space
```

## 层与通信

| 方向 | 方式 |
|---|---|
| 任意层 → 切面板 | `this.GetSystem<IUISystem>().ChangePanel(type)` |
| UISystem → 通知外部 | `SendEvent(UIPanelStateChangedEvent)` |
| UIManager → 面板 | 直接 `SetActive` / `Instantiate`（Manager 职责） |
| Panel 内部逻辑 | GetModel, SendCommand, RegisterEvent（标准 IController） |
| Panel 之间 | 禁止互相引用，通过 Event / System 通信 |

---

## 新增文件清单

```
Assets/Scripts/
├── Model/
│   └── UIPanelType.cs
├── Event/
│   └── UIPanelStateChangedEvent.cs
├── System/
│   └── UISystem.cs
└── ViewController/
    └── UIController/
        ├── UIManager.cs            ← 集中工厂（Prefab 引用 + 生成 + 显隐）
        ├── StartPanel.cs           ← 开始界面逻辑
        ├── GameplayHUD.cs          ← 游戏 HUD 容器（含 PlayerHpView）
        ├── PausePanel.cs           ← 暂停界面逻辑
        ├── PlayerHpView.cs         ← 玩家血条（挂在 GameplayHUD 内）
        └── EnemyHpBarView.cs       ← 敌人血条 Prefab（挂敌人下，World Space）

Assets/Resource/UI/Prefabs/
├── StartPanel.prefab
├── GameplayHUD.prefab
├── PausePanel.prefab
├── Heart.prefab                    ← PlayerHpView 用它生成心
└── EnemyHpBar.prefab              ← 敌人血条
```

`RogueLikeGame.cs` 里注册一行：
```csharp
this.RegisterSystem<IUISystem>(new UISystem());
```

---

## 一、基础设施（先建）

### 1. `UIPanelType` 枚举 — `Model/UIPanelType.cs`

```csharp
public enum UIPanelType
{
    None,
    Start,
    Gameplay,
    Pause,
    // 后面扩展
}
```

### 2. `UIPanelStateChangedEvent` — `Event/UIPanelStateChangedEvent.cs`

```csharp
public class UIPanelStateChangedEvent
{
    public UIPanelType OldPanel;
    public UIPanelType NewPanel;
}
```

### 3. `UISystem` — `System/UISystem.cs`

```csharp
public interface IUISystem : ISystem
{
    UIPanelType CurrentPanel { get; }
    void ChangePanel(UIPanelType newPanel);
}

public class UISystem : AbstractSystem, IUISystem
{
    public UIPanelType CurrentPanel { get; private set; } = UIPanelType.None;

    public void ChangePanel(UIPanelType newPanel)
    {
        if (newPanel == CurrentPanel) return;
        var old = CurrentPanel;
        CurrentPanel = newPanel;
        this.SendEvent(new UIPanelStateChangedEvent { OldPanel = old, NewPanel = newPanel });
    }

    protected override void OnInit() { }
}
```

---

## 二、核心：UIManager（集中工厂）

### `ViewController/UIController/UIManager.cs`

挂在场景中一个持久化 GameObject（如 `UI`），配有 `Canvas`。

```csharp
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour, IController
{
    [SerializeField] private GameObject _startPanelPrefab;
    [SerializeField] private GameObject _gameplayHUDPrefab;
    [SerializeField] private GameObject _pausePanelPrefab;
    // 后面新增 Prefab 在这里加字段 + 拖入

    private Dictionary<UIPanelType, GameObject> _panels = new();

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        this.RegisterEvent<UIPanelStateChangedEvent>(OnPanelChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void Start()
    {
        this.GetSystem<IUISystem>().ChangePanel(UIPanelType.Start);
    }

    void OnPanelChanged(UIPanelStateChangedEvent e)
    {
        // 隐藏旧面板
        if (e.OldPanel != UIPanelType.None && _panels.TryGetValue(e.OldPanel, out var oldGo))
            oldGo.SetActive(false);

        // 显示/实例化新面板
        if (e.NewPanel == UIPanelType.None) return;

        if (!_panels.TryGetValue(e.NewPanel, out var newGo))
        {
            var prefab = GetPrefab(e.NewPanel);
            if (prefab == null) return;
            newGo = Instantiate(prefab, transform);
            _panels[e.NewPanel] = newGo;
        }
        newGo.SetActive(true);
    }

    GameObject GetPrefab(UIPanelType type) => type switch
    {
        UIPanelType.Start    => _startPanelPrefab,
        UIPanelType.Gameplay => _gameplayHUDPrefab,
        UIPanelType.Pause    => _pausePanelPrefab,
        // 后面新增 case
        _ => null
    };
}
```

职责：
- 持有所有 Panel Prefab 的引用（Inspector 拖入）
- 首次切入某个 Panel 时 `Instantiate`，后续直接 `SetActive`
- Panel Prefab 根节点初始应设为 disabled

---

## 三、Panel 脚本（极简）

Panel 不再自己管显隐逻辑，UIManager 统一操作。Panel 脚本只负责自己的业务。

### `StartPanel.cs`

```csharp
using UnityEngine;

public class StartPanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    // 按钮点击时调用
    public void OnStartGame()
    {
        this.GetSystem<IUISystem>().ChangePanel(UIPanelType.Gameplay);
    }
}
```

### `GameplayHUD.cs`

```csharp
using UnityEngine;

public class GameplayHUD : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    // 暂停按钮点击时调用
    public void OnPause()
    {
        this.GetSystem<IUISystem>().ChangePanel(UIPanelType.Pause);
    }
}
```

`PlayerHpView` 作为子物体直接挂在 GameplayHUD Prefab 内。

### `PausePanel.cs`

```csharp
using UnityEngine;

public class PausePanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void OnResume()
    {
        this.GetSystem<IUISystem>().ChangePanel(UIPanelType.Gameplay);
    }
}
```

---

## 四、血条

### 1. `PlayerHpView` — 挂在 GameplayHUD Prefab 内

需要先建 `Heart.prefab`：一个带 `Image` 组件的 GameObject，精灵拖好。

```csharp
using UnityEngine;
using UnityEngine.UI;

public class PlayerHpView : MonoBehaviour, IController
{
    [SerializeField] private GameObject _heartPrefab;

    private Image[] _hearts;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        var combat = this.GetModel<ICombatModel>();

        BuildHearts(combat.MaxHp.Value);

        combat.CurrentHp.RegisterOnValueChanged(_ => RefreshHearts())
            .UnRegisterWhenGameObjectDestroyed(gameObject);
        combat.MaxHp.RegisterOnValueChanged(max =>
        {
            foreach (var h in _hearts) Destroy(h.gameObject);
            BuildHearts(max);
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void BuildHearts(int count)
    {
        _hearts = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(_heartPrefab, transform);
            _hearts[i] = go.GetComponent<Image>();
        }
    }

    void RefreshHearts()
    {
        int hp = this.GetModel<ICombatModel>().CurrentHp.Value;
        for (int i = 0; i < _hearts.Length; i++)
            _hearts[i].enabled = i < hp;
    }
}
```

Prefab 搞定后，父物体上加 `HorizontalLayoutGroup` 自动排列。

### 2. `EnemyHpBarView` — 挂在敌人 Prefab 上

`EnemyHpBar.prefab`：World Space Canvas + Slider。挂到敌人 Prefab 上作为子物体。

```csharp
using UnityEngine;
using UnityEngine.UI;

public class EnemyHpBarView : MonoBehaviour, IController
{
    private int _enemyId;
    [SerializeField] private Slider _slider;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        _enemyId = GetComponentInParent<EnemyView>().EnemyId;
    }

    void OnEnable()
    {
        var model = this.GetModel<IEnemyModel>();
        if (model.TryGet(_enemyId, out var data))
        {
            _slider.maxValue = data.MaxHp;
            _slider.value = data.CurrentHp;
        }

        this.RegisterEvent<EnemyHpChangedEvent>(OnHpChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<EnemyDeadEvent>(OnDead)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void OnHpChanged(EnemyHpChangedEvent e)
    {
        if (e.EnemyId != _enemyId) return;
        _slider.value = e.CurrentHp;
    }

    void OnDead(EnemyDeadEvent e)
    {
        if (e.EnemyId != _enemyId) return;
        gameObject.SetActive(false);
    }
}
```

`OnEnable` 里主动读一次 Model 初始化（`EnemyHpChangedEvent` 只在变化时发，不会重放）。

---

## 五、加新 Panel 的步骤

1. `UIPanelType` 枚举加一项
2. 做 Prefab（Canvas + UI 元素），根节点挂 Panel 脚本
3. `UIManager` 加 `[SerializeField] GameObject _xxxPanelPrefab`
4. `UIManager.GetPrefab` switch 加一个 case
5. 哪里需要触发切换，就 `GetSystem<IUISystem>().ChangePanel(type)`

---

## 六、变更总结

| 操作 | 文件 |
|---|---|
| 新建 | `Model/UIPanelType.cs` |
| 新建 | `Event/UIPanelStateChangedEvent.cs` |
| 新建 | `System/UISystem.cs` |
| 修改 | `RogueLikeGame.cs` — 注册 IUISystem |
| 新建 | `ViewController/UIController/UIManager.cs` |
| 新建 | `ViewController/UIController/StartPanel.cs` |
| 新建 | `ViewController/UIController/GameplayHUD.cs` |
| 新建 | `ViewController/UIController/PausePanel.cs` |
| 新建 | `ViewController/UIController/PlayerHpView.cs` |
| 新建 | `ViewController/UIController/EnemyHpBarView.cs` |
| 新建 | Prefabs：StartPanel、GameplayHUD、PausePanel、Heart、EnemyHpBar |
