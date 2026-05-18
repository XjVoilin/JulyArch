using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// Store 非泛型基类 — 作为 GetStore 泛型约束和 GameContext 内部管理的公共类型。
    /// 生命周期方法全部 internal，仅 GameContext 可调用。
    /// </summary>
    public abstract class StoreBase : IArchNode
    {
        private IGameContext _architecture;

        public IGameContext GetArchitecture() => _architecture;

        internal void SetArchitecture(IGameContext ctx) => _architecture = ctx;

        internal abstract void Load();
        internal abstract bool IsAsyncLoadable { get; }
        internal abstract UniTask LoadAsync();
        internal void Ready() => OnReady();
        internal abstract void Shutdown();

        protected virtual void OnReady() { }

        /// <summary>
        /// 在 Store 写方法中调用，通过 EventBus 发布 StoreModifiedEvent。
        /// Release 下为空方法，零开销；DEBUG 下可订阅事件统一拦截所有 Store 写操作。
        /// </summary>
        protected void TraceModify([CallerMemberName] string method = null)
        {
#if JULYGF_DEBUG
            GetArchitecture()?.Event?.Publish(new StoreModifiedEvent(this, method));
#endif
        }
    }

    /// <summary>
    /// Store 泛型基类 — 所有业务数据的唯一所有者。
    /// 只要有第二个类需要读这份数据，它就进 Store；System 私有的内部数据除外。
    /// </summary>
    public abstract class StoreBase<TData> : StoreBase where TData : class, new()
    {
        protected TData Data { get; set; }

        protected void Publish<T>(T eventData)
            => GetArchitecture().Event.Publish(eventData);

        internal sealed override bool IsAsyncLoadable => this is IAsyncLoadable;

        internal sealed override void Load()
        {
            if (this is IAsyncLoadable)
            {
                throw new System.InvalidOperationException(
                    $"[{GetType().Name}] implements IAsyncLoadable, must use LoadAsync() via GameContext");
            }
            Data = OnLoad();
        }

        internal sealed override UniTask LoadAsync()
        {
            return ((IAsyncLoadable)this).OnLoadAsync();
        }

        internal sealed override void Shutdown()
        {
            OnShutdown();
            Data = null;
        }

        protected virtual TData OnLoad() => new TData();

        protected virtual void OnShutdown() { }
    }
}
