using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Data.UI;

namespace JulyArch
{
    /// <summary>
    /// 场景切换协调器
    /// 
    /// 用法：
    ///   await SceneTransitionHandler.EnterAsync(ct);
    ///   // … 下载资源、切场景、初始化 …
    ///   await SceneTransitionHandler.ExitAsync(ct);
    /// </summary>
    public static class SceneTransitionHandler
    {
        private static UIOpenOptions _loadingOptions;
        private static ISceneTransitionView _activeView;

        /// <summary>
        /// 初始化过渡窗口配置（热更阶段调用，传入项目侧的窗口 ID 和名称）
        /// </summary>
        public static void Initialize(int loadingWindowId, string loadingWindowName)
        {
            _loadingOptions = new UIOpenOptions
            {
                WindowIdentifier = new WindowIdentifier(loadingWindowId, loadingWindowName),
                Layer = UILayer.Loading,
                AddToStack = false
            };
        }

        /// <summary>
        /// 入场：打开过渡窗口 → 播放入场动画 → 清理业务 UI / 音效
        /// </summary>
        public static async UniTask EnterAsync(CancellationToken ct = default)
        {
            _activeView = null;

            if (_loadingOptions != null)
                _activeView = await GF.UI.OpenAsync(_loadingOptions, ct) as ISceneTransitionView;

            if (_activeView != null)
                await _activeView.PlayEnterAsync();

            GF.UI.CloseLayer(UILayer.Background);
            GF.UI.CloseLayer(UILayer.Normal);
            GF.UI.CloseLayer(UILayer.Popup);
            GF.UI.CloseLayer(UILayer.Top);
            GF.Audio.StopAllSFX();
        }

        /// <summary>
        /// 退场：播放退场动画 → 关闭过渡窗口
        /// </summary>
        public static async UniTask ExitAsync(CancellationToken ct = default)
        {
            if (_activeView != null)
            {
                await _activeView.PlayExitAsync();
                _activeView = null;
            }

            if (_loadingOptions != null)
                await GF.UI.CloseAsync(_loadingOptions.WindowIdentifier.ID);
        }
    }
}
