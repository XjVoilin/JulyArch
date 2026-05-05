# JulyArch v2 设计文档

> 版本：v2 设计稿
> 状态：方案待评审
> 创建日期：2026-05-04
> 适用范围：JulyArch 框架本体 + GooseMarket 项目所有 System / Store / View / 小游戏代码

---

## 一、背景

### 1.1 v1 现状回顾

JulyArch v1 是 MVI / Flux 风格的单向数据流框架，核心角色：

| 角色 | 时间模型 | 异步能力 |
|---|---|---|
| `Store` | 状态-时刻 | 仅 `IAsyncLoadable.LoadAsync` |
| `System` | 帧驱动（OnUpdate / OnLateUpdate） | 接口 void，子类自由 |
| `Mutation` | 同步原子状态跃迁 | ❌ |
| `View` | OnEnable / OnDisable 生命周期 | ❌ |
| `EventBus` | 同步发布 | ❌ |

通信方式：`View → Mutate → Store → Event → View 刷新`，控制流由 System 帧驱动 + 事件订阅推动。

Cap 接口体系（`ICanQuery / ICanGetSystem / ICanMutate / ICanGetStore / ICanEvent / ICanRegister`）的设计意图是通过显式实现接口来**门控**不同角色的能力。

### 1.2 v1 的根本问题

**框架完全缺失"过程（Procedure）"维度**。所有角色都是状态-时刻模型，没有任何角色专门承载"持续一段时间、有多步、可被 await、可被 cancel 的过程"。

后果：

- **盒子层** 用得顺，因为编排对象是 JulyCore 的 Provider（`GF.Scene` / `GF.UI` / `GF.Resource`），这些 Provider 已是异步可 await 的服务，System 不需要持有 View
- **小游戏层** 用得撕裂，因为关卡流程的"被等待对象"包含表现层动画（`MiniGameTransitionView.PlayAsync()` / `GooseFindAnimator.PlayBoardEntryAsync()`），框架没有合规的角色去 await 它们，System 被迫违反原则直接持有 View 引用

具体证据（v1 代码）：

- `GooseFindGameSystem` 持有 `_gestureDetector` / `_boardView` / `_transitionView` 三个 View 引用，并在 `AdvanceToNextLevelAsync` 中 `await _transitionView.PlayAsync()`
- `GooseTDGameSystem` 持有 6 个 View 引用，且 `InitViews()` 反向把 `this`（System）注入回 View，形成双向耦合
- `Game101Lifecycle.OnGameReady` 持有 5 个 View + 3 个非框架角色 Manager，本质是个"关卡导演"但框架未承认此角色

### 1.3 v1 的次要问题

**public Cap 体系是"声明式标记"而非真正门控**。`GameSystemBase` 没实现 `ICanEvent`，但通过 `protected Subscribe<T>` / `protected Publish<T>` 把等价能力暴露给子类（`GameView` 同理）；所有 public Cap 接口（`ICanQuery` / `ICanMutate` / `ICanGetSystem` / `ICanEvent`）业务侧都能随意实现，无法在编译期阻止越权。

**但这并非必须修的问题，而是可接受的设计权衡**。Cap 接口真正发挥门控作用的是 internal 接口 `ICanGetStore`——业务 assembly 看不到该接口，只有继承 `GameSystemBase` / `ProcedureBase` 的类才能间接调到 GetStore；业务代码无法伪装出该能力。其余 public Cap 如果强制业务侧在类签名上显式声明（即编译期门控），代价是所有业务代码增加心智负担，而收益只是"代码审阅时能看到声明"——性价比不高。**v2 保持 internal Cap 的强门控，public Cap 维持 v1 的弱门控语义（基类 protected + 扩展方法兜底），不强制显式声明**。

### 1.4 修复方向

参照所有成熟的 Store-based 架构（Redux + Saga/Thunk/Effect、MVI + Use Case、GameFramework + Procedure），**把"状态变更"和"过程编排"分开放是必然的**：

- Mutation 必须严守同步原子性 —— 否则破坏所有 store-based 架构的核心保证
- Procedure 必须独立成第二类原语 —— 否则要么 Mutation 变 async（破坏原子性），要么 Procedure 被切成无数 Mutation 用事件串联（callback hell）

JulyArch v1 正确地把 Mutation 设计成 readonly struct + 同步 + 跨 Store 原子操作，但**没有补 Procedure 这个搭档**。v2 的核心任务就是补上这个搭档，并借机把 Cap 接口闭环。

---

## 二、设计目标与非目标

### 2.1 目标

1. **补 Procedure 角色**：作为长流程编排的 first-class 原语，可 await / cancel / 嵌套
2. **确立 Procedure 的依赖注入模式**：View 引用通过 Procedure 构造函数显式传入，框架不维护 View 注册表（避免双生命周期问题）
3. **收紧 internal Cap 门控**：`ICanGetStore` 保持 internal —— 业务 assembly 看不到该接口，无法在非派生类上"伪装出" Store 写能力。public Cap 保持 v1 的弱门控模式（基类 protected 暴露快捷方法），不强制业务侧在类签名上显式声明
4. **保留 Mutation / EventBus / Store 不变**：v1 这部分设计是正确的
5. **现有代码渐进迁移**：盒子层几乎不动（仅个别命名调整），小游戏层重写违规部分（拆 Procedure），无需一次性全改

