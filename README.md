# 上层游戏架构标准 v6.0

> 命名空间：`GameArch`
> 程序集：`GameArchitecture`
> 框架依赖：JulyGF（GF.* 门面 + IEvent + ISaveData）、UniTask

---

## 一、架构总览

### 核心思想

**Store 管存储，System 管运行，Command 管编排，Context 管协调，View 管表现。**

5 个核心概念，~750 行框架代码，零强制约束、API 引导为主。

### 分层结构

```
┌──────────────────────────────────────────────────────┐
│  View — 表现层                                         │
│  ┌─────────────────────────┬───────────────────────┐ │
│  │ GameView : MonoBehaviour│ GameUIView : UIBase    │ │
│  │ 场景表现 / 场景初始化      │ UI 面板                │ │
│  │ OnViewEnable / OnViewDisable       │ OnBeforeOpen / OnClose │ │
│  └─────────────────────────┴───────────────────────┘ │
│  · Query<T>() 只读查询 Store                           │
│  · GetSystem<T>() 访问 System                          │
│  · Execute<T>() 执行 Command                           │
├──────────────────────────────────────────────────────┤
│  Command（自执行）                                       │
│  · 业务操作的唯一编排入口                                    │
│  · 通过 context.GetStore<T>() 变更持久数据                  │
│  · 通过 context.GetSystem<T>() 驱动运行时逻辑               │
├──────────────────────────────────────────────────────┤
│  System                                               │
│  · 运行时逻辑 + 瞬态数据                                   │
│  · 帧驱动、状态机、AI 等                                   │
├──────────────────────────────────────────────────────┤
│  Store                                                │
│  · 持久数据的唯一所有者                                     │
│  · 加载/保存/领域操作                                      │
│  · MarkDirty() 标记脏数据                                │
├──────────────────────────────────────────────────────┤
│  GameContext                                          │
│  · 统一协调中心                                           │
│  · 管理注册、生命周期、分派                                   │
└──────────────────────────────────────────────────────┘
```

---

## 二、核心概念

### 1. Store — 持久数据

Store 是持久数据的唯一所有者。每个 Store 管理一个领域的存档数据。

**关键特征：**
- 继承 `StoreBase<TData>`，`TData` 实现 `ISaveData`
- 通过 `SaveKey` 指定存储路径（子类完全控制格式）
- 实现 `IStoreQueries` 子接口，暴露只读查询 API（返回防御性副本，防止外部修改内部数据）
- 数据变更后调用 `MarkDirty()` 标记脏数据
- 通过 `Query<T>()` 在 `OnReady()` 及之后可读取其他 Store 的数据

**示例：**

```csharp
// 查询接口（只读）
public interface ICurrencyQueries : IStoreQueries
{
    long GetAmount(CurrencyType type);
    bool HasEnough(CurrencyType type, long amount);
}

// Store 实现
public class CurrencyStore : StoreBase<CurrencyData>, ICurrencyQueries
{
    protected override string SaveKey => "Currency";

    // 查询
    public long GetAmount(CurrencyType type) { ... }
    public bool HasEnough(CurrencyType type, long amount) { ... }

    // 变更（供 Command 调用）
    public bool AddAmount(CurrencyType type, long amount)
    {
        // ... 修改数据 ...
        MarkDirty();
        return true;
    }
}
```

### 2. System — 运行时逻辑

System 管理运行时逻辑和瞬态数据（不存盘），拥有帧更新能力。

**关键特征：**
- 继承 `GameSystemBase`
- 拥有完整生命周期（Init → Start → Update → Shutdown → Dispose）
- 直接暴露公开方法和属性，外部通过 `GetSystem<T>()` 访问
- 通过 `Query<T>()` 读取 Store 数据，通过 `Execute<T>()` 触发 Command

**示例：**

```csharp
public class DungeonSystem : GameSystemBase
{
    // 只读属性（供 UI 查询）
    public bool IsInDungeon => _runtime != null;
    public int CurrentWave => _runtime?.CurrentWave ?? 0;

    // 操作方法（供场景/战斗逻辑调用）
    public void EnterDungeon(int dungeonId, int totalWaves) { ... }
    public void StartFight() { ... }

    // 通过 Command 将结果写入持久数据
    private async UniTaskVoid SettleDungeonResult()
    {
        var result = await Execute(new DungeonSettleCommand(...));
    }
}
```

### 3. Command — 业务编排（自执行）

Command 是**业务操作的唯一编排入口**。当一个操作涉及多个 Store、或同时涉及 Store 和 System 时，由 Command 统一编排，确保业务逻辑集中在一处，调用方只需关心结果。

数据和执行逻辑在同一个 `readonly struct` 中，无需单独的 Handler 类。

