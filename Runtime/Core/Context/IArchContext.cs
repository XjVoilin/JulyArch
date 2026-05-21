using System.Threading;
using Cysharp.Threading.Tasks;
using JulyEvents;

namespace JulyArch
{
    /// <summary>
    /// 架构上下文的消费者接口 — View / System / Procedure / 外部代码通过此接口与架构交互。
    /// 注册和生命周期管理不在此接口，由 ArchContext 具体类直接提供。
    /// </summary>
    public interface IArchContext
    {
        T GetStore<T>() where T : StoreBase;
        T GetSystem<T>() where T : GameSystemBase;
        IEventBus Event { get; }
        UniTask RunProcedure(ProcedureBase procedure, CancellationToken ct = default);
    }
}
