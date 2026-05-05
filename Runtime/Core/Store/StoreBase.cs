using System;

namespace JulyArch
{
    /// <summary>
    /// Store 基类 —— 所有业务数据的唯一所有者（无论持久化还是瞬时）。
    /// 只要有第二个类需要读这份数据，它就进 Store；System 私有的内部数据除外。
    /// </summary>
    public abstract class StoreBase<TData> : IStore, IArchNode where TData : class, new()
    {
        private IGameContext _architecture;

        public IGameContext GetArchitecture() => _architecture;

        void IArchitectureSettable.SetArchitecture(IGameContext ctx) => _architecture = ctx;

        protected TData Data { get; set; }

        #region IStore 显式实现

        void IStore.Load()
        {
            if (this is IAsyncLoadable)
            {
                throw new InvalidOperationException(
                    $"[{GetType().Name}] implements IAsyncLoadable, must use LoadAsync() via GameContext");
            }

            Data = LoadData();
        }

        void IStore.OnReady() => OnReady();

        void IStore.Shutdown()
        {
            OnShutdown();
            Data = null;
        }

        #endregion

        protected virtual TData LoadData() => new TData();

        protected virtual void OnReady() { }

        protected virtual void OnShutdown() { }
    }
}
