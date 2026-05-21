using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    public abstract class GameSystemBase : ICanGetStore, ICanEvent, ICanGetSystem, ICanRunProcedure
    {
        private IArchContext _architecture;

        public IArchContext GetArchitecture() => _architecture;

        internal void SetArchitecture(IArchContext ctx) => _architecture = ctx;

        internal void Initialize() => OnInitialize();
        internal void Start() => OnStart();
        internal void Shutdown() => OnShutdown();

        protected virtual void OnInitialize() { }
        protected virtual void OnStart() { }
        protected virtual void OnShutdown() { }

        protected T GetStore<T>() where T : StoreBase
            => _architecture.GetStore<T>();

        protected void Subscribe<T>(Action<T> handler)
            => _architecture.Event.Subscribe(handler, this);

        protected void Unsubscribe<T>(Action<T> handler)
            => _architecture.Event.Unsubscribe(handler);

        protected void Publish<T>(T eventData)
            => _architecture.Event.Publish(eventData);

        protected T GetSystem<T>() where T : GameSystemBase
            => _architecture.GetSystem<T>();

        protected UniTask RunProcedure(ProcedureBase procedure, CancellationToken ct = default)
            => _architecture.RunProcedure(procedure, ct);
    }
}
