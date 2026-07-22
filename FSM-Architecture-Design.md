# FSM 状态机架构设计文档

## 项目架构总览

本项目的架构为 **QFramework 风格的 MVCS** 分层，以 `Architecture<T>` 单例作为 IoC 枢纽。

层级从 **上到下** 依次为：

```
RogueLikeGameEditor (Architecture<T> 单例)
│
├── ViewController 层 (IController)    ← 表现层：MonoBehaviour，接收输入、更新表现
│   ├── PlayerController               ← 调度层：驱动 Utility 和 FSM
│   └── PlayerAnimationController      ← 订阅事件，驱动 Animator
│
├── Command 层 (ICommand)              ← 可校验的一次性动作，不能有状态
│   ├── TryAttackCommand               ← 尝试攻击
│   ├── TryMoveCommand                 ← 尝试移动
│   └── TryIdleCommand                 ← 回到 Idle
│
├── System 层 (ISystem)                ← 系统层：业务逻辑，纯 C#，无 MonoBehaviour
│   └── FSMSystem                      ← 状态机系统（Tick + ChangeState + 转换表）
│
├── Model 层 (IModel)                  ← 模型层：数据定义与存储
│   └── PlayerModel                    ← 玩家状态数据（BindableProperty）
│
└── Utility 层 (IUtility)              ← 工具层：基础设施，封装平台依赖
    └── InputUtility                   ← 输入数据容器 + Unity Input 封装
```

> **关键认知**：`ISystem` 只有 `Init()`，没有 `Update`。System 层不会自己每帧运行，必须由 MonoBehaviour 在 `Update` 中驱动。

---

## 一、层级通信规则

上层可以获取下层对象，下层不能获取上层对象。通信方向：

```
ViewController (IController)          ← 顶层
    │
    │  ★ 修改 ISystem/IModel 状态 → 必须走 Command
    │  ★ 查询数据 → 可 GetSystem/GetModel/GetUtility
    │  ★ 监听变化 → RegisterEvent 或 BindableProperty
    │
    ▼
Command (ICommand)                    ← 无状态、一次性
    │
    │  ★ 可 GetSystem/GetModel（校验用）
    │
    ▼
System (ISystem)                      ← 业务逻辑
    │
    │  ★ 向下可 GetModel/GetUtility（方法调用）
    │  ★ 向上通知 IController → SendEvent / BindableProperty
    │
    ▼
Model (IModel)                        ← 数据层
    │
    │  ★ 向上通知 → BindableProperty
    │
    ▼
Utility (IUtility)                    ← 工具层，基础设施（底层层）
    │
    ★ 不获取任何上层对象
```

| 规则 | 说明 |
|------|------|
| IController 更改 ISystem/IModel 状态 | **必须用 Command**，不能直接调方法 |
| IController 查询数据 | 可直接 GetSystem / GetModel / GetUtility |
| ISystem/IModel → IController 通知 | **必须用 Event 或 BindableProperty** |
| ICommand | **不能有状态**（无字段、无属性） |
| 上层 → 下层 | 方法调用（IController 除外，它必须走 Command） |
| 下层 → 上层 | 事件（Event / BindableProperty） |

> **什么是"业务状态变更"？** 指修改 Model/Sysem 中直接影响游戏逻辑的数据——切换状态、改变 HP、增减体力等。这些操作需要前置校验（比如"当前状态能不能切换到攻击？"），所以必须走 Command。`Tick()` 只是推进一帧让 FSM 自己检查输入做决策，Tick 本身不修改任何数据，不属业务变更，允许直接调。

---

## 二、层级归属

