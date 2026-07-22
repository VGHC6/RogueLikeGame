# FSM 状态机架构设计文档

## 项目架构总览

本项目的架构为 **QFramework 风格的 MVCS** 分层，以 `Architecture<T>` 单例作为 IoC 枢纽，各层通过它完成依赖注入、命令派发、事件通信。

```
RogueLikeGameEditor (Architecture<T> 单例)
│
├── System 层 (ISystem)      ← 业务逻辑、状态机
│   └── FSMSystem            ← 状态机系统
│   └── MoveSystem           ← 移动/攻击系统
│
├── Model 层 (IModel)         ← 数据存储（BindableProperty）
│   └── PlayerModel           ← 玩家状态数据
│
├── Command 层 (ICommand)     ← 可校验的一次性动作
│   └── ChangeStateCommand    ← 切换状态
│   └── TryAttackCommand      ← 尝试攻击
│   └── AnimationEndCommand   ← 动画播完回调
│
└── ViewController 层         ← MonoBehaviour + IController
    ├── PlayerController      ← 输入采集
    └── PlayerAnimationController ← 动画驱动
```

---

## 一、层级归属

| 类 | 所属层 | 父类/接口 | 职责 |
|----|--------|-----------|------|
| `IFSMSystem` / `FSMSystem` | **System** | `ISystem` / `AbstractSystem` | 管理所有状态、状态切换逻辑、状态生命周期 |
| `IFSMState` | **System** | 独立接口 | 单个状态的定义（OnEnter/OnUpdate/OnExit） |
| `PlayerModel` | **Model** | `IModel` / `AbstractModel` | 用 `BindableProperty` 存当前状态枚举，供 UI 等层订阅 |
| `PlayerController` | **ViewController** | `MonoBehaviour` + `IController` | 收集玩家输入，通过 Command 请求状态变更 |
| `PlayerAnimationController` | **ViewController** | `MonoBehaviour` + `IController` | 持有 `Animator`，订阅状态变更事件来驱动动画 |

> **核心原则**：FSM 不持有 Unity 引用（Animator、Transform 等），它只做状态逻辑。与 Unity 交互的事全部交给 ViewController。

---

## 二、通信流程

### 2.1 输入驱动状态切换

```
玩家按下攻击键
  │
  ▼
PlayerController.Update()
  │  SendCommand<TryAttackCommand>()
  ▼
TryAttackCommand.OnExcute()
  │  从 Model 读取当前状态: CanTransitionTo(Attack)
  │  校验: 当前不在地面/不能攻击 → 拒绝
  │  调用: FSMSystem.ChangeState<AttackState>()
  ▼
FSMSystem.ChangeState<T>()
  │  旧状态.OnExit() → 执行退出逻辑
  │  新状态 = new T()
  │  新状态.OnEnter() → 执行进入逻辑
  │  PlayerModel.CurrentState.Value = PlayerStateType.Attack  ← 更新 Model
  │  SendEvent<PlayerStateChangedEvent>()                     ← 发事件
  ▼
PlayerAnimationController.OnStateChanged()
  │  animator.CrossFade("Attack", 0.1f)
  ▼
Unity Animator 播放攻击动画
  │  动画结束 → AnimationEvent 触发
  ▼
SendCommand<AnimationEndCommand>()
  │  回到 Idle 状态
```

### 2.2 通信手段一览

| 方向 | 机制 | 特点 |
|------|------|------|
| ViewController → Command | `this.SendCommand<T>()` | 通过扩展方法获取 Architecture 并派发 |
| Command → System/Model | `this.GetSystem<T>()` / `this.GetModel<T>()` | Command 实现了对应 Rule 接口，直接获取 |
| System → View | `this.SendEvent<T>()` | 解耦 —— 发事件的不知道谁在监听 |
| View → 监听事件 | `this.RegisterEvent<T>(callback)` | 在 `Awake`/`Start` 订阅，自动在 `OnDestroy` 取消 |
| System → Model | 直接赋值 `BindableProperty.Value` | 同一层可直接操作 |

### 2.3 Command 的校验职责

Command 是**一次性可校验的动作**。状态切换请求不直接调用 FSMSystem，而是先经过 Command：

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

这样做的好处：
- 统一校验入口，不会出现跳过校验的状态切换
- Command 可以组合多个 System 的操作（比如攻击时同时扣 Stamina）

---

## 三、状态机内部设计

### 3.1 状态接口

```csharp
/// <summary>
/// FSM 状态的基接口。每个具体状态是一个 class，不继承 MonoBehaviour。
/// </summary>
public interface IFSMState
{
    /// <summary>Animator 里对应的动画状态名，如 "Idle"、"Attack"、"Hurt"</summary>
    string AnimationName { get; }

    /// <summary>进入状态时调用一次</summary>
    void OnEnter(IAchitecture arch);

    /// <summary>每帧调用</summary>
    void OnUpdate(IAchitecture arch, float deltaTime);

    /// <summary>退出状态时调用一次</summary>
    void OnExit(IAchitecture arch);
}
```

### 3.2 FSMSystem 接口

```csharp
public interface IFSMSystem : ISystem
{
    /// <summary>当前运行中的状态</summary>
    IFSMState CurrentState { get; }

    /// <summary>当前状态枚举（方便订阅）</summary>
    PlayerStateType CurrentStateType { get; }

    /// <summary>切换状态。如果 allowSameState=false 且当前已是同类型则忽略</summary>
    bool ChangeState<T>(bool allowSameState = false) where T : IFSMState, new();

    /// <summary>查询是否可以切换到某状态（用于 Command 校验）</summary>
    bool CanTransitionTo<T>() where T : IFSMState;

    /// <summary>查询是否可以切换到某状态</summary>
    bool CanTransitionTo(PlayerStateType target);
}
```

