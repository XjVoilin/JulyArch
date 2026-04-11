using Cysharp.Threading.Tasks;

namespace JulyArch
{
    public abstract class GameSystemBase : IGameSystem, ICanQuery, ICanGetSystem, ICanExecute, ICanGetStore
    {
        public virtual string Name => GetType().Name;

        void IGameSystem.OnInit(IGameContext context)
        {
            OnInitialize();
        }

        public virtual void OnStart() { }

        public virtual void OnUpdate(float deltaTime) { }

        public virtual void OnLateUpdate(float deltaTime) { }

        public virtual UniTask OnShutdown() => UniTask.CompletedTask;

        public virtual void Dispose() { }

        protected virtual void OnInitialize() { }

        #region 快捷方法（委托给 ArchExtensions）

        protected T Query<T>() where T : class, IStoreQueries
            => ArchExtensions.Query<T>(this);

        protected T GetSystem<T>() where T : class, IGameSystem
            => ArchExtensions.GetSystem<T>(this);

        protected CommandResult Execute<TCommand>(TCommand command) where TCommand : ICommand
            => ArchExtensions.Execute(this, command);

        /// <summary>
        /// 获取 System 所管理的 Store。仅用于 System 直接管理自己的 Store。
        /// 跨 Store 操作必须走 Command。
        /// </summary>
        protected T GetStore<T>() where T : class, IStore
            => ArchExtensions.GetStore<T>(this);

        #endregion
    }
}
