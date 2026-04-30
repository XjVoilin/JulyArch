using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 标记接口：Store 数据需要持久化时实现此接口。
    /// 具体的序列化/存储契约由宿主的 IArchSave 实现决定。
    /// </summary>
    public interface IArchSaveData { }

    /// <summary>
    /// 存档抽象，宿主实现此接口桥接到自身的存档系统。
    /// </summary>
    public interface IArchSave
    {
        UniTask<T> LoadAndRegisterAsync<T>(string key) where T : class, new();
        void MarkDirty(string key);
    }
}