### 3.3 FSMSystem 实现要点

```csharp
public class FSMSystem : AbstractSystem, IFSMSystem
{
    private IFSMState _currentState;
    public IFSMState CurrentState => _currentState;
    public PlayerStateType CurrentStateType { get; private set; }

    // 状态转换表 —— 定义哪些状态可以转到哪些状态
    // Key = 当前状态, Value = 允许切换到的目标状态集合
    private Dictionary<PlayerStateType, HashSet<PlayerStateType>> _transitionTable;

    private PlayerModel _playerModel; // Model 引用，缓存

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

    public bool ChangeState<T>(bool allowSameState = false) where T : IFSMState, new()
    {
        var newState = new T();
        var newStateType = newState.GetStateType();

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

        // 6. 更新 Model（触发 BindableProperty 通知）
        _playerModel.CurrentState.Value = CurrentStateType;

        // 7. 发事件（通知动画层）
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
        return CanTransitionTo(targetState.GetStateType());
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

## 四、动画切换

### 4.1 核心原则

FSM **不持有** Animator 引用。FSM 只负责状态逻辑并通过事件通知。由专门的 `PlayerAnimationController`（MonoBehaviour）来驱动 Unity Animator。

### 4.2 PlayerAnimationController

```csharp
public class PlayerAnimationController : MonoBehaviour, IController
{
    private Animator _animator;
    private IAchitecture _architecture;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _architecture = RogueLikeGameEditor.Interface;
    }

    void Start()
    {
        // 订阅状态变更事件
        _architecture.RegisterEvent<PlayerStateChangedEvent>(OnPlayerStateChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void OnPlayerStateChanged(PlayerStateChangedEvent e)
    {
        _animator.CrossFade(e.AnimationName, 0.1f); // 0.1 秒过渡
    }
}
```

### 4.3 动画结束回到 Idle

当一个不可打断的动画（攻击、受伤）播放完毕后，需要回到 Idle。有三种方式：

| 方式 | 做法 | 适用场景 |
|------|------|----------|
| **Animation Event** | 在攻击动画最后一帧挂 `AnimationEndEvent`，调用 `SendCommand<AnimationEndCommand>()` | 攻击、技能 |
| **StateMachineBehaviour** | 在 AnimatorController 的状态上挂脚本，`OnStateExit` 时发事件 | 需要复用逻辑时 |
| **FSM 内部计时器** | 在攻击状态的 `OnEnter` 记录开始时间，`OnUpdate` 中检查是否超时 | 不需要动画参与、纯逻辑状态 |

推荐使用 **Animation Event** + **Command** 的方式，符合现有架构模式：

```csharp
// 挂在 Player 上的 MonoBehaviour，被 AnimationEvent 调用
public class PlayerAnimationEvents : MonoBehaviour
{
    public void OnAttackAnimationEnd()
    {
        RogueLikeGameEditor.Interface.SendCommand<AnimationEndCommand>();
    }
}

// Command
public class AnimationEndCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        this.GetSystem<IFSMSystem>().ChangeState<IdleState>();
    }
}
```

---

## 五、PlayerModel（状态数据）

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

## 六、具体状态类示例

```csharp
public class IdleState : IFSMState
{
    public string AnimationName => "Idle";
    public PlayerStateType StateType => PlayerStateType.Idle;

    public void OnEnter(IAchitecture arch) { }
    public void OnUpdate(IAchitecture arch, float dt)
    {
        // 检查是否有移动输入 → 通过 Command 切换到 Move
        var moveSystem = arch.GetSystem<IMoveSystem>();
        if (moveSystem.Move.Value.magnitude > 0.01f)
        {
            arch.SendCommand<TryMoveCommand>();
        }
    }
    public void OnExit(IAchitecture arch) { }
}

public class AttackState : IFSMState
{
    public string AnimationName => "Attack";
    public PlayerStateType StateType => PlayerStateType.Attack;

    private bool _animationFinished = false;

    public void OnEnter(IAchitecture arch)
    {
        _animationFinished = false;
    }

    public void OnUpdate(IAchitecture arch, float dt)
    {
        // 等待动画事件回调（或超时兜底）
    }

    public void OnExit(IAchitecture arch)
    {
        _animationFinished = true;
    }
}
```

---

## 七、RogueLikeGameEditor 注册

```csharp
public class RogueLikeGameEditor : Architecture<RogueLikeGameEditor>
{
    protected override void Init()
    {
        // 注册 Model
        RegisterModel<IPlayerModel>(new PlayerModel());

        // 注册 System
        RegisterSystem<IMoveSystem>(new MoveSystem());
        RegisterSystem<IFSMSystem>(new FSMSystem());
    }
}
```

---

## 八、设计要点总结

1. **FSM 是纯逻辑**：不持有 Unity 引用，不碰 Animator/Transform/GameObject
2. **输入 → Command → FSM**：所有状态变更都走 Command，校验集中在 Command 层
3. **FSM → Event → 动画**：状态变更后发事件，ViewController 订阅来驱动 Animator
4. **动画结束 → Command → FSM**：动画播完通过 AnimationEvent + Command 回到 Idle
5. **转换表**：用字典定义合法状态转换，避免 hardcode 条件判断
6. **Model 做数据中心**：`PlayerModel.CurrentState`（BindableProperty）供 UI、其他 System 订阅当前状态
