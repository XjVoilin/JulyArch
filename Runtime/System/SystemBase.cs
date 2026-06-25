using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    public abstract class SystemBase : ICanGetStore, ICanEvent, ICanGetSystem, ICanGetView, ICanRunProcedure
    {
        private ArchContext _architecture;

        internal void SetContext(ArchContext ctx) => _architecture = ctx;

        internal void Initialize() => OnInitialize();
        internal void Start() => OnStart();
        internal void Shutdown()
        {
            // 兜底注销本 System 所有事件订阅，避免子类忘记 Unsubscribe 导致泄漏。
            // 与 GameView.OnDisable 的 UnsubscribeAll 行为对齐。
            try { _architecture?.Event?.UnsubscribeAll(this); }
            catch { /* Shutdown 期间不应因清理失败而中断后续逻辑 */ }

            OnShutdown();
        }

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
