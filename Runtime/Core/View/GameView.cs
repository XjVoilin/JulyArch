using JulyCore;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 场景级 View 基类
    /// 所有需要接入架构的场景 MonoBehaviour（场景表现、场景初始化等）应继承此类
    /// </summary>
    public abstract class GameView : MonoBehaviour, ICanQuery, ICanGetSystem, ICanExecute
    {
        protected virtual void OnEnable()
        {
            OnViewEnable();
        }

        protected virtual void OnDisable()
        {
            GF.Event.UnsubscribeAll(this);
            OnViewDisable();
        }

        protected virtual void OnViewEnable() { }

        protected virtual void OnViewDisable() { }

        #region 快捷方法（委托给 ArchExtensions）

        protected T Query<T>() where T : class, IStoreQueries
            => ArchExtensions.Query<T>(this);

        protected T GetSystem<T>() where T : class, IGameSystem
            => ArchExtensions.GetSystem<T>(this);

        protected CommandResult Execute<TCommand>(TCommand command) where TCommand : ICommand
            => ArchExtensions.Execute(this, command);

        #endregion
    }
}
