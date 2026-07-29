# 敌人状态机与状态切换实现方案

## 复用分析

现有 FSM 基础设施完全通用，以下是复用情况：

| 组件 | 复用? | 原因 |
|------|-------|------|
| `IFSMState` 接口 | 复用 | 通用接口，零改动 |
| `IFSMSystem` / `FSMSystem` | 复用 | 已注册在 EnemyArchitecture，完全通用 |
| `FsmIdleState` | **复用** | 纯占位，无任何依赖 |
| `FsmMoveState` | **不复用** | `OnFixUpdate` 读 `IInputUtility`（键盘），敌人需要 AI 驱动 |
| `FsmAttackState` | **复用** | 纯时间驱动，通过 `this.GetSystem<IFSMSystem>()` 解析到实体自己的 FSM |
| `FsmHurtState` | **复用** | 纯时间驱动，同上 |
| `TryIdleCommand` | **复用** | `ChangeState<FsmIdleState>()`，已注册 |
| `TryMoveCommand` | **不复用** | `ChangeState<FsmMoveState>()`，敌人不用 FsmMoveState |
| `TryAttackCommand` | **复用** | `ChangeState<FsmAttackState>()`，已注册 |
| `TryHurtCommand` | **复用** | `ChangeState<FsmHurtState>()`，已注册 |
| `PlayerStateChangedEvent` | **复用** | 纯数据类，每个实体独立事件总线，不互扰 |
| `PlayerAnimationController` | **替换为 Utility** | 改为 `IAnimationUtility`，对标 `IInputUtility` 模式，对接 Unity Animator API |
| `IEntityModel` | **扩展** | 新增 `Position` 属性，不改现有属性 |
| `EnemyIdleState`（EnemyState.cs） | **删除** | 未被使用，复用 `FsmIdleState` 即可 |

---

## 架构设计

### 分层职责

| 层 | 职责 | 已有示例 | 新增 |
|----|------|---------|------|
| **Utility** | 对接外部数据源（Unity API、输入） | `IInputUtility` | `IEnemyAIUtility`、`IAnimationUtility` |
| **Model** | 实体数据 | `IEntityModel` / `ICombatModel` | `PlayerEntityModel` / `EnemyEntityModel` |
| **System** | 纯逻辑，不碰 Unity 场景对象 | `FSMSystem`、`FsmMoveState` | `EnemyMoveState` |
| **Command** | 状态切换动作 | `TryIdleCommand` 等 | `TryEnemyMoveCommand` |
| **Controller** | 桥接 Unity 生命周期，注入依赖 | `PlayerController` | `EnemyController` 填充 AI 逻辑 |

### 关键设计决策

**1. 敌人追踪用 Utility 而非 System 层直接查找**

对标 `FsmMoveState` 读 `IInputUtility` 的模式，敌人追踪也走 Utility：

```
玩家：FsmMoveState → IInputUtility.Move      → 键盘输入
敌人：EnemyMoveState → IEnemyAIUtility        → 场景中玩家位置
```

System 层不碰 `GameObject.FindWithTag`，不碰 `Transform`，保持纯逻辑。

**2. 动画控制器做成 Utility**

对标 `IInputUtility` 对接键盘的模式，`IAnimationUtility` 对接 Unity Animator API。Controller 在 `InitArchitecture` 中调用 `Init(animator)` 注入依赖。Player 和 Enemy 注册同一个 `AnimationUtility`，不需要 MonoBehaviour 组件。

**3. Model 按实体类型分子类**

```csharp
IEntityModel              ← 接口，FSM 和 Command 只依赖这个
  └─ PlayerEntityModel    ← 玩家独有属性（体力等）
  └─ EnemyEntityModel     ← 敌人独有属性（追击范围、攻击范围等）
```

各自 IoC 容器注册自己的实现，`GetModel<IEntityModel>()` 拿到不同的实例。

**4. IEntityModel 新增 Position 属性**

System 层 State 没有 Transform，需要通过 Model 获取自身位置来计算追踪方向。由 Controller 在 FixedUpdate 中同步。

---

## 需要新建的文件

### 1. `Assets/Scripts/Utility/IEnemyAIUtility.cs`

