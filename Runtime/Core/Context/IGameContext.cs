using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 游戏上下文的消费者接口 —— View / Lifecycle / 外部代码通过此接口与架构交互。
    /// 不暴露 GetStore，防止绕过 Mutation 直接写 Store。
    /// </summary>
    public interface IGameContext
    {
        /// <summary>
        /// 获取 Store 的只读查询接口（未注册时 LogError + 返回 null）
        /// </summary>
        T Query<T>() where T : class, IStoreQueries;

        /// <summary>
        /// 尝试获取 Store 的只读查询接口（未注册时静默返回 false）
        /// </summary>
        bool TryQuery<T>(out T result) where T : class, IStoreQueries;

        /// <summary>
        /// 获取 System 实例
        /// </summary>
        T GetSystem<T>() where T : class, IGameSystem;

        /// <summary>
        /// 执行 Mutation（同步）
        /// </summary>
        MutationResult Mutate<TMutation>(TMutation mutation) where TMutation : IMutation;

        /// <summary>
        /// 执行 lambda Mutation（同步，单 Store）
        /// </summary>
        MutationResult Mutate<TStore>(Action<TStore> mutation) where TStore : class, IStore;

        /// <summary>
        /// 事件总线
        /// </summary>
        IEventBus Event { get; }

        /// <summary>
        /// 注册 Store（仅初始化阶段可用，运行时调用会报错）
        /// </summary>
        void RegisterStore(IStore store);

        /// <summary>
        /// 注册 System（仅初始化阶段可用，运行时调用会报错）
        /// </summary>
        void RegisterSystem(IGameSystem system);

        /// <summary>
        /// 运行一个 Procedure（异步流程）。
        /// Procedure 由调用方 new 实例，框架负责 SetArchitecture + 调度 ExecuteAsync。
        /// </summary>
        UniTask RunProcedure(IProcedure procedure, CancellationToken ct = default);
    }

    /// <summary>
    /// Mutation 专用上下文 —— 提供 GetStore 以获取 Store 完整访问权。
    /// 仅在 Mutation.Execute 内由框架传入。
    /// </summary>
    public interface IMutationContext : IGameContext
    {
        /// <summary>
        /// 获取具体 Store 实例（完整访问权）
        /// 仅供 Mutation 在执行数据变更时使用
        /// </summary>
        T GetStore<T>() where T : class, IStore;
    }
}
