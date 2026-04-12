using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Data.Save;

namespace JulyArch
{
    /// <summary>
    /// 可存档的 Store 基类，走底层的 GF.Save 流程。
    /// 实现 IAsyncLoadable，GameContext 初始化时通过 LoadAsync 加载存档。
    /// </summary>
    public abstract class SavableStoreBase<TData> : StoreBase<TData>, IAsyncLoadable
        where TData : class, ISaveData, new()
    {
        protected abstract string SaveKey { get; }

        public async UniTask LoadAsync()
        {
            Data = await GF.Save.LoadAndRegisterAsync<TData>(SaveKey);
            OnDataLoaded();
        }

        protected void MarkDirty()
        {
            GF.Save.MarkDirty(SaveKey);
        }
    }
}
