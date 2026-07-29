# FSM 状态机架构设计文档

## 项目架构总览

本项目的架构为 **QFramework 风格的 MVCS** 分层。核心变化：引入 `EntityArchitecture`（非单例版 `IAchitecture`），每个实体持有一个。

```
全局 RogueLikeGame.Interface（Architecture<T> 单例）
│
├── IInputUtility           ← 全局唯一（输入设备只有一套）
├── ICombatSystem           ← 全局唯一（伤害公式纯计算，不区分实体）
│
├── PlayerController  ──→  PlayerArchitecture : EntityArchitecture
│   │                         ├── IEntityModel         ← Player 自己的移动数据
│   │                         ├── ICombatModel         ← Player 自己的 HP/攻击力
│   │                         ├── FsmIdleState         ← Player 自己的状态实例
│   │                         ├── FsmMoveState
│   │                         ├── FsmAttackState
│   │                         ├── FsmHurtState
│   │                         └── IFSMSystem           ← Player 自己的状态机
│   │
│   └── GetArchitecture() → _architecture（Player 自己的 IoC）
│
├── EnemyController   ──→  EnemyArchitecture : EntityArchitecture
│   │                         ├── IEntityModel         ← Enemy 自己的移动数据
│   │                         ├── ICombatModel         ← Enemy 自己的 HP/攻击力
│   │                         ├── FsmIdleState         ← Enemy 自己的状态实例
│   │                         ├── FsmMoveState
│   │                         ├── FsmAttackState
│   │                         ├── FsmHurtState
│   │                         └── IFSMSystem           ← Enemy 自己的状态机
│   │
│   └── GetArchitecture() → _architecture（Enemy 自己的 IoC）
│
└── CombatUIController ──→  RogueLikeGame.Interface（全局，收 DamageEvent）
```

> **核心设计**：FSM 状态类、FSMSystem、EntityModel、CombatModel **一行代码不改**，仍然 `RegisterSystem` / `GetSystem` / `SendCommand` / `SendEvent`，API 完全不变。唯一区别是它们被注册在实体自己的 `EntityArchitecture` 里，而非全局单例里。

---

## 一、EntityArchitecture

非单例的 `IAchitecture` 实现。API 和 `Architecture<T>` 完全一致。

### 1.1 查找规则：先本地，后父级

```
this.GetSystem<FsmMoveState>()        ← 本地容器有 → 返回 Player 自己的实例
this.GetModel<IEntityModel>()         ← 本地容器有 → 返回 Player 自己的数据
this.GetUtility<IInputUtility>()      ← 本地没有   → 回退到父级（全局 IoC）
this.SendCommand<TryMoveCommand>()    ← 走本地架构 → Command.SetArchitecture(this)
this.SendEvent(PlayerStateChanged)    ← 走本地事件总线 → 不会串到其他实体
```

### 1.2 实现

