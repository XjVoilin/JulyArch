using Cysharp.Threading.Tasks;
using JulyCore;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 场景级 View 基类
    /// 所有需要接入架构的场景 MonoBehaviour（场景表现、场景初始化等）应继承此类
    /// </summary>
    public abstract class GameView : MonoBehaviour
    {
        private IGameContext _context;

        protected virtual void OnEnable()
        {
            _context = GameContext.Instance;
            if (_context == null)
            {
                GF.LogWarning($"[GameView] {GetType().Name}.OnEnable: GameContext 未就绪，跳过 OnViewEnable");
                return;
            }

            OnViewEnable();
        }

        protected virtual void OnDisable()
        {
            GF.Event.UnsubscribeAll(this);
            OnViewDisable();
            _context = null;
        }

        /// <summary>
        /// View 激活：订阅事件、获取初始数据等
        /// </summary>
        protected virtual void OnViewEnable() { }

        /// <summary>
        /// View 停用：清理等
        /// </summary>
        protected virtual void OnViewDisable() { }

        #region 快捷方法（与 GameSystemBase 对齐）

        protected T Query<T>() where T : class, IStoreQueries
        {
            if (_context == null) { LogContextMissing(); return null; }
            return _context.Query<T>();
        }

        protected T GetSystem<T>() where T : class, IGameSystem
        {
            if (_context == null) { LogContextMissing(); return null; }
            return _context.GetSystem<T>();
        }

        protected UniTask<CommandResult> Execute<TCommand>(TCommand command) where TCommand : ICommand
        {
            if (_context == null) { LogContextMissing(); return UniTask.FromResult(CommandResult.Fail("GameContext not ready")); }
            return _context.Execute(command);
        }

        private void LogContextMissing()
        {
            GF.LogError($"[GameView] {GetType().Name}: GameContext 未就绪，无法执行操作");
        }

        #endregion
    }
}
