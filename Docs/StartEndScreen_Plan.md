# 开始/结束界面 + 敌人动态生成 方案

## 核心思路

- **Model 控状态**：`IGameStateModel` 存 `CurrentPhase`（BindableProperty），所有状态变更走 Model
- **UISystem 做桥梁**：绑定 Model 的 `CurrentPhase`，变化时自动切面板并发 `UIPanelChangeEvent`
- **UIPanelChangeEvent 驱动其余 System**：`EnemyManagerSystem` 监听它来生成/清理敌人
- **不新建任何 Event 类型**：复用 `BindableProperty` 机制 + 已有的 `UIPanelChangeEvent`

---

## 复用基础设施

| 已有 | 用途 |
|------|------|
| `BindableProperty<T>` | Model 的 CurrentPhase 变化自动通知订阅者（PlayerCombatModel 同款用法） |
| `UIPanelChangeEvent` | UISystem → UIManager + EnemyManagerSystem |
| `UIPanelType` 枚举 | Start / GamePlay / GameOver |
| `EnemyDeadEvent` | GameplayPanel 监听 |
| `ICombatModel.IsDead` | GameplayPanel 监听玩家死亡 |
| `IEnemyModel` | EnemyManagerSystem 读写 |
| `IInputUtility` | UIManager 控制输入 |

### 本次新增

| 新增 | 类型 | 说明 |
|------|------|------|
| `ISpawnUtility` | Utility | 纯生成：Resources.Load + Instantiate + 构建数据。不调 View.Init / Model.Register，只返回 GameObject |
| `IGameStateModel` | Model | 存 CurrentPhase + IsWin，BindableProperty 驱动 UISystem |

---

## 完整数据流

### 初始化

```
UIManager.Start()
  ↓ Changepanel(Start) → UIPanelChangeEvent(None→Start)
UIManager → 实例化 StartPanel Prefab，显示
```

### 开始游戏

```
StartPanel.OnStartButton()
  ↓ GetModel<IGameStateModel>().StartGame()
Model: CurrentPhase.Value = GamePlay
  ↓ BindableProperty 通知 UISystem
UISystem: Changepanel(GamePlay) → UIPanelChangeEvent(Start→GamePlay)
  ├── UIManager          → 隐藏 Start，显示 GamePlay，Enable 输入
  └── EnemyManagerSystem → GetUtility<ISpawnUtility>().SpawnPlayer() + SpawnEnemies()
```

### 结束游戏

```
GameplayPanel 检测胜负
  ↓ model.GameOver(isWin)
Model: IsWin=..., CurrentPhase.Value = GameOver
  ↓ BindableProperty 通知 UISystem
UISystem: Changepanel(GameOver) → UIPanelChangeEvent(GamePlay→GameOver)
  ├── UIManager            → 隐藏 GamePlay，显示 GameOver，Disable 输入
  └── GameOverPanel.OnEnable() → 读 model.IsWin → 显示结果
```

### 返回菜单

```
GameOverPanel.OnRestartButton()
  ↓ model.ReturnToMenu()
Model: CurrentPhase.Value = Start
  ↓ BindableProperty 通知 UISystem
UISystem: Changepanel(Start) → UIPanelChangeEvent(GameOver→Start)
  └── UIManager → 隐藏 GameOver，显示 Start
```

---

## 一、新建 GameStateModel

直接使用已有的 `UIPanelType` 枚举，不新建 `GamePhase`。

```csharp
// Assets/Scripts/Model/GameStateModel.cs
public interface IGameStateModel : IModel
{
    BindableProperty<UIPanelType> CurrentPhase { get; }
    bool IsWin { get; set; }
    void StartGame();
    void GameOver(bool isWin);
    void ReturnToMenu();
}

public class GameStateModel : AbstractModel, IGameStateModel
{
    public BindableProperty<UIPanelType> CurrentPhase { get; } = new BindableProperty<UIPanelType>();
    public bool IsWin { get; set; }

    public void StartGame()
    {
        CurrentPhase.Value = UIPanelType.GamePlay;
    }

    public void GameOver(bool isWin)
    {
        IsWin = isWin;
        CurrentPhase.Value = UIPanelType.GameOver;
    }

    public void ReturnToMenu()
    {
        CurrentPhase.Value = UIPanelType.Start;
    }

    protected override void OnInit() { }
}
```

### 注册

```csharp
// RogueLikeGame.cs → Init()
this.RegisterModel<IGameStateModel>(new GameStateModel());
```

---

## 二、修改 UISystem：绑定 Model，自动切面板

