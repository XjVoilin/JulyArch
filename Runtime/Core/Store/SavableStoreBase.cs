using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 需要持久化的 Store 基类，走 ArchServices.Save 流程。
    /// 不需要持久化的 Store 直接继承 <see cref="StoreBase{TData}"/> 即可。
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
