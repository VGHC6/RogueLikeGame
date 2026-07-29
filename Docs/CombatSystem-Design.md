# 战斗系统设计文档

## 前置：架构约束

战斗系统遵守 MVCS 分层规则。架构采用 `EntityArchitecture` 模式（详见 `FSM-Architecture-Design.md`）：

- 每个实体持有自己的 `EntityArchitecture`，内含独立的 Model、FSM、事件总线
- 全局 `RogueLikeGame.Interface` 只放共享资源（`IInputUtility`、`ICombatSystem`）
- 实体容器的查找回退到全局容器

---

## 一、对象归属

| 对象 | 归属 | 原因 |
|------|------|------|
| `IInputUtility` | 全局 IoC | 输入设备只有一套 |
| `ICombatSystem` | 全局 IoC | 伤害公式纯计算，无实例状态 |
| `IEntityModel` | 实体架构 | 每个实体的移动数据独立 |
| `ICombatModel` | 实体架构 | 每个实体的 HP/攻击力独立 |
| `IFSMSystem` + 状态类 | 实体架构 | 每个实体的 FSM 实例独立 |

---

## 二、ICombatModel

```csharp
public interface ICombatModel : IModel
{
    BindableProperty<int> CurrentHp { get; }
    BindableProperty<int> MaxHp { get; }
    BindableProperty<int> AttackPower { get; }
    BindableProperty<int> DefensePower { get; }
    BindableProperty<bool> IsDead { get; }
}

public class CombatModel : AbstractModel, ICombatModel
{
    public BindableProperty<int> CurrentHp { get; } = new BindableProperty<int>();
    public BindableProperty<int> MaxHp { get; } = new BindableProperty<int>();
    public BindableProperty<int> AttackPower { get; } = new BindableProperty<int>();
    public BindableProperty<int> DefensePower { get; } = new BindableProperty<int>();
    public BindableProperty<bool> IsDead { get; } = new BindableProperty<bool>();

    protected override void OnInit()
    {
        MaxHp.Value = 100;
        CurrentHp.Value = 100;
        AttackPower.Value = 15;
        DefensePower.Value = 5;
        IsDead.Value = false;
    }
}
```

> 玩家和敌人各自在自己的 `EntityArchitecture` 里注册 `CombatModel`，HP 互不干扰。如需差异化属性，在 `OnInit` 前设置，或子类化覆盖。

---

## 三、ICombatSystem（全局）

伤害公式纯计算，不区分实体，注册在全局 IoC。

```csharp
public struct DamageResult
{
    public int FinalDamage;
    public bool IsDead;
}

public interface ICombatSystem : ISystem
{
    /// <summary>
    /// 受击处理。targetCombat 是哪方的 CombatModel，就扣哪方的血。
    /// </summary>
    DamageResult ApplyDamage(ICombatModel targetCombat, int rawDamage);
}

public class CombatSystem : AbstractSystem, ICombatSystem
{
    public DamageResult ApplyDamage(ICombatModel targetCombat, int rawDamage)
    {
        int finalDamage = Mathf.Max(1, rawDamage - targetCombat.DefensePower.Value);
        targetCombat.CurrentHp.Value = Mathf.Max(0, targetCombat.CurrentHp.Value - finalDamage);

        bool dead = targetCombat.CurrentHp.Value <= 0;
        targetCombat.IsDead.Value = dead;

        // 全局事件总线 → UI 层订阅
        this.SendEvent(new DamageEvent
        {
            RawDamage = rawDamage,
            FinalDamage = finalDamage,
            IsDead = dead
        });

        return new DamageResult { FinalDamage = finalDamage, IsDead = dead };
    }

    protected override void OnInit() { }
}
```

> `ApplyDamage` 接收目标 `ICombatModel` 参数——调用方传入自己实体的 CombatModel。事件走全局总线，UI 订阅全局架构即可。

---

## 四、事件

### RequestAttackHitCheckEvent

```csharp
/// <summary>
/// 攻击判定事件。FsmAttackState 到达判定帧时发送（本地事件总线）。
/// Controller 订阅后调用 PerformAttackHitCheck() 执行 Physics2D 碰撞检测。
/// </summary>
public class RequestAttackHitCheckEvent { }
```

