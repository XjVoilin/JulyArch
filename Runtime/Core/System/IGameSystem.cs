using System;

namespace JulyArch
{
    /// <summary>
    /// 游戏系统接口
    /// 生命周期：OnInit → OnStart → OnShutdown → Dispose
    /// 需要帧更新的 System 额外实现 IUpdatableSystem
    /// </summary>
    public interface IGameSystem : IDisposable, IArchitectureSettable
    {
        void OnInit();
        void OnStart();
        void OnShutdown();
    }

    public interface IUpdatableSystem
    {
        void OnUpdate(float deltaTime);
    }
}
