# EntityArchitecture 设计方案

## 一、问题

当前所有 Model / System / FSM 状态都注册在全局单例 `RogueLikeGame.Interface` 的 IoC 容器中：

```csharp
// RogueLikeGame.Init()
this.RegisterSystem<FsmIdleState>(new FsmIdleState());
this.RegisterSystem<IFSMSystem>(new FSMSystem());
this.RegisterModel<IPlayerModel>(new PlayerModel());
```

这意味着整个游戏只有**一套** CombatModel、**一套** FSMSystem。敌人无法拥有独立的 HP 和状态机，无法复用现有架构。

## 二、目标

让**每个实体（Player / Enemy）**都拥有自己独立的 Model、System、FSM 实例，同时复用现有类的逻辑代码。

## 三、核心思路

引入 `EntityArchitecture` —— 非单例、轻量级的 `IAchitecture` 实现。每个 IController 创建一个实例，作为该实体的"专属 IoC 口袋"。

```
全局 RogueLikeGame.Interface（只放共享资源：InputUtility）
│
├── PlayerController  ──→  Player 的 EntityArchitecture
│   ├── IEntityModel      （改名后的 PlayerModel）
│   ├── ICombatModel
│   ├── IFSMSystem
│   ├── FsmIdleState
│   ├── FsmMoveState
│   ├── FsmAttackState
│   └── FsmHurtState
│
└── EnemyController   ──→  Enemy 的 EntityArchitecture
    ├── IEntityModel
    ├── ICombatModel
    ├── IFSMSystem
    ├── FsmIdleState
    ├── FsmMoveState
    ├── FsmAttackState
    └── FsmHurtState
```

**关键点**：FSM 状态类、CombatModel、FSMSystem 的逻辑代码**一行不改**，只是每个实体拥有自己的实例。

## 四、EntityArchitecture 设计

```csharp
public abstract class EntityArchitecture : IAchitecture
{
    private IOCContainer _container = new IOCContainer();
    private ITypeEventSystem _typeEventSystem = new TypeEventSystem<EntityArchitecture>();
    private IAchitecture _parent;

    private List<IModel> _models = new List<IModel>();
    private List<ISystem> _systems = new List<ISystem>();

    public EntityArchitecture(IAchitecture parent = null)
    {
        _parent = parent;
        Init();                              // 1. 调用子类 Init（收集注册）
        foreach (var system in _systems)     // 2. 统一初始化 System
            system.Init();
        _systems.Clear();
        foreach (var model in _models)       // 3. 统一初始化 Model
            model.Init();
        _models.Clear();
    }

    protected abstract void Init();          // 子类在此注册（和 RogueLikeGame 一致）

    // Model / System / Utility：先查本地，查不到回退到 parent
    public T GetModel<T>() where T : class, IModel { ... }
    public T GetSystem<T>() where T : class, ISystem { ... }
    public T GetUtility<T>() where T : class, IUtility { ... }

    // Register：收集到 _models/_systems 列表，构造时统一 Init
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

    // Command / Event：同上
}
```

**为什么事件走本地总线？**

FSM 状态切换时发送 `PlayerStateChangedEvent`。如果走全局总线，敌人的状态变化也会触发玩家的动画控制器。本地总线天然隔离，无需在事件里加 entityId 过滤。

**跨实体通信怎么做？**

不走事件，走 Controller 层直接方法调用：

```
PlayerController.PerformAttackHitCheck()
  → Physics2D 碰撞检测
  → 找到敌人 GameObject
  → enemy.GetComponent<EnemyController>().TakeDamage(amount)
  → EnemyController 修改本地 CombatModel、发送 TryHurtCommand
```

## 五、接口重命名

`IPlayerModel` → `IEntityModel`，`PlayerModel` → `EntityModel`

原因：`FsmMoveState` 内部写死了 `this.GetModel<IPlayerModel>()`。敌人的 FSM 状态实例也需要通过这个接口解析到自己实体的移动数据。改名的目的是让接口名反映它的通用性，而不是绑定在"Player"上。

```csharp
public interface IEntityModel : IModel
{
    BindableProperty<PlayerStateType> _currentState { get; }
    Vector2 MoveDelta { get; set; }
    float MoveSpeed { get; set; }
}
```

> `PlayerStateType` 枚举（Idle / Move / Attack / Hurt）不改名，它对玩家和敌人都适用。

## 六、各文件改动清单