**使用原则：**
> Store 的公开方法是 Command 调用的原子构件。
> System 的公开方法是 Command 驱动运行时的手段。
> 调用方（UI/场景）只调 `Execute()`，不需要知道内部编排细节。

**关键特征：**
- Command 是 `readonly struct`，同时携带数据和 `Execute` 方法
- 通过 `context.Query<T>()` 获取只读查询接口做前置检查
- 通过 `context.GetStore<T>()` 获取具体 Store 进行持久数据变更
- 通过 `context.GetSystem<T>()` 获取 System 驱动运行时逻辑
- Execute 返回 `CommandResult`（同步），前置检查合并在 Execute 开头
- **无需注册**：`GameContext.Execute()` 直接调用 `command.Execute(this)`

**示例（纯 Store 编排）：**

```csharp
public readonly struct PurchaseItemCommand : ICommand
{
    public readonly int ItemId;

    public PurchaseItemCommand(int itemId) => ItemId = itemId;

    public CommandResult Execute(ICommandContext ctx)
    {
        var currency = ctx.Query<ICurrencyQueries>();
        if (!currency.HasEnough(CurrencyType.Gold, GetPrice(ItemId)))
            return CommandResult.Fail("金币不足");

        var currencyStore = ctx.GetStore<CurrencyStore>();
        currencyStore.ConsumeCurrency(CurrencyType.Gold, GetPrice(ItemId));

        var inventoryStore = ctx.GetStore<InventoryStore>();
        inventoryStore.AddItem(ItemId, default, GF.Time.ServerTimeUtcTimestamp);

        return CommandResult.Success();
    }
}
```

**示例（Store + System 编排）：**

```csharp
public readonly struct DungeonEnterCommand : ICommand
{
    public readonly int DungeonId;

    public DungeonEnterCommand(int dungeonId) => DungeonId = dungeonId;

    public CommandResult Execute(ICommandContext ctx)
    {
        var dungeon = ctx.Query<IDungeonQueries>();
        if (!dungeon.IsDungeonUnlocked(DungeonId))
            return CommandResult.Fail("副本未解锁");

        var currencyStore = ctx.GetStore<CurrencyStore>();
        currencyStore.ConsumeCurrency(CurrencyType.Energy, GetEntryCost(DungeonId));

        var dungeonSystem = ctx.GetSystem<DungeonSystem>();
        dungeonSystem.EnterDungeon(DungeonId);

        return CommandResult.Success();
    }
}
```

### 4. GameContext — 统一协调

GameContext 是上层架构的核心枢纽，管理所有 Store 和 System 的注册与生命周期，并作为 Command 的集中分派点。

**核心 API：**

| 接口 | API | 用途 | 典型调用方 |
|------|-----|------|-----------|
| IGameContext | `Query<T>()` | 获取 Store 只读查询接口 | UI、System、Command、任意代码 |
| IGameContext | `GetSystem<T>()` | 获取 System 实例 | Command、System 互访、场景控制器 |
| IGameContext | `Execute<T>(cmd)` | 执行 Command | UI、System |
| ICommandContext | `GetStore<T>()` | 获取具体 Store 实例（写权限） | Command.Execute |

### 5. View — 表现层

View 是表现层的框架基类，提供与 `GameSystemBase` 对称的架构访问能力。

**两个基类，对应两条继承链：**

| 基类 | 继承自 | 适用场景 | 生命周期 |
|------|--------|---------|---------|
| `GameView` | MonoBehaviour | 场景表现、场景初始化 | OnViewEnable (OnEnable) / OnViewDisable (OnDisable) |
| `GameUIView` | UIBase (JulyGF) | UI 面板 | OnBeforeOpen / OnClose (JulyGF 驱动) |

两者均提供与 `GameSystemBase` 一致的快捷方法：`Query<T>()`、`GetSystem<T>()`、`Execute<T>()`。
`GameUIView` 额外提供 `ExecuteCommand<T>()`：执行命令 + 失败时自动弹出错误提示。

**示例（场景 View）：**

```csharp
public class BattleView : GameView
{
    protected override void OnViewEnable()
    {
        GF.Event.Subscribe<BattleActionExecutedEvent>(OnActionExecuted, this);
    }

    protected override void OnViewDisable()
    {
        GF.Event.UnsubscribeAll(this);
    }

    private void OnActionExecuted(BattleActionExecutedEvent e)
    {
        PlayActionSequence(e).Forget();
    }

    private async UniTaskVoid PlayActionSequence(BattleActionExecutedEvent e)
    {
        await UniTask.Delay(500);
        GetSystem<BattleSystem>().ContinueAfterAction();
    }
}
```

**示例（UI 面板）：**

