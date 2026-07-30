# 远程攻击敌人制作指南

## 当前架构回顾

```
攻击流程：
  EnemyController.Update()
    → 距离判断（dist < AttackRange → 攻击）
    → SendCommand<TryAttackCommand>()
    → FSMSystem.ChangeState<FsmAttackState>()
    → FsmAttackState.OnUpdate() 计时 → 到达判定帧
    → SendEvent<RequestAttackHitCheckEvent>()
    → EnemyController.PerformAttackHitCheck()
    → Physics2D.OverlapCircleAll() 近战圆形范围检测
    → player.TakeDamage()
```

远程敌人的核心区别：**不在自身周围做碰撞检测，而是生成一个飞行物（Projectile），由飞行物碰撞目标后造成伤害。**

---

## 需要新增/修改的文件

| 文件 | 操作 | 说明 |
|------|------|------|
| `Model/CombatModel.cs` | 修改 | 新增 `AttackType` 枚举，区分近战/远程 |
| `Event/RequestAttackHitCheckEvent.cs` | 修改 | 事件带上攻击者信息（位置、朝向、伤害） |
| `System/EnemyState.cs` | 新增状态 | `EnemyRangedAttackState`，生成投射物 |
| `ViewController/Projectile.cs` | **新建** | 飞行物 MonoBehaviour |
| `ViewController/EnemyController.cs` | 修改 | AI 逻辑适配远程（保持距离而非贴脸） |
| `EntityArchitecture/EnemyArchitecture.cs` | 修改 | 注册新状态 |
| `Model/IEntityModel.cs` | 修改 | EnemyEntityModel 加 `ComfortRange` 等远程参数 |

---

## 第一步：区分攻击类型

在 `CombatModel.cs` 顶部加一个枚举：

```csharp
public enum AttackType
{
    Melee,   // 近战：OverlapCircleAll
    Ranged   // 远程：生成 Projectile
}
```

在 `ICombatModel` 接口和两个实现类中加：

```csharp
// ICombatModel 接口
BindableProperty<AttackType> AttackType { get; }

// PlayerCombatModel.OnInit()
AttackType.Value = AttackType.Melee;

// EnemyCombatModel.OnInit()
AttackType.Value = AttackType.Melee;  // 默认近战，远程敌人改成 Ranged
```

---

## 第二步：改造攻击事件

将 `RequestAttackHitCheckEvent` 从空壳改为携带攻击数据：

```csharp
public class RequestAttackHitCheckEvent
{
    public AttackType AttackType;
    public int AttackPower;
    public Vector3 AttackerPosition;
    public int FacingDir;          // 1 = 右, -1 = 左
    public GameObject Attacker;    // 攻击者自身，用于排除
    public Transform Target;       // 远程攻击的目标（敌人用）
}
```

对应修改 `FsmAttackState.OnUpdate()` 发送事件处：

```csharp
this.SendEvent(new RequestAttackHitCheckEvent
{
    AttackType = combat.AttackType.Value,
    AttackPower = combat.AttackPower.Value,
    AttackerPosition = entity.Position,
    FacingDir = /* 从 Controller 获取朝向 */,
    Attacker = /* controller.gameObject */,
    Target = /* enemyAI 的目标 Transform */
});
```

> **问题**：FsmAttackState 是纯 System，拿不到 Transform/GameObject。两种解法：
> - **A**（简单）：在 `IEntityModel` 中加 `FacingDir` 字段，由 Controller 在 Update 中写入
> - **B**（干净）：Controller 不再订阅事件做检测，而是 FsmAttackState 直接通过事件把数据传给 Controller
>
> 推荐用 A，改动最小。

---

## 第三步：新建 Projectile 脚本

新建 `Assets/Scripts/ViewController/Projectile.cs`：

```csharp
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float Speed = 8f;
    public int Damage = 1;
    public float MaxLifetime = 3f;
    public LayerMask HitMask;           // 碰撞检测层
    public GameObject Owner;            // 发射者，不碰撞自己

    private Vector2 _direction;
    private float _elapsed;

    public void Launch(Vector2 direction, int damage, GameObject owner)
    {
        _direction = direction.normalized;
        Damage = damage;
        Owner = owner;
        _elapsed = 0f;

        // 朝向旋转
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed > MaxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)(_direction * Speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == Owner) return;

        var target = other.GetComponent<IController>();
        if (target == null) return;

        // 反射调用 TakeDamage
        var playerCtrl = other.GetComponent<PlayerController>();
        var enemyCtrl = other.GetComponent<EnemyController>();

        Vector2 knockback = _direction; // 击退方向 = 飞行方向

        if (playerCtrl != null)
            playerCtrl.TakeDamage(Damage, knockback);
        else if (enemyCtrl != null)
            enemyCtrl.TakeDamage(Damage, knockback);

        Destroy(gameObject);
    }
}
```

### Projectile 预制体设置

1. 在 Hierarchy 创建空 GameObject，命名为 `Projectile`
2. 添加 **SpriteRenderer**，给一个圆形/箭头 Sprite（临时用 Unity 内置 Square 也可）
3. 添加 **CircleCollider2D**，勾选 `Is Trigger`，Radius 设 0.15
4. 添加 **Rigidbody2D**，Body Type 设为 `Kinematic`
5. 添加上述 `Projectile` 脚本
6. 拖到 `Assets/Prefabs/` 做成预制体

---

## 第四步：新增远程攻击状态

在 `System/EnemyState.cs` 中新增：

