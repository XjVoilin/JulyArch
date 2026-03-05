using Cysharp.Threading.Tasks;
using JulyCore.Core;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// - 这个类属于 Composition Root（启动胶水），不属于业务逻辑
    /// - 它是唯一同时知道 Framework 和 Game 两个世界的地方
    /// - 框架耦合被限制在此一处
    /// </summary>
    public abstract class GameEntryBase : JulyGameEntry
    {
        private GameContext _gameContext;
        private bool _isGameInitialized;

        protected override async UniTask InnerInit()
        {
            // 1. 创建 GameContext 实例
            _gameContext = GameContext.Create();

            // 2. 注册游戏组件（子类实现）
            RegisterStores(_gameContext);
            RegisterSystems(_gameContext);

            // 3. 初始化 GameContext（初始化 Store/System → 加载数据）
            await _gameContext.InitializeAsync(destroyCancellationToken);

            // 4. 注册应用退出回调
            Application.quitting += OnApplicationQuitting;

            _isGameInitialized = true;

            // 5. 执行项目特定的启动后逻辑
            await OnGameInitialized();
        }

        protected override void Update()
        {
            base.Update(); // 驱动框架 Update

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
        /// 注册 Store
        /// </summary>
        protected virtual void RegisterStores(GameContext ctx) { }

        /// <summary>
        /// 注册 System
        /// </summary>
        protected virtual void RegisterSystems(GameContext ctx) { }

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
                // 统一走 Destroy 路径（内部同步关闭）
                GameContext.Destroy();
                _gameContext = null;
            }
        }
    }
}
