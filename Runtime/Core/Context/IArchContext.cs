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

        /// <summary>
        /// 按类型获取已注册的可定位 View（实现 ISingletonView 的 GameView）。
        /// View 随场景动态注册/注销，未命中返回 null。
        /// </summary>
        T GetView<T>() where T : GameView;

        IEventBus Event { get; }
        UniTask RunProcedure(ProcedureBase procedure, CancellationToken ct = default);
    }
}