```csharp
public class EnemyRangedAttackState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Attack";
    public PlayerStateType StateType { get; } = PlayerStateType.Attack;

    private float _elapsed;
    private bool _fired;
    private const float FireTime = 0.3f;     // 发射时机
    private const float Duration = 0.6f;      // 总时长
    private const string ProjectilePrefabPath = "Prefabs/Projectile";

    public void OnEnter()
    {
        _elapsed = 0f;
        _fired = false;
    }

    public void OnUpdate(float deltaTime)
    {
        _elapsed += deltaTime;

        if (!_fired && _elapsed >= FireTime)
        {
            _fired = true;
            SpawnProjectile();
        }

        if (_elapsed >= Duration)
        {
            this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
        }
    }

    void SpawnProjectile()
    {
        var entity = this.GetModel<IEntityModel>();
        var combat = this.GetModel<ICombatModel>();
        var ai = this.GetUtility<IEnemyAIUtility>();

        var prefab = Resources.Load<GameObject>(ProjectilePrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Projectile prefab not found at Resources/" + ProjectilePrefabPath);
            return;
        }

        // 发射位置：敌人位置
        Vector3 spawnPos = (Vector3)entity.Position;

        // 发射方向：指向目标
        Vector2 dir;
        if (ai.HasTarget)
            dir = (ai.TargetPosition - (Vector2)spawnPos).normalized;
        else
            dir = Vector2.right; // 默认向右

        var proj = Object.Instantiate(prefab, spawnPos, Quaternion.identity)
                        .GetComponent<Projectile>();
        proj.Launch(dir, combat.AttackPower.Value, null); // Owner 是 null 或传 enemyObj
    }

    public void OnFixUpdate(float deltaTime) { }
    public void OnExit() { }
    protected override void OnInit() { }
}
```

> **预制体加载**：Projectile 预制体需放在 `Assets/Resources/Prefabs/` 下，用 `Resources.Load`。如果不想用 Resources，可以在 `EnemyController` 上挂一个 `[SerializeField]` 引用，通过事件传过来。

---

## 第五步：修改 AI 行为

远程敌人与近战敌人行为不同：
- **近战**：离得远 → 追 → 进入攻击范围 → 砍
- **远程**：离得远 → 追 → 进入射程 → 停下射击（不应贴脸）

修改 `EnemyController.Update()` 的距离判断：

```csharp
public void Update()
{
    if (!_initialized) return;

    var currentState = _fsmSystem._currentState.StateType;
    float dist = Vector2.Distance(transform.position, _enemyAIUtility.TargetPosition);
    var combat = this.GetModel<ICombatModel>();
    bool isRanged = combat.AttackType.Value == AttackType.Ranged;

    if (currentState != PlayerStateType.Attack && currentState != PlayerStateType.Hurt)
    {
        if (dist <= _entityModel.AttackRange && isRanged)
        {
            // 远程：在射程内就射击，不需要贴脸
            this.SendCommand<TryAttackCommand>();
        }
        else if (dist < _entityModel.AttackRange && !isRanged)
        {
            // 近战：贴脸砍
            this.SendCommand<TryAttackCommand>();
        }
        else if (dist <= _entityModel.ChaseRange)
        {
            // 在追击范围内且超出攻击范围 → 追击
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
```

关键区别：
- **远程**：`dist <= AttackRange` 就可以攻击了（比如 AttackRange=5），这时候敌人离玩家还很远
- **近战**：还是老逻辑 `dist < AttackRange`（AttackRange 约 1.5）

---

## 第六步：注册新状态

修改 `EnemyArchitecture.cs`，将 `FsmAttackState` 替换（或追加）为远程攻击状态。最简单的方式是让远程敌人使用另一个 Architecture：

```csharp
// 新建 RangedEnemyArchitecture.cs
public class RangedEnemyArchitecture : EntityArchitecture
{
    public RangedEnemyArchitecture(IAchitecture parent) : base(parent)
    {
        RegisterModel<IEntityModel>(new EnemyEntityModel());
        RegisterModel<ICombatModel>(new EnemyCombatModel());
        RegisterSystem<FsmIdleState>(new FsmIdleState());
        RegisterSystem<EnemyMoveState>(new EnemyMoveState());
        RegisterSystem<EnemyRangedAttackState>(new EnemyRangedAttackState()); // 远程
        RegisterSystem<FsmHurtState>(new FsmHurtState());
        RegisterSystem<IFSMSystem>(new FSMSystem());
        RegisterUtility<IEnemyAIUtility>(new EnemyAIUtility());
        RegisterUtility<IAnimationUtility>(new AnimationUtility());
        InitEntities();
    }
}
```

然后在远程敌人的 Controller（可以继承 `EnemyController` 或直接复制一份 `RangedEnemyController`）中：

```csharp
private void InitArchitecture()
{
    _architecture = new RangedEnemyArchitecture(RogueLikeGame.Interface);
    // ... 其余初始化一致
}
```

并且在 `OnInit()` 中将 `AttackType` 设为 `Ranged`：

```csharp
// 在 RangedEnemyController 的 InitArchitecture 中
var combat = _architecture.GetModel<ICombatModel>();
combat.AttackType.Value = AttackType.Ranged;
```

---

## 总结：制作流程

1. `CombatModel.cs` — 加 `AttackType` 枚举和属性
2. `Projectile.cs` — 新建飞行物脚本，做预制体放 `Resources/Prefabs/`
3. `EnemyState.cs` — 新建 `EnemyRangedAttackState`，在 FireTime 时 SpawnProjectile
4. `EnemyController.Update()` — 区分远程/近战的距离逻辑
5. 新建 `RangedEnemyArchitecture` / `RangedEnemyController`，注册远程攻击状态
6. 场景中挂 `RangedEnemyController` 替代 `EnemyController`，EntityModel 中设 `AttackRange = 5f`

这样就完成了远程敌人，改动最小且不破坏现有近战逻辑。