### 2.2 非目标

1. **不引入 Mutation / Event 的异步版本** —— 异步语义全部归 Procedure
2. **不拆分 `IGameSystem` 基类** —— 单一基类已足够，靠 Cap 接口表达职责差异
3. **不引入运行时反射门控** —— 编译期接口门控 + IDE 警告即可，避免 WebGL 反射限制
4. **不维护 Procedure 注册表** —— Procedure 是一次性的，每次 new
5. **不维护 View 注册表** —— View 是 MonoBehaviour，有 Unity 生命周期；框架维护 View 注册表必然制造双生命周期错位问题。Procedure 通过构造函数显式接收依赖
6. **不引入 SceneScopedContext / ProcedureTree** —— 保持框架简单，作用域追踪由调用方显式管理 CancellationToken

### 2.3 兼容性约束

- Unity 2022.3.62f2 + URP 2D
- 微信 / 抖音小游戏（WebGL/WASM）：不依赖反射、JIT
- 30 FPS 性能预算：热路径零 GC Alloc；Procedure 调用本身不在热路径，允许微小 GC（一次 new + AsyncStateMachine）

---

## 三、v2 角色全景

### 3.1 角色表

| 角色 | 时间模型 | 帧驱动 | 异步 | 能拿 Store | 能拿 View | 能改 Store | 能发 Event | 能跑 Procedure |
|---|---|---|---|---|---|---|---|---|
| `Store` | 状态 | ❌ | LoadAsync 一次性 | 自身 | ❌ | 自身 | `Publish`（protected） | ❌ |
| `Mutation` | 状态跃迁（同步） | ❌ | ❌ | `ctx.GetStore<T>()`（IMutationContext 由框架传入） | ❌ | ✅ 跨 Store | `ctx.Publish()` | ❌ |
| `System` | 业务命令 + 帧逻辑 | ✅ | 自由 | `GetStore<T>()`（protected） | **约定不持有**（需 await View → 拆 Procedure） | `Mutate`（protected） | `Publish`（protected） | `RunProcedure(...).Forget()`（protected） |
| `Procedure`（**新**） | 长流程编排 | ❌ | ✅ ExecuteAsync | `GetStore<T>()`（protected） | **构造函数注入** | `Mutate`（protected） | `Publish`（protected） | `await RunProcedure(...)`（protected） |
| `GameView`（被动 View） | 被动渲染 | 不变 | ❌ | ❌ | 自身 | `Mutate`（protected） | `Subscribe`（protected） | ❌ |
| `EventBus` | 同步通知 | — | ❌ | — | — | — | — | — |

### 3.2 数据流与控制流

```
        ┌───────── User Input ─────────┐
        ▼                              ▼
   [GameView] ──────────────► [System.PublicMethod()]
                                    │
                                    ├─► Mutate(...) ─► [Store] ─► Event ─► [GameView]
                                    │
                                    └─► RunProcedure(new XxxProcedure(viewRefs)) ─► [Procedure]
                                                                  │
                                                                  ├─► await _view.PlayAsync(ct)   // 构造函数注入的 View 引用
                                                                  ├─► Mutate(...)
                                                                  ├─► Publish(...)
                                                                  └─► await RunProcedure(new ChildProcedure(...))
```

**关键变化**：System 不再持有 View。需要 await 表现层的代码全部归到 Procedure。Procedure 通过构造函数接收 View 引用（由调用方——通常是 Lifecycle——负责传入）。System 退回到"接受外部命令、改 Store、跑帧逻辑"三件本职。

---

## 四、新增角色 1：Procedure

### 4.1 接口设计

```csharp
namespace JulyArch
{
    public interface IProcedure : IArchitectureSettable
    {
        UniTask ExecuteAsync(CancellationToken ct);
    }
}
```

与 `IStore : IArchitectureSettable`、`IGameSystem : IDisposable, IArchitectureSettable` 对齐。

Procedure 只有无返回值版本。编排过程的结果应落入 Store，调用方通过 Query 获取——保持 Store 作为唯一真相源。如果将来出现真实需求，加 `IProcedure<T>` 是纯增量操作。

### 4.2 基类

**能力暴露方式**（与 `GameSystemBase` 对齐）：

- **所有常用能力通过 protected 方法暴露**：`Query` / `GetSystem` / `GetStore` / `Mutate` / `Subscribe` / `Publish` / `RunProcedure`。业务子类直接调用即可，不需要在类签名上显式声明 Cap 接口。
- **internal Cap**（`ICanGetStore`）：基类自身实现，因此业务继承基类可间接拿到 GetStore；但业务的非派生类（如任意普通 class）无法通过 `ArchExtensions` 伪造出 GetStore（internal 不可见）。这是 Store 写能力的强门控入口。
- **View 引用**：不走框架——通过**构造函数**从调用方接收。见 4.4。

