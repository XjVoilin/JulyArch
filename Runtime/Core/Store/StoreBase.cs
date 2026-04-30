using System;

namespace JulyArch
{
    /// <summary>
    /// Store 基类 —— 所有业务数据的唯一所有者（无论持久化还是瞬时）。
    /// 只要有第二个类需要读这份数据，它就进 Store；System 私有的内部数据除外。
    /// </summary>
    public abstract class StoreBase<TData> : IStore, ICanQuery where TData : class, new()
    {
        protected TData Data { get; set; }

        #region IStore 显式实现

        void IStore.Initialize()
        {
            OnInitialize();
        }

        void IStore.Load()
        {
            if (this is IAsyncLoadable)
            {
                throw new InvalidOperationException(
                    $"[{GetType().Name}] implements IAsyncLoadable, must use LoadAsync() via GameContext");
            }
            Data = LoadData();
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

        protected void PublishEvent<T>(T eventData)
            => GameContext.Publish(eventData);

        #endregion

        #region 子类可覆盖的生命周期钩子

        protected virtual TData LoadData()
        {
            return new TData();
        }

        protected virtual void OnInitialize() { }

        protected virtual void OnDataLoaded() { }

        protected virtual void OnReady() { }

        protected virtual void OnShutdown() { }

        #endregion
    }
}
