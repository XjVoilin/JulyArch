using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    public abstract class ProcedureBase : IProcedure, IArchNode
    {
        private IGameContext _architecture;

        public IGameContext GetArchitecture() => _architecture;

        void IArchitectureSettable.SetArchitecture(IGameContext ctx) => _architecture = ctx;

        public abstract UniTask ExecuteAsync(CancellationToken ct);
    }
}
