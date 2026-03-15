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

            // Step 3: 热更注册器 — 替换 Provider + 注册 Store/System
            _gameContext = GameContext.Create();
            _registrar = FindRegistrar();
            if (_registrar != null)
                await _registrar.RegisterAsync(_gameContext);

            // Step 4: 统一初始化所有待处理的 Provider 和 Module
            await InitPendingAsync();

            // Step 5: 初始化 GameContext（Store/System）
            await _gameContext.InitializeAsync(destroyCancellationToken);

            Application.quitting += OnApplicationQuitting;

            _isGameInitialized = true;

            if (_registrar != null)
                await _registrar.OnGameReady();

            await OnGameInitialized();
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
        /// GameContext 初始化完成后，用于启动游戏流程（AOT 侧逻辑）。
        /// </summary>
        protected virtual UniTask OnGameInitialized()
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