```csharp
public abstract class EntityArchitecture : IAchitecture
{
    private IOCContainer _container = new IOCContainer();
    private IAchitecture _parent;

    private List<IModel> _models = new List<IModel>();
    private List<ISystem> _systems = new List<ISystem>();

    private ITypeEventSystem _typeEventSystem = new TypeEventSystem<EntityArchitecture>();

    public EntityArchitecture(IAchitecture parent = null)
    {
        _parent = parent;
    }

    /// <summary>子类在构造中调 Register，构造完成后调此方法完成初始化</summary>
    protected void InitEntities()
    {
        foreach (var system in _systems) system.Init();
        _systems.Clear();
        foreach (var model in _models) model.Init();
        _models.Clear();
    }

    // ========== 查找：先本地，后父级 ==========

    public T GetModel<T>() where T : class, IModel
    {
        var result = _container.Get<T>();
        return result ?? _parent?.GetModel<T>();
    }

    public T GetSystem<T>() where T : class, ISystem
    {
        var result = _container.Get<T>();
        return result ?? _parent?.GetSystem<T>();
    }

    public T GetUtility<T>() where T : class, IUtility
    {
        var result = _container.Get<T>();
        return result ?? _parent?.GetUtility<T>();
    }

    // ========== 注册（同 Architecture<T> 的模式） ==========

    public void RegisterModel<T>(T instance) where T : IModel
    {
        instance.SetArchitecture(this);
        _container.Register<T>(instance);
        _models.Add(instance);
    }

    public void RegisterSystem<T>(T instance) where T : ISystem
    {
        instance.SetArchitecture(this);
        _container.Register<T>(instance);
        _systems.Add(instance);
    }

    public void RegisterUtility<T>(T instance) where T : IUtility
    {
        _container.Register<T>(instance);
    }

    // ========== Command（走本地架构） ==========

    public void SendCommand<T>() where T : ICommand, new()
    {
        var command = new T();
        command.SetArchitecture(this);
        command.Excute();
    }

    public void SendCommand<T>(T command) where T : ICommand
    {
        command.SetArchitecture(this);
        command.Excute();
    }

    // ========== 事件（走本地总线，实体间隔离） ==========

    public void SendEvent<T>() where T : new()
    {
        _typeEventSystem.Send<T>();
    }

    public void SendEvent<T>(T e)
    {
        _typeEventSystem.Send<T>(e);
    }

    public IUnRegister RegisterEvent<T>(Action<T> OnEvent)
    {
        return _typeEventSystem.Register<T>(OnEvent);
    }

    public void UnRegisterEvent<T>(Action<T> OnEvent)
    {
        _typeEventSystem.UnRegister<T>(OnEvent);
    }
}
```

> **本地事件总线的意义**：`FSMSystem.ChangeState` 发送的 `PlayerStateChangedEvent` 只在当前实体的架构内传播。Player 的动画控制器订阅 Player 架构的事件，Enemy 的动画控制器订阅 Enemy 架构的事件——天然隔离，不需要 `EntityId` 过滤。

---

## 二、RogueLikeGame —— 全局 IoC

```csharp
public class RogueLikeGame : Architecture<RogueLikeGame>
{
    protected override void Init()
    {
        // 只放真正全局唯一的共享资源
        this.RegisterUtility<IInputUtility>(new InputUtility());
        this.RegisterSystem<ICombatSystem>(new CombatSystem());

        // FSM 状态 / FSMSystem / EntityModel / CombatModel 不再注册到这里
        // 由各实体的 EntityArchitecture 管理
    }
}
```

---

## 三、实体架构子类

### 3.1 PlayerArchitecture

```csharp
public class PlayerArchitecture : EntityArchitecture
{
    public PlayerArchitecture(IAchitecture parent) : base(parent)
    {
        RegisterModel<IEntityModel>(new EntityModel());
        RegisterModel<ICombatModel>(new CombatModel());

        RegisterSystem<FsmIdleState>(new FsmIdleState());
        RegisterSystem<FsmMoveState>(new FsmMoveState());
        RegisterSystem<FsmAttackState>(new FsmAttackState());
        RegisterSystem<FsmHurtState>(new FsmHurtState());

        RegisterSystem<IFSMSystem>(new FSMSystem());

        InitEntities();
    }
}
```

### 3.2 EnemyArchitecture

```csharp
public class EnemyArchitecture : EntityArchitecture
{
    public EnemyArchitecture(IAchitecture parent) : base(parent)
    {
        RegisterModel<IEntityModel>(new EntityModel());
        RegisterModel<ICombatModel>(new CombatModel());

        RegisterSystem<FsmIdleState>(new FsmIdleState());
        RegisterSystem<FsmMoveState>(new FsmMoveState());
        RegisterSystem<FsmAttackState>(new FsmAttackState());
        RegisterSystem<FsmHurtState>(new FsmHurtState());

        RegisterSystem<IFSMSystem>(new FSMSystem());

        InitEntities();
    }
}
```

