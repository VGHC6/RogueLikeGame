# 项目架构总览

## 入口

全局唯一架构入口：`RogueLikeGame.Interface`（单例，懒加载）

所有 Controller 通过 `RogueLikeGame.Interface` 获取架构，再通过扩展方法访问下层：
- `this.GetModel<T>()`
- `this.GetSystem<T>()`
- `this.GetUtility<T>()`
- `this.SendCommand<T>()`
- `this.RegisterEvent<T>()`
- `this.SendEvent<T>()`

## 层级规则（见 Rule.txt）

```
Controller (表现层)
  ├── 可 GetSystem / GetModel / GetUtility（只读查询）
  ├── 修改 System/Model 状态 → 必须用 Command
  └── 监听 Event（System/Model 状态变更通知）

System (系统层)
  ├── 可 GetSystem / GetModel / GetUtility
  ├── 可直接调 Model 方法
  └── 可 RegisterEvent / SendEvent

Model (数据层)
  ├── 可 GetUtility
  └── 可 SendEvent（向上通知）

Utility (工具层)
  └── 不访问任何架构内的东西

Command
  ├── 可 GetSystem / GetModel / GetUtility
  ├── 可 SendEvent / SendCommand
  └── 无状态（字段只在单次执行内有效）
```

通信方向：
- 上层 → 下层：方法调用（Controller 用 Command 代替）
- 下层 → 上层：事件

## 注册清单（RogueLikeGame.Init）

| 接口 | 实例 | 说明 |
|------|------|------|
| `IEntityModel` | `PlayerEntityModel` | 玩家位置/移动/击退 |
| `ICombatModel` | `PlayerCombatModel` | 玩家HP/攻击/防御（BindableProperty） |
| `IEnemyModel` | `EnemyModel` | Dictionary<int, EnemyRuntimeData> 存所有敌人 |
| `ICombatSystem` | `CombatSystem` | 伤害公式 |
| `IEnemyManagerSystem` | `EnemyManagerSystem` | 驱动所有敌人状态机 |
| `FsmIdleState` | | 玩家待机 |
| `FsmMoveState` | | 玩家移动 |
| `FsmAttackState` | | 玩家攻击（0.5s / 0.25s判定帧） |
| `FsmHurtState` | | 玩家受伤（0.4s + 击退衰减） |
| `IFSMSystem` | `FSMSystem` | 玩家状态机调度 |
| `IInputUtility` | `InputUtility` | 输入封装 |
| `IHitstopUtility` | `HitstopUtility` | 时停/卡肉 |
| `ICameraUtility` | `CameraUtility` | 震屏 |
| `IAnimationUtility` | `AnimationUtility` | 动画切换 |

## 敌人架构

### EnemyModel（数据）
```
Dictionary<int, EnemyRuntimeData>
├── Register(init) → int enemyId
├── Unregister(id)
├── Get(id) / GetAll()
└── Set*方法 → 内部发 Event
```

### EnemyManagerSystem（逻辑）
```
Update(dt) — 遍历所有敌人
├── Idle: 距离判断 → Chase / Attack
├── Chase: 距离判断 → Attack / Idle，计算追逐方向
├── Attack: 计时 0.5s → 0.25s发攻击判定事件
├── Hurt: 计时 0.4s + 击退衰减 → Idle / Dead
└── Dead: 无操作
```

### EnemyView（表现）
```
Start: 场景敌人自动注册到Model
FixedUpdate: 读Model的MoveDelta，驱动Rigidbody2D
监听事件: StateChanged(切动画) / RequestHitCheck(物理判定) / Dead(销毁)
```

## 敌人受伤流程

```
PlayerController.PerformAttackHitCheck
  → Physics2D.OverlapCircleAll → 找到 EnemyView
  → enemyView.TakeDamage(damage, knockbackDir)

EnemyView.TakeDamage
  → SendCommand<ApplyDamageToEnemyCommand>

ApplyDamageToEnemyCommand
  → 读 Model.Get(enemyId).DefensePower
  → 算伤害 → Model.SetCurrentHp(enemyId, newHp)
  → Hitstop + CameraShake
  → System.OnEnemyDamaged (进入Hurt状态)
  → SendEvent(DamageEvent)
```

## 文件结构

```
Assets/Scripts/
├── Architecture/       # IAchitecture, Architecture<T>, Rule/*, Bind/*, IOC/*
├── Command/            # TryAttack/Move/Idle/Hurt, ApplyDamageToEnemy
├── Event/              # Damage, EnemyHpChanged, EnemyStateChanged, EnemyDead, EnemyRequestHitCheck, PlayerStateChanged, RequestAttackHitCheck
├── Model/              # CombatModel, IEntityModel, EnemyRuntimeData, EnemyModel
├── System/             # CombatSystem, FSMSystem, FSMState, EnemyManagerSystem
├── Utility/            # Input, Animation, Hitstop, Camera
└── ViewController/     # PlayerController, EnemyView, EnemyManagerDriver(Init+Update), UIController/
```