```csharp
using UnityEngine;

/// <summary>
/// 敌人 AI 工具。提供追踪目标的方向信息。
/// </summary>
public interface IEnemyAIUtility : IUtility
{
    bool HasTarget { get; }
    Vector3 TargetPosition { get; }
}

public class EnemyAIUtility : IEnemyAIUtility
{
    private Transform _target;

    public bool HasTarget => _target != null;
    public Vector3 TargetPosition => _target != null ? _target.position : Vector3.zero;

    public void Init()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) _target = player.transform;
    }
}
```

### 2. `Assets/Scripts/Model/PlayerEntityModel.cs`

```csharp
using UnityEngine;

/// <summary>
/// 玩家实体模型。继承 EntityModel，可扩展玩家独有属性。
/// </summary>
public class PlayerEntityModel : EntityModel
{
    // 玩家独有属性示例
    public int Stamina { get; set; } = 100;
    public int MaxStamina { get; set; } = 100;

    protected override void OnInit()
    {
        MoveSpeed = 5f;
        Stamina = MaxStamina;
    }
}
```

### 3. `Assets/Scripts/Model/EnemyEntityModel.cs`

```csharp
using UnityEngine;

/// <summary>
/// 敌人实体模型。继承 EntityModel，可扩展敌人独有属性。
/// </summary>
public class EnemyEntityModel : EntityModel
{
    // 敌人独有属性
    public float ChaseRange { get; set; } = 5f;
    public float AttackRange { get; set; } = 1.5f;

    protected override void OnInit()
    {
        MoveSpeed = 3f;
    }
}
```

### 4. `Assets/Scripts/System/Enemy/EnemyMoveState.cs`

```csharp
using UnityEngine;

/// <summary>
/// 敌人移动状态。通过 IEnemyAIUtility 获取追踪方向（对标 FsmMoveState 读 IInputUtility）。
/// </summary>
public class EnemyMoveState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Move";
    public PlayerStateType StateType { get; } = PlayerStateType.Move;

    public void OnEnter() { }

    public void OnUpdate(float datetime) { }

    public void OnFixUpdate(float datetime)
    {
        var model = this.GetModel<IEntityModel>();
        var ai = this.GetUtility<IEnemyAIUtility>();

        if (ai.HasTarget)
        {
            Vector2 direction = ((Vector2)(ai.TargetPosition - model.Position)).normalized;
            model.MoveDelta = direction * model.MoveSpeed;
        }
        else
        {
            model.MoveDelta = Vector2.zero;
        }
    }

    public void OnExit()
    {
        var model = this.GetModel<IEntityModel>();
        model.MoveDelta = Vector2.zero;
    }

    protected override void OnInit() { }
}
```

> 自身位置通过 `model.Position` 获取，由 Controller 每帧同步。

### 5. `Assets/Scripts/Command/Enemy/TryEnemyMoveCommand.cs`

```csharp
/// <summary>
/// 敌人切换到移动状态。
/// </summary>
public class TryEnemyMoveCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();
        if (fsm._currentState.StateType != PlayerStateType.Move)
            fsm.ChangeState<EnemyMoveState>();
    }
}
```

### 6. `Assets/Scripts/Utility/IAnimationUtility.cs`

```csharp
using UnityEngine;

/// <summary>
/// 实体动画工具。对标 IInputUtility 模式，对接 Unity Animator API。
/// 通过 this.GetArchitecture() 注册事件 — 前提：RegisterUtility 调了 SetArchitecture（见下文修改 7）。
/// </summary>
public interface IAnimationUtility : IUtility
{
    void Init(Animator animator);
}

public class AnimationUtility : IAnimationUtility
{
    private Animator _animator;

    public void Init(Animator animator)
    {
        _animator = animator;
        this.GetArchitecture().RegisterEvent<PlayerStateChangedEvent>(OnStateChanged);
    }

    void OnStateChanged(PlayerStateChangedEvent e)
    {
        _animator?.CrossFade(e.AnimationName, 0.1f);
    }
}
```

### 7. `Assets/Scripts/Architecture/IAchitecture.cs` — EntityArchitecture.RegisterUtility

`RegisterUtility` 没有调 `SetArchitecture`，导致 Utility 中 `GetArchitecture()` 返回 null。加一行对标 `RegisterSystem`：

