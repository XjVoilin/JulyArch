using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    public abstract class SystemBase : ICanGetStore, ICanEvent, ICanGetSystem, ICanGetView, ICanRunProcedure
    {
        private ArchContext _architecture;
        private bool _initialized;

        internal bool IsInitialized => _initialized;

        internal void SetContext(ArchContext ctx) => _architecture = ctx;

        internal async UniTask InitializeAsync()
        {
            if (_initialized) return;
            await OnInitializeAsync();
            _initialized = true;
        }

        internal void Shutdown()
        {
            if (!_initialized) return;
            try { _architecture?.Event?.UnsubscribeAll(this); }
            catch { }
            OnShutdown();
            _initialized = false;
        }

        protected virtual UniTask OnInitializeAsync() => UniTask.CompletedTask;
        protected virtual void OnShutdown() { }

        protected T GetStore<T>() where T : StoreBase
            => _architecture.GetStore<T>();

        protected void Subscribe<T>(Action<T> handler)
            => _architecture.Event.Subscribe(handler, this);

        protected void Unsubscribe<T>(Action<T> handler)
            => _architecture.Event.Unsubscribe(handler);

        protected void Publish<T>(T eventData)
            => _architecture.Event.Publish(eventData);

        protected T GetSystem<T>() where T : class
            => _architecture.GetSystem<T>();

        protected T TryGetSystem<T>() where T : class
            => _architecture.TryGetSystem<T>();

        protected T GetView<T>() where T : GameView
            => _architecture.GetView<T>();

        protected UniTask RunProcedure(ProcedureBase procedure, CancellationToken ct = default)
            => _architecture.RunProcedure(procedure, ct);
    }
}
