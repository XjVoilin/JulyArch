using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 可存档的 Store 基类，走 ArchServices.Save 流程。
    /// 实现 IAsyncLoadable，GameContext 初始化时通过 LoadAsync 加载存档。
    /// </summary>
    public abstract class SavableStoreBase<TData> : StoreBase<TData>, IAsyncLoadable
        where TData : class, IArchSaveData, new()
    {
        protected abstract string SaveKey { get; }

        public async UniTask LoadAsync()
        {
            Data = await ArchServices.Save.LoadAndRegisterAsync<TData>(SaveKey);
            OnDataLoaded();
        }

        protected void MarkDirty()
        {
            ArchServices.Save.MarkDirty(SaveKey);
        }
    }
}
