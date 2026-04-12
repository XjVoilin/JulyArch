using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 标记接口：Store 需要异步加载数据（如从本地存档读取）。
    /// GameContext 在初始化时对实现此接口的 Store 调用 LoadAsync 而非 IStore.Load。
    /// </summary>
    public interface IAsyncLoadable
    {
        UniTask LoadAsync();
    }
}
