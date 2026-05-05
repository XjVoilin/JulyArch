namespace JulyArch
{
    public abstract class GameSystemBase : IGameSystem, IArchNode
    {
        private IGameContext _architecture;

        public IGameContext GetArchitecture() => _architecture;

        void IArchitectureSettable.SetArchitecture(IGameContext ctx) => _architecture = ctx;

        void IGameSystem.OnInit() => OnInitialize();

        public virtual void OnStart() { }

        public virtual void OnShutdown() { }

        public virtual void Dispose() { }

        protected virtual void OnInitialize() { }
    }
}
