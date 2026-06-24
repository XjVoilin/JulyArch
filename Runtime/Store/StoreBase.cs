using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// Store 非泛型基类 — 作为 GetStore 泛型约束和 ArchContext 内部管理的公共类型。
    /// 生命周期方法全部 internal，仅 ArchContext 可调用。
    /// </summary>
    public abstract class StoreBase
    {
        private ArchContext _architecture;

        internal void SetContext(ArchContext ctx) => _architecture = ctx;

        internal abstract void Load();
        internal abstract bool IsAsyncLoadable { get; }
        internal abstract UniTask LoadAsync();
        internal void Ready() => OnReady();
        internal abstract void Shutdown();

        protected virtual void OnReady() { }

        /// <summary>
        /// 预留的 Store 写操作追踪钩子。
        /// 当前为空方法，保留签名以便未来接入 debug 追踪。
        /// </summary>
        protected void TraceModify([CallerMemberName] string method = null) { }
    }

    /// <summary>
    /// Store 泛型基类 — 所有业务数据的唯一所有者。
    /// 只要有第二个类需要读这份数据，它就进 Store；System 私有的内部数据除外。
    /// </summary>
    public abstract class StoreBase<TData> : StoreBase where TData : class, new()
    {
        protected TData Data { get; set; }

        protected void Publish<T>(T eventData)
            => ArchContext.Current.Event.Publish(eventData);

        internal sealed override bool IsAsyncLoadable => this is IAsyncLoadable;

        internal sealed override void Load()
        {
            if (this is IAsyncLoadable)
            {
                throw new System.InvalidOperationException(
                    $"[{GetType().Name}] implements IAsyncLoadable, must use LoadAsync() via ArchContext");
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
        }

        protected virtual TData OnLoad() => new TData();

        protected virtual void OnShutdown() { }
    }
}