| 类 | 所属层 | 父类/接口 | 职责 |
|----|--------|-----------|------|
| `IFSMSystem` / `FSMSystem` | **System** | `ISystem` / `AbstractSystem` | 管理状态切换、转换表校验、Tick 驱动当前状态 |
| `IFSMState` | **System** | 独立接口（不继承 ISystem） | 单个状态的定义（OnEnter/OnUpdate/OnExit） |
| `PlayerModel` | **Model** | `IModel` / `AbstractModel` | 用 `BindableProperty` 存当前状态枚举 |
| `IInputUtility` / `InputUtility` | **Utility** | `IUtility` | 输入数据存储 + Unity Input API 封装 |
| `PlayerController` | **ViewController** | `MonoBehaviour` + `IController` | 调度层：调 Tick，发 Command |
| `PlayerAnimationController` | **ViewController** | `MonoBehaviour` + `IController` | 持有 `Animator`，订阅事件播动画 |

> **核心原则**：FSM 不持有 Unity 引用（Animator、Transform 等），与 Unity 交互全交给 ViewController / Utility。

---

## 三、每帧完整流程

```
PlayerController.Update()                         ← 唯一 MonoBehaviour 入口
  │
  ├── 1. 采集输入（调 Utility）
  │     _inputUtility.Tick()
  │       │  读 UnityEngine.Input
  │       │  存入自身属性（MoveInput / AttackPressed / AttackHolding）
  │       │  不访问任何上层对象 ✓
  │
  └── 2. 驱动状态机（直接方法调用，Tick 例外）
        _fsmSystem.Tick(Time.deltaTime)
        │
        └── FSMSystem.Tick(dt)
              │  CurrentState.OnUpdate(this.GetArchitecture(), dt)
              ▼
            IdleState.OnUpdate()                   ← 读 Utility 判断输入
              │  var input = arch.GetUtility<IInputUtility>();
              │  if (input.AttackPressed)
              │      arch.SendCommand<TryAttackCommand>();    ← IController 规则：发 Command
              │  else if (input.MoveInput.magnitude > 0.01f)
              │      arch.SendCommand<TryMoveCommand>();
              ▼
            TryAttackCommand.OnExcute()
              │  var fsm = this.GetSystem<IFSMSystem>();
              │  if (!fsm.CanTransitionTo<AttackState>()) return;// 校验
              │  fsm.ChangeState<AttackState>();                  // 方法调用（上层→下层）
              ▼
            FSMSystem.ChangeState<T>()
              │  _currentState.OnExit(arch)       ← 旧状态退出
              │  _currentState.OnEnter(arch)       ← 新状态进入
              │  _playerModel.CurrentState.Value = newStateType  ← 写 Model
              │  this.SendEvent(new PlayerStateChangedEvent{...}) ← 下层→上层：事件
              ▼
            PlayerAnimationController.OnPlayerStateChanged()
              │  _animator.CrossFade(e.AnimationName, 0.1f)
```

---

## 四、状态机内部设计

### 4.1 状态接口（IFSMState）

每个具体状态是一个 class，不继承 MonoBehaviour，也不继承 ISystem。

```csharp
/// <summary>
/// FSM 状态的基接口。每个具体状态是一个 class，不继承 MonoBehaviour。
/// </summary>
public interface IFSMState
{
    /// <summary>Animator 里对应的动画状态名，如 "Idle"、"Attack"、"Hurt"</summary>
    string AnimationName { get; }

    /// <summary>当前状态枚举</summary>
    PlayerStateType StateType { get; }

    /// <summary>进入状态时调用一次</summary>
    void OnEnter(IAchitecture arch);

    /// <summary>每帧由 FSMSystem.Tick 调用，在此读取输入、判断切换</summary>
    void OnUpdate(IAchitecture arch, float deltaTime);

    /// <summary>退出状态时调用一次</summary>
    void OnExit(IAchitecture arch);
}
```

### 4.2 FSMSystem 接口

```csharp
public interface IFSMSystem : ISystem
{
    /// <summary>每帧由 PlayerController 调用，驱动当前状态</summary>
    void Tick(float deltaTime);

    /// <summary>当前运行中的状态</summary>
    IFSMState CurrentState { get; }

    /// <summary>当前状态枚举（方便订阅）</summary>
    PlayerStateType CurrentStateType { get; }

    /// <summary>切换状态。allowSameState=false 时同状态忽略</summary>
    bool ChangeState<T>(bool allowSameState = false) where T : IFSMState, new();

    /// <summary>查询是否可以切换到某状态（用于 Command 校验）</summary>
    bool CanTransitionTo<T>() where T : IFSMState;

    /// <summary>查询是否可以切换到某状态</summary>
    bool CanTransitionTo(PlayerStateType target);
}
```

