namespace JulyArch
{
    public abstract class GameSystemBase : IGameSystem, ICanQuery, ICanGetSystem, ICanExecute, ICanGetStore
    {
        void IGameSystem.OnInit()
        {
            OnInitialize();
        }

        public virtual void OnStart() { }

        public virtual void OnUpdate(float deltaTime) { }

        public virtual void OnLateUpdate(float deltaTime) { }

        public virtual void OnShutdown() { }

        public virtual void Dispose() { }

        protected virtual void OnInitialize() { }

        #region 快捷方法（委托给 ArchExtensions）

        protected T Query<T>() where T : class, IStoreQueries
            => ArchExtensions.Query<T>(this);

        protected T GetSystem<T>() where T : class, IGameSystem
            => ArchExtensions.GetSystem<T>(this);

        protected CommandResult Execute<TCommand>(TCommand command) where TCommand : ICommand
            => ArchExtensions.Execute(this, command);

        protected T GetStore<T>() where T : class, IStore
            => ArchExtensions.GetStore<T>(this);

        #endregion
    }
}
