using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 游戏上下文的消费者接口 — View / System / Procedure / 外部代码通过此接口与架构交互。
    /// 注册和生命周期管理不在此接口，由 GameContext 具体类直接提供。
    /// </summary>
    public interface IGameContext
    {
        T GetStore<T>() where T : StoreBase;
        T GetSystem<T>() where T : GameSystemBase;
        IEventBus Event { get; }
        UniTask RunProcedure(ProcedureBase procedure, CancellationToken ct = default);
    }
}