`CurrentPhase` 直接就是 `UIPanelType`，不需要转换。

```csharp
public interface IUISystem : ISystem
{
    UIPanelType _currentPanelType { get; }
    void Changepanel(UIPanelType newPanelType);
}

public class UISystem : AbstractSystem, IUISystem
{
    public UIPanelType _currentPanelType { get; private set; } = UIPanelType.None;

    protected override void OnInit()
    {
        this.GetModel<IGameStateModel>().CurrentPhase.RegisterOnValueChanged(Changepanel);
        Changepanel(UIPanelType.Start);   // 初始显示
    }

    public void Changepanel(UIPanelType newPanelType)
    {
        if (_currentPanelType == newPanelType) return;
        var old = _currentPanelType;
        _currentPanelType = newPanelType;
        this.SendEvent(new UIPanelChangeEvent { OldPanel = old, NewPanel = newPanelType });
    }
}
```

---

## 三、新建 ISpawnUtility

Player 和 Enemy 的 Prefab 加载和实例化收进 Utility。ISpawnUtility 只做纯生成（Instantiate + 构建数据），不调 View.Init / Model.Register，这些编排逻辑由 EnemyManagerSystem 负责。接入地图时只改这个类。

```csharp
// Assets/Scripts/Utility/ISpawnUtility.cs
public interface ISpawnUtility : IUtility
{
    GameObject SpawnPlayer();
    void SpawnEnemies(List<EnemySpawnData> outSpawnList);
    void CleanupAll();
}

public struct EnemySpawnData
{
    public GameObject GO;
    public EnemyRuntimeData Data;
}

public class SpawnUtility : ISpawnUtility
{
    private IAchitecture _arch;
    public IAchitecture GetArchitecture() => _arch;
    public void SetArchitecture(IAchitecture architecture) => _arch = architecture;

    public GameObject SpawnPlayer()
    {
        var prefab = Resources.Load<GameObject>("Perfabs/Player");
        var go = GameObject.Instantiate(prefab, Vector2.zero, Quaternion.identity);
        return go;
    }

    public void SpawnEnemies(List<EnemySpawnData> outSpawnList)
    {
        var prefab = Resources.Load<GameObject>("Perfabs/Enemy");
        foreach (var pos in GetSpawnPositions())
        {
            var go = GameObject.Instantiate(prefab, pos, Quaternion.identity);
            var data = BuildEnemyData(pos);
            outSpawnList.Add(new EnemySpawnData { GO = go, Data = data });
        }
    }

    public void CleanupAll()
    {
        foreach (var obj in GameObject.FindGameObjectsWithTag("Enemy"))
            GameObject.Destroy(obj);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) GameObject.Destroy(player);
    }

    EnemyRuntimeData BuildEnemyData(Vector2 pos) => new EnemyRuntimeData
    {
        MaxHp = 6, CurrentHp = 6,
        AttackPower = 1, DefensePower = 1,
        AttackRange = 1f, ChaseRange = 5f, MoveSpeed = 3f,
        AttackDuration = 0.5f, HitCheckTime = 0.25f, HurtDuration = 0.4f,
        KnockbackForce = 8f, KnockbackDecay = 0.85f,
        State = EnemyActionState.Idle,
        Position = pos
    };

    // 暂无地图时的生成点
    Vector2[] GetSpawnPositions() => new[]
    {
        new Vector2(3f,  1f),
        new Vector2(5f, -1f),
        new Vector2(7f,  0f),
    };
}
```

### 注册

```csharp
// RogueLikeGame.Init()
this.RegisterUtility<ISpawnUtility>(new SpawnUtility());
```

---

## 四、修改 EnemyManagerSystem：只做 AI 编排

ISpawnUtility 只负责 Instantiate，System 负责编排：调 Spawn → Init → Register。`_playerTransform` 在 SpawnPlayer 后刷新。

