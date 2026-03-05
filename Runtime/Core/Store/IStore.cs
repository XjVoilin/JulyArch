using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// Store 是持久数据的唯一所有者，负责加载、保存、领域操作。
    /// Store 不持有运行时状态，不参与帧更新。
    /// </summary>
    public interface IStore
    {
        /// <summary>初始化（由 GameContext 调用，注入上下文引用）</summary>
        void Initialize(GameContext context);

        /// <summary>加载存档数据</summary>
        UniTask LoadAsync();

        /// <summary>所有 Store 数据加载完成后的回调（可安全访问其他 Store）</summary>
        void OnReady();

        /// <summary>关闭（应用退出时）</summary>
        void Shutdown();
    }

    /// <summary>
    /// 标记接口：标识 Store 的只读查询接口
    /// 业务 Store 通过实现此接口暴露只读查询 API
    /// </summary>
    public interface IStoreQueries { }
}
