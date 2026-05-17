using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    public abstract class ProcedureBase : IProcedure, IArchNode
    {
        private IGameContext _architecture;

        public IGameContext GetArchitecture() => _architecture;

        void IArchitectureSettable.SetArchitecture(IGameContext ctx) => _architecture = ctx;

        protected T GetStore<T>() where T : class, IStore
            => GetArchitecture().GetStore<T>();

        public abstract UniTask ExecuteAsync(CancellationToken ct);
    }
}
