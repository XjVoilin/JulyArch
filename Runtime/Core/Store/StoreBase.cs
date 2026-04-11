using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Core;

namespace JulyArch
{
    /// <summary>
    /// Store 基类 —— 数据的唯一所有者
    /// </summary>
    public abstract class StoreBase<TData> : IStore, ICanQuery where TData : class, new()
    {
        protected TData Data { get; private set; }

        #region IStore 显式实现

        void IStore.Initialize()
        {
            OnInitialize();
        }

        async UniTask IStore.LoadAsync()
        {
            Data = await LoadDataAsync();
            OnDataLoaded();
        }

        void IStore.OnReady()
        {
            OnReady();
        }

        void IStore.Shutdown()
        {
            OnShutdown();
            Data = null;
        }

        #endregion

        #region 快捷方法（委托给 ArchExtensions）

        protected T Query<T>() where T : class, IStoreQueries
            => ArchExtensions.Query<T>(this);

        protected void PublishEvent<T>(T eventData) where T : IEvent
            => GF.Event.Publish(eventData);

        #endregion

        #region 子类可覆盖的生命周期钩子

        protected virtual UniTask<TData> LoadDataAsync()
        {
            return UniTask.FromResult(new TData());
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnDataLoaded() { }

        protected virtual void OnReady() { }

        protected virtual void OnShutdown() { }

        #endregion
    }
}