# 战斗系统设计文档

## 前置：当前架构的关键约束

战斗系统必须遵守 MVCS 分层规则（详见 `FSM-Architecture-Design.md`）：

```
ViewController (MonoBehaviour)  →  Command  →  System  →  Model
                                      ↑                      ↓
                                  只改状态               Event 向上通知
```

| 约束 | 说明 |
|------|------|
| System/Model/Command 层**不持有 Unity 引用** | 不能有 Transform、Animator、Collider、Rigidbody 等 |
| ViewController 修改状态**必须走 Command** | 不能直接调 System 的方法来改数据 |
| 下层 → 上层通知**必须用 Event 或 BindableProperty** | System 不能直接调 ViewController |
| Command **不能有状态** | 无字段、无属性 |
| FSM 状态类**继承 AbstractSystem** | 通过 IoC 容器 `GetSystem<T>()` 获取，不是 `new()` |

---

## 一、当前状态与目标

### 已有

- `PlayerStateType` 枚举含 `Idle / Attack / Move / Hurt`（`Hurt` 只定义未实现）
- `FsmAttackState` 仅等待 0.5s 后回 Idle，没有伤害判定
- `TryAttackCommand` 校验只检查"当前是否已经是 Attack"
- 没有任何血量、伤害、防御数据
- 没有敌人实体

### 目标

- 玩家有 HP / 攻击力 / 防御力数据
- 攻击到达判定帧时执行碰撞检测，命中敌人造成伤害
- 敌人有独立的行为（可后续扩展 FSM）
- 玩家可受击进入 Hurt 状态，HP 归零进入 Dead 状态
- UI 响应血量变化和伤害事件

---

## 二、新增文件清单

```
Assets/Scripts/
  Model/
    CombatModel.cs                     — 战斗数值模型（HP、攻击力、防御力）
  Event/
    DamageEvent.cs                     — 伤害结算事件（携带原始/最终伤害）
    RequestAttackHitCheckEvent.cs      — 请求 ViewController 执行攻击碰撞检测
  System/
    CombatSystem.cs                    — 战斗系统（伤害计算、Buff 介入点）
    FsmHurtState.cs                    — 受伤状态（硬直 + 自动恢复）
  Command/
    TryHurtCommand.cs                  — 进入受伤状态
  ViewController/
    CombatUIController.cs              — 血条、伤害数字、GameOver（MonoBehaviour）
    EnemyController.cs                 — 敌方单位（MonoBehaviour，轻量方案）
```

### 需要修改的文件

| 文件 | 改动 |
|------|------|
| `RogueLikeGame.cs` | 注册 CombatModel、CombatSystem、FsmHurtState |
| `FSMState.cs` | 新增 FsmAttackState 判定帧逻辑；新增 FsmHurtState |
| `PlayerModel.cs` | `PlayerStateType` 枚举加 `Dead` |
| `PlayerController.cs` | 订阅 `RequestAttackHitCheckEvent`，执行碰撞检测 |
| `FSMSystem.cs` | 无需改动（ChangeState 已通用） |

---

## 三、Model 层 —— CombatModel

纯数据层，只定义属性和默认值。**不包含任何业务逻辑。**

```csharp
// Assets/Scripts/Model/CombatModel.cs

public interface ICombatModel : IModel
{
    BindableProperty<int> HP { get; }
    BindableProperty<int> MaxHP { get; }
    BindableProperty<int> AttackPower { get; }
    BindableProperty<int> Defense { get; }
    BindableProperty<bool> IsDead { get; }
}

public class CombatModel : AbstractModel, ICombatModel
{
    public BindableProperty<int> HP { get; } = new BindableProperty<int>();
    public BindableProperty<int> MaxHP { get; } = new BindableProperty<int>();
    public BindableProperty<int> AttackPower { get; } = new BindableProperty<int>();
    public BindableProperty<int> Defense { get; } = new BindableProperty<int>();
    public BindableProperty<bool> IsDead { get; } = new BindableProperty<bool>();

    protected override void OnInit()
    {
        MaxHP.Value = 100;
        HP.Value = 100;
        AttackPower.Value = 15;
        Defense.Value = 5;
        IsDead.Value = false;
    }
}
```

### 注册

```csharp
this.RegisterModel<ICombatModel>(new CombatModel());
```