```csharp
namespace JulyArch
{
    public abstract class ProcedureBase
        : IProcedure, IArchitectureSettable,
          ICanQuery, ICanGetSystem, ICanMutate, ICanEvent, ICanRunProcedure, ICanGetStore
    {
        private GameContext _architecture;
        public IGameContext GetArchitecture() => _architecture;
        void IArchitectureSettable.SetArchitecture(GameContext ctx) => _architecture = ctx;

        public abstract UniTask ExecuteAsync(CancellationToken ct);

        // ---- Query / State ----
        protected TQueries Query<TQueries>() where TQueries : class, IStoreQueries
            => _architecture.Query<TQueries>();

        protected T GetStore<T>() where T : class, IStore
            => _architecture.GetStoreInternal<T>();

        // ---- System ----
        protected T GetSystem<T>() where T : class, IGameSystem
            => _architecture.GetSystem<T>();

        // ---- Mutation ----
        protected MutationResult Mutate(IMutation mutation)
            => _architecture.Mutate(mutation);

        // ---- Events ----
        protected void Subscribe<T>(Action<T> handler)
            => _architecture.Event.Subscribe(handler, this);

        protected void Publish<T>(T evt)
            => _architecture.Event.Publish(evt);

        // ---- Procedure 嵌套 ----
        protected UniTask RunProcedure(IProcedure child, CancellationToken ct = default)
            => _architecture.RunProcedure(child, ct);
    }
}
```

业务子类直接 override `ExecuteAsync`。取消清理用 try/finally，错误处理用 try/catch——和普通 async 方法一样，无额外心智负担。

### 4.3 GameContext 入口

```csharp
public async UniTask RunProcedure(IProcedure procedure, CancellationToken ct = default)
{
    if (!_initialized) throw new InvalidOperationException("...");
    procedure.SetArchitecture(this);
    await procedure.ExecuteAsync(ct);
}
```

### 4.4 业务侧定义示例（含 View 依赖注入）

所有能力来自 `ProcedureBase` 的 protected 方法，业务子类无需在类签名上声明 Cap 接口。

**View 引用**通过构造函数传入。构造函数签名即 Procedure 的依赖契约：

```csharp
namespace MiniGames.Game101
{
    public class AdvanceLevelProcedure : ProcedureBase
    {
        private readonly MiniGameTransitionView _transition;

        public AdvanceLevelProcedure(MiniGameTransitionView transition)
        {
            _transition = transition;
        }

        public override async UniTask ExecuteAsync(CancellationToken ct)
        {
            var store = GetStore<GooseFindStore>();       // protected（来自基类）
            if (store.IsTransitioning) return;
            store.SetTransitioning(true);
            try
            {
                store.SetState(GameState.Loading);
                await UniTask.Delay(500, cancellationToken: ct);

                await _transition.PlayAsync(ct);          // 构造函数注入的引用

                store.AdvanceLevel();
                await RunProcedure(new StartLevelProcedure(), ct);
            }
            finally { store.SetTransitioning(false); }
        }
    }
}
```

**依赖数量多时，业务侧自行打包** —— 框架不定义打包格式：

```csharp
// 业务侧打包，放在小游戏 Assembly 内
public sealed class Game101SceneRefs
{
    public MiniGameTransitionView Transition;
    public GooseFindBoardView Board;
    public GestureDetector Gesture;
    public HintOverlayView Hint;
}

// Lifecycle 组装
var refs = new Game101SceneRefs
{
    Transition = Object.FindObjectOfType<MiniGameTransitionView>(true),
    Board = Object.FindObjectOfType<GooseFindBoardView>(),
    // ...
};

// Procedure 接收打包对象
await ctx.RunProcedure(new EnterGameProcedure(refs));
```

子 Procedure 复用父 Procedure 的 refs 对象即可，无需逐层传散参。

**小游戏可以共用一个 SceneRefs，模拟经营等大型项目可按功能分多个**（`BuildingRefs` / `UIRefs` / `NpcRefs`）——粒度由业务方决定。

### 4.5 触发 Procedure 的几种姿势

```csharp
// 1. 在 System / Procedure 基类子类内（走 protected RunProcedure）
await RunProcedure(new AdvanceLevelProcedure(refs.Transition), ct);

// 2. 在 OnUpdate 内 fire-and-forget（OnUpdate 是 void，不能 await）
RunProcedure(new SomeFxProcedure(), _cts.Token).Forget();

// 3. 在 Lifecycle / 外部代码 / 测试内（直接调 Context）
await ctx.RunProcedure(new EnterGameProcedure(refs), ct);
```

