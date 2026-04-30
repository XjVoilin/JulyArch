namespace JulyArch
{
    /// <summary>
    /// Store 是所有业务数据的唯一所有者（无论持久化还是瞬时）。
    /// 判断标准：只要有第二个类需要读这份数据，它就进 Store。
    /// Store 不参与帧更新；需要持久化的 Store 继承 <see cref="SavableStoreBase{TData}"/>。
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