### 4.3 FSMSystem 实现

```csharp
public class FSMSystem : AbstractSystem, IFSMSystem
{
    private IFSMState _currentState;
    public IFSMState CurrentState => _currentState;
    public PlayerStateType CurrentStateType { get; private set; }

    // 状态转换表 —— Key = 当前状态, Value = 允许切换到的目标状态集合
    private Dictionary<PlayerStateType, HashSet<PlayerStateType>> _transitionTable;

    private PlayerModel _playerModel;

    protected override void OnInit()
    {
        BuildTransitionTable();
        _playerModel = this.GetModel<IPlayerModel>() as PlayerModel;

        // 默认进入 Idle
        ChangeState<IdleState>();
    }

    private void BuildTransitionTable()
    {
        _transitionTable = new Dictionary<PlayerStateType, HashSet<PlayerStateType>>
        {
            [PlayerStateType.Idle]    = new() { PlayerStateType.Move, PlayerStateType.Attack, PlayerStateType.Hurt, PlayerStateType.Dead },
            [PlayerStateType.Move]    = new() { PlayerStateType.Idle, PlayerStateType.Attack, PlayerStateType.Hurt, PlayerStateType.Dead },
            [PlayerStateType.Attack]  = new() { PlayerStateType.Idle, PlayerStateType.Hurt, PlayerStateType.Dead },
            [PlayerStateType.Hurt]    = new() { PlayerStateType.Idle, PlayerStateType.Dead },
            [PlayerStateType.Dead]    = new() { /* 死亡是终点 */ },
        };
    }

    /// <summary>
    /// 每帧由 PlayerController 调用。ISystem 没有 Update，必须由 MonoBehaviour 驱动。
    /// 这是 Tick 例外，不算 IController 越权，因为它只是"推进一帧"，不直接改变业务状态。
    /// </summary>
    public void Tick(float deltaTime)
    {
        _currentState?.OnUpdate(this.GetArchitecture(), deltaTime);
    }

    public bool ChangeState<T>(bool allowSameState = false) where T : IFSMState, new()
    {
        var newState = new T();
        var newStateType = newState.StateType;

        // 1. 同状态检查
        if (!allowSameState && CurrentStateType == newStateType)
            return false;

        // 2. 转换表检查
        if (!CanTransitionTo(newStateType))
            return false;

        // 3. 旧状态退出
        _currentState?.OnExit(this.GetArchitecture());

        // 4. 切换
        _currentState = newState;
        CurrentStateType = newStateType;

        // 5. 新状态进入
        _currentState.OnEnter(this.GetArchitecture());

        // 6. 更新 Model（BindableProperty 向上通知订阅者）
        _playerModel.CurrentState.Value = CurrentStateType;

        // 7. 发事件（下层 → 上层，通知 ViewController）
        this.SendEvent(new PlayerStateChangedEvent
        {
            StateType = CurrentStateType,
            AnimationName = _currentState.AnimationName
        });

        return true;
    }

    public bool CanTransitionTo<T>() where T : IFSMState
    {
        var targetState = new T();
        return CanTransitionTo(targetState.StateType);
    }

    public bool CanTransitionTo(PlayerStateType target)
    {
        if (_transitionTable.TryGetValue(CurrentStateType, out var allowed))
            return allowed.Contains(target);
        return false;
    }
}
```

---

## 五、Command 层

### 5.1 TryAttackCommand

```csharp
public class TryAttackCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();

        // 校验 —— 只有 Idle/Move 时可以攻击
        if (!fsm.CanTransitionTo<AttackState>())
            return;

        fsm.ChangeState<AttackState>();
    }
}
```

### 5.2 TryMoveCommand

