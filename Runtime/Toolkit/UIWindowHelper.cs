using JulyCore;
using JulyCore.Data.UI;

namespace JulyArch
{
    /// <summary>
    /// UI 配置提供者接口
    /// 项目侧实现此接口，将配置表数据映射为 UIOpenOptions
    /// </summary>
    public interface IUIWindowConfigProvider
    {
        UIOpenOptions GetUIOpenOptions(int uiWindowID);
    }

    /// <summary>
    /// UI 窗口辅助类
    /// 通过注入的 IUIWindowConfigProvider 从配置表获取参数并打开 UI
    /// </summary>
    public static class UIWindowHelper
    {
        private static IUIWindowConfigProvider _provider;

        public static void SetProvider(IUIWindowConfigProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// 打开 UI 窗口
        /// </summary>
        /// <param name="uiWindowID">UI 窗口 ID</param>
        /// <param name="data">传递给 UI 的数据</param>
        public static void Open(int uiWindowID, object data = null)
        {
            var option = GetUIOpenOptions(uiWindowID);
            if (option == null) return;

            option.Data = data;
            GF.UI.Open(option);
        }

        /// <summary>
        /// 从配置表获取 UI 打开参数
        /// </summary>
        /// <param name="uiWindowID">UI 窗口 ID</param>
        /// <returns>UI 打开参数，provider 未设置或未找到配置时返回 null</returns>
        public static UIOpenOptions GetUIOpenOptions(int uiWindowID)
        {
            if (_provider == null)
            {
                GF.LogWarning("[UIWindowHelper] IUIWindowConfigProvider 未设置，请在初始化时调用 SetProvider");
                return null;
            }

            return _provider.GetUIOpenOptions(uiWindowID);
        }
    }
}