> Player 和 Enemy 的架构完全一样。如果需要差异化（如敌人没有 Hurt 状态），在子类覆盖即可。

---

## 四、IEntityModel

原 `IPlayerModel` 改名，因为"移动数据"对 Player 和 Enemy 都适用。

```csharp
public enum PlayerStateType
{
    Idle,
    Attack,
    Move,
    Hurt
}

public interface IEntityModel : IModel
{
    BindableProperty<PlayerStateType> _currentState { get; }
    Vector2 MoveDelta { get; set; }
    float MoveSpeed { get; set; }
}

public class EntityModel : AbstractModel, IEntityModel
{
    public BindableProperty<PlayerStateType> _currentState { get; } = new BindableProperty<PlayerStateType>()
    {
        Value = PlayerStateType.Idle
    };
    public Vector2 MoveDelta { get; set; }
    public float MoveSpeed { get; set; } = 5f;

    protected override void OnInit() { }
}
```

> `MoveDelta` 是普通属性——每帧连续变化的数据流，轮询优于事件。`_currentState` 是 `BindableProperty`——状态切换是离散事件，需要立即通知多方（动画、UI）。

---

## 五、IFSMState 接口

```csharp
public interface IFSMState : ISystem
{
    string AnimationName { get; }
    PlayerStateType StateType { get; }
    void OnEnter();
    void OnUpdate(float deltaTime);
    void OnFixUpdate(float deltaTime);
    void OnExit();
}
```

> **状态类继承 `AbstractSystem`**，通过 `this.GetModel<IEntityModel>()` 获取 Model，通过 `this.GetUtility<IInputUtility>()` 获取输入。API 不变。

---

## 六、各状态实现

### 6.1 FsmIdleState

```csharp
public class FsmIdleState : AbstractSystem, IFSMState
{
    public string AnimationName => "Idle";
    public PlayerStateType StateType => PlayerStateType.Idle;

    public void OnEnter() { }
    public void OnUpdate(float dt) { }
    public void OnFixUpdate(float dt) { }
    public void OnExit() { }

    protected override void OnInit() { }
}
```

### 6.2 FsmMoveState

```csharp
public class FsmMoveState : AbstractSystem, IFSMState
{
    public string AnimationName => "Move";
    public PlayerStateType StateType => PlayerStateType.Move;

    public void OnEnter() { }
    public void OnUpdate(float dt) { }

    public void OnFixUpdate(float dt)
    {
        var model = this.GetModel<IEntityModel>();       // ← 本地 IoC
        var input = this.GetUtility<IInputUtility>();    // ← 本地无，回退全局

        Vector2 direction = new Vector2(input.Move.x, input.Move.y).normalized;
        model.MoveDelta = direction * model.MoveSpeed;
    }

    public void OnExit()
    {
        this.GetModel<IEntityModel>().MoveDelta = Vector2.zero;
    }

    protected override void OnInit() { }
}
```

### 6.3 FsmAttackState

```csharp
public class FsmAttackState : AbstractSystem, IFSMState
{
    public string AnimationName => "Attack";
    public PlayerStateType StateType => PlayerStateType.Attack;

    private float _elapsed;
    private bool _hitChecked;
    private const float HitCheckTime = 0.25f;
    private const float AttackDuration = 0.5f;

    public void OnEnter()
    {
        _elapsed = 0f;
        _hitChecked = false;
    }

    public void OnUpdate(float dt)
    {
        _elapsed += dt;

        if (!_hitChecked && _elapsed >= HitCheckTime)
        {
            _hitChecked = true;
            this.SendEvent(new RequestAttackHitCheckEvent());   // ← 本地事件总线
        }

        if (_elapsed >= AttackDuration)
            this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
    }

    public void OnFixUpdate(float dt) { }
    public void OnExit() { }

    protected override void OnInit() { }
}
```

### 6.4 FsmHurtState