```csharp
public class TryMoveCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();

        if (!fsm.CanTransitionTo<MoveState>())
            return;

        fsm.ChangeState<MoveState>();
    }
}
```

### 5.3 TryIdleCommand

```csharp
public class TryIdleCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();

        if (!fsm.CanTransitionTo<IdleState>())
            return;

        fsm.ChangeState<IdleState>();
    }
}
```

> Command 不能有状态：三个 Command 类都没有字段或属性，只包含 `OnExcute` 逻辑。好处是校验集中在 Command 层，状态类只负责"判断是否该切换"。

---

## 六、输入工具类（InputUtility）

合并了输入数据存储和 Unity Input 封装。处于最底层（Utility），不访问任何上层对象。

```csharp
/// <summary>
/// 输入工具类。存储当前帧输入数据 + 封装 Unity Input API。
/// 位于 Utility 层（最底层），不获取任何上层对象。
/// </summary>
public interface IInputUtility : IUtility
{
    /// <summary>移动输入向量（WASD / 摇杆）</summary>
    Vector2 MoveInput { get; }

    /// <summary>攻击键是否在本帧按下（按下瞬间为 true）</summary>
    bool AttackPressed { get; }

    /// <summary>攻击键是否正在持续按住</summary>
    bool AttackHolding { get; }

    /// <summary>每帧调用，读取 UnityEngine.Input</summary>
    void Tick();
}

public class InputUtility : IInputUtility
{
    public Vector2 MoveInput { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool AttackHolding { get; private set; }

    public IAchitecture GetArchitecture() => null; // Utility 层不访问上层

    public void Tick()
    {
        MoveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        AttackPressed = Input.GetButtonDown("Fire1");
        AttackHolding = Input.GetButton("Fire1");
    }
}
```

> Unity Input 的依赖锁在这个类里。换输入方案（新 Input System、手柄、触屏）只需改此一处。FSM 状态通过 `arch.GetUtility<IInputUtility>()` 读取输入数据（上层读下层，方法调用，合规）。

---

## 七、ViewController 层实现

### 7.1 PlayerController（调度层）

只做调度，不碰 Unity Input API。

```csharp
public class PlayerController : MonoBehaviour, IController
{
    private IInputUtility _inputUtility;
    private IFSMSystem _fsmSystem;

    void Start()
    {
        _inputUtility = this.GetUtility<IInputUtility>();
        _fsmSystem = this.GetSystem<IFSMSystem>();
    }

    void Update()
    {
        // Tick 是框架驱动，不是业务状态变更，允许直接调
        _inputUtility.Tick();                // Utility 读取 Unity Input
        _fsmSystem.Tick(Time.deltaTime);     // FSM 推进一帧 → 状态读输入判断切换
    }
}
```

### 7.2 PlayerAnimationController

FSM 不持有 Animator。由 ViewController 订阅事件来驱动动画（下层→上层：事件）。

```csharp
public class PlayerAnimationController : MonoBehaviour, IController
{
    private Animator _animator;

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    void Start()
    {
        // ISystem 通过 Event 通知 IController（下层→上层）
        this.RegisterEvent<PlayerStateChangedEvent>(OnPlayerStateChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void OnPlayerStateChanged(PlayerStateChangedEvent e)
    {
        _animator.CrossFade(e.AnimationName, 0.1f);
    }
}
```

---

## 八、PlayerStateChangedEvent（状态变更事件）

`FSMSystem.ChangeState` 发出，供动画层、UI 层等订阅。纯数据载体（POCO），不继承任何基类。

```csharp
/// <summary>
/// 玩家状态变更事件。由 FSMSystem.ChangeState 发出（下层→上层：事件），
/// 由 PlayerAnimationController、UI 等订阅。
/// </summary>
public class PlayerStateChangedEvent
{
    /// <summary>Animator 中对应的动画状态名，如 "Idle"、"Attack"</summary>
    public string AnimationName { get; set; }

    /// <summary>当前状态枚举</summary>
    public PlayerStateType StateType { get; set; }
}
```