**Lifecycle 走哪条路径**：项目层的 `IMiniGameLifecycle` 不继承框架基类，通过持有 `GameContext` 引用调用 `ctx.RunProcedure(...)`。这与 v1 里 Lifecycle 持有 ctx 调 `ctx.RegisterStore` / `ctx.GetSystem` 的方式完全一致，无新增心智。

### 4.6 嵌套与取消语义

- **嵌套**：Procedure 内可调 `RunProcedure(child)`，无层数限制，无隐式作用域追踪
- **取消传递**：调用方显式传 CancellationToken；父 Procedure 持有 CTS 时通常透传子 Procedure
- **异常与清理**：业务侧在 `ExecuteAsync` 内自行 try/catch/finally（如 `store.SetTransitioning(false)`）。框架不包装、不吞错、不提供额外钩子

### 4.7 何时该用 Procedure

**用 Procedure 的判定条件（满足任意一条）：**
1. 需要在流程中 `await` 表现层异步操作（动画、镜头、UI 入场/退场）
2. 需要 `await` 资源/场景异步加载（YooAsset / 场景切换），且加载后还要接续多步业务
3. 流程跨多个 Store，需要**顺序执行**且中间存在异步点
4. 流程可被取消（进度到一半玩家点了返回）

**不用 Procedure 的场景：**
- 纯同步状态变更 → 直接 Mutation
- 单次点击反馈、单次数值计算 → System 的 public 方法 + Mutation
- 监听事件后纯同步响应 → Event Handler 里直接处理
- 帧驱动持续逻辑（倒计时、物理模拟） → System.OnUpdate

**判断口诀**：`await` 出现在流程里 → Procedure；全是瞬时操作 → Mutation / System 即可。

### 5.1 为什么框架不管 View

框架管理 Store / System 是可行的——它们是纯 C# 对象，没有 Unity 生命周期，Context 全权管理。

框架管理 View 是不可行的——View 是 MonoBehaviour，受 Unity 生命周期管理（场景、GameObject 销毁）。如果框架维护 View 注册表，就会出现双生命周期错位：

- Unity 销毁 View 时，框架注册表持有的引用变成 fake null
- Context 销毁时，View 可能还在场景里
- Procedure 在 GetView 后到 await 过程中，View 可能被销毁

成熟 DI 框架（VContainer / Zenject）为此引入 Scope + 弱引用机制——代价巨大。JulyArch 选择另一条路：**框架不碰 View，依赖通过构造函数显式注入**。

### 5.2 依赖注入模式

**Procedure 只有一条 View 获取路径：构造函数**。

```csharp
public class BuildingProcedure : ProcedureBase, ICanEvent
{
    private readonly BuildingView _view;
    private readonly int _buildingId;

    public BuildingProcedure(BuildingView view, int buildingId)
    {
        _view = view;
        _buildingId = buildingId;
    }

    public override async UniTask ExecuteAsync(CancellationToken ct)
    {
        await _view.PlayBuildAnimationAsync(ct);
        // ...
    }
}

// 调用方（Lifecycle / System / 父 Procedure）负责传入
await ctx.RunProcedure(new BuildingProcedure(clickedBuilding.View, clickedBuilding.Id));
```

### 5.3 与 Composition Root 模式对齐

Lifecycle 承担"依赖装配"职责——这是 DI 的 Composition Root 模式在游戏场景下的自然对应：

```csharp
public async UniTask OnGameEnter(GameStartParams startParams)
{
    // 1. 收集场景级引用
    var refs = new Game101SceneRefs
    {
        Transition = Object.FindObjectOfType<MiniGameTransitionView>(true),
        Board = Object.FindObjectOfType<GooseFindBoardView>(),
        Gesture = Object.FindObjectOfType<GestureDetector>(),
    };

    // 2. 保存给自己后续用
    _refs = refs;

    // 3. 发起启动 Procedure
    await ctx.RunProcedure(new EnterGameProcedure(refs));
}
```

**Lifecycle 不 await 长流程逻辑**——那是 Procedure 的职责。Lifecycle 只做三件事：
1. 收集依赖
2. 创建 Procedure 并 Run
3. 清理

### 5.4 支持的 View 场景

| View 类型 | 处理方式 |
|---|---|
| 场景级稳定 View（小游戏） | Lifecycle 在 OnGameEnter 中 `FindObjectOfType` 获取，传给 Procedure |
| Prefab 动态实例化 View | Procedure 内自己 `Instantiate`，或由调用方传入已实例化的引用 |
| 多实例 View（多个建筑） | 调用方传入具体实例（通常来自玩家点击目标） |
| 跨场景 View | 由业务方在场景切换时更新自己持有的引用，不涉及框架 |

**所有场景都通过"调用方显式传引用"解决**，没有注册表介入。

### 5.5 安全约束

