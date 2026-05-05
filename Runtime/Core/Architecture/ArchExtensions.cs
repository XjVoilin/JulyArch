using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    public static class ArchExtensions
    {
        public static T Query<T>(this IArchNode self) where T : class, IStoreQueries
            => self.GetArchitecture().Query<T>();

        public static bool TryQuery<T>(this IArchNode self, out T result) where T : class, IStoreQueries
            => self.GetArchitecture().TryQuery(out result);

        public static T GetSystem<T>(this IArchNode self) where T : class, IGameSystem
            => self.GetArchitecture().GetSystem<T>();

        public static MutationResult Mutate<T>(this IArchNode self, T mutation) where T : IMutation
            => self.GetArchitecture().Mutate(mutation);

        public static MutationResult Mutate<TStore>(this IArchNode self, Action<TStore> mutation) where TStore : class, IStore
            => self.GetArchitecture().Mutate(mutation);

        public static void Subscribe<T>(this IArchNode self, Action<T> handler)
            => self.GetArchitecture().Event.Subscribe(handler, self);

        public static void Unsubscribe<T>(this IArchNode self, Action<T> handler)
            => self.GetArchitecture()?.Event.Unsubscribe(handler);

        public static void UnsubscribeAll(this IArchNode self)
            => self.GetArchitecture()?.Event.UnsubscribeAll(self);

        public static void Publish<T>(this IArchNode self, T eventData)
            => self.GetArchitecture().Event.Publish(eventData);

        public static UniTask RunProcedure(this IArchNode self, IProcedure procedure, CancellationToken ct = default)
            => self.GetArchitecture().RunProcedure(procedure, ct);

        public static void RegisterStore(this IArchNode self, IStore store)
            => self.GetArchitecture().RegisterStore(store);

        public static void RegisterSystem(this IArchNode self, IGameSystem system)
            => self.GetArchitecture().RegisterSystem(system);
    }
}