---

## 四、System 层 —— CombatSystem

战斗业务逻辑。**伤害计算放 System 而非 Model，原因：**

| 考量 | Model 放 ApplyDamage | System 放 ApplyDamage |
|------|---------------------|----------------------|
| 分层职责 | Model 混了业务规则 | Model=数据, System=逻辑 |
| Buff/护盾介入 | Model 要调其他 System（**下层调上层，违规**） | System 调其他 System（**平级调用，合规**） |
| 参数传递 | 方法参数（合规） | 方法参数（合规） |
| Command 无状态约束 | 不涉及 | 不涉及 |

```csharp
// Assets/Scripts/System/CombatSystem.cs

/// <summary>
/// 伤害结算结果。纯数据结构。
/// </summary>
public struct DamageResult
{
    public int FinalDamage;
    public bool IsDead;
}

public interface ICombatSystem : ISystem
{
    /// <summary>
    /// 受击处理。执行伤害公式，更新 HP，发送 DamageEvent。
    /// 提供给表现层（IController / 外部 MonoBehaviour）调用。
    /// 上层 → 下层方法调用，合规。参数传递不受 Command 无状态约束。
    /// </summary>
    DamageResult ApplyDamage(int rawDamage);
}

public class CombatSystem : AbstractSystem, ICombatSystem
{
    public DamageResult ApplyDamage(int rawDamage)
    {
        var combat = this.GetModel<ICombatModel>();

        int finalDamage = Mathf.Max(1, rawDamage - combat.Defense.Value);
        combat.HP.Value = Mathf.Max(0, combat.HP.Value - finalDamage);

        bool dead = combat.HP.Value <= 0;
        combat.IsDead.Value = dead;

        // System → 上层通知：Event
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

### 注册

```csharp
this.RegisterSystem<ICombatSystem>(new CombatSystem());
```

### 为什么不是 Command？

Command 有两条硬约束：**不能有状态**（无字段无属性）、**一次性执行**。伤害计算需要传入 `rawDamage` 参数——方法参数不受"不能有状态"约束，但 Command 没有自然的参数传递通道。System 的方法是标准的"上层→下层方法调用"，带参无限制，且 System 天然可以调其他 System（Buff 系统、护盾系统等），扩展性最好。

> 伤害公式 `FinalDamage = Max(1, AttackPower - Defense)` 目前很简单。将来 Buff 系统加入后，`ApplyDamage` 内可以调 `IBuffSystem` 做增减伤，全程在 System 层内部完成，不违反任何分层规则。

---

## 五、事件定义

### 4.1 攻击判定请求事件

System 层不能执行碰撞检测（无 Unity 引用），因此在判定帧发送此事件，由 ViewController 层执行。

```csharp
// Assets/Scripts/Event/RequestAttackHitCheckEvent.cs

/// <summary>
/// FsmAttackState 到达判定帧时发送。
/// ViewController 订阅并执行 Physics2D 碰撞检测。
/// </summary>
public class RequestAttackHitCheckEvent { }
```

### 4.2 伤害结算事件

伤害计算完毕后发送，供 UI 层显示伤害数字等。

```csharp
// Assets/Scripts/Event/DamageEvent.cs

/// <summary>
/// 伤害结算完毕后发送。
/// UI 层订阅用来弹出伤害数字、震屏等反馈。
/// </summary>
public class DamageEvent
{
    /// <summary>攻击方的原始攻击力</summary>
    public int RawDamage { get; set; }

    /// <summary>经过防御减免后的实际伤害</summary>
    public int FinalDamage { get; set; }

    /// <summary>本次伤害是否导致目标死亡</summary>
    public bool IsDead { get; set; }
}
```

---

## 六、命令层

### 5.1 TryHurtCommand

```csharp
// Assets/Scripts/Command/TryHurtCommand.cs

public class TryHurtCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var fsm = this.GetSystem<IFSMSystem>();
        var combat = this.GetModel<ICombatModel>();

        // 已死亡则忽略
        if (combat.IsDead.Value)
            return;

        // 已在 Hurt 状态则忽略（不可叠加受伤硬直）
        if (fsm._currentState.StateType == PlayerStateType.Hurt)
            return;

        fsm.ChangeState<FsmHurtState>();
    }
}
```

---

## 七、状态层

### 6.1 FsmAttackState 改造

原版只计时然后回 Idle。改造后加入"判定帧"概念——动画进行到攻击判定时刻，发送事件让 ViewController 做碰撞检测。

```csharp
// 在 FSMState.cs 中改造 FsmAttackState

