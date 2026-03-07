using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Data.Save;

namespace JulyArch
{
    /// <summary>
    /// 可存档的 Store 基类,走底层的GF.Save流程
    /// </summary>
    public abstract class SavableStoreBase<TData> : StoreBase<TData>
        where TData : class, ISaveData, new()
    {
        /// <summary>
        /// 存储键
        /// </summary>
        protected abstract string SaveKey { get; }

        /// <summary>
        /// 从 GF.Save 加载数据，不存在时返回新实例
        /// </summary>
        protected override async UniTask<TData> LoadDataAsync()
        {
            return await GF.Save.LoadAndRegisterAsync<TData>(SaveKey);
        }

        /// <summary>
        /// 标记数据为脏
        /// </summary>
        protected void MarkDirty()
        {
            GF.Save.MarkDirty(SaveKey);
        }
    }
}
