# RogueLikeGame

2D 像素风 Roguelike 游戏，基于 Unity + 自定义 MVCS 架构。

## 已实现功能

- **随机地图生成**：60×40 网格，5-8 个随机房间 + 走廊连接，岛屿被海洋包围的地形
- **海岸线自动瓦片**：4-bit 邻居掩码自动选择边/角瓦片，岛屿到水体的自然过渡

  **玩家移动与攻击**：WASD 移动，J 攻击，状态机驱动（Idle/Move/Attack/Hurt）
- **战斗系统**：伤害公式 `max(1, atk - def)`，击退 + 震屏 + 卡肉效果
- **敌人 AI**：状态机驱动（Idle/Chase/Attack/Hurt/Dead），根据距离自动切换
- **道具掉落与拾取**：击杀敌人加权随机掉落，触碰拾取进入背包（ScriptableObject 配置）
- **开始/结束界面**：开始 → 战斗 → 胜利/失败 → 重新开始 的完整游戏循环
- **UI 状态管理**：BindableProperty 驱动的面板切换（Start/GamePlay/GameOver）

## 架构

项目使用自定义 MVCS 架构（参考 QFramework），所有组件通过 `RogueLikeGame.Interface` 统一管理。

### 层级

| 层级 | 目录 | 职责 |
|------|------|------|
| **Controller** | `ViewController/` | 挂载到节点，接收事件，管理视图表现 |
| **System** | `System/` | 纯逻辑实现，可直接修改 Model |
| **Model** | `Model/` | 存放数据，通过 BindableProperty 和 Event 向上通知 |
| **Utility** | `Utility/` | 基础设施（输入、动画、震屏、对象生成等） |
| **Command** | `Command/` | 无状态命令，Controller 通过它修改 System/Model |

### 通信规则

- 上层 → 下层：方法调用（Controller 必须通过 Command）
- 下层 → 上层：Event 或 BindableProperty
- Utility 不访问任何架构内对象

## 游戏流程

```
开始界面 → 点击开始
  → 生成随机地图（MapGeneratorSystem）
  → MapBuilder 渲染 Tilemap（含海岸线自动瓦片）
  → 生成玩家 + 敌人（EnemyManagerSystem）
  → 战斗（玩家 WASD 移动 / J 攻击，敌人 FSM 自动追击）
  → 击杀掉落道具（DropSystem → ItemPickup → ItemModel）
  → 全部敌人死亡 = 胜利 / 玩家死亡 = 失败
  → 结束界面 → 重新开始
```