```diff
  public void RegisterUtility<T>(T instance) where T : IUtility
  {
+     instance.SetArchitecture(this);
      _container.Register<T>(instance);
  }
```

> 做成 Utility 而非 MonoBehaviour 的好处：
> - 对标 `IInputUtility` 模式，所有对接 Unity API 的服务统一归到 Utility 层
> - 不需要在 GameObject 上额外挂组件，少一个脚本
> - 注册在实体自己的 IoC 中，随架构销毁自动释放，无需手动 `OnDestroy` 取消订阅
> - 不再有"同一 GameObject 上多个 IController 导致 GetComponent 不确定"的问题

---

## 需要修改的文件

### 8. `Assets/Scripts/Model/IEntityModel.cs`

新增 `Position` 属性，供 State 层获取自身位置：

```diff
 public interface IEntityModel : IModel
 {
     BindableProperty<PlayerStateType> _currentState { get; }
     Vector2 MoveDelta { get; set; }
     float MoveSpeed { get; set; }
+    Vector3 Position { get; set; }
 }
```

同时 `EntityModel` 实现类加一行：

```diff
 public class EntityModel : AbstractModel, IEntityModel
 {
     public BindableProperty<PlayerStateType> _currentState { get; } = new() { Value = PlayerStateType.Idle };
     public Vector2 MoveDelta { get; set; }
     public float MoveSpeed { get; set; } = 5f;
+    public Vector3 Position { get; set; }

     protected override void OnInit() { }
 }
```

### 9. `Assets/Scripts/EntityArchitecture/PlayerArchitecture.cs`

替换为 `PlayerEntityModel`：

```diff
  public PlayerArchitecture(IAchitecture parent) : base(parent)
  {
-     RegisterModel<IEntityModel>(new EntityModel());
+     RegisterModel<IEntityModel>(new PlayerEntityModel());
      RegisterModel<ICombatModel>(new CombatModel());
      RegisterSystem<FsmIdleState>(new FsmIdleState());
      RegisterSystem<FsmMoveState>(new FsmMoveState());
      RegisterSystem<FsmAttackState>(new FsmAttackState());
      RegisterSystem<FsmHurtState>(new FsmHurtState());
      RegisterSystem<IFSMSystem>(new FSMSystem());
+     RegisterUtility<IAnimationUtility>(new AnimationUtility());
      InitEntities();
  }
```

### 10. `Assets/Scripts/EntityArchitecture/EnemyArchitecture.cs`

替换为 `EnemyEntityModel`，替换 `FsmMoveState` → `EnemyMoveState`，新增 `IEnemyAIUtility` 和 `IAnimationUtility`：

```diff
  public EnemyArchitecture(IAchitecture parent) : base(parent)
  {
-     RegisterModel<IEntityModel>(new EntityModel());
+     RegisterModel<IEntityModel>(new EnemyEntityModel());
      RegisterModel<ICombatModel>(new CombatModel());
      RegisterSystem<FsmIdleState>(new FsmIdleState());
-     RegisterSystem<FsmMoveState>(new FsmMoveState());
+     RegisterSystem<EnemyMoveState>(new EnemyMoveState());
      RegisterSystem<FsmAttackState>(new FsmAttackState());
      RegisterSystem<FsmHurtState>(new FsmHurtState());
      RegisterSystem<IFSMSystem>(new FSMSystem());
+     RegisterUtility<IEnemyAIUtility>(new EnemyAIUtility());
+     RegisterUtility<IAnimationUtility>(new AnimationUtility());
      InitEntities();
  }
```

### 11. `Assets/Scripts/ViewController/EnemyController.cs`

全量改写。核心变化：
- `Update()` 填充 AI 状态切换逻辑
- `FixedUpdate()` 应用移动、同步 Position、翻转朝向
- `InitArchitecture()` 初始化 `IEnemyAIUtility` 和 `IAnimationUtility`
- `TakeDamage()` 加 `GetArchitecture()` 保护
- AI 参数从 `EnemyEntityModel` 读取