public class FsmAttackState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Attack";
    public PlayerStateType StateType { get; } = PlayerStateType.Attack;

    private float _elapsed;
    private bool _hitChecked;

    private const float HitCheckTime = 0.25f;    // 判定帧时间（动画挥砍到伤害点的时刻）
    private const float AttackDuration = 0.5f;   // 攻击总时长

    public void OnEnter()
    {
        _elapsed = 0f;
        _hitChecked = false;
    }

    public void OnUpdate(float dt)
    {
        _elapsed += dt;

        // 判定帧：发事件请求 ViewController 执行碰撞检测（仅一次）
        if (!_hitChecked && _elapsed >= HitCheckTime)
        {
            _hitChecked = true;
            this.SendEvent(new RequestAttackHitCheckEvent());
        }

        // 攻击结束 → 回 Idle（System → System 直接调用，不用 Command）
        if (_elapsed >= AttackDuration)
        {
            this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
        }
    }

    public void OnFixUpdate(float dt) { }

    public void OnExit() { }

    protected override void OnInit() { }
}
```

### 6.2 FsmHurtState（新增）

受伤后短暂的硬直状态，不可移动/攻击。硬直结束自动回 Idle；若已死亡则进入 Dead。

```csharp
// 新增到 FSMState.cs

public class FsmHurtState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Hurt";
    public PlayerStateType StateType { get; } = PlayerStateType.Hurt;

    private float _elapsed;
    private const float HurtDuration = 0.4f;

    public void OnEnter()
    {
        _elapsed = 0f;
    }

    public void OnUpdate(float dt)
    {
        _elapsed += dt;

        if (_elapsed >= HurtDuration)
        {
            var combat = this.GetModel<ICombatModel>();
            if (combat.IsDead.Value)
            {
                // 死亡 → Dead（暂用 Idle 代替，后续加 DeadState）
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

### 6.3 状态转换表更新

在 `FSMSystem` 的转换表中加入 Hurt 的规则（当前代码无转换表，记录以备后续重构）：

| 当前状态 | 允许切换到的状态 |
|----------|-----------------|
| Idle | Move, Attack, Hurt, Dead |
| Move | Idle, Attack, Hurt, Dead |
| Attack | Idle, Hurt, Dead |
| Hurt | Idle, Dead |
| Dead | （终点，不可切换） |

---

## 八、碰撞检测 —— 架构桥接

这是战斗系统的核心挑战：**System 不能持有 Unity 引用，但攻击判定必须用 Physics2D。**

解决方案：**事件桥接**。FsmAttackState 发 `RequestAttackHitCheckEvent`（System → 上层：Event），PlayerController 订阅后执行 `Physics2D.OverlapCircleAll`，命中后调用 EnemyController 方法；敌人攻击玩家则通过 `CombatSystem.ApplyDamage()`。

### 7.1 PlayerController 改造

```csharp
// 在 PlayerController.Awake() 中添加

private void Awake()
{
    _inputUtility = this.GetUtility<IInputUtility>();
    _fsmSystem = this.GetSystem<IFSMSystem>();
    _playerModel = this.GetModel<IPlayerModel>();
    _rigidbody2D = this.GetComponent<Rigidbody2D>();

    _inputUtility.Awake();

    // 订阅攻击判定请求 —— System 层通过事件请求 ViewController 执行碰撞检测
    this.RegisterEvent<RequestAttackHitCheckEvent>(_ =>
    {
        PerformAttackHitCheck();
    }).UnRegisterWhenGameObjectDestroyed(gameObject);
}

private void PerformAttackHitCheck()
{
    var combat = this.GetModel<ICombatModel>();
    float attackRange = 1.5f;
    Vector3 attackCenter = transform.position + transform.right * 0.8f;

    Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRange);
    foreach (var hit in hits)
    {
        if (hit.gameObject == gameObject) continue; // 排除自己

        // 检测到敌人（通过标签或 EnemyController 组件）
        var enemy = hit.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.TakeDamage(combat.AttackPower.Value);
            break; // 只命中第一个敌人
        }
    }
}
```

### 7.2 敌人类（轻量方案）

敌人不跑框架的 IoC/FSM，直接用 MonoBehaviour 写。**EnemyController 不实现 IController**，因此它不在 MVCS 框架内，只是一个外部表现层脚本。它通过 `RogueLikeGame.Interface` 访问架构的公共 API。

```csharp
// Assets/Scripts/ViewController/EnemyController.cs

using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [SerializeField] private int _maxHP = 50;
    [SerializeField] private int _attackPower = 10;
    [SerializeField] private int _defense = 3;

    private int _currentHP;
    private bool _isDead;

    private void Awake()
    {
        _currentHP = _maxHP;
    }

    /// <summary>
    /// 受击入口。由玩家攻击判定调用（PlayerController → 本方法）。
    /// EnemyController 是外部 MonoBehaviour，不在框架内，方法调用即可。
    /// </summary>
    public void TakeDamage(int rawDamage)
    {
        if (_isDead) return;

        int finalDamage = Mathf.Max(1, rawDamage - _defense);
        _currentHP = Mathf.Max(0, _currentHP - finalDamage);

        // TODO: 播放受伤动画、弹出伤害数字

        if (_currentHP <= 0)
        {
            _isDead = true;
            Die();
        }
    }

    /// <summary>
    /// 敌人攻击玩家。由敌人的 AI / 碰撞检测触发。
    /// 通过 CombatSystem.ApplyDamage() 执行伤害计算（System 提供的公开方法，上层→下层调用），
    /// 然后通过 Architecture 发 Command 触发玩家受击状态。
    /// </summary>
    public void AttackPlayer()
    {
        // 上层（MonoBehaviour）→ 下层（System）方法调用，合规
        var combatSystem = RogueLikeGame.Interface.GetSystem<ICombatSystem>();
        var result = combatSystem.ApplyDamage(_attackPower);

        // 触发玩家 FSM 进入 Hurt 状态（通过架构发 Command）
        if (!result.IsDead)
            RogueLikeGame.Interface.SendCommand<TryHurtCommand>();
        // 死亡流程由 CombatUIController 监听 IsDead 触发
    }

    private void Die()
    {
        GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject, 1f);
    }
}
```

> **架构说明**：`EnemyController` 不实现 `IController`，是一个外部 MonoBehaviour。它只能通过 `RogueLikeGame.Interface` 公开 API 访问架构。伤害计算走 `GetSystem<ICombatSystem>().ApplyDamage()`（上层→下层方法调用），触发 FSM 走 `SendCommand<TryHurtCommand>()`。

---

## 九、UI 反馈层

```csharp
// Assets/Scripts/ViewController/CombatUIController.cs

using UnityEngine;
using UnityEngine.UI;

public class CombatUIController : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    [SerializeField] private Slider _healthBar;
    [SerializeField] private GameObject _gameOverPanel;

    private void Awake()
    {
        var combat = this.GetModel<ICombatModel>();

        // 血量变化 → 更新血条
        combat.HP.RegisterOnValueChanged(hp =>
        {
            if (_healthBar != null)
                _healthBar.value = (float)hp / combat.MaxHP.Value;
        }).UnRegisterWhenGameObjectDestroyed(gameObject);

        // 最大血量变化 → 更新血条上限
        combat.MaxHP.RegisterOnValueChanged(maxHP =>
        {
            if (_healthBar != null)
                _healthBar.maxValue = maxHP;
        }).UnRegisterWhenGameObjectDestroyed(gameObject);

        // 死亡
        combat.IsDead.RegisterOnValueChanged(isDead =>
        {
            if (isDead && _gameOverPanel != null)
                _gameOverPanel.SetActive(true);
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }
}
```

> 伤害数字的弹出可通过订阅 `DamageEvent` 实现（`this.RegisterEvent<DamageEvent>(...)`）。

---

## 十、完整数据流 —— 一次攻击的全链路

```
用户按 J 键
  │
  ├─ 1. InputUtility.Attack = true（Utility 层存输入）
  │
  ├─ 2. PlayerController.Update() 检测到攻击输入（IController 读输入、判断切换）
  │     └─ this.SendCommand<TryAttackCommand>()
  │
  ├─ 3. TryAttackCommand.OnExcute()
  │     └─ fsm.ChangeState<FsmAttackState>()
  │
  ├─ 4. FSMSystem.ChangeState<T>()
  │     ├─ 旧状态 OnExit()
  │     ├─ 新状态 OnEnter()
  │     ├─ _playerModel._currentState.Value = Attack
  │     └─ this.SendEvent(new PlayerStateChangedEvent("Attack", Attack))
  │
  ├─ 5. PlayerAnimationController 收到事件
  │     └─ _animator.CrossFade("Attack", 0.1f)
  │
  ├─ 6. FsmAttackState.OnUpdate(0.25s) → 到达判定帧（状态内部计时）
  │     └─ this.SendEvent(new RequestAttackHitCheckEvent())
  │
  ├─ 7. PlayerController.PerformAttackHitCheck()
  │     └─ Physics2D.OverlapCircleAll() 检测到 Enemy
  │
  ├─ 8. EnemyController.TakeDamage(playerAttackPower)
  │     └─ 敌人扣血 → 死亡则销毁
  │
  ├─ 9. FsmAttackState.OnUpdate(0.5s) → 攻击时长结束（System → System 直接调用）
  │     └─ this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>()
  │
  └─ 10. 回到 FsmIdleState


如果敌人攻击玩家：

  EnemyController.AttackPlayer()
  │
  ├─ GetSystem<ICombatSystem>().ApplyDamage(_attackPower)   ← 上层→下层：调 System 方法
  │     ├─ HP.Value -= FinalDamage                          ← System 改 Model（下层→更下层，合规）
  │     ├─ IsDead.Value = true/false
  │     └─ this.SendEvent(new DamageEvent)                  ← System → 上层：事件通知
  │
  ├─ RogueLikeGame.Interface.SendCommand<TryHurtCommand>()  ← 触发 FSM 切换
  │     └─ FSMSystem.ChangeState<FsmHurtState>()
  │           └─ SendEvent(PlayerStateChangedEvent(Hurt))
  │
  ├─ CombatUIController:
  │     ├─ HP BindableProperty 变化 → 更新血条
  │     └─ DamageEvent → 弹出伤害数字
  │
  └─ 如果 IsDead == true：
       └─ CombatUIController → 显示 GameOver
```

---

## 十一、建议开发顺序

| 步骤 | 内容 | 可独立测试 |
|------|------|-----------|
| 1 | 新建 `CombatModel` + `CombatSystem`，注册到 `RogueLikeGame` | 断点确认注册成功 |
| 2 | 新建 `EnemyController`，场景放一个静态敌人 | 手动调 `CombatSystem.ApplyDamage` 验证扣血 |
| 3 | 改造 `FsmAttackState` 加判定帧 + `RequestAttackHitCheckEvent` | 按 J 键看 Console 打印事件 |
| 4 | `PlayerController.PerformAttackHitCheck()` 执行碰撞检测 | 按 J 键打中敌人，敌人扣血 |
| 5 | 新建 `FsmHurtState` + `TryHurtCommand` | 敌人打玩家，玩家进受伤硬直 |
| 6 | 新建 `CombatUIController` 挂血条 | 看到血条跟随血量变化 |
| 7 | 加死亡流程（`PlayerStateType.Dead` + GameOver UI） | HP = 0 时看到 GameOver |

---

## 十二、后续扩展方向

1. **多实体 FSM** —— 将 FSMSystem / CombatModel 改为非单例，每个角色持有一份实例，敌人也走完整 FSM
2. **ScriptableObject 配置** —— 敌人属性、武器数据用 `.asset` 文件配置，脱离硬编码
3. **攻击判定抽象** —— 不同武器有不同判定形状（圆形/扇形/矩形）、判定时机（多段判定帧）
4. **Buff / 状态效果** —— 毒、减速、眩晕等。Buff 系统作为另一个 System，在 `CombatSystem.ApplyDamage()` 中调用 `IBuffSystem` 做增减伤，全程 System 层平级交互，不违反分层规则
5. **韧性 / 霸体系统** —— 控制哪些攻击可以打断当前状态
6. **音效与特效** —— 订阅 `DamageEvent`、`PlayerStateChangedEvent` 触发对应反馈
