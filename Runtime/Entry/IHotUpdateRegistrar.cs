using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 热更程序集注册入口接口。
    /// 框架在 DLL 加载后自动发现实现类并调用，业务侧实现此接口完成所有热更注册。
    /// </summary>
    public interface IHotUpdateRegistrar
    {
        /// <summary>
        /// 注册热更 Provider / Store / System。
        /// 在 DLL 加载后、Module 初始化前调用。
        /// </summary>
        UniTask RegisterAsync(GameContext ctx);

        /// <summary>
        /// 启动游戏业务流程。
        /// 时机：AOT 基础设施就绪后、System.OnUpdate 开始驱动后。
        /// 典型用途：配置 UI 窗口、通过 System 进入首个业务场景。
        /// </summary>
        UniTask OnGameLaunch();
    }
}
