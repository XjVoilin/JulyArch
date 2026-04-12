using System;
using JulyCore;
using JulyCore.Core;

namespace JulyArch
{
    /// <summary>
    /// Store 基类 —— 可存储数据（服务器同步 / 本地持久化）的唯一所有者
    /// 运行时瞬时状态（不需要存储的数据）由 System 管理，不放 Store
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

        protected void PublishEvent<T>(T eventData) where T : IEvent
            => GF.Event.Publish(eventData);

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
