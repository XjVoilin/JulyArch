using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 命令接口（自执行）
    /// 
    /// Command 同时携带数据和执行逻辑，推荐使用 readonly struct 实现。
    /// 
    /// 【职责】
    /// 编排跨 Store 的业务操作（如购买道具、副本结算、装备穿戴等）
    /// 
    /// 【使用指引】
    /// - 所有玩家主动触发的、会改变持久化数据的业务操作，走 Command
    /// - Store 的公开方法是给 Command 调用的原子构件，不是给外部随意调用的入口
    /// - 前置检查合并在 Execute 开头，不满足时直接返回 Fail
    /// - 通过 context.GetStore&lt;T&gt;() 获取具体 Store 进行数据变更
    /// - 通过 context.Query&lt;T&gt;() 获取只读查询接口进行条件判断
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// 执行命令
        /// 同步场景直接返回 UniTask.FromResult(...)，异步场景使用 async/await
        /// </summary>
        UniTask<CommandResult> Execute(ICommandContext context);
    }
}