- **Procedure 生命周期 ≤ 传入引用的生命周期**：调用方必须保证在 Procedure 执行完之前，传入的 View 不会被销毁；如果有可能被销毁，调用方应传取消 token（Procedure 在 await 点响应 cancel）
- **Procedure 不得跨 await 持有「可能被销毁」的引用**：如果必须持有短生命周期引用，Procedure 应在 await 之前一次性取完需要的数据，而非 await 后再访问
- **框架不检测引用有效性**：这是 C# 对象所有权问题，不是框架职责

---

## 六、Cap 接口体系

### 6.1 设计定位

Cap 接口体系在 v2 的定位是"**能力标记 + 扩展方法挂载点**"，不是编译期物理门控（除 `ICanGetStore` 外）。因此：

- **不要求业务侧在类签名上显式声明** public Cap 接口
- 业务能力统一通过 base 类 protected 方法获取（简洁、不需要 `this.` 前缀）
- `ArchExtensions` 扩展方法保留给"无法继承框架 base 类的项目层类"使用（如 UI 框架的 `UIBase` 子类通过 `IAppArch` 声明归属后用 `this.Query<T>()`）

### 6.2 门控强度

| 门控类型 | 实现方式 | 覆盖接口 | 业务能否绕过 |
|---|---|---|---|
| **强门控** | `internal` interface + base 类自身实现 + protected 方法 | `ICanGetStore` | ❌ 不能——业务 assembly 看不到 interface |
| **弱门控** | `public` interface + base 类 protected 方法 + `ArchExtensions` 兜底 | `ICanQuery` / `ICanMutate` / `ICanEvent` / `ICanGetSystem` / `ICanRunProcedure` / `ICanRegister` | ✅ 可以——任何类都能声明，扩展方法都能调 |

**关键**：**`ICanGetStore` 是唯一真正发挥门控作用的 Cap 接口**。它决定了"谁能直接改 Store"——只有 `GameSystemBase` / `ProcedureBase` 的派生类能用。其余 public Cap 不构成物理屏障，靠**开发规范 + code review** 保证使用纪律。

### 6.3 v2 相比 v1 的变化

| 方面 | v1 | v2 |
|---|---|---|
| `ICanGetStore` 门控 | 已经是 internal，已达强门控 | 不变 |
| public Cap 门控 | 弱门控（基类 protected + 扩展方法） | 不变 |
| 新增 `ICanRunProcedure` | — | 新增：Procedure 能力的标记 + 扩展方法（弱门控） |
| 基类 protected API | 分散于 `StoreBase` / `GameSystemBase` / `GameView` | 不变（v1 已合理） |
| 基类 Cap 接口声明 | `GameSystemBase` / `GameView` / `StoreBase` protected 暴露 `Subscribe / Publish` 但没声明 `ICanEvent`——"基类能力"与"基类 Cap 声明"不一致 | **补齐** `ICanEvent` 声明，让三个基类的 Cap 列表反映其真实能力（零业务破坏） |
| `ArchExtensions.GetStore` internal 扩展 | 存在但无人调用（死代码） | **删除** |

**实际改动清单**：
1. 新增 `Procedure/IProcedure.cs` / `Procedure/ProcedureBase.cs` / `Architecture/ICanRunProcedure.cs`
2. `GameContext` / `IGameContext` 加 `RunProcedure` 方法
3. `GameSystemBase` 加 `ICanRunProcedure` 接口标记 + `protected UniTask RunProcedure(...)`
4. `StoreBase` / `GameSystemBase` / `GameView` 补声明 `ICanEvent`（零业务破坏的语义修正）
5. `ArchExtensions` 新增 `RunProcedure` 扩展；删除未使用的 internal `GetStore` 扩展

### 6.4 基类 Cap 接口声明规则

为让类签名诚实反映能力，v2 确立规则：**基类实现某个 Cap 接口 ↔ 基类通过 protected 暴露了该 Cap 对应的能力**。

| 基类 | Cap 接口声明（v2） | 对应 protected 方法 |
|---|---|---|
| `StoreBase<TData>` | `IStore, ICanQuery, ICanEvent` | `Query` / `PublishEvent` |
| `GameSystemBase` | `IGameSystem, ICanQuery, ICanGetSystem, ICanMutate, ICanEvent, ICanRunProcedure, ICanGetStore` | `Query` / `GetSystem` / `Mutate` / `Subscribe` / `Publish` / `RunProcedure` / `GetStore` |
| `GameView` | `MonoBehaviour, ICanQuery, ICanGetSystem, ICanMutate, ICanEvent` | `Query` / `GetSystem` / `Mutate` / `Subscribe` |
| `ProcedureBase`（新） | `IProcedure, IArchitectureSettable, ICanQuery, ICanGetSystem, ICanMutate, ICanEvent, ICanRunProcedure, ICanGetStore` | 同 `GameSystemBase` 的能力集合（不含 OnUpdate 等生命周期） |

业务子类继承基类后，自动获得这些 Cap 能力，不需要再在类签名上重复声明。

### 6.5 关键约束（规范性 + 物理性）