```csharp
public class UIDungeonSelectWindow : GameUIView
{
    public SmartButton Dungeon1Btn;

    protected override void OnBeforeOpen()
    {
        base.OnBeforeOpen();
        Dungeon1Btn.onClick.AddListener(OnDungeon1BtnClick);
    }

    private void OnDungeon1BtnClick()
    {
        ExecuteCommand(new DungeonEnterCommand(1)).Forget();
        CloseWindow();
    }
}
```

---

## 三、数据分类

| 类别 | 所有者 | 生命周期 | 存盘 | 示例 |
|------|--------|---------|------|------|
| 持久数据 | Store | 应用生命周期 | 是 | 玩家数据、背包、货币、设置 |
| 运行时数据 | System | 应用生命周期 | 否 | 副本进度、战斗统计 |
| 表现瞬态数据 | SceneView / UI | 跟随场景或窗口 | 否 | 动画播放状态、特效序列、镜头位置 |
| 瞬态数据 | 局部变量 | 短暂 | 否 | 输入缓存、临时计算 |

---

## 四、API 使用指引

### 数据访问

```csharp
// ✅ 在 View 中通过基类快捷方法访问（推荐）
public class MyView : GameView
{
    protected override void OnViewEnable()
    {
        var player = Query<IPlayerQueries>();
        var dungeon = GetSystem<DungeonSystem>();
    }
}

// ✅ 在 UI 面板中执行命令（推荐）
public class MyPanel : GameUIView
{
    private void OnBuyClick()
    {
        ExecuteCommand(new PurchaseItemCommand(itemId));
    }
}

// ✅ 在 Command.Execute 中编排 Store 和 System
public CommandResult Execute(ICommandContext ctx)
{
    var store = ctx.GetStore<CurrencyStore>();
    store.ConsumeCurrency(CurrencyType.Energy, 10);

    var system = ctx.GetSystem<DungeonSystem>();
    system.EnterDungeon(dungeonId);

    return CommandResult.Success();
}

// ✅ 在非 View 代码中通过能力接口访问
var result = this.Execute(new PurchaseItemCommand { ... });
```

### Command 使用时机

```
纯运行时操作（不碰持久数据）          → 直接调 System
单模块简单操作（改昵称、GM 指令）     → 可以直接调 Store 方法
涉及多 Store 或 Store + System      → 走 Command（业务编排的唯一入口）
```

---

## 五、生命周期

```
GameEntryBase.InnerInit()
  ├── GameContext.Create()
  ├── RegisterStores()     → ctx.RegisterStore(...)
  ├── RegisterSystems()    → ctx.RegisterSystem(...)
  └── GameContext.InitializeAsync()
        ├── Store.Initialize()        （全部）
        ├── Store.LoadAsync()         （全部）
        ├── Store.OnReady()           （全部，此时可安全访问其他 Store）
        ├── System.OnInit()           （全部）
        ├── System.OnStart()          （全部）
        └── 发布 GameReadyEvent

应用退出 → GameContext.ShutdownAsync()
  ├── System.OnShutdown()       （逆序）
  ├── System.Dispose()          （全部）
  └── Store.Shutdown()          （逆序）
```

---

## 六、新建模块指南

### 新增 Store

1. 定义数据类：`public class MyData : ISaveData { ... }`
2. 定义查询接口：`public interface IMyQueries : IStoreQueries { ... }`
3. 实现 Store：`public class MyStore : StoreBase<MyData>, IMyQueries { ... }`
4. 在 `GameEntry.RegisterStores` 中注册：`ctx.RegisterStore(new MyStore());`

### 新增 System

1. 实现系统：`public class MySystem : GameSystemBase { ... }`
2. 在 `GameEntry.RegisterSystems` 中注册：`ctx.RegisterSystem(new MySystem());`

### 新增 Command

1. 定义命令并实现 Execute：

```csharp
public readonly struct MyCommand : ICommand
{
    public readonly int Param;

    public MyCommand(int param) => Param = param;

    public CommandResult Execute(ICommandContext ctx)
    {
        // 前置检查 + 执行变更
        return CommandResult.Success();
    }
}
```

无需注册，直接可用：`Execute(new MyCommand(42));`

### 新增 View（场景）

1. 继承 `GameView`
2. 在 `OnViewEnable()` 中订阅事件、初始化
3. 在 `OnViewDisable()` 中取消订阅、清理
4. 挂载到场景 GameObject 上

### 新增 View（UI 面板）

1. 继承 `GameUIView`（替代直接继承 `UIBase`）
2. 在 `OnBeforeOpen()` 中初始化
3. 使用 `ExecuteCommand()` 执行带错误提示的命令
4. 在 `tbuiwindow` 配置表中注册窗口信息