```csharp
public class EnemyManagerSystem : AbstractSystem, IEnemyManagerSystem
{
    private Transform _playerTransform;
    private List<EnemySpawnData> _spawnList = new();

    protected override void OnInit()
    {
        this.RegisterEvent<UIPanelChangeEvent>(OnPanelChange);
    }

    void OnPanelChange(UIPanelChangeEvent e)
    {
        var spawn = this.GetUtility<ISpawnUtility>();
        var enemyModel = this.GetModel<IEnemyModel>();

        if (e.NewPanel == UIPanelType.GamePlay)
        {
            // Player: Instantiate（Utility） → Init（System）
            var playerGO = spawn.SpawnPlayer();
            playerGO.GetComponent<PlayerController>().Init();
            _playerTransform = playerGO.transform;

            // Enemy: Instantiate + 数据（Utility） → Register + Init（System）
            _spawnList.Clear();
            spawn.SpawnEnemies(_spawnList);
            foreach (var sd in _spawnList)
            {
                int id = enemyModel.Register(sd.Data);
                sd.GO.GetComponent<EnemyView>().Init(id, sd.Data);
            }
        }
        else if (e.OldPanel == UIPanelType.GamePlay)
        {
            // Model 清理由 System 负责（Utility 不允许访问 Model）
            foreach (var kv in enemyModel.GetAll())
                enemyModel.Unregister(kv.Key);

            _playerTransform = null;
            spawn.CleanupAll();  // 只做 GameObject.Destroy
        }
    }

    // ... Update / ChangeState / OnEnemyDamaged 不变 ...
}
```

---

## 五、修改 UIManager

```csharp
void OnPanelChange(UIPanelChangeEvent e)
{
    // 隐藏旧面板
    if (e.OldPanel != UIPanelType.None && _panelPrefabs.TryGetValue(e.OldPanel, out GameObject oldPanel))
        oldPanel.SetActive(false);

    // 没实例化则创建
    if (!_panelPrefabs.TryGetValue(e.NewPanel, out GameObject newPanel))
    {
        var prefab = GetPrefab(e.NewPanel);
        if (prefab == null) return;
        newPanel = Instantiate(prefab, transform);
        _panelPrefabs[e.NewPanel] = newPanel;
    }

    // 显示
    newPanel.SetActive(true);

    // 输入控制
    if (e.NewPanel == UIPanelType.GamePlay)
        this.GetUtility<IInputUtility>().Enable();
    else
        this.GetUtility<IInputUtility>().Disable();
}
```

---

## 六、新建 StartPanel

```csharp
// Assets/Scripts/ViewController/UIController/StartPanel.cs
public class StartPanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void OnStartButton()
    {
        this.GetModel<IGameStateModel>().StartGame();
    }
}
```

### Prefab 结构
```
StartPanel (Canvas, ScreenSpaceOverlay)
  ├── Title (Text)
  └── StartButton → onClick → StartPanel.OnStartButton()
```

---

## 七、新建 GameOverPanel

```csharp
// Assets/Scripts/ViewController/UIController/GameOverPanel.cs
public class GameOverPanel : MonoBehaviour, IController
{
    [SerializeField] private Text _resultText;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void OnEnable()
    {
        _resultText.text = this.GetModel<IGameStateModel>().IsWin ? "胜利！" : "失败！";
    }

    public void OnRestartButton()
    {
        this.GetModel<IGameStateModel>().ReturnToMenu();
    }
}
```

### Prefab 结构
```
GameOverPanel (Canvas, ScreenSpaceOverlay)
  ├── ResultText (Text)
  └── RestartButton → onClick → GameOverPanel.OnRestartButton()
```

---

## 八、修改 GameplayPanel

```csharp
public class GameplayPanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        this.RegisterEvent<EnemyDeadEvent>(OnEnemyDead);
    }

    void Start()
    {
        this.GetModel<ICombatModel>().IsDead.RegisterOnValueChanged(OnPlayerDead);
    }

    void OnEnemyDead(EnemyDeadEvent e)
    {
        if (this.GetModel<IEnemyModel>().GetAll().Count == 0)
            this.GetModel<IGameStateModel>().GameOver(true);
    }

    void OnPlayerDead(bool isDead)
    {
        if (isDead)
            this.GetModel<IGameStateModel>().GameOver(false);
    }
}
```

---

## 九、修改 EnemyView

删掉 `Start()` 里写死数据的自动注册：

```csharp
// 删掉
if (_enemyId == 0)
{
    var data = new EnemyRuntimeData { MaxHp = 6, ... };
    _enemyId = model.Register(data);
}
```

---

## 十、文件清单

### 新建（4 个）

| 文件 | 说明 |
|------|------|
| `Assets/Scripts/Model/GameStateModel.cs` | IGameStateModel + GameStateModel |
| `Assets/Scripts/Utility/ISpawnUtility.cs` | ISpawnUtility + SpawnUtility，纯生成：Instantiate + 构建数据，不调 Init/Register |
| `Assets/Scripts/ViewController/UIController/StartPanel.cs` | 开始界面 |
| `Assets/Scripts/ViewController/UIController/GameOverPanel.cs` | 结束界面 |