**物理保证：**
- **`ICanGetStore` internal**：业务无法在非派生类上直接改 Store
- **Mutation 无异步**：`IMutation.Execute` 返回 `MutationResult`（同步）而非 `UniTask`
- **Procedure 的 View 引用只来自构造函数**：框架不提供 `GetView` / View 注册表

**规范约束（靠 code review）：**
- **System 不持有 View**：需要 await View 时拆 Procedure，由 Procedure 通过构造函数接收 View
- **Mutation 只做同步原子状态变更**：可在 Execute 内通过 `ctx.GetStore<T>()` 写多个 Store
- **Store 不调 Mutate / RunProcedure**：Store 只管自身数据；跨 Store 写用 Mutation；异步编排用 Procedure
- **Event Handler 不 await 长流程**：需要的话，在 Handler 里 fire-and-forget 一个 Procedure

---

## 七、System / Mutation / EventBus / Store 不变之处

v1 设计正确的部分，v2 不动：

- **`IMutation.Execute(IMutationContext)` 保持同步**，返回 `MutationResult`
- **`IEventBus.Publish<T>(T)` 保持同步**，订阅者按注册顺序立即调用
- **`IStore` / `IAsyncLoadable` 接口不变**
- **`IGameSystem.OnInit / OnStart / OnUpdate / OnLateUpdate / OnShutdown / Dispose` 不变**
- **`IMutationContext.GetStore<T>()` 仍 internal**，仅 Mutation 在 Execute 内可用
- **`StoreBase` / `GameSystemBase` / `GameView` 的 protected 快捷方法全部保留**（`Query` / `GetSystem` / `GetStore` / `Mutate` / `Subscribe` / `Publish`）

**唯一增量**：
1. `GameSystemBase` 新增 `protected UniTask RunProcedure(...)`（调 Procedure 的快捷入口）
2. `ArchExtensions` 新增挂在 `ICanRunProcedure` 上的 `RunProcedure` 扩展方法（给项目层无继承关系的类用）
3. `StoreBase` / `GameSystemBase` / `GameView` 类签名上补声明 `ICanEvent`（零业务破坏；详见 6.4）
4. 删除 `ArchExtensions` 里未被调用的 internal `GetStore` 扩展方法（死代码清理）

盒子层现有 System / Store / View 代码**几乎不需要改动**，除非要在某处用 Procedure 才新增调用——迁移成本极低。

---

## 八、错误处理与取消语义

### 8.1 异常传播

| 触发点 | 行为 |
|---|---|
| Mutation.Execute 抛异常 | `GameContext.Mutate` 捕获 → Debug.LogException → 返回 `MutationResult.Fail`（v1 已有，不变） |
| EventBus 订阅者抛异常 | EventBus 捕获 → Debug.LogError → 不影响其他订阅者（v1 已有，不变） |
| System.OnUpdate / OnLateUpdate 抛异常 | GameContext 捕获 → Debug.LogException → 不影响其他 System（v1 已有，不变） |
| Procedure.ExecuteAsync 抛异常 | 直接冒泡给调用方；业务侧在 ExecuteAsync 内自行 try/catch/finally |
| 调用方未 try/catch Procedure 异常 | 异常向上冒泡，最终由 UniTask 报告（如 fire-and-forget 则进 UniTask 全局未捕获处理器） |

### 8.2 取消传递约定

- **Procedure 不持有自己的 CTS**（除非业务需要内部超时）
- **CancellationToken 由调用方传入**，Procedure 透传给子 Procedure 与 await 操作
- **小游戏 System 通常持有一个 `_cts`**，绑定到游戏会话生命周期（OnShutdown 时 Cancel + Dispose）；触发 Procedure 时传 `_cts.Token`
- **Lifecycle 触发 Procedure** 时根据需要传 default 或外部 CTS
- **GameContext.Shutdown 不主动取消 in-flight Procedure**：如果业务需要场景退出时取消所有 Procedure，由对应 System 在 OnShutdown 中 Cancel 自己的 CTS（已是 v1 现状）

---

## 九、迁移指南

### 9.1 盒子层迁移

工作量：**零**。

盒子层 System / Store / View **完全不需要改动**：
- `ICanGetStore` 仍是 internal，base 类 protected 入口不变
- public Cap 的 protected 快捷方法全部保留
- Store 的 `PublishEvent` / `Query`、System 的 `Subscribe` / `Publish` / `Mutate` / `Query` / `GetSystem`、View 的 `Subscribe` / `Mutate` / `Query` / `GetSystem` —— 调用方式都不变

只在需要新增 Procedure 触发点时，用 `RunProcedure(new XxxProcedure(...))` 即可（基类 protected）。

### 9.2 小游戏层迁移（以 Game101 为例）

工作量：**一天**。需要拆出 Procedure 并改 System / Lifecycle。

步骤：
1. **新建 SceneRefs 打包类**：`Assets/Game/MiniGames/Game101/Scripts/Game101SceneRefs.cs`
   - 内含 `TransitionView` / `BoardView` / `GestureDetector` / `HintOverlayView` 等字段