| 文件 | 改动 | 说明 |
|------|------|------|
| **新建** `EntityArchitecture.cs` | 新建 | 非单例 IAchitecture 实现 |
| `PlayerModel.cs` | 改 | `IPlayerModel` → `IEntityModel`，`PlayerModel` → `EntityModel` |
| `FSMSystem.cs` | 改 | `_playerModel` 类型从 `IPlayerModel` → `IEntityModel` |
| `FSMState.cs` | 改 | `this.GetModel<IPlayerModel>()` → `this.GetModel<IEntityModel>()` |
| `PlayerController.cs` | 改 | 创建 EntityArchitecture，注册本地 Model/System/FSM，GetArchitecture() 返回它 |
| `PlayerAnimationController.cs` | 改 | GetArchitecture() 改为从 PlayerController 获取 EntityArchitecture |
| `EnemyController.cs` | 重写 | 创建 EntityArchitecture，注册本地实例，添加 AI 驱动和 TakeDamage |
| `RogueLikeGame.cs` | 改 | Init() 只保留 `RegisterUtility<IInputUtility>` |
| `CombatSystem.cs` | 修 bug | ApplyDamage 补上 `CurrentHp.Value -= finalDamage`，OnInit 去掉异常 |

## 七、EnemyController 示例

```csharp
// 每种敌人类型定义一个 Architecture 子类（注册逻辑集中）
public class EnemyArchitecture : EntityArchitecture
{
    public EnemyArchitecture(IAchitecture parent) : base(parent) { }

    protected override void Init()
    {
        this.RegisterModel<IEntityModel>(new EntityModel());
        this.RegisterModel<ICombatModel>(new CombatModel());

        this.RegisterSystem<FsmIdleState>(new FsmIdleState());
        this.RegisterSystem<FsmMoveState>(new FsmMoveState());
        this.RegisterSystem<FsmAttackState>(new FsmAttackState());
        this.RegisterSystem<FsmHurtState>(new FsmHurtState());

        this.RegisterSystem<IFSMSystem>(new FSMSystem());
    }
}

public class EnemyController : MonoBehaviour, IController
{
    private EnemyArchitecture _architecture;

    public IAchitecture GetArchitecture() => _architecture;

    public void Awake()
    {
        _architecture = new EnemyArchitecture(RogueLikeGame.Interface);
        _architecture.RegisterEvent<RequestAttackHitCheckEvent>(e => PerformAttackHitCheck());
    }

    public void TakeDamage(int rawDamage)
    {
        var combat = _architecture.GetModel<ICombatModel>();
        if (combat.IsDead.Value) return;

        combat.CurrentHp.Value -= rawDamage;
        combat.IsDead.Value = combat.CurrentHp.Value <= 0;

        if (combat.IsDead.Value)
        {
            // 死亡处理
        }
        else
        {
            this.SendCommand<TryHurtCommand>();
        }
    }
}
```

## 八、PlayerController 改动要点

```csharp
public class PlayerArchitecture : EntityArchitecture
{
    public PlayerArchitecture(IAchitecture parent) : base(parent) { }

    protected override void Init()
    {
        this.RegisterModel<IEntityModel>(new EntityModel());
        this.RegisterModel<ICombatModel>(new CombatModel());

        this.RegisterSystem<FsmIdleState>(new FsmIdleState());
        this.RegisterSystem<FsmMoveState>(new FsmMoveState());
        this.RegisterSystem<FsmAttackState>(new FsmAttackState());
        this.RegisterSystem<FsmHurtState>(new FsmHurtState());

        this.RegisterSystem<IFSMSystem>(new FSMSystem());
    }
}

public class PlayerController : MonoBehaviour, IController
{
    private PlayerArchitecture _architecture;

    public IAchitecture GetArchitecture() => _architecture;

    public void Awake()
    {
        _architecture = new PlayerArchitecture(RogueLikeGame.Interface);

        _inputUtility = this.GetUtility<IInputUtility>();  // 本地没有，回退到全局
        _inputUtility.Awake();

        _architecture.RegisterEvent<RequestAttackHitCheckEvent>(e => PerformAttackHitCheck());
    }
    // ... Update / FixedUpdate 逻辑不变
}
```

## 九、Command 调度流程（不变）

Command 依然无状态，依然由 IController 发送。区别只是：

- **之前**：`this.SendCommand<TryAttackCommand>()` → 全局 Architecture 创建 Command，SetArchitecture(全局)，Command 内的 `GetSystem<IFSMSystem>()` 解析到全局单例
- **之后**：`this.SendCommand<TryAttackCommand>()` → EntityArchitecture 创建 Command，SetArchitecture(本地)，Command 内的 `GetSystem<IFSMSystem>()` 解析到本地实例

**TryAttackCommand 代码一行不改**——这是复用的关键。

## 十、总结

| 维度 | 说明 |
|------|------|
| 复用程度 | FSMState / FSMSystem / CombatModel / CombatSystem / Command 全部原样复用 |
| 新增文件 | 1 个：`EntityArchitecture.cs` |
| 改动文件 | 7 个（大部分是改名和注册位置变化） |
| 架构规则 | 不变。IController → Command → System → Model 的层级不变 |
| 事件隔离 | 本地事件总线，实体间不会串消息 |
| 跨实体通信 | Controller 层直接调用（Physics → GetComponent → 方法调用） |
