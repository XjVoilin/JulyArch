using JulyCore;
using JulyCore.Provider.UI;

namespace JulyArch
{
    /// <summary>
    /// UI 面板 View 基类
    /// 所有需要接入架构的 UI 面板应继承此类
    /// </summary>
    public abstract class GameUIView : UIBase, ICanQuery, ICanGetSystem, ICanExecute
    {
        #region 快捷方法（委托给 ArchExtensions）

        protected T Query<T>() where T : class, IStoreQueries
            => ArchExtensions.Query<T>(this);

        protected T GetSystem<T>() where T : class, IGameSystem
            => ArchExtensions.GetSystem<T>(this);

        protected CommandResult Execute<TCommand>(TCommand command) where TCommand : ICommand
            => ArchExtensions.Execute(this, command);

        #endregion

        /// <summary>
        /// UI 专用：执行命令 + 失败时自动弹出错误提示
        /// </summary>
        protected CommandResult ExecuteCommand<TCommand>(
            TCommand command, bool showErrorTip = true) where TCommand : ICommand
        {
            var result = Execute(command);
            if (!result.IsSuccess && showErrorTip && !string.IsNullOrEmpty(result.Message))
                GF.UI.ShowTip(result.Message);
            return result;
        }
    }
}