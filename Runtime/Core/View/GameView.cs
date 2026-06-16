using System;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 场景级 View 基类
    /// 子类必须实现 GetArchitecture() 声明自己所属的 Context
    /// </summary>
    public abstract class GameView : MonoBehaviour, ICanGetStore, ICanEvent, ICanGetSystem, ICanGetView
    {
        public abstract IArchContext GetArchitecture();

        // 可定位性自动注册：仅当实现 ISingletonView 时入注册表。基类提供管道，标记表达意图。
        // 注册绑 Awake/OnDestroy（对象存在期 = 可定位期），与 OnEnable/OnDisable 的激活期解耦，
        // 以便 Procedure 能拿到当前隐藏的分区 View 再显示它。
        protected virtual void Awake()
        {
            if (this is ISingletonView && GetArchitecture() is ArchContext ctx)
                ctx.RegisterView(this);
        }

        protected virtual void OnDestroy()
        {
            if (this is ISingletonView && GetArchitecture() is ArchContext ctx)
                ctx.UnregisterView(this);
        }

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

        protected virtual void OnEnable() => OnViewEnable();

        protected virtual void OnDisable()
        {
            this.UnsubscribeAll();
            OnViewDisable();
        }

        protected virtual void OnViewEnable() { }

        protected virtual void OnViewDisable() { }
    }
}
