using System;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 场景级 View 基类。
    /// 子类禁止直接覆写 Awake / OnDestroy / OnEnable / OnDisable，
    /// 一律使用 OnViewAwake / OnViewDestroy / OnViewEnable / OnViewDisable 钩子。
    /// </summary>
    public abstract class GameView : MonoBehaviour, ICanGetStore, ICanEvent, ICanGetSystem, ICanGetView
    {
        #region Lifecycle — 框架独占，子类使用 OnViewXxx 钩子

        private void Awake()
        {
            if (this is ISingletonView)
                ArchContext.Current?.RegisterView(this);
            OnViewAwake();
        }

        private void OnDestroy()
        {
            OnViewDestroy();
            if (this is ISingletonView)
                ArchContext.Current?.UnregisterView(this);
        }

        private void OnEnable() => OnViewEnable();

        private void OnDisable()
        {
            this.UnsubscribeAll();
            OnViewDisable();
        }

        #endregion

        #region 子类钩子

        protected virtual void OnViewAwake() { }
        protected virtual void OnViewDestroy() { }
        protected virtual void OnViewEnable() { }
        protected virtual void OnViewDisable() { }

        #endregion

        #region 能力方法

        protected T GetStore<T>() where T : StoreBase
            => ArchContext.Current.GetStore<T>();

        protected T GetView<T>() where T : GameView
            => ArchContext.Current.GetView<T>();

        protected void Subscribe<T>(Action<T> handler)
            => ArchContext.Current.Event.Subscribe(handler, this);

        protected void Unsubscribe<T>(Action<T> handler)
            => ArchContext.Current.Event.Unsubscribe(handler);

        protected void Publish<T>(T eventData)
            => ArchContext.Current.Event.Publish(eventData);

        protected T GetSystem<T>() where T : class
            => ArchContext.Current.GetSystem<T>();

        #endregion
    }
}
