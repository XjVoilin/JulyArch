# JulyArch v3

基于**能力接口**的 Unity 上层架构框架。接口定义能力，Base 类组合接口，Store 用 C# 原生访问控制保护数据。

> **本文档描述框架的真实行为，与 `Runtime/` 代码一一对应。** 若代码与文档冲突，以代码为准并提 issue 修正文档。

## 核心机制

### 全局 `ArchContext`

框架只有一个架构上下文类 `ArchContext`，通过 `ArchContext.Current` 静态属性暴露当前实例。第一个被构造的 `ArchContext` 成为 `Current`，`Shutdown` 时若自己是 `Current` 则清空。

> 与某些依赖注入框架不同，JulyArch **不**走"每个节点持有自己的 Scope 引用"的模型——所有能力访问都落在 `ArchContext.Current` 上（见下文扩展方法）。项目层只需在启动期构造一个 `ArchContext` 并完成 Store/System 注册即可，无需为每个节点手写 scope 绑定。

`ArchContext` 提供：

- **注册/注销**：`RegisterStore` / `RegisterSystem` / `RegisterView`（及对应 Unregister）
- **生命周期**：`InitializeAsync` / `Update` / `Shutdown`
- **查询**：`GetStore<T>()` / `GetSystem<T>()` / `GetView<T>()`
- **流程**：`RunProcedure(ProcedureBase)` / `Event`（`IEventBus`）

### 能力接口

框架的一等公民。能力接口是**空标记接口**（继承自根接口 `IArchNode`），实现哪个接口就获得哪组扩展方法，扩展方法内部转调 `ArchContext.Current`：

| 接口 | 扩展方法 | 说明 |
|------|----------|------|
| `ICanGetStore` | `GetStore<T>()` | 获取 Store 实例（约束 `T : StoreBase`） |
| `ICanEvent` | `Subscribe<T>()` / `Unsubscribe<T>()` / `UnsubscribeAll()` / `Publish<T>()` | 事件订阅与发布 |
| `ICanGetSystem` | `GetSystem<T>()` | 获取 System 实例（按具体类型 + 基类 + 非框架接口查询） |
| `ICanGetView` | `GetView<T>()` | 获取可定位 View（需标记 `ISingletonView`） |
| `ICanRunProcedure` | `RunProcedure()` | 运行异步流程 |

> `IArchNode` 是根标记接口，**不携带 `GetScope()` 方法**。它的唯一作用：在 `RegisterSystem` 过滤掉框架内部能力接口，防止按 `ICanGetStore` 等把 System 注册成多个接口键。

纯 C# 类要接入架构，只需实现所需能力接口即可，无需任何额外方法：

```csharp
public class MyHandler : ICanGetStore, ICanEvent, ICanGetSystem
{
    // this.GetStore<T>() / this.Subscribe<T>() / this.GetSystem<T>() 立即可用，
    // 全部转调 ArchContext.Current。
}
```

### Base 类

Base 类是能力接口的便利组合 + 生命周期管理。框架生命周期方法全部为 `internal`，仅 `ArchContext` 可调用：

| Base 类 | 能力 | 特点 |
|---------|------|------|
| `SystemBase` | GetStore + Event + GetSystem + RunProcedure + GetView | 帧驱动（实现 `IUpdatableSystem` 时）、业务命令 |
| `ProcedureBase` | GetStore + Event + GetSystem + RunProcedure + GetView | 异步长流程，子类 override `OnExecuteAsync` |
| `GameView` | GetStore + Event + GetSystem + GetView | MonoBehaviour 视图基类 |
| `StoreBase<TData>` | Event（内部 Publish） | 数据持有，不访问其他 Store |

### 事件订阅清理约定

`IEventBus` 支持 owner 追踪，框架在两处兜底 `UnsubscribeAll`，业务侧无需手动逐个注销：

- **`GameView`**：`OnDisable` 中自动 `UnsubscribeAll(this)`
- **`SystemBase`**：`Shutdown` 中自动 `UnsubscribeAll(this)`（在 `OnShutdown` 之前）
- **`ProcedureBase`**：`ArchContext.RunProcedure` 执行结束（含异常/取消）后自动 `UnsubscribeAll(procedure)`

System/Procedure 若在运行期订阅了事件，正常情况下不需要在 `OnShutdown` 里手写 `Unsubscribe`——但显式注销仍然合法（去重，不会报错）。

## GameView 生命周期钩子

`GameView` 独占 Unity 的 `Awake` / `OnDestroy` / `OnEnable` / `OnDisable`，子类使用对应钩子，**禁止直接覆写这四个 Unity 方法**：

| Unity 方法 | 子类钩子 | 框架行为 |
|---|---|---|
| `Awake` | `OnViewAwake()` | 实现了 `ISingletonView` 则注册到 `ArchContext` → 调 `OnViewAwake` |
| `OnDestroy` | `OnViewDestroy()` | 调 `OnViewDestroy` → 实现了 `ISingletonView` 则注销 |
| `OnEnable` | `OnViewEnable()` | 调 `OnViewEnable` |
| `OnDisable` | `OnViewDisable()` | `UnsubscribeAll` → 调 `OnViewDisable` |

### ISingletonView — View 可定位性

空标记接口（无任何方法）。实现它的 `GameView` 在 `Awake` 时自动注册到 `ArchContext`，可被 `GetView<T>()` 检索（场景内按类型唯一）。多实例对象（棋子、敌人、列表项）**不应**实现此接口——开发期出现第二个同类型实例会抛异常快速失败，发布期保留先注册者并忽略后来者。

## Store

Store 直接用具体类，无需接口。写方法用 `internal` —— 同程序集可写，跨程序集只读：

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

`GetStore<ScoreStore>()` 直接返回具体类。`ArchContext.RegisterStore` 按 `Type` 注册。

**异步加载**：Store 实现 `IAsyncLoadable` 标记接口即声明需要异步 `OnLoadAsync()`。`ArchContext.InitializeAsync` 会把异步 Store 收集起来 `WhenAll` 并行加载，同步 Store 走 `Load()`，全部就绪后统一 `Ready()`。异步 Store 误用同步 `Load()` 会抛 `InvalidOperationException`。

## 数据流

```
Input → View → GetSystem().Method() → System → GetStore<T>().Method() → Store
                                                                           ↓
View ← Subscribe(event) ← Publish(event) ←─────────────────────────────────┘
```

- **读**：`GetStore<ScoreStore>().Score`
- **写**：`GetStore<ScoreStore>().AddScore(100)`（仅同程序集可调）
- **通知**：Store 内部 `this.Publish(event)`

## 约定

| 约定 | 说明 |
|------|------|
| View 不调 Store 写方法 | 修改走 `GetSystem().Method()`，code review 保证 |
| Store 不访问其他 Store | 跨 Store 协调走 System / 事件 |
| Store 写方法是语义操作 | `ResetRound()` 而非 `SetScore(0); SetLevel(0);` |
| Store 写方法用 internal | 编译器保证跨程序集不可写 |
| 事件订阅靠框架兜底注销 | GameView/System/Procedure 无需手动逐个 Unsubscribe |