```csharp
public class FsmHurtState : AbstractSystem, IFSMState
{
    public string AnimationName => "Hurt";
    public PlayerStateType StateType => PlayerStateType.Hurt;

    private float _elapsed;
    private const float HurtDuration = 0.4f;

    public void OnEnter() { _elapsed = 0f; }

    public void OnUpdate(float dt)
    {
        _elapsed += dt;
        if (_elapsed >= HurtDuration)
        {
            var combat = this.GetModel<ICombatModel>();
            if (combat.IsDead.Value)
            {
                // 死亡暂回 Idle，后续可加 DeadState
                this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
            }
            else
            {
                this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
            }
        }
    }

    public void OnFixUpdate(float dt) { }
    public void OnExit() { }

    protected override void OnInit() { }
}
```

---

## 七、IFSMSystem 接口

```csharp
public interface IFSMSystem : ISystem
{
    IFSMState _currentState { get; }
    void Update(float deltaTime);
    void FixUpdate(float deltaTime);
    void ChangeState<T>() where T : class, IFSMState;
}
```

---

## 八、FSMSystem 实现

```csharp
public class FSMSystem : AbstractSystem, IFSMSystem
{
    public IFSMState _currentState { get; private set; }
    private IEntityModel _entityModel;

    protected override void OnInit()
    {
        _currentState = this.GetSystem<FsmIdleState>();   // ← 本地 IoC
        _entityModel = this.GetModel<IEntityModel>();      // ← 本地 IoC
    }

    public void Update(float dt) => _currentState?.OnUpdate(dt);
    public void FixUpdate(float dt) => _currentState?.OnFixUpdate(dt);

    public void ChangeState<T>() where T : class, IFSMState
    {
        var newState = this.GetSystem<T>();                // ← 本地 IoC

        if (_currentState != null)
            _currentState.OnExit();

        _currentState = newState;
        _currentState.OnEnter();

        _entityModel._currentState.Value = _currentState.StateType;

        this.SendEvent(new PlayerStateChangedEvent         // ← 本地事件总线
        {
            StateType = _currentState.StateType,
            AnimationName = _currentState.AnimationName
        });
    }
}
```

> **一行都没改。** 和旧版全局单例时代的代码完全一致。唯一区别是 `this.GetSystem<T>()` 现在从实体自己的 IoC 容器取，而不是全局容器。

---

## 九、Command 层

Command 代码**完全不变**，仍然走 `new T()` 无参构造：

```csharp
public class TryMoveCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();             // ← 从当前架构取
        if (fsm._currentState.StateType != PlayerStateType.Move)
            fsm.ChangeState<FsmMoveState>();
    }
}

public class TryIdleCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();
        if (fsm._currentState.StateType != PlayerStateType.Idle)
            fsm.ChangeState<FsmIdleState>();
    }
}

public class TryAttackCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();
        if (fsm._currentState.StateType != PlayerStateType.Attack)
            fsm.ChangeState<FsmAttackState>();
    }
}

public class TryHurtCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();
        var combat = this.GetModel<ICombatModel>();

        if (combat.IsDead.Value) return;
        if (fsm._currentState.StateType == PlayerStateType.Hurt) return;

        fsm.ChangeState<FsmHurtState>();
    }
}
```

> `SendCommand<T>()` 走的是调用方的架构。PlayerController 发 Command → Command 的架构 = PlayerArchitecture → `GetSystem<IFSMSystem>()` 返回 Player 的 FSMSystem。EnemyController 发 Command → Command 的架构 = EnemyArchitecture → 返回 Enemy 的 FSMSystem。**同一个 Command 类，不同上下文，自动操作不同的实体。**

---

## 十、PlayerController

