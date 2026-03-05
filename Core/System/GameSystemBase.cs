using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    public abstract class GameSystemBase : IGameSystem
    {
        /// <summary>GameContext 提供的上下文接口</summary>
        protected IGameContext Context { get; private set; }

        /// <summary>系统名称，默认为类名</summary>
        public virtual string Name => GetType().Name;

        public async UniTask OnInit(IGameContext context, CancellationToken ct)
        {
            Context = context;
            await OnInitialize(ct);
        }

        public virtual void OnStart() { }

        public virtual void OnUpdate(float deltaTime) { }

        public virtual void OnLateUpdate(float deltaTime) { }

        public virtual UniTask OnShutdown() => UniTask.CompletedTask;

        public virtual void Dispose() { }

        /// <summary>异步初始化（子类覆盖，Context 已注入）</summary>
        protected virtual UniTask OnInitialize(CancellationToken ct) => UniTask.CompletedTask;

        #region 快捷方法

        protected T Query<T>() where T : class, IStoreQueries
            => Context.Query<T>();

        protected UniTask<CommandResult> Execute<TCommand>(TCommand command) where TCommand : ICommand
            => Context.Execute(command);

        protected T GetSystem<T>() where T : class, IGameSystem
            => Context.GetSystem<T>();

        #endregion
    }
}
