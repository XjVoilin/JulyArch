using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Data.UI;

namespace JulyArch
{
    /// <summary>
    /// 场景切换协调器
    /// 
    /// 职责：统一处理运行时场景切换的完整流程
    /// 显示过渡界面 → 清理业务 UI / 音效 → 执行准备逻辑 → 切换场景 → 隐藏过渡界面
    /// </summary>
    public static class SceneTransitionHandler
    {
        private static UIOpenOptions _loadingOptions;

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
        /// 带过渡界面的场景切换。
        /// onPrepare 在过渡界面显示后、场景切换前执行，用于下载资源、加载配置等。
        /// </summary>
        public static async UniTask SwitchAsync(
            string sceneName,
            Func<CancellationToken, UniTask> onPrepare = null,
            CancellationToken ct = default)
        {
            if (_loadingOptions != null)
                await GF.UI.OpenAsync(_loadingOptions, ct);

            try
            {
                GF.UI.CloseLayer(UILayer.Background);
                GF.UI.CloseLayer(UILayer.Normal);
                GF.UI.CloseLayer(UILayer.Popup);
                GF.UI.CloseLayer(UILayer.Top);
                GF.Audio.StopAllSFX();

                if (onPrepare != null)
                    await onPrepare(ct);

                await GF.Scene.SwitchAsync(sceneName, ct);
            }
            finally
            {
                if (_loadingOptions != null)
                    GF.UI.Close(_loadingOptions.WindowIdentifier.ID);
            }
        }
    }
}
