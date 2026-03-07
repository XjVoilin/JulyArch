using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 命令接口
    /// Command 同时携带数据和执行逻辑，推荐使用 readonly struct 实现。
    /// 编排跨 Store 的业务操作（如购买道具、副本结算、装备穿戴等）
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        UniTask<CommandResult> Execute(ICommandContext context);
    }
}
