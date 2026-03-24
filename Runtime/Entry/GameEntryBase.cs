using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Core;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 游戏入口
    /// 负责热更流程编排和 GameContext 生命周期管理
    /// 
    /// 初始化三段式（GameContext 就绪后）：
    ///   Phase 1  OnInfrastructureReady  (AOT)  基础设施就绪：Handler 初始化等
    ///   Phase 2  OnGameLaunch           (热更)  业务起飞：System 驱动进入首个业务场景
    ///   Phase 3  OnPostLaunch           (AOT)  发射后收尾：销毁启动 UI 等
    /// </summary>
    public abstract class GameEntryBase : JulyGameEntry
    {
        private GameContext _gameContext;
        private IHotUpdateRegistrar _registrar;
        private bool _isGameInitialized;

        protected override async UniTask InnerInit()
        {
            // Step 1: 热更新（GF 基础能力已就绪，可使用 GF.Resource 等 API）
            await OnHotUpdate();

            // Step 2: 注册默认业务 Module/Provider
            RegisterBusinessDefaults();

            // Step 3: OnConfigureBusiness — 热更注册器替换 Provider + 注册 Store/System
            _gameContext = GameContext.Create();
            _registrar = FindRegistrar();
            if (_registrar != null)
                _registrar.Register(_gameContext);

            // Step 4: 统一初始化所有待处理的 Provider/Module/Store/System
            await InitPendingAsync();

            // Step 5: 初始化 GameContext（Store/System）
            await _gameContext.InitializeAsync(destroyCancellationToken);

            Application.quitting += OnApplicationQuitting;

            // Phase 1: AOT 基础设施就绪（早于场景切换、早于 System.OnUpdate）
            await OnInfrastructureReady();

            // System 开始接收 OnUpdate / OnLateUpdate
            _isGameInitialized = true;

            // Phase 2: 热更业务起飞（System 驱动进入首个场景）
            if (_registrar != null)
                await _registrar.OnGameLaunch();

            // Phase 3: AOT 发射后收尾（销毁启动 UI 等）
            await OnPostLaunch();
        }

        protected override void Update()
        {
            base.Update();

            if (_isGameInitialized)
            {
                _gameContext.Update(Time.deltaTime);
            }
        }

        private void LateUpdate()
        {
            if (_isGameInitialized)
            {
                _gameContext.LateUpdate(Time.deltaTime);
            }
        }

        /// <summary>
        /// 热更新钩子：下载并加载热更 DLL。
        /// 此阶段基础 Module/Provider 已就绪，可使用 GF.* 基础 API。
        /// </summary>
        protected virtual UniTask OnHotUpdate()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Phase 1: AOT 基础设施就绪。
        /// 时机：GameContext 初始化完成后，OnGameLaunch 和 System.OnUpdate 之前。
        /// 典型用途：初始化 SceneTransitionHandler、CameraStackHandler 等全局 Handler。
        /// </summary>
        protected virtual UniTask OnInfrastructureReady()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Phase 3: 业务流程启动后的 AOT 侧收尾。
        /// 时机：OnGameLaunch 完成后（首个业务场景已进入）。
        /// 典型用途：销毁启动 UI、释放启动阶段临时资源。
        /// </summary>
        protected virtual UniTask OnPostLaunch()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 查找热更程序集中的 IHotUpdateRegistrar 实现
        /// </summary>
        private static IHotUpdateRegistrar FindRegistrar()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = Array.FindAll(e.Types, t => t != null);
                }

                foreach (var type in types)
                {
                    if (typeof(IHotUpdateRegistrar).IsAssignableFrom(type)
                        && !type.IsAbstract && !type.IsInterface)
                    {
                        return (IHotUpdateRegistrar)Activator.CreateInstance(type);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 应用退出时关闭 GameContext
        /// </summary>
        protected virtual void OnApplicationQuitting()
        {
            Application.quitting -= OnApplicationQuitting;
            _isGameInitialized = false;

            if (_gameContext != null)
            {
                GameContext.Destroy();
                _gameContext = null;
            }
        }
    }
}
