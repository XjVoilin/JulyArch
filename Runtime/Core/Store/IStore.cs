namespace JulyArch
{
    /// <summary>
    /// Store 是可存储数据（服务器同步 / 本地持久化）的唯一所有者。
    /// Store 不持有运行时瞬时状态（瞬时状态由 System 管理），不参与帧更新。
    /// </summary>
    public interface IStore
    {
        void Initialize();

        /// <summary>
        /// 同步加载数据（纯内存 Store）。
        /// 需要异步加载的 Store 应实现 IAsyncLoadable，GameContext 会分派调用。
        /// </summary>
        void Load();

        /// <summary>
        /// 所有 Store 数据加载完成后的回调
        /// </summary>
        void OnReady();

        void Shutdown();
    }

    /// <summary>
    /// 标记接口：标识 Store 的只读查询接口
    /// 业务 Store 通过实现此接口暴露只读查询 API
    /// </summary>
    public interface IStoreQueries { }
}
