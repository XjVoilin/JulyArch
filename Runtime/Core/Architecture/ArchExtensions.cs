namespace JulyArch
{
    public static class ArchExtensions
    {
        public static T Query<T>(this ICanQuery self) where T : class, IStoreQueries
            => GameContext.ActiveAsContext.Query<T>();

        public static T GetSystem<T>(this ICanGetSystem self) where T : class, IGameSystem
            => GameContext.ActiveAsContext.GetSystem<T>();

        public static MutationResult Mutate<T>(this ICanMutate self, T mutation) where T : IMutation
            => GameContext.ActiveAsContext.Mutate(mutation);

        internal static T GetStore<T>(this ICanGetStore self) where T : class, IStore
            => GameContext.ActiveAsMutationContext.GetStore<T>();
    }
}
