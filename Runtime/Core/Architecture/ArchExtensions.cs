namespace JulyArch
{
    public static class ArchExtensions
    {
        public static T Query<T>(this ICanQuery self) where T : class, IStoreQueries
            => GameContext.Instance.Query<T>();

        public static T GetSystem<T>(this ICanGetSystem self) where T : class, IGameSystem
            => GameContext.Instance.GetSystem<T>();

        public static CommandResult Execute<T>(this ICanExecute self, T command) where T : ICommand
            => GameContext.Instance.Execute(command);

        internal static T GetStore<T>(this ICanGetStore self) where T : class, IStore
            => GameContext.GetStoreInternal<T>();
    }
}
