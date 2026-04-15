namespace JulyArch
{
    /// <summary>
    /// Mutation 接口
    /// Mutation 同时携带数据和执行逻辑，推荐使用 readonly struct 实现。
    /// 编排跨 Store 的同步业务操作（如购买道具、副本结算、装备穿戴等）
    /// </summary>
    public interface IMutation
    {
        /// <summary>
        /// 执行 Mutation（同步）
        /// </summary>
        MutationResult Execute(IMutationContext ctx);
    }
}