### DamageEvent

```csharp
/// <summary>
/// CombatSystem.ApplyDamage 发送（全局事件总线）。
/// UI 层订阅用来弹出伤害数字、更新血条等。
/// </summary>
public class DamageEvent
{
    public int RawDamage { get; set; }
    public int FinalDamage { get; set; }
    public bool IsDead { get; set; }
}
```

### PlayerStateChangedEvent

```csharp
/// <summary>
/// FSMSystem.ChangeState 发送（本地事件总线）。
/// 实体动画控制器订阅。
/// </summary>
public class PlayerStateChangedEvent
{
    public PlayerStateType StateType { get; set; }
    public string AnimationName { get; set; }
}
```

---

## 五、完整数据流 —— 一次攻击

```
用户按 J 键
  │
  ├─ 1. PlayerController.Update() 检测到攻击输入
  │     └─ this.SendCommand<TryAttackCommand>()    ← 走 PlayerArchitecture
  │
  ├─ 2. TryAttackCommand.OnExcute()
  │     └─ fsm.ChangeState<FsmAttackState>()        ← fsm 来自 PlayerArchitecture
  │
  ├─ 3. FsmAttackState.OnUpdate() 计时到判定帧
  │     └─ this.SendEvent(new RequestAttackHitCheckEvent())   ← PlayerArchitecture 本地事件
  │
  ├─ 4. PlayerController 收到事件 → PerformAttackHitCheck()
  │     └─ Physics2D 检测到 Enemy → enemy.TakeDamage(playerAttackPower)
  │
  ├─ 5. EnemyController.TakeDamage()
  │     ├─ GetSystem<ICombatSystem>()                    ← 回退到全局
  │     │     .ApplyDamage(_combatModel, rawDamage)      ← 传入 Enemy 自己的 CombatModel
  │     └─ SendCommand<TryHurtCommand>()                 ← 走 EnemyArchitecture
  │
  └─ 6. CombatUIController 收到 DamageEvent（全局总线）
        └─ 更新血条、弹出伤害数字


敌人攻击玩家（同理）：

  Enemy AI 驱动
  │
  ├─ EnemyController.SendCommand<TryAttackCommand>()     ← 走 EnemyArchitecture
  ├─ FsmAttackState → this.SendEvent(new RequestAttackHitCheckEvent())  ← Enemy 本地事件
  ├─ EnemyController 收到事件 → PerformAttackHitCheck()
  │     └─ 命中 Player → player.TakeDamage(enemyAttackPower)
  │
  └─ PlayerController.TakeDamage()
        ├─ GetSystem<ICombatSystem>().ApplyDamage(playerCombatModel, rawDamage)
        └─ SendCommand<TryHurtCommand>()                  ← 走 PlayerArchitecture
```

---

## 六、PerformAttackHitCheck

`PerformAttackHitCheck()` 是 Controller 层的私有方法，由 `RequestAttackHitCheckEvent` 事件触发，执行 Physics2D 碰撞检测并完成跨实体伤害传递。

### 6.1 触发链路

```
FsmAttackState.OnUpdate()
  └─ 计时到判定帧 (HitCheckTime = 0.25s)
      └─ this.SendEvent(new RequestAttackHitCheckEvent())   ← 本地事件总线
          └─ Controller 订阅回调
              └─ PerformAttackHitCheck()
```

### 6.2 PlayerController 实现

```csharp
// Awake() 中注册
this.RegisterEvent<RequestAttackHitCheckEvent>(e =>
{
    PerformAttackHitCheck();
}).UnRegisterWhenGameObjectDestroyed(gameObject);

private void PerformAttackHitCheck()
{
    var combat = this.GetModel<ICombatModel>();                // 本地 IoC → Player 攻击力
    float attackRange = 1.5f;
    Vector3 attackCenter = transform.position + transform.right * 0.8f;

    Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRange);
    foreach (var hit in hits)
    {
        if (hit.gameObject == gameObject) continue;           // 排除自己

        var enemy = hit.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.TakeDamage(combat.AttackPower.Value);
            break;                                            // 单次只命中一个目标
        }
    }
}
```