```csharp
using UnityEngine;

public class EnemyController : MonoBehaviour, IController
{
    private EnemyArchitecture _architecture;
    private IFSMSystem _fsmSystem;
    private EnemyEntityModel _entityModel;
    private ICombatModel _combatModel;
    private IEnemyAIUtility _aiUtility;
    private Rigidbody2D _rigidbody2D;

    private bool _initialized;

    public IAchitecture GetArchitecture()
    {
        if (!_initialized) InitArchitecture();
        return _architecture;
    }

    private void InitArchitecture()
    {
        _architecture = new EnemyArchitecture(RogueLikeGame.Interface);
        _fsmSystem = _architecture.GetSystem<IFSMSystem>();
        _entityModel = _architecture.GetModel<IEntityModel>() as EnemyEntityModel;
        _combatModel = _architecture.GetModel<ICombatModel>();
        _aiUtility = _architecture.GetUtility<IEnemyAIUtility>() as EnemyAIUtility;
        _aiUtility.Init();

        var animUtil = _architecture.GetUtility<IAnimationUtility>() as AnimationUtility;
        animUtil.Init(GetComponent<Animator>());

        _initialized = true;

        this.RegisterEvent<RequestAttackHitCheckEvent>(e =>
        {
            PerformAttackHitCheck();
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    public void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    public void Update()
    {
        if (!_initialized) return;

        var currentState = _fsmSystem._currentState.StateType;
        float dist = Vector2.Distance(transform.position, _aiUtility.TargetPosition);

        // Hurt / Attack 期间锁定
        if (currentState != PlayerStateType.Hurt && currentState != PlayerStateType.Attack)
        {
            if (dist <= _entityModel.AttackRange)
            {
                this.SendCommand<TryAttackCommand>();
            }
            else if (dist <= _entityModel.ChaseRange)
            {
                if (currentState != PlayerStateType.Move)
                    this.SendCommand<TryEnemyMoveCommand>();
            }
            else
            {
                if (currentState != PlayerStateType.Idle)
                    this.SendCommand<TryIdleCommand>();
            }
        }

        _fsmSystem.Update(Time.deltaTime);
    }

    public void FixedUpdate()
    {
        if (!_initialized) return;

        _fsmSystem.FixUpdate(Time.fixedDeltaTime);

        _entityModel.Position = transform.position;

        if (Mathf.Abs(_entityModel.MoveDelta.x) > 0.01f)
        {
            transform.localScale = new Vector3(_entityModel.MoveDelta.x > 0 ? 1 : -1, 1, 1);
        }

        _rigidbody2D.velocity = _entityModel.MoveDelta;
    }

    public void TakeDamage(int rawDamage)
    {
        GetArchitecture(); // 确保初始化（敌人可能在被攻击时才首次激活）
        if (_combatModel.IsDead.Value) return;

        var combatSystem = this.GetSystem<ICombatSystem>();
        combatSystem.ApplyDamage(_combatModel, rawDamage);

        if (!_combatModel.IsDead.Value)
            this.SendCommand<TryHurtCommand>();
        else
            Die();
    }

    private void Die()
    {
        GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject, 0.2f);
    }

    private void PerformAttackHitCheck()
    {
        var combat = this.GetModel<ICombatModel>();
        float attackRange = 1.5f;
        int facingDir = transform.localScale.x > 0 ? 1 : -1;
        Vector3 attackCenter = transform.position + Vector3.right * facingDir * 0.8f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRange);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var player = hit.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(combat.AttackPower.Value);
                break;
            }
        }
    }
}
```

### 12. `Assets/Scripts/ViewController/PlayerController.cs`

`InitArchitecture()` 新增动画 Utility 初始化；`Update()`/`FixedUpdate()` 加保护 + 同步 Position：

```diff
+ // InitArchitecture() 末尾新增：
+ var animUtil = _architecture.GetUtility<IAnimationUtility>() as AnimationUtility;
+ animUtil.Init(GetComponent<Animator>());
+
  public void Update()
  {
+     if (!_initialized) return;
      var input = _inputUtility;
      var currentState = _fsmSystem._currentState.StateType;
      // ...
  }

  public void FixedUpdate()
  {
+     if (!_initialized) return;
      _fsmSystem.FixUpdate(Time.fixedDeltaTime);
+     _playerModel.Position = transform.position;

      if (Mathf.Abs(_playerModel.MoveDelta.x) > 0.01f)
      // ...
  }
```

---

## 文件变更汇总

