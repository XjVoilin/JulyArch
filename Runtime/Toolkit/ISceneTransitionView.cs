using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 场景过渡动画接口
    /// 由过渡窗口实现，供 SceneTransitionHandler 驱动入场/退场动画
    /// </summary>
    public interface ISceneTransitionView
    {
        /// <summary>
        /// 播放入场动画
        /// </summary>
        UniTask PlayEnterAsync();

        /// <summary>
        /// 播放退场动画
        /// </summary>
        UniTask PlayExitAsync();
    }
}