### 6.3 EnemyController 实现

对称实现，目标类型换为 `PlayerController`：

```csharp
this.RegisterEvent<RequestAttackHitCheckEvent>(e =>
{
    PerformAttackHitCheck();
}).UnRegisterWhenGameObjectDestroyed(gameObject);

private void PerformAttackHitCheck()
{
    var combat = this.GetModel<ICombatModel>();
    float attackRange = 1.5f;
    Vector3 attackCenter = transform.position + transform.right * 0.8f;

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
```

### 6.4 设计要点

| 要点 | 说明 |
|------|------|
| 事件与函数分离 | `RequestAttackHitCheckEvent` 是信号，`PerformAttackHitCheck()` 是处理函数 |
| 攻击力来源 | `this.GetModel<ICombatModel>()` 走本地 IoC，不随事件传递 |
| attackCenter | 角色前方偏移 0.8f，攻击半径 1.5f，覆盖前方扇形区域 |
| 单目标 | `break` 保证一次攻击只命中首个检测到的目标 |
| 防自伤 | `hit.gameObject == gameObject` 排除自身碰撞体 |
| Physics API 隔离 | 全项目唯一使用 `Physics2D` 的代码，隔离在 Controller 的私有方法中 |

---

## 七、跨实体通信

跨实体不通过事件（事件是本地总线），而是通过 Controller 层直接方法调用：

```
RequestAttackHitCheckEvent 触发
  → Controller.PerformAttackHitCheck()
      → Physics2D.OverlapCircleAll()
          → hit.GetComponent<EnemyController>()
              → enemy.TakeDamage(amount)
                  → GetSystem<ICombatSystem>().ApplyDamage(_combatModel, rawDamage)  ← 回退到全局
                  → SendCommand<TryHurtCommand>()                                    ← 走 Enemy 本地架构
```

> `TakeDamage` 是跨实体通信的唯一入口。调用方通过 Physics2D 检测拿到目标 Controller，直接调用目标的方法，操作目标的 CombatModel 和 FSMSystem。不需要跨实体事件。

---

## 八、FsmHurtState

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
            var combat = this.GetModel<ICombatModel>();     // ← 本地 IoC
            if (combat.IsDead.Value)
                this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
            else
                this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
        }
    }

    public void OnFixUpdate(float dt) { }
    public void OnExit() { }

    protected override void OnInit() { }
}
```

---

## 九、TryHurtCommand

```csharp
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

---

## 十、CombatUIController

```csharp
public class CombatUIController : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    [SerializeField] private Slider _healthBar;

    private void Awake()
    {
        this.RegisterEvent<DamageEvent>(e =>
        {
            // 收到任何 DamageEvent（全局总线）
            Debug.Log($"Damage: {e.FinalDamage}, Dead: {e.IsDead}");
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }
}
```

---

## 十一、建议开发顺序

| 步骤 | 内容 |
|------|------|
| 1 | 新建 `EntityArchitecture.cs` |
| 2 | 新建 `PlayerArchitecture.cs`、`EnemyArchitecture.cs` |
| 3 | 改 `PlayerModel.cs`（`IPlayerModel` → `IEntityModel`） |
| 4 | 改 `RogueLikeGame.cs`（删 FSM 相关注册） |
| 5 | 改 `FSMSystem.cs`（`_playerModel` 类型 → `IEntityModel`） |
| 6 | 改 `FSMState.cs`（`IPlayerModel` → `IEntityModel`） |
| 7 | 改 `PlayerController.cs`（创建 PlayerArchitecture） |
| 8 | 改 `PlayerAnimationController.cs`（从 PlayerController 获取架构） |
| 9 | 重写 `EnemyController.cs`（创建 EnemyArchitecture + 完整 FSM） |
| 10 | 新建 `CombatModel.cs` + `CombatSystem.cs` |
| 11 | 测试：玩家移动、攻击、受击，敌人行为 |
