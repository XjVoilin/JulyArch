using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    public static class ArchExtensions
    {
        // ── ICanGetStore ──

        public static T GetStore<T>(this ICanGetStore self) where T : StoreBase
            => self.GetArchitecture()?.GetStore<T>();

        // ── ICanEvent ──

        public static void Subscribe<T>(this ICanEvent self, Action<T> handler)
            => self.GetArchitecture()?.Event?.Subscribe(handler, self);

        public static void Unsubscribe<T>(this ICanEvent self, Action<T> handler)
            => self.GetArchitecture()?.Event?.Unsubscribe(handler);

        public static void UnsubscribeAll(this ICanEvent self)
            => self.GetArchitecture()?.Event?.UnsubscribeAll(self);

        public static void Publish<T>(this ICanEvent self, T eventData)
            => self.GetArchitecture()?.Event?.Publish(eventData);

        // ── ICanGetSystem ──

        public static T GetSystem<T>(this ICanGetSystem self) where T : GameSystemBase
            => self.GetArchitecture()?.GetSystem<T>();

        // ── ICanRunProcedure ──

        public static UniTask RunProcedure(this ICanRunProcedure self, ProcedureBase procedure, CancellationToken ct = default)
            => self.GetArchitecture()?.RunProcedure(procedure, ct) ?? UniTask.CompletedTask;
    }
}
