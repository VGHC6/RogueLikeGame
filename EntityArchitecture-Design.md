# EntityArchitecture 设计文档

## 定位

`EntityArchitecture` 是非单例的 `IAchitecture` 实现。每个实体（Player / Enemy）持有一个，作为该实体的专属 IoC 容器。

详见 `FSM-Architecture-Design.md` 第一节。

## 为什么需要

- FSM 状态类有实例字段（`_elapsedTime`、`_hitChecked`），必须每个实体持有独立实例
- 全局 `IOCContainer` 按类型存单例，无法满足多实例需求
- `EntityArchitecture` 保持和 `Architecture<T>` 完全一致的 API，代码无需重写

## 和 Architecture<T> 的对比

| | Architecture<T> | EntityArchitecture |
|---|---|---|
| 实例数 | 全局唯一（单例） | 每个实体一个 |
| Register / Get API | 相同 | 相同 |
| SendCommand / SendEvent API | 相同 | 相同 |
| 事件总线 | 全局 | 本地（实体间隔离） |
| 查找未命中 | 返回 null | 回退到父级（全局） |
| 适用资源 | 共享服务 | 实体私有状态 |

## 完整实现

见 `FSM-Architecture-Design.md` 第一节。
