using Cysharp.Threading.Tasks;
using JulyCore.Core;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 游戏入口
    /// </summary>
    public abstract class GameEntryBase : JulyGameEntry
    {
        private GameContext _gameContext;
        private bool _isGameInitialized;

        protected override async UniTask InnerInit()
        {
            _gameContext = GameContext.Create();

    		await OnPreGameInit();

            RegisterStores(_gameContext);
            RegisterSystems(_gameContext);

            await _gameContext.InitializeAsync(destroyCancellationToken);

            Application.quitting += OnApplicationQuitting;

            _isGameInitialized = true;

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

		// 热更 DLL 加载点：YooAsset 已就绪，Store/System 注册前
		protected virtual UniTask OnPreGameInit()
		{
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 注册 Store
        /// </summary>
        protected virtual void RegisterStores(GameContext ctx)
        {
        }

        /// <summary>
        /// 注册 System
        /// </summary>
        protected virtual void RegisterSystems(GameContext ctx)
        {
        }

        /// <summary>
        /// GameContext初始化完成后
        /// 可用于启动游戏流程
        /// </summary>
        protected virtual UniTask OnGameInitialized()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 应用退出时关闭 GameContext
        /// Application.quitting 在 OnDestroy 之前触发
        /// 确保游戏层在框架层之前完成清理
        /// </summary>
        protected virtual void OnApplicationQuitting()
        {
            Application.quitting -= OnApplicationQuitting;
            _isGameInitialized = false;

            if (_gameContext != null)
            {
                // 统一走 Destroy 路径）
                GameContext.Destroy();
                _gameContext = null;
            }
        }
    }
}