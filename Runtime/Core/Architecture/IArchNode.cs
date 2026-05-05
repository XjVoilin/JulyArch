namespace JulyArch
{
    /// <summary>
    /// 架构节点 —— 所有接入框架的类的唯一标识接口。
    /// 框架基类和业务类均通过实现此接口获得 ArchExtensions 扩展方法。
    /// </summary>
    public interface IArchNode
    {
        IGameContext GetArchitecture();
    }
}