```csharp
public class PlayerController : MonoBehaviour, IController
{
    private PlayerArchitecture _architecture;
    private IInputUtility _inputUtility;
    private IFSMSystem _fsmSystem;
    private IEntityModel _entityModel;
    private Rigidbody2D _rigidbody2D;
    private bool _prevAttack;

    public IAchitecture GetArchitecture() => _architecture;

    public void Awake()
    {
        _architecture = new PlayerArchitecture(RogueLikeGame.Interface);

        _inputUtility = this.GetUtility<IInputUtility>();   // 回退到全局
        _fsmSystem = this.GetSystem<IFSMSystem>();           // 本地
        _entityModel = this.GetModel<IEntityModel>();        // 本地

        _rigidbody2D = this.GetComponent<Rigidbody2D>();
        _inputUtility.Awake();

        this.RegisterEvent<RequestAttackHitCheckEvent>(e =>
        {
            PerformAttackHitCheck();
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    public void OnEnable()
    {
        _inputUtility.Enable();
    }

    public void Update()
    {
        var input = this.GetUtility<IInputUtility>();
        var currentState = _fsmSystem._currentState.StateType;
        bool attackPressed = input.Attack && !_prevAttack;
        bool hasMoveInput = Mathf.Abs(input.Move.x) > 0.1f || Mathf.Abs(input.Move.y) > 0.1f;

        if (currentState != PlayerStateType.Hurt && currentState != PlayerStateType.Attack)
        {
            if (attackPressed)
                this.SendCommand<TryAttackCommand>();
            else if (hasMoveInput && currentState != PlayerStateType.Move)
                this.SendCommand<TryMoveCommand>();
            else if (!hasMoveInput && currentState == PlayerStateType.Move)
                this.SendCommand<TryIdleCommand>();
        }

        _prevAttack = input.Attack;
        _fsmSystem.Update(Time.deltaTime);
    }

    public void FixedUpdate()
    {
        _fsmSystem.FixUpdate(Time.fixedDeltaTime);
        _rigidbody2D.velocity = _entityModel.MoveDelta;
    }

    public void OnDisable()
    {
        _inputUtility.Disable();
    }

    private void PerformAttackHitCheck()
    {
        var combat = this.GetModel<ICombatModel>();
        float attackRange = 1.5f;
        Vector3 attackCenter = transform.position + transform.right * 0.8f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRange);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var enemy = hit.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.TakeDamage(combat.AttackPower.Value);
                break;
            }
        }
    }
}
```

---

## 十一、EnemyController

```csharp
public class EnemyController : MonoBehaviour, IController
{
    private EnemyArchitecture _architecture;
    private IFSMSystem _fsmSystem;
    private IEntityModel _entityModel;
    private ICombatModel _combatModel;
    private Rigidbody2D _rigidbody2D;

    public IAchitecture GetArchitecture() => _architecture;

    public void Awake()
    {
        _architecture = new EnemyArchitecture(RogueLikeGame.Interface);

        _fsmSystem = this.GetSystem<IFSMSystem>();          // 本地
        _entityModel = this.GetModel<IEntityModel>();        // 本地
        _combatModel = this.GetModel<ICombatModel>();        // 本地
        _entityModel.MoveSpeed = 3f;

        _rigidbody2D = this.GetComponent<Rigidbody2D>();

        this.RegisterEvent<RequestAttackHitCheckEvent>(e =>
        {
            PerformAttackHitCheck();
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    public void TakeDamage(int rawDamage)
    {
        if (_combatModel.IsDead.Value) return;

        var combatSystem = this.GetSystem<ICombatSystem>(); // 回退到全局
        combatSystem.ApplyDamage(_combatModel, rawDamage);

        if (!_combatModel.IsDead.Value)
            this.SendCommand<TryHurtCommand>();
        else
            Die();
    }

    private void Die()
    {
        GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject, 1f);
    }
}
```

> Enemy 的 Update / FixedUpdate 逻辑和 Player 一致。输入来源从键盘改为 AI 驱动即可。

---

## 十二、事件隔离

### 本地事件（实体内）

`FSMSystem.ChangeState` 发送 `PlayerStateChangedEvent`，`FsmAttackState.OnUpdate` 发送 `RequestAttackHitCheckEvent`。它们走的是 `EntityArchitecture` 的本地事件总线。