2. **新建 Procedure 文件夹**：`Assets/Game/MiniGames/Game101/Scripts/Procedures/`
   - `EnterGameProcedure(Game101SceneRefs refs)`：替代 `Game101Lifecycle.OnGameEnter` 的内部逻辑 + `GooseFindGameSystem.StartNewGameAsync`
   - `StartLevelProcedure(Game101SceneRefs refs)`：替代 `GooseFindGameSystem.StartNewGameAsync`
   - `AdvanceLevelProcedure(Game101SceneRefs refs)`：替代 `GooseFindGameSystem.AdvanceToNextLevelAsync`
   - `RestartProcedure(Game101SceneRefs refs)`：替代 `GooseFindGameSystem.RestartAsync`
   - `ReviveProcedure(Game101SceneRefs refs)`：替代 `GooseFindGameSystem.Revive`
3. **`Game101Lifecycle.OnGameEnter`**：`FindObjectOfType` 组装 `Game101SceneRefs`，保存到自身字段，`await ctx.RunProcedure(new EnterGameProcedure(refs))`
4. **`GooseFindGameSystem` 字段瘦身**：删除 `_gestureDetector` / `_boardView` / `_transitionView`，只保留 `_store` / `_hintManager` / `_cts`
5. **`GooseFindGameSystem` 公开方法改造**：原 `StartNewGameAsync` / `AdvanceToNextLevelAsync` 等 await View 的方法删除；`OnResetClicked` / `OnHintButtonClicked` 等纯改 Store 的方法保留
6. **System 触发 Procedure 用 fire-and-forget**（需要 refs 时由调用方提供，或 System 从 Lifecycle 缓存的 refs 取）：
   ```csharp
   public void RequestAdvanceLevel(Game101SceneRefs refs)
   {
       RunProcedure(new AdvanceLevelProcedure(refs.Transition), _cts.Token).Forget();
   }
   ```
7. **删除 `BindSceneRefs(...)`**（System 不再持有 View）

### 9.3 Game102 迁移

类似 Game101，SceneRefs 字段更多（6 个 View），Procedure 拆分粒度需细化（`EnterGeneratingProcedure` / `EnterLevelClearProcedure` / `RegenerateProcedure` 等）。预估工作量：**一天半**。

### 9.4 框架本体改动

预估工作量：**2 小时**。

新增文件：
- `Packages/JulyArch/Runtime/Core/Procedure/IProcedure.cs`
- `Packages/JulyArch/Runtime/Core/Procedure/ProcedureBase.cs`
- `Packages/JulyArch/Runtime/Core/Architecture/ICanRunProcedure.cs`

修改文件：
- `Packages/JulyArch/Runtime/Core/Context/IGameContext.cs`：加 `RunProcedure(IProcedure, CancellationToken)` 方法签名
- `Packages/JulyArch/Runtime/Core/Context/GameContext.cs`：实现 `RunProcedure`
- `Packages/JulyArch/Runtime/Core/Architecture/ArchExtensions.cs`：
  - 新增 `RunProcedure` 扩展方法（挂在 `ICanRunProcedure` 上）
  - **删除**未被使用的 `internal static T GetStore<T>(this ICanGetStore self)`（死代码清理）
- `Packages/JulyArch/Runtime/Core/System/GameSystemBase.cs`：
  - 类签名加 `ICanEvent, ICanRunProcedure`（与已有的 `Subscribe / Publish` + 新增 `RunProcedure` 对齐）
  - 新增 `protected UniTask RunProcedure(IProcedure, CancellationToken)`
- `Packages/JulyArch/Runtime/Core/Store/StoreBase.cs`：类签名加 `ICanEvent`（与 protected `PublishEvent` 对齐；零业务破坏）
- `Packages/JulyArch/Runtime/Core/View/GameView.cs`：类签名加 `ICanEvent`（与 protected `Subscribe` 对齐；零业务破坏）

**不动**：`Mutation*` / `Events*` / 其他 Cap 接口文件 / `StoreBase` 和 `GameView` 的 protected 方法实现。

---

## 十、Breaking Changes 清单

| 改动 | 影响范围 | 修复方式 |
|---|---|---|
| `IGameContext` 新增 `RunProcedure` | 框架本体 / 实现 `IGameContext` 的 mock | 实现新方法 |
| 小游戏 System 持有 View 引用的代码 | 小游戏 System 全部（Game101 / Game102 等） | 拆出 Procedure，Procedure 通过构造函数接收 View 引用 |
| 小游戏 Lifecycle 的 `BindSceneRefs(...)` | 小游戏 Lifecycle | 改为 `FindObjectOfType` 组装 `SceneRefs` 对象，传给 Procedure |
| 小游戏 System 的 `await ViewMethod(...)` | 小游戏 System | 改为 `RunProcedure(new XxxProcedure(refs)).Forget()` |

**盒子层零破坏**：所有 Store / System / View 现有代码保持原样工作，只是多了 `RunProcedure` 可用。