| 操作 | 文件 | 说明 |
|------|------|------|
| **新建** | `Scripts/Utility/IEnemyAIUtility.cs` | AI 追踪接口 + 实现 |
| **新建** | `Scripts/Utility/IAnimationUtility.cs` | 动画播放接口 + 实现，Player/Enemy 通用 |
| **新建** | `Scripts/Model/PlayerEntityModel.cs` | 玩家专用模型 |
| **新建** | `Scripts/Model/EnemyEntityModel.cs` | 敌人专用模型 |
| **新建** | `Scripts/System/Enemy/EnemyMoveState.cs` | AI 驱动移动状态 |
| **新建** | `Scripts/Command/Enemy/TryEnemyMoveCommand.cs` | 切换到 EnemyMoveState |
| **修改** | `Scripts/Architecture/IAchitecture.cs` | RegisterUtility 加 SetArchitecture（1 行） |
| **修改** | `Scripts/Model/IEntityModel.cs` | 新增 Position 属性（1 行） |
| **修改** | `Scripts/EntityArchitecture/PlayerArchitecture.cs` | EntityModel → PlayerEntityModel，新增 IAnimationUtility |
| **修改** | `Scripts/EntityArchitecture/EnemyArchitecture.cs` | 替换 Model、State，新增 IEnemyAIUtility 和 IAnimationUtility |
| **修改** | `Scripts/ViewController/PlayerController.cs` | 初始化 AnimationUtility，加保护，同步 Position |
| **修改** | `Scripts/ViewController/EnemyController.cs` | 填充 AI 逻辑，初始化 AnimationUtility，加保护 |
| **可删除** | `Scripts/System/EnemyState.cs` | 里面的 EnemyIdleState 未被使用 |
| **可删除** | `Scripts/ViewController/PlayerAnimationController.cs` | 被 IAnimationUtility 替代 |

---

## 状态切换流程

```
                     TryEnemyMoveCommand
    ┌─────────┐     (玩家在追击范围内)     ┌──────────┐
    │  IDLE   │ ────────────────────────→ │   MOVE   │
    │ (复用   │ ←──────────────────────── │ (新建    │
    │ FsmIdle)│      TryIdleCommand       │ EnemyMove)│
    └────┬────┘      (超出追击范围)        └────┬─────┘
         │                                     │
         │ TryAttackCommand                    │ TryAttackCommand
         │ (进入攻击范围)                        │ (进入攻击范围)
         ▼                                     ▼
    ┌──────────┐                        ┌──────────┐
    │  ATTACK  │ ←──────────────────────│  ATTACK  │
    │ (复用    │    时间到自动回 Idle     │ (复用    │
    │FsmAttack)│                        │FsmAttack)│
    └────┬─────┘                        └────┬─────┘
         │                                   │
         │ TryHurtCommand                    │ TryHurtCommand
         │ (受到伤害)                         │ (受到伤害)
         ▼                                   ▼
    ┌──────────┐                        ┌──────────┐
    │  HURT    │                        │  HURT    │
    │ (复用    │                        │ (复用    │
    │FsmHurt)  │                        │FsmHurt)  │
    └──────────┘                        └──────────┘
    时间到自动回 Idle                    时间到自动回 Idle
```

## 数据流

```
EnemyController.Update()
  │
  ├─ 读 _aiUtility.TargetPosition（Utility 层）
  ├─ 读 _entityModel.AttackRange / ChaseRange（Model 层，敌人独有）
  ├─ 计算距离 → SendCommand<TryXxxCommand>()
  │
  ▼
FSMSystem.ChangeState<T>()
  │
  ├─ OnExit() 旧状态
  ├─ OnEnter() 新状态
  ├─ 更新 model._currentState
  ├─ SendEvent(PlayerStateChangedEvent)
  │
  ▼
AnimationUtility.OnStateChanged()
  └─ _animator.CrossFade(animationName)

EnemyController.FixedUpdate()
  │
  ├─ _fsmSystem.FixUpdate() → EnemyMoveState.OnFixUpdate()
  │     ├─ 读 model.Position（自身位置）
  │     ├─ 读 ai.TargetPosition（目标位置）
  │     └─ 计算 model.MoveDelta
  │
  ├─ 同步 model.Position = transform.position
  └─ 应用 _rigidbody2D.velocity = model.MoveDelta
```