```
Player 的 FSMSystem.ChangeState("Attack")
  → PlayerArchitecture.SendEvent(PlayerStateChangedEvent)
  → PlayerAnimationController（订阅了 PlayerArchitecture）✓ 收到
  → EnemyAnimationController（订阅的是 EnemyArchitecture）✗ 收不到  ← 天然隔离
```

### 全局事件（跨实体）

`CombatSystem.ApplyDamage` 发送 `DamageEvent`。`CombatSystem` 注册在全局 IoC，它的 `SendEvent` 走全局总线。

```
CombatSystem.ApplyDamage()
  → RogueLikeGame.Interface.SendEvent(DamageEvent)
  → CombatUIController（订阅了全局架构）✓ 收到
```

### PlayerStateChangedEvent

```csharp
public class PlayerStateChangedEvent
{
    public PlayerStateType StateType { get; set; }
    public string AnimationName { get; set; }
}
```

> **不需要 EntityId**。事件在本地总线内传播，天然不会跨实体。

---

## 十三、动画控制器

```csharp
public class PlayerAnimationController : MonoBehaviour, IController
{
    private Animator _animator;

    public IAchitecture GetArchitecture()
    {
        // 从同一个 GameObject 上的 PlayerController 获取架构
        return GetComponent<PlayerController>().GetArchitecture();
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    void Start()
    {
        this.RegisterEvent<PlayerStateChangedEvent>(e =>
        {
            _animator.CrossFade(e.AnimationName, 0.1f);
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }
}
```

> `GetArchitecture()` 返回和 `PlayerController` 相同的 `PlayerArchitecture` 实例，所以订阅的是同一个本地事件总线。

---

## 十四、改动清单

| 文件 | 改动 |
|------|------|
| **新建** `EntityArchitecture.cs` | 非单例 `IAchitecture`，含本地 IOC + 父级回退 |
| **新建** `PlayerArchitecture.cs` | 注册 Player 的 Model / System / FSM |
| **新建** `EnemyArchitecture.cs` | 注册 Enemy 的 Model / System / FSM |
| `RogueLikeGame.cs` | Init() 删掉 FSM 状态 / FSMSystem / PlayerModel 的注册，只保留 IInputUtility + ICombatSystem |
| `PlayerModel.cs` | `IPlayerModel` → `IEntityModel`，`PlayerModel` → `EntityModel` |
| `FSMSystem.cs` | `_playerModel` 类型改为 `IEntityModel` |
| `FSMState.cs` | 状态类内部 `GetModel<IPlayerModel>()` → `GetModel<IEntityModel>()` |
| `PlayerController.cs` | 创建 `PlayerArchitecture`，GetArchitecture() 返回它 |
| `PlayerAnimationController.cs` | GetArchitecture() 从 PlayerController 获取 |
| `EnemyController.cs` | 创建 `EnemyArchitecture`，完整 FSM + TakeDamage |
| Command 各文件 | **不改** |
| `IFSMSystem` | **不改** |
| `IFSMState` | **不改** |

---

## 十五、设计要点总结

1. **架构统一** —— `EntityArchitecture` 的 API 和 `Architecture<T>` 完全一致，同一套 Register / Get / Send 规则
2. **代码不动** —— FSM 状态类、FSMSystem、Command 全部原样复用，只是注册位置从全局移到实体自己的容器
3. **查找回退** —— 实体容器没有的（如 `IInputUtility`），自动回退到全局容器
4. **事件天然隔离** —— 每个实体有自己的事件总线，`PlayerStateChangedEvent` 不需要 `EntityId`
5. **全局 IoC 只放共享资源** —— `IInputUtility`、`ICombatSystem` 等真正的跨实体服务
6. **每个实体持有独立实例** —— Player 和 Enemy 各有自己的 FSM 状态对象、Model 数据，互不干扰
