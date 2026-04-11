using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// Store 是持久数据的唯一所有者，负责加载、保存、领域操作。
    /// Store 不持有运行时状态，不参与帧更新。
    /// </summary>
    public interface IStore
    {
        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize();

        /// <summary>
        /// 加载存档数据
        /// </summary>
        UniTask LoadAsync();

        /// <summary>
        /// 所有 Store 数据加载完成后的回调
        /// </summary>
        void OnReady();

        /// <summary>
        /// 关闭
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// 标记接口：标识 Store 的只读查询接口
    /// 业务 Store 通过实现此接口暴露只读查询 API
    /// </summary>
    public interface IStoreQueries { }
}