### 修改（7 个）

| 文件 | 改动 |
|------|------|
| `Assets/Scripts/RogueLikeGame.cs` | 注册 IGameStateModel + ISpawnUtility |
| `Assets/Scripts/System/UI/UISystem.cs` | 绑定 CurrentPhase，自动切面板 |
| `Assets/Scripts/System/EnemyManagerSystem.cs` | 监听 UIPanelChangeEvent，调 ISpawnUtility（Player+Enemy 生成/清理已移走） |
| `Assets/Scripts/ViewController/UIController/UIManager.cs` | SetActive Bug + 输入控制 |
| `Assets/Scripts/ViewController/UIController/GameplayPanel.cs` | 胜负检测，调 Model.GameOver |
| `Assets/Scripts/ViewController/EnemyView.cs` | 删自动注册 |
| `Assets/Scripts/ViewController/PlayerController.cs` | 加 Init 方法，支持 Prefab 动态生成 |

### 无需改动

`UIPanelChangeEvent`、`UIPanelType`、`BindableProperty`、`EnemyDeadEvent`、`ICombatModel`、`IEnemyModel`、`IInputUtility`、`CombatSystem`、`FSMSystem` 等。

---

## 十一、数据流总结

```
Controller          Model                       UISystem                 EnemyManagerSystem
──────────          ─────                       ────────                 ──────────────────
StartPanel ──→ StartGame()
              CurrentPhase=GamePlay ──→ Changepanel(GamePlay)
                                        UIPanelChangeEvent ──→ ISpawnUtility.SpawnPlayer() + SpawnEnemies()
                                                          ──→ System 调 Init() + Register() 完成初始化
                                                          ──→ UIManager.OnPanelChange()

GameplayPanel ──→ GameOver(true)
              CurrentPhase=GameOver ──→ Changepanel(GameOver)
                                         UIPanelChangeEvent ──→ CleanupAll()
                                                          ──→ UIManager.OnPanelChange()

GameOverPanel ──→ ReturnToMenu()
              CurrentPhase=Start ──→ Changepanel(Start)
                                      UIPanelChangeEvent ──→ UIManager.OnPanelChange()
```

**没有新枚举，没有新事件。** ISpawnUtility 只做纯生成（Instantiate + 数据），Init/Register 由 EnemyManagerSystem 编排。接入地图时只改 ISpawnUtility。

---

## 十二、执行顺序

1. 新建 `GameStateModel` → 注册到 RogueLikeGame
2. 改 `UISystem`：绑定 CurrentPhase
3. 改 `UIManager`：修 Bug + 输入控制
4. 写 `StartPanel.cs` + 做 Prefab → 验证：点按钮 → Model 状态变 Playing
5. 新建 `ISpawnUtility` → 注册到 RogueLikeGame
6. 改 `EnemyManagerSystem`：监听 UIPanelChangeEvent → 调 ISpawnUtility
7. 改 `EnemyView`：删自动注册
8. 改 `PlayerController`：加 Init 方法
9. 做 `Player.prefab` + `Enemy.prefab`，删除场景里的 Player → 验证：点开始 → 生成 Player + Enemy
10. 改 `GameplayPanel.cs`：胜负检测
11. 写 `GameOverPanel.cs` + 做 Prefab → 验证闭环
12. 完整测试

---

## 十三、为地图生成预留

接入只改 `ISpawnUtility.SpawnPlayer()` 和 `SpawnEnemies()` 的坐标逻辑，其余文件不动。

```csharp
public GameObject SpawnPlayer()
{
    var prefab = Resources.Load<GameObject>("Perfabs/Player");
    // 接入地图后：从 IMapModel.Rooms[0].Center 拿坐标
    var pos = Vector2.zero;
    return GameObject.Instantiate(prefab, pos, Quaternion.identity);
}

public void SpawnEnemies(List<EnemySpawnData> outSpawnList)
{
    var prefab = Resources.Load<GameObject>("Perfabs/Enemy");
    // 接入地图后：从 IMapModel.Rooms.Skip(1) 拿坐标
    var positions = GetSpawnPositions();
    foreach (var pos in positions) { ... }
}

// 接入地图后改这个方法的实现
Vector2[] GetSpawnPositions()
{
    // 现在：固定坐标
    return new[] { new Vector2(3f, 1f), new Vector2(5f, -1f), new Vector2(7f, 0f) };
    // 以后：return this.GetModel<IMapModel>().Rooms.Skip(1).Select(r => r.Center).ToArray();
}
```
