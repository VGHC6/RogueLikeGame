# 战斗表现优化方案

## 当前状态

```
攻击命中流程：
  Attacker.PerformAttackHitCheck()
    → target.TakeDamage(attackPower)
      → CombatSystem.ApplyDamage(combatModel, rawDamage)  // 算伤害、改HP、发DamageEvent
      → if not dead: SendCommand<TryHurtCommand>()
        → FSMSystem.ChangeState<FsmHurtState>()
          → FsmHurtState.OnUpdate: 计时 0.4s → 自动切回 Idle
```

已有硬直（`FsmHurtState`，0.4s 固定），但缺少击退、卡肉、屏幕震动等表现。

---

## 1. 击退（Knockback）—— 最优先

### 效果

被击中后沿攻击方向弹开，速度逐帧衰减。

### 新增：`IEntityModel` 加两个属性

```diff
 public interface IEntityModel : IModel
 {
     BindableProperty<PlayerStateType> _currentState { get; }
     Vector2 MoveDelta { get; set; }
     float MoveSpeed { get; set; }
     Vector2 Position { get; set; }
+    Vector2 KnockbackDirection { get; set; }
+    float KnockbackForce { get; }
 }
```

`PlayerEntityModel` 和 `EnemyEntityModel` 各自加上实现：

```csharp
// PlayerEntityModel
public Vector2 KnockbackDirection { get; set; }
public float KnockbackForce { get; } = 8f;    // 玩家被击退的力度
public float KnockbackDecay { get; } = 0.85f;  // 每帧衰减系数

// EnemyEntityModel
public Vector2 KnockbackDirection { get; set; }
public float KnockbackForce { get; } = 6f;     // 敌人被击退的力度（稍小）
public float KnockbackDecay { get; } = 0.85f;
```

### 修改：`FsmHurtState`

```csharp
public class FsmHurtState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Hurt";
    public PlayerStateType StateType { get; } = PlayerStateType.Hurt;

    private float _elapsed;
    private const float HurtDuration = 0.4f;
    private Vector2 _knockbackVelocity;

    public void OnEnter()
    {
        _elapsed = 0f;

        // 击退初始速度
        var model = this.GetModel<IEntityModel>();
        _knockbackVelocity = model.KnockbackDirection * model.KnockbackForce;
    }

    public void OnUpdate(float datetime)
    {
        _elapsed += datetime;
        if (_elapsed >= HurtDuration)
        {
            this.GetSystem<IFSMSystem>().ChangeState<FsmIdleState>();
        }
    }

    public void OnFixUpdate(float datetime)
    {
        // 击退速度逐帧衰减
        var model = this.GetModel<IEntityModel>();
        _knockbackVelocity *= model.KnockbackDecay;
        model.MoveDelta = _knockbackVelocity.magnitude < 0.1f ? Vector2.zero : _knockbackVelocity;
    }

    public void OnExit()
    {
        var model = this.GetModel<IEntityModel>();
        model.MoveDelta = Vector2.zero;
        model.KnockbackDirection = Vector2.zero;
    }

    protected override void OnInit() { }
}
```

### 修改：`PlayerController.TakeDamage` 和 `PerformAttackHitCheck`

```csharp
// TakeDamage 加击退方向参数
public void TakeDamage(int rawDamage, Vector2 knockbackDirection)
{
    var combat = this.GetModel<ICombatModel>();
    if (combat.IsDead.Value) return;

    var combatSystem = this.GetSystem<ICombatSystem>();
    combatSystem.ApplyDamage(combat, rawDamage);

    if (!combat.IsDead.Value)
    {
        _playerModel.KnockbackDirection = knockbackDirection;
        this.SendCommand<TryHurtCommand>();
    }
}

// PerformAttackHitCheck 计算方向并传入
private void PerformAttackHitCheck()
{
    var combat = this.GetModel<ICombatModel>();
    float attackRange = 1f;
    int facingDir = transform.localScale.x > 0 ? 1 : -1;
    Vector3 attackCenter = transform.position + Vector3.right * facingDir * 0.8f;

    Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRange);
    foreach (var hit in hits)
    {
        if (hit.gameObject == gameObject) continue;

        var enemy = hit.GetComponent<EnemyController>();
        if (enemy != null)
        {
            Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
            enemy.TakeDamage(combat.AttackPower.Value, knockbackDir);
            Debug.Log("敌人剩余生命值：" + enemy.GetModel<ICombatModel>().CurrentHp.Value);
            break;
        }
    }
}
```

### 修改：`EnemyController.TakeDamage` 和 `PerformAttackHitCheck`

对称改动，和 Player 完全一样：

