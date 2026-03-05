using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 游戏上下文接口
    /// 
    /// 【API 使用指引】
    /// Query&lt;T&gt;()     — 获取 Store 只读查询接口（推荐路径，UI/System 日常使用）
    /// GetSystem&lt;T&gt;()  — 获取 System 实例
    /// Execute&lt;T&gt;()    — 执行 Command（跨 Store 业务操作的标准入口）
    /// </summary>
    public interface IGameContext
    {
        /// <summary>
        /// 获取 Store 的只读查询接口
        /// </summary>
        T Query<T>() where T : class, IStoreQueries;

        /// <summary>
        /// 获取 System 实例
        /// </summary>
        T GetSystem<T>() where T : class, IGameSystem;

        /// <summary>
        /// 执行命令
        /// </summary>
        UniTask<CommandResult> Execute<TCommand>(TCommand command) where TCommand : ICommand;
    }

    /// <summary>
    /// 命令执行上下文 — 继承 IGameContext，额外提供 Store 写权限
    /// 仅由 Command 的 Execute 方法接收
    /// </summary>
    public interface ICommandContext : IGameContext
    {
        /// <summary>
        /// 获取具体 Store 实例（完整访问权）
        /// 仅供 Command 在执行数据变更时使用
        /// </summary>
        T GetStore<T>() where T : class, IStore;
    }
}
