using System;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 场景级 View 基类。
    /// 子类必须实现 GetArchitecture() 声明自己所属的 Context。
    /// 子类禁止直接覆写 Awake / OnDestroy / OnEnable / OnDisable，
    /// 一律使用 OnViewAwake / OnViewDestroy / OnViewEnable / OnViewDisable 钩子。
    /// </summary>
    public abstract class GameView : MonoBehaviour, ICanGetStore, ICanEvent, ICanGetSystem, ICanGetView
    {
        public abstract IArchContext GetArchitecture();

        #region Lifecycle — 框架独占，子类使用 OnViewXxx 钩子

        // 可定位性自动注册：仅当实现 ISingletonView 时入注册表。
        // 注册绑 Awake / 注销绑 OnDestroy（对象存在期 = 可定位期），
        // 与 OnEnable / OnDisable 的激活期解耦，
        // 以便 Procedure 能拿到当前隐藏的分区 View 再显示它。

        private void Awake()
        {
            if (this is ISingletonView && GetArchitecture() is ArchContext ctx)
                ctx.RegisterView(this);
            OnViewAwake();
        }

        private void OnDestroy()
        {
            OnViewDestroy();
            if (this is ISingletonView && GetArchitecture() is ArchContext ctx)
                ctx.UnregisterView(this);
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
            => GetArchitecture().GetStore<T>();

        protected T GetView<T>() where T : GameView
            => GetArchitecture().GetView<T>();

        protected void Subscribe<T>(Action<T> handler)
            => GetArchitecture().Event.Subscribe(handler, this);

        protected void Unsubscribe<T>(Action<T> handler)
            => GetArchitecture().Event.Unsubscribe(handler);

        protected void Publish<T>(T eventData)
            => GetArchitecture().Event.Publish(eventData);

        protected T GetSystem<T>() where T : GameSystemBase
            => GetArchitecture().GetSystem<T>();

        #endregion
    }
}
