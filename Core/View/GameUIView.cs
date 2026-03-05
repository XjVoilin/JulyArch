using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Provider.UI;

namespace JulyArch
{
    /// <summary>
    /// UI 面板 View 基类
    /// 所有需要接入架构的 UI 面板应继承此类（替代直接继承 UIBase）
    ///
    /// 提供：
    ///   · Context 引用 + Query / GetSystem / Execute 快捷方法（与 GameSystemBase 对称）
    ///   · ExecuteCommand：UI 专用的命令执行，失败时自动弹出错误提示
    ///
    /// 生命周期由 JulyGF UI 系统驱动：
    ///   OnBeforeOpen → OnOpen → OnClose → OnAfterClose
    ///   OnClose 时 UIBase 自动调用 GF.Event.UnsubscribeAll(this)
    /// </summary>
    public abstract class GameUIView : UIBase
    {
        private IGameContext Context => GameContext.Instance;

        #region 快捷方法（与 GameSystemBase 对齐）

        protected T Query<T>() where T : class, IStoreQueries
            => Context.Query<T>();

        protected T GetSystem<T>() where T : class, IGameSystem
            => Context.GetSystem<T>();

        protected UniTask<CommandResult> Execute<TCommand>(TCommand command) where TCommand : ICommand
            => Context.Execute(command);

        #endregion

        /// <summary>
        /// UI 专用：执行命令 + 失败时自动弹出错误提示
        /// </summary>
        protected async UniTask<CommandResult> ExecuteCommand<TCommand>(
            TCommand command, bool showErrorTip = true) where TCommand : ICommand
        {
            var result = await Execute(command);
            if (!result.IsSuccess && showErrorTip && !string.IsNullOrEmpty(result.Message))
                GF.UI.ShowTip(result.Message);
            return result;
        }
    }
}