```csharp
public void TakeDamage(int rawDamage, Vector2 knockbackDirection)
{
    GetArchitecture();
    if (_combatModel.IsDead.Value) return;

    var combatSystem = this.GetSystem<ICombatSystem>();
    combatSystem.ApplyDamage(_combatModel, rawDamage);

    if (!_combatModel.IsDead.Value)
    {
        _entityModel.KnockbackDirection = knockbackDirection;
        this.SendCommand<TryHurtCommand>();
    }
    else
        Die();
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
            Vector2 knockbackDir = (player.transform.position - transform.position).normalized;
            player.TakeDamage(combat.AttackPower.Value, knockbackDir);
            break;
        }
    }
}
```

> 注意：之前攻击方（如 PlayerController）调用 `target.TakeDamage` 的地方只传了一个参数，需要全部找到并加上方向参数。

---

## 2. 卡肉（Hitstop）

### 效果

攻击命中瞬间画面停顿 0.05s~0.1s，模拟攻击阻力感。

### 新增：`Assets/Scripts/Utility/IHitstopUtility.cs`

```csharp
using System.Collections;
using UnityEngine;

/// <summary>
/// 卡肉工具。全局注册在 RogueLikeGame。
/// </summary>
public interface IHitstopUtility : IUtility
{
    void Trigger(float duration);
}

public class HitstopUtility : IHitstopUtility
{
    private IAchitecture _architecture;
    private MonoBehaviour _runner;
    private Coroutine _current;
    private bool _isFrozen;

    public IAchitecture GetArchitecture() => _architecture;

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
    }

    public void Init(MonoBehaviour runner)
    {
        _runner = runner;
    }

    public void Trigger(float duration)
    {
        if (_runner == null) return;
        if (_current != null) _runner.StopCoroutine(_current);
        _current = _runner.StartCoroutine(Run(duration));
    }

    private IEnumerator Run(float duration)
    {
        _isFrozen = true;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        _isFrozen = false;
    }
}
```

### 注册：`RogueLikeGame.cs`

```diff
  protected override void Init()
  {
      this.RegisterSystem<ICombatSystem>(new CombatSystem());
      this.RegisterUtility<IInputUtility>(new InputUtility());
+     this.RegisterUtility<IHitstopUtility>(new HitstopUtility());
  }
```

### 初始化：`PlayerController.InitArchitecture`

```diff
+ var hitstop = _architecture.GetUtility<IHitstopUtility>() as HitstopUtility;
+ hitstop.Init(this);
```

同样在 `EnemyController.InitArchitecture` 加一行。

### 触发：`PlayerController.TakeDamage`

```diff
  public void TakeDamage(int rawDamage, Vector2 knockbackDirection)
  {
      var combat = this.GetModel<ICombatModel>();
      if (combat.IsDead.Value) return;

      var combatSystem = this.GetSystem<ICombatSystem>();
      combatSystem.ApplyDamage(combat, rawDamage);

+     this.GetUtility<IHitstopUtility>().Trigger(0.06f);  // 卡肉

      if (!combat.IsDead.Value)
      {
          _playerModel.KnockbackDirection = knockbackDirection;
          this.SendCommand<TryHurtCommand>();
      }
  }
```

`EnemyController.TakeDamage` 同样加一行。

### 卡肉时长来源

不同攻击力度可以传不同时长。更细致的做法是把时长放到 `ICombatModel` 中：

```diff
 public interface ICombatModel : IModel
 {
     BindableProperty<int> CurrentHp { get; }
     BindableProperty<int> MaxHp { get; }
     BindableProperty<int> AttackPower { get; }
     BindableProperty<int> DefensePower { get; }
     BindableProperty<bool> IsDead { get; }
+    float HitstopDuration { get; }
 }
```

这样攻击方可以从自己的 ComatModel 读取时长传入。不用急着加，先用固定值 0.06f。

---

## 3. 屏幕震动（Screen Shake）

### 新增：`Assets/Scripts/Utility/ICameraUtility.cs`

```csharp
using System.Collections;
using UnityEngine;

/// <summary>
/// 相机工具。全局注册在 RogueLikeGame。
/// </summary>
public interface ICameraUtility : IUtility
{
    void Shake(float intensity, float duration);
}

public class CameraUtility : ICameraUtility
{
    private IAchitecture _architecture;
    private Camera _camera;
    private MonoBehaviour _runner;

    public IAchitecture GetArchitecture() => _architecture;

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
    }

    public void Init(Camera camera, MonoBehaviour runner)
    {
        _camera = camera;
        _runner = runner;
    }

    public void Shake(float intensity, float duration)
    {
        if (_runner == null || _camera == null) return;
        _runner.StartCoroutine(Run(intensity, duration));
    }

    private IEnumerator Run(float intensity, float duration)
    {
        float elapsed = 0f;
        Vector3 origin = _camera.transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float decay = 1f - elapsed / duration;
            float x = Random.Range(-1f, 1f) * intensity * decay;
            float y = Random.Range(-1f, 1f) * intensity * decay;
            _camera.transform.position = origin + new Vector3(x, y, 0);
            yield return null;
        }

        _camera.transform.position = origin;
    }
}
```

### 注册：`RogueLikeGame.cs`

```diff
+ this.RegisterUtility<ICameraUtility>(new CameraUtility());
```

### 初始化

在某个早期初始化的 MonoBehaviour（比如 PlayerController 的 Awake 或场景中一个 Bootstrap GameObject）中：

```csharp
var camUtil = RogueLikeGame.Interface.GetUtility<ICameraUtility>() as CameraUtility;
camUtil.Init(Camera.main, this);
```

### 触发：`TakeDamage` 中

```csharp
this.GetUtility<ICameraUtility>().Shake(0.15f, 0.12f);
```

---

## 4. 受击闪白

### 效果

受击瞬间 SpriteRenderer 变白 0.08s，给视觉反馈。

不需要新建类，直接在 Controller 中加协程即可。因为用的是 `MonoBehaviour` 的能力（协程），放在 Controller 最合适。

### 修改：`PlayerController.TakeDamage`

```csharp
public void TakeDamage(int rawDamage, Vector2 knockbackDirection)
{
    var combat = this.GetModel<ICombatModel>();
    if (combat.IsDead.Value) return;

    var combatSystem = this.GetSystem<ICombatSystem>();
    combatSystem.ApplyDamage(combat, rawDamage);

    // 卡肉 + 震动 + 闪白
    this.GetUtility<IHitstopUtility>().Trigger(0.06f);
    this.GetUtility<ICameraUtility>().Shake(0.15f, 0.12f);
    StartCoroutine(FlashWhite(0.08f));

    if (!combat.IsDead.Value)
    {
        _playerModel.KnockbackDirection = knockbackDirection;
        this.SendCommand<TryHurtCommand>();
    }
}

private System.Collections.IEnumerator FlashWhite(float duration)
{
    var sr = GetComponent<SpriteRenderer>();
    if (sr == null) yield break;
    Color original = sr.color;
    sr.color = Color.white;
    yield return new WaitForSeconds(duration);
    sr.color = original;
}
```

`EnemyController.TakeDamage` 同样加。

---

## 5. 命中特效/音效（可选）

### 思路

`CombatSystem.ApplyDamage` 已经发 `DamageEvent`。可以在 Controller 的 `TakeDamage` 中再发一个事件给 VFX Manager：

```csharp
// 新建事件类
public class HitImpactEvent
{
    public Vector2 HitPoint;
    public bool IsHeavy;
}
```

场景中挂一个 `HitVFXManager : MonoBehaviour`，订阅全局 `HitImpactEvent`，负责生成火花粒子/播放音效。

这不需要改动战斗核心代码，属于表现层独立模块。

---

## 改动汇总

| 优先级 | 效果 | 新建文件 | 修改文件 |
|--------|------|---------|---------|
| 1 | **击退** | — | `IEntityModel`、`PlayerEntityModel`、`EnemyEntityModel`、`FsmHurtState`、`PlayerController`、`EnemyController` |
| 2 | **卡肉** | `IHitstopUtility.cs` | `RogueLikeGame`、`PlayerController`、`EnemyController` |
| 3 | **屏幕震动** | `ICameraUtility.cs` | `RogueLikeGame`、`PlayerController`、`EnemyController`、Bootstrap |
| 4 | **闪白** | — | `PlayerController`、`EnemyController` |
| 5 | **特效音效** | `HitImpactEvent.cs` + 场景 VFX Manager | `CombatSystem`（可选） |

## 完整数据流（全部加入后）

```
Attacker.PerformAttackHitCheck()
  │
  ├─ 计算击退方向
  ├─ target.TakeDamage(damage, knockbackDir)
  │
  ▼
Target.TakeDamage(rawDamage, knockbackDir)
  │
  ├─ CombatSystem.ApplyDamage(combat, rawDamage)
  │     └─ SendEvent(DamageEvent) → UI 层
  │
  ├─ IHitstopUtility.Trigger(0.06f)        ← 卡肉
  ├─ ICameraUtility.Shake(0.15f, 0.12f)    ← 震屏
  ├─ StartCoroutine(FlashWhite(0.08f))     ← 闪白
  │
  ├─ 存储 model.KnockbackDirection = knockbackDir
  ├─ SendCommand<TryHurtCommand>()
  │
  ▼
FsmHurtState.OnEnter()
  │
  ├─ _knockbackVelocity = model.KnockbackDirection * model.KnockbackForce
  │
  ▼
FsmHurtState.OnFixUpdate()
  │
  ├─ _knockbackVelocity *= model.KnockbackDecay
  └─ model.MoveDelta = _knockbackVelocity
  │
  ▼
Controller.FixedUpdate()
  │
  └─ _rigidbody2D.velocity = model.MoveDelta  (击退生效)

FsmHurtState.OnUpdate()
  │
  └─ 0.4s 计时结束 → ChangeState<FsmIdleState>()

FsmHurtState.OnExit()
  │
  └─ model.MoveDelta = 0; KnockbackDirection = 0
```
