namespace JulyArch
{
    /// <summary>
    /// 游戏上下文接口
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
        /// 执行命令（同步）
        /// </summary>
        CommandResult Execute<TCommand>(TCommand command) where TCommand : ICommand;
    }

    /// <summary>
    /// Command专用,提供Store的Get,使其能操作数据,具备数据的完整权限
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