---

## 七、框架文件清单

```
Architecture/
├── Core/
│   ├── Store/
│   │   ├── IStore.cs              IStore 接口 + IStoreQueries 标记接口
│   │   └── StoreBase.cs           统一 Store 基类（SaveKey、MarkDirty、生命周期钩子）
│   ├── System/
│   │   ├── IGameSystem.cs         System 接口（生命周期）
│   │   └── GameSystemBase.cs      System 基类（快捷方法）
│   ├── Command/
│   │   ├── ICommand.cs            命令接口（自执行，含 Execute 方法）
│   │   └── CommandResult.cs       命令结果（IsSuccess + Message）
│   ├── Context/
│   │   ├── IGameContext.cs         上下文接口
│   │   └── GameContext.cs          上下文实现
│   ├── View/
│   │   ├── GameView.cs            场景 View 基类（MonoBehaviour）
│   │   └── GameUIView.cs          UI View 基类（UIBase）
│   └── Events/
│       └── LifecycleEvents.cs     框架生命周期事件
├── Entry/
│   └── GameEntryBase.cs           Composition Root 基类
└── README.md                      本文件
```

---

## 八、命名规范

本项目统一采用 **微软 C# 官方命名规范**（Microsoft Framework Design Guidelines）。

### 总览

| 类别 | 命名风格 | 示例 |
|------|----------|------|
| 类 / 结构体 | PascalCase | `PlayerStore`, `StackItem` |
| 接口 | I + PascalCase | `IGameContext`, `IPlayerQueries` |
| 方法 | PascalCase | `GetAmount()`, `AddItem()` |
| 属性 | PascalCase | `PlayerName`, `IsSlotEmpty` |
| **公有字段** | **PascalCase** | `ItemId`, `SlotId`, `MainWeapon` |
| `readonly struct` 字段 | PascalCase | `public readonly int DungeonId;` |
| 私有字段 | _camelCase | `_context`, `_selectedItem` |
| 参数 | camelCase | `slotId`, `itemId`, `amount` |
| 局部变量 | camelCase | `var oldItem = ...` |
| 常量 | PascalCase | `const int MaxLevel = 100;` |
| 枚举类型 | PascalCase | `EquipSlot`, `CurrencyType` |
| 枚举成员 | PascalCase | `MainWeapon`, `Gold`, `Common` |
| 命名空间 | PascalCase | `GameArch`, `JulyGF.Framework` |
| 泛型参数 | T + PascalCase | `TData`, `TCommand` |

### 详细规则

#### 1. 公有字段 — PascalCase

所有 `public` 字段（包括 Unity 序列化字段、数据类字段、`readonly struct` 字段）统一使用 PascalCase：

```csharp
// 数据类
[Serializable]
public class PlayerData : ISaveData
{
    public string PlayerName;
    public int Level = 1;
    public long Exp;
}

// readonly struct（Command / Event）
public readonly struct NewGameCommand : ICommand
{
    public readonly string PlayerName;
}

// MonoBehaviour 序列化字段
public class UIArchiveItem : MonoBehaviour
{
    public TMP_Text ArchiveName;
    public SmartButton ArchiveButton;
    public GameObject SelectedGo;
}
```

#### 2. 私有字段 — _camelCase

以下划线 `_` 开头 + camelCase，包括 `[SerializeField]` 私有字段：

```csharp
public class UIMainHud : GameUIView
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private SmartButton _claimRewardBtn;
    private UIMainHudVo _vo;
}
```

#### 3. 属性 — PascalCase

属性始终使用 PascalCase，与公有字段同风格但通过 `{ get; }` 区分：

```csharp
public SaveImportance Importance => SaveImportance.Critical;
public int AvailableSlots => MaxSlots - LockedSlots;
public SlotInfo Archive { get; private set; }
```

#### 4. 方法与参数 — PascalCase / camelCase

方法名 PascalCase，参数名 camelCase：

```csharp
public bool AddAmount(CurrencyType type, long amount) { ... }
public SlotInfo CreateSlot(string playerName) { ... }
```

#### 5. 字段名冲突处理

当公有字段改为 PascalCase 后与同名属性冲突时，字段加 `Raw` 后缀：

```csharp
public class CurrencyItem
{
    public int TypeRaw;          // 字段：存储原始 int 值
    public long Amount;
    public CurrencyType Type => (CurrencyType)TypeRaw;  // 属性：类型转换
}
```

### 不适用范围

- **第三方库 / 生成代码**：`Game/3rd/`、`Game/Scripts/Generated/` 下的代码不受本规范约束
- **Unity 引擎回调**：`Awake()`、`Start()`、`Update()` 等遵循 Unity 命名（已为 PascalCase）
