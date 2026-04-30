using System;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 场景级 View 基类
    /// 所有需要接入架构的场景 MonoBehaviour（场景表现、场景初始化等）应继承此类
    /// </summary>
    public abstract class GameView : MonoBehaviour, ICanQuery, ICanGetSystem, ICanMutate
    {
        private GameContext _boundContext;

        protected virtual void OnEnable()
        {
            OnViewEnable();
        }

        protected virtual void OnDisable()
        {
            _boundContext?.Event.UnsubscribeAll(this);
            _boundContext = null;
            OnViewDisable();
        }

        protected virtual void OnViewEnable() { }

        protected virtual void OnViewDisable() { }

        /// <summary>
        /// 订阅事件，自动绑定到当前 Active Context，OnDisable 时自动退订
        /// </summary>
        protected void Subscribe<T>(Action<T> handler)
        {
            _boundContext ??= GameContext.Active;
            _boundContext?.Event.Subscribe<T>(handler, this);
        }

        #region 快捷方法（委托给 ArchExtensions）

        protected T Query<T>() where T : class, IStoreQueries
            => ArchExtensions.Query<T>(this);

        protected T GetSystem<T>() where T : class, IGameSystem
            => ArchExtensions.GetSystem<T>(this);

        protected MutationResult Mutate<TMutation>(TMutation mutation) where TMutation : IMutation
            => ArchExtensions.Mutate(this, mutation);

        #endregion
    }
}
