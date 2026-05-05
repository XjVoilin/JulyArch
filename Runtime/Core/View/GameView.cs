using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 场景级 View 基类
    /// 子类必须实现 GetArchitecture() 声明自己所属的 Context
    /// </summary>
    public abstract class GameView : MonoBehaviour, IArchNode
    {
        public abstract IGameContext GetArchitecture();

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
