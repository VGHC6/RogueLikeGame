# 架构重构完成总结

## 目标

去掉 `EntityArchitecture`，敌人和玩家全部统一到 `RogueLikeGame` 全局单例管理。

## 完成状态：全部完成

## 最终架构

```
RogueLikeGame (全局唯一 Architecture)
├── Model
│   ├── IEntityModel    → PlayerEntityModel    (玩家实体数据)
│   ├── ICombatModel    → PlayerCombatModel    (玩家战斗数据)
│   └── IEnemyModel     → EnemyModel           (字典存N个敌人运行时数据)
│
├── System
│   ├── ICombatSystem       → CombatSystem       (伤害公式)
│   ├── IEnemyManagerSystem → EnemyManagerSystem (驱动所有敌人状态机)
│   ├── FSMSystem × 5       → FsmIdle/Move/Attack/Hurt + FSMSystem (玩家FSM)
│
└── Utility
    ├── IInputUtility      → InputUtility      (输入)
    ├── IHitstopUtility    → HitstopUtility    (卡肉/时停)
    ├── ICameraUtility     → CameraUtility     (震屏)
    └── IAnimationUtility  → AnimationUtility  (动画)
```

## Controller

| Controller | 架构入口 | 职责 |
|---|---|---|
| `PlayerController` | `RogueLikeGame.Interface` | 输入→FSM、攻击判定 |
| `EnemyView` | `RogueLikeGame.Interface` | 持有enemyId、物理桥接、动画 |
| `EnemyManagerDriver` | `RogueLikeGame.Interface` | 驱动 EnemyManagerSystem.Update |

## 新建文件

| 文件 | 层 | 作用 |
|------|-----|------|
| `Model/EnemyRuntimeData.cs` | Data | EnemyActionState 枚举 + 运行时结构体 |
| `Model/EnemyModel.cs` | Model | 字典存储敌人数据，Set方法内部发事件 |
| `Event/EnemyHpChangedEvent.cs` | Event | 血量变化（带EnemyId） |
| `Event/EnemyStateChangedEvent.cs` | Event | 状态变化 |
| `Event/EnemyDeadEvent.cs` | Event | 敌人死亡 |
| `Event/EnemyRequestHitCheckEvent.cs` | Event | 攻击判定帧通知 |
| `System/EnemyManagerSystem.cs` | System | 驱动所有敌人FSM（switch-based） |
| `Command/ApplyDamageToEnemyCommand.cs` | Command | 敌人受伤完整流程 |
| `ViewController/EnemyView.cs` | Controller | 替代EnemyController |
| `ViewController/EnemyManagerDriver.cs` | Controller | 桥接Unity Update到System |

## 修改文件

| 文件 | 改动 |
|------|------|
| `RogueLikeGame.cs` | 注册所有 Model/System/Utility |
| `IAchitecture.cs` | 删除 EntityArchitecture 类 |
| `PlayerController.cs` | 去除 PlayerArchitecture，直接用 `RogueLikeGame.Interface` |
| `CombatModel.cs` | 删除 EnemyCombatModel |
| `IEntityModel.cs` | 删除 EnemyEntityModel |
| `DamageEvent.cs` | 加 `int? EnemyId` |
| `TryMoveCommand.cs` | 删除 TryEnemyMoveCommand |

## 删除文件

- `EntityArchitecture/` — PlayerArchitecture.cs, EnemyArchitecture.cs
- `EnemyController.cs`
- `EnemyState.cs`
- `IEnemyAIUtility.cs`
- `EnemyCombatModel` 类, `EnemyEntityModel` 类, `TryEnemyMoveCommand` 类

## 待完成

- [ ] Unity Editor: 场景加 EnemyManagerDriver 的持久化 GameObject（同时负责 Hitstop/Camera 初始化 + EnemyManagerSystem.Update 驱动）
- [ ] Unity Editor: Enemy1 上把 EnemyController 换成 EnemyView
- [ ] UI: PlayerUIManager + EnemyUIManager + 血格 Prefab（设计思考中）