升级路径：

1. 合并框架本体改动（新增 Procedure 文件 + `GameContext.RunProcedure`）
2. 编译通过后逐个小游戏迁移（可一个游戏一个 PR）

---

## 十一、不在范围内的事

明确**v2 不做**的事，避免范围蔓延：

- ❌ Procedure 取消树 / 自动级联取消
- ❌ Procedure 优先级 / 队列调度
- ❌ View 注册表 / ViewService 机制（见 2.2 / 5.1 的论证）
- ❌ Mutation 异步版本
- ❌ EventBus 异步发布
- ❌ Roslyn 分析器（Cap 接口约束的编译期警告，靠接口设计本身已能拦截大多数违规）
- ❌ 单元测试基础设施（Procedure 测试由项目侧自行写，框架不强制 mock 体系）
- ❌ ScriptableObject 配置化的 Procedure 注册
- ❌ Procedure 进度回调 / 进度条集成
- ❌ 多 GameContext 之间的 Procedure 跨越

如果将来需要其中任意一项，开新的 v2.x 增量 spec。

---

## 十二、风险与 Open Questions

### 12.1 已知风险

- **R1：迁移工作量**。盒子层零改动；小游戏 Game101 + Game102 合计 2.5 天。期间只需冻结小游戏相关文件
- **R2：Procedure 滥用**。开发者可能把简单同步逻辑也写成 Procedure。第 4.7 节明确了判断标准；code review 时按此审阅
- **R3：SceneRefs 对象设计**。小游戏侧需要合理组织 View 打包对象——粒度太粗会出现 Procedure 只用其中一两个字段但承担了整组引用的生命周期；粒度太细又回到散参传递。建议：**单场景单 SceneRefs**，大型项目按功能域（UI / 建筑 / NPC）拆分
- **R4：View 引用生命周期责任在业务侧**。框架不检测注入的 View 是否已销毁——如果 Procedure 跨 await 后访问已销毁的 View，会触发 Unity fake null。业务侧通过合理设计 CancellationToken + 不跨长 await 持有短生命周期引用避免

### 12.2 Open Questions（v2 实施前需澄清）

- **Q1**：Procedure 是否需要"未实现 IArchitectureSettable 时也能跑"的支持，方便单元测试 mock？（**当前方案：基类自动实现，测试不需要 mock**）

---

## 十三、附录：v1 与 v2 文件对照表

| v1 文件 | v2 改动 |
|---|---|
| `Architecture/ArchExtensions.cs` | 加 `RunProcedure`（挂在 `ICanRunProcedure` 上）；**删除**未被使用的 internal `GetStore` 扩展方法 |
| `Architecture/IBelongToArchitecture.cs` | 不变 |
| `Architecture/IArchitectureSettable.cs` | 不变 |
| `Architecture/ICanRegister.cs` | 不变 |
| `Architecture/ICanEvent.cs` | 不变 |
| `Architecture/ICanQuery.cs` | 不变 |
| `Architecture/ICanMutate.cs` | 不变 |
| `Architecture/ICanGetSystem.cs` | 不变 |
| `Architecture/ICanGetStore.cs` | 不变（保持 internal） |
| `Architecture/ICanRunProcedure.cs` | **新增**（public，挂扩展方法用） |
| `Context/IGameContext.cs` | 加 `UniTask RunProcedure(IProcedure, CancellationToken)` |
| `Context/GameContext.cs` | 实现 `RunProcedure` |
| `Store/IStore.cs` | 不变 |
| `Store/IAsyncLoadable.cs` | 不变 |
| `Store/StoreBase.cs` | **类签名加 `ICanEvent`**（与已有 protected `PublishEvent` 对齐，零业务破坏）；protected 方法实现不变 |
| `System/IGameSystem.cs` | 不变 |
| `System/GameSystemBase.cs` | **类签名加 `ICanEvent, ICanRunProcedure`**（与能力对齐）；新增 `protected UniTask RunProcedure(IProcedure, CancellationToken)`；其余 protected 方法不变 |
| `Mutation/IMutation.cs` | 不变 |
| `Mutation/MutationResult.cs` | 不变 |
| `View/GameView.cs` | **类签名加 `ICanEvent`**（与已有 protected `Subscribe` 对齐，零业务破坏）；protected 方法实现不变 |
| `Procedure/IProcedure.cs` | **新增** |
| `Procedure/ProcedureBase.cs` | **新增**（含完整 Cap 接口声明 + protected 方法集合） |
| `Events/IEventBus.cs` | 不变 |
| `Events/EventBus.cs` | 不变 |
| `Events/LifecycleEvents.cs` | 不变 |

**变更最小化原则**：v2 只做加法（新增 Procedure 机制 + 补齐基类 Cap 声明 + 清理死代码），不删减任何 v1 现有 protected 方法；升级后 v1 业务代码零修改即可编译通过。

---

**文档结束。** 请评审后给出反馈，再决定是否进入 implementation plan 编写。
