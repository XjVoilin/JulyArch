# JulyArch v3

基于**能力接口**的 Unity 上层架构框架。接口定义能力，Base 类组合接口，Store 用 C# 原生访问控制保护数据。

## 核心概念

### IScope — 架构上下文消费者接口

`IScope` 是架构的消费者视角，通过它访问 Store、System、View、Event、Procedure：

```csharp
public interface IScope
{
    T GetStore<T>() where T : StoreBase;
    T GetSystem<T>() where T : class;
    T GetView<T>() where T : GameView;
    IEventBus Event { get; }
    UniTask RunProcedure(ProcedureBase procedure, CancellationToken ct = default);
}
```

`ArchContext` 是 `IScope` 的具体实现类，额外提供注册/初始化/Shutdown 等管理 API。

### 能力接口

框架的一等公民。实现哪个接口就获得哪组扩展方法：

| 接口 | 扩展方法 | 说明 |
|------|----------|------|
| `ICanGetStore` | `GetStore<T>()` | 获取 Store 实例（约束 `T : StoreBase`） |
| `ICanEvent` | `Subscribe<T>()` / `Unsubscribe<T>()` / `UnsubscribeAll()` / `Publish<T>()` | 事件订阅与发布 |
| `ICanGetSystem` | `GetSystem<T>()` | 获取 System 实例 |
| `ICanGetView` | `GetView<T>()` | 获取可定位 View（需标记 `ISingletonView`） |
| `ICanRunProcedure` | `RunProcedure()` | 运行异步流程 |

所有能力接口继承自 `IArchNode`（根接口），通过 `GetScope()` 获取所属 `IScope`。

### Base 类

Base 类是能力接口的便利组合 + 生命周期管理：

| Base 类 | 能力 | 特点 |
|---------|------|------|
| `GameSystemBase` | GetStore + Event + GetSystem + RunProcedure + GetView | 帧驱动、业务命令 |
| `ProcedureBase` | GetStore + Event + GetSystem + RunProcedure + GetView | 异步长流程，子类 override `OnExecuteAsync` |
| `GameView` | GetStore + Event + GetSystem + GetView | MonoBehaviour 视图基类 |
| `StoreBase<TData>` | Event | 数据持有，不访问其他 Store |

框架生命周期方法全部为 `internal`，仅 `ArchContext` 可调用。

### GameView 生命周期钩子

`GameView` 独占 Unity 的 `Awake` / `OnDestroy` / `OnEnable` / `OnDisable`，子类使用对应钩子：

| Unity 方法 | 子类钩子 | 框架行为 |
|---|---|---|
| `Awake` | `OnViewAwake()` | ISingletonView 注册 → 调 OnViewAwake |
| `OnDestroy` | `OnViewDestroy()` | 调 OnViewDestroy → ISingletonView 注销 |
| `OnEnable` | `OnViewEnable()` | 调 OnViewEnable |
| `OnDisable` | `OnViewDisable()` | UnsubscribeAll → 调 OnViewDisable |

### ISingletonView — View 可定位性

标记 `ISingletonView` 的 `GameView` 可被 `GetView<T>()` 检索（场景内唯一）。

### Store

Store 直接用具体类，无需接口。写方法用 `internal` — 同程序集可写，跨程序集只读：

```csharp
public class ScoreStore : StoreBase<ScoreData>
{
    public int Score => Data.Score;          // public 读

    internal void AddScore(int amount)       // internal 写
    {
        Data.Score += amount;
        this.Publish(new ScoreChangedEvent(Data.Score));
    }
}
```

`GetStore<ScoreStore>()` 直接返回具体类。`ArchContext.RegisterStore` 按 Type 注册。

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

项目根据常用组合定义复合接口，非框架角色通过实现复合接口接入架构：

```csharp
public interface IAppArch : ICanGetStore, ICanEvent, ICanGetSystem { }

public class MyHandler : IAppArch
{
    public IScope GetScope() => AppArch.Context;
}
```

## Scope 绑定

`GetScope()` 是 `IArchNode` 要求的方法，每个参与架构的类都需实现：

- **GameView 子类**：override `GetScope()`，返回所属的 ArchContext
- **纯 C# 类**：实现能力接口 + `GetScope()`

框架本身不提供全局 ArchContext 实例，具体 scope 由项目层定义。

## 约定

| 约定 | 说明 |
|------|------|
| View 不调 Store 写方法 | 修改走 `GetSystem().Method()`，code review 保证 |
| Store 不访问其他 Store | 跨 Store 协调走 System / 事件 |
| Store 写方法是语义操作 | `ResetRound()` 而非 `SetScore(0); SetLevel(0);` |
| Store 写方法用 internal | 编译器保证跨程序集不可写 |
| GetScope() 由项目层提供 | 框架包不预设全局实例 |