> **发送**（System 层）：`this.SendEvent(new PlayerStateChangedEvent { ... })`
> **订阅**（ViewController 层）：`this.RegisterEvent<PlayerStateChangedEvent>(callback).UnRegisterWhenGameObjectDestroyed(gameObject)`

---

## 九、PlayerModel（状态数据）

```csharp
public enum PlayerStateType
{
    Idle,
    Move,
    Attack,
    Hurt,
    Dead
}

public interface IPlayerModel : IModel
{
    /// <summary>当前状态（BindableProperty，向上通知订阅者）</summary>
    BindableProperty<PlayerStateType> CurrentState { get; }
}

public class PlayerModel : AbstractModel, IPlayerModel
{
    public BindableProperty<PlayerStateType> CurrentState { get; } = new()
    {
        Value = PlayerStateType.Idle
    };

    protected override void OnInit() { }
}
```

---

## 十、具体状态类示例

状态在 `OnUpdate` 中读 `IInputUtility`（上层读下层，合规），通过 `SendCommand` 请求切换（走校验）。

```csharp
public class IdleState : IFSMState
{
    public string AnimationName => "Idle";
    public PlayerStateType StateType => PlayerStateType.Idle;

    public void OnEnter(IAchitecture arch) { }

    public void OnUpdate(IAchitecture arch, float dt)
    {
        var input = arch.GetUtility<IInputUtility>();
        if (input.AttackPressed)
            arch.SendCommand<TryAttackCommand>();               // 走 Command 校验
        else if (input.MoveInput.magnitude > 0.01f)
            arch.SendCommand<TryMoveCommand>();
    }

    public void OnExit(IAchitecture arch) { }
}

public class AttackState : IFSMState
{
    public string AnimationName => "Attack";
    public PlayerStateType StateType => PlayerStateType.Attack;

    private float _elapsed;
    private const float MaxDuration = 1.5f; // 最大攻击时长兜底

    public void OnEnter(IAchitecture arch)
    {
        _elapsed = 0f;
    }

    public void OnUpdate(IAchitecture arch, float dt)
    {
        _elapsed += dt;
        var input = arch.GetUtility<IInputUtility>();

        // 攻击键松开 或 超过最大时长 → 回到 Idle
        if (!input.AttackHolding || _elapsed > MaxDuration)
            arch.SendCommand<TryIdleCommand>();                 // 走 Command 校验
    }

    public void OnExit(IAchitecture arch) { }
}
```

> 之前 `AttackState` 直接调 `ChangeState` 绕过 Command，与 `IdleState` 不一致。现在统一走 `SendCommand`，校验集中在 Command 层。

---

## 十一、RogueLikeGameEditor 注册

```csharp
public class RogueLikeGameEditor : Architecture<RogueLikeGameEditor>
{
    protected override void Init()
    {
        // 注册 Model
        RegisterModel<IPlayerModel>(new PlayerModel());

        // 注册 System
        RegisterSystem<IFSMSystem>(new FSMSystem());

        // 注册 Utility
        RegisterUtility<IInputUtility>(new InputUtility());
    }
}
```

---

## 十二、设计要点总结

1. **层级从上到下**：ViewController → Command → System → Model → Utility，上层可获取下层，下层不访问上层
2. **IController 修改状态必须走 Command**：不可直接调 System/Model 的写方法。Tick() 是驱动帧循环的例外
3. **下层通知上层用 Event / BindableProperty**：`FSMSystem.ChangeState` 发 `PlayerStateChangedEvent`，`PlayerModel.CurrentState` 是 `BindableProperty`
4. **Command 无状态**：纯校验 + 调用，没有字段属性
5. **FSM 是纯逻辑**：不持有 Unity 引用，状态是独立 class
6. **InputUtility 在最底层**：合并数据存储 + Unity Input 封装，不依赖任何上层
7. **状态在 OnUpdate 中主动读输入**：不靠动画事件回调驱动状态切换
8. **转换表**：用字典定义合法状态转换，避免 hardcode 条件判断
