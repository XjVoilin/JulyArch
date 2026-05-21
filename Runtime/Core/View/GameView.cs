using System;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 场景级 View 基类
    /// 子类必须实现 GetArchitecture() 声明自己所属的 Context
    /// </summary>
    public abstract class GameView : MonoBehaviour, ICanGetStore, ICanEvent, ICanGetSystem
    {
        public abstract IArchContext GetArchitecture();

        protected T GetStore<T>() where T : StoreBase
            => GetArchitecture().GetStore<T>();

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
