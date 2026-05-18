# JulyArch v3

基于**能力接口**的 Unity 上层架构框架。接口定义能力，Base 类组合接口，Store 用 C# 原生访问控制保护数据。

## 核心概念

### 能力接口

框架的一等公民。实现哪个接口就获得哪组扩展方法：

| 接口 | 扩展方法 | 说明 |
|------|----------|------|
| `ICanGetStore` | `GetStore<T>()` / `TryGetStore<T>()` | 获取 Store 实例（泛型约束 `T : StoreBase`） |
| `ICanEvent` | `Subscribe<T>()` / `Unsubscribe<T>()` / `UnsubscribeAll()` / `Publish<T>()` | 事件订阅与发布 |
| `ICanGetSystem` | `GetSystem<T>()` | 获取 System 实例 |
| `ICanRunProcedure` | `RunProcedure()` | 运行异步流程 |

所有能力接口继承自 `IArchNode`（根接口），通过 `GetArchitecture()` 获取所属 `IGameContext`。

### Base 类

Base 类是能力接口的便利组合 + 生命周期管理：

| Base 类 | 能力 | 特点 |
|---------|------|------|
| `GameSystemBase` | GetStore + Event + GetSystem + RunProcedure | 帧驱动、业务命令 |
| `ProcedureBase` | GetStore + Event + GetSystem + RunProcedure | 异步长流程，子类 override `OnExecuteAsync` |
| `GameView` | GetStore + Event + GetSystem | MonoBehaviour 视图基类 |
| `StoreBase<TData>` | Event | 数据持有，不访问其他 Store |

框架生命周期方法（`SetArchitecture`、`Load`、`Shutdown`、`ExecuteAsync`）全部为 `internal`，仅 `GameContext` 可调用。业务代码无法直接触碰。

### Store

Store 直接用具体类，无需接口。写方法用 `internal` — 同程序集可写，跨程序集只读：

```csharp
public class ScoreStore : StoreBase<ScoreData>
{
    public int Score => Data.Score;          // public 读
    public int HighScore => Data.HighScore;

    internal void AddScore(int amount)       // internal 写
    {
        Data.Score += amount;
        if (Data.Score > Data.HighScore) Data.HighScore = Data.Score;
        this.Publish(new ScoreChangedEvent(Data.Score));
    }

    internal void ResetScore() => Data.Score = 0;
}
```

`GetStore<ScoreStore>()` 直接返回具体类。`GameContext.RegisterStore` 按 Type 注册，无反射扫描。

### 数据流

```
Input → View → GetSystem().Method() → System → GetStore<T>().Method() → Store
                                                                           ↓
View ← Subscribe(event) ← Publish(event) ←─────────────────────────────────┘
```

- **读**：`GetStore<ScoreStore>().Score`
- **写**：`GetStore<ScoreStore>().AddScore(100)`（仅同程序集可调）
- **通知**：Store 内部 `this.Publish(event)`

## 项目层复合接口

项目根据常用组合定义复合接口：

```csharp
public interface IAppArch : ICanGetStore, ICanEvent, ICanGetSystem { }
public interface IMiniGameArch : ICanGetStore, ICanEvent, ICanGetSystem { }
```

非框架角色通过实现复合接口接入架构：

```csharp
public class ArrowClickHandler : IMiniGameArch
{
    public IGameContext GetArchitecture() => MiniGameArch.Context;
}
```

## IGameContext 职责分离

| 接口/类 | 职责 |
|---------|------|
| `IGameContext` | 消费者接口：GetStore / GetSystem / Event / RunProcedure |
| `GameContext` | 具体类：额外提供 RegisterStore / RegisterSystem / InitializeAsync / Shutdown |

注册和生命周期管理不在 `IGameContext` 上，业务代码只面向消费者接口。

## 约定

| 约定 | 说明 |
|------|------|
| View 不调 Store 写方法 | 修改走 `GetSystem().Method()`，code review 保证 |
| Store 不访问其他 Store | 跨 Store 协调走 System / 事件 |
| Store 写方法是语义操作 | `ResetRound()` 而非 `SetScore(0); SetLevel(0);` |
| Store 写方法用 internal | 编译器保证跨程序集不可写 |
