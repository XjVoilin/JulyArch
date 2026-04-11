using System;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 游戏系统接口
    /// 生命周期：OnInit → OnStart → OnUpdate/OnLateUpdate → OnShutdown → Dispose
    /// </summary>
    public interface IGameSystem : IDisposable
    {
        void OnInit();
        void OnStart();
        void OnUpdate(float deltaTime);
        void OnLateUpdate(float deltaTime);
        UniTask OnShutdown();
    }
}
