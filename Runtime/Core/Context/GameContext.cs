using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore;

namespace JulyArch
{
    /// <summary>
    /// 游戏上下文 — 上层架构的统一协调中心
    /// 管理 Store 注册与生命周期、System 注册与帧驱动、Command 分派
    /// </summary>
    public sealed class GameContext : ICommandContext
    {
        #region 静态

        private static GameContext _instance;

        public static IGameContext Instance
        {
            get
            {
                if (_instance == null)
                    GF.LogError("[GameContext] 实例未创建，请确保 GameEntryBase 已启动");
                return _instance;
            }
        }

        internal static GameContext Create()
        {
            if (_instance != null)
            {
                GF.LogWarning("[GameContext] 实例已存在，先销毁旧实例");
                _instance.Shutdown();
            }

            _instance = new GameContext();
            return _instance;
        }

        internal static void Destroy()
        {
            _instance?.Shutdown();
            _instance = null;
        }

        #endregion

        private readonly Dictionary<Type, IStore> _stores = new();
        private readonly Dictionary<Type, IStore> _storeQueryRegistry = new();
        private readonly List<IStore> _storeList = new();

        private readonly List<IGameSystem> _systems = new();
        private readonly Dictionary<Type, IGameSystem> _systemLookup = new();

        private bool _initialized;

        public void RegisterStore(IStore store)
        {
            if (_initialized)
            {
                GF.LogError($"[GameContext] 初始化后不允许注册 Store: {store?.GetType().Name}");
                return;
            }

            if (store == null) throw new ArgumentNullException(nameof(store));

            var type = store.GetType();
            if (!_stores.TryAdd(type, store))
            {
                GF.LogWarning($"[GameContext] Store {type.Name} 已注册，跳过");
                return;
            }

            _storeList.Add(store);

            foreach (var iface in type.GetInterfaces())
            {
                if (iface != typeof(IStoreQueries) && typeof(IStoreQueries).IsAssignableFrom(iface))
                    _storeQueryRegistry[iface] = store;
            }
        }

        public void RegisterSystem(IGameSystem system)
        {
            if (_initialized)
            {
                GF.LogError($"[GameContext] 初始化后不允许注册 System: {system?.GetType().Name}");
                return;
            }

            if (system == null) throw new ArgumentNullException(nameof(system));

            var type = system.GetType();
            if (!_systemLookup.TryAdd(type, system))
            {
                GF.LogWarning($"[GameContext] System {type.Name} 已注册，跳过");
                return;
            }

            _systems.Add(system);

            foreach (var iface in type.GetInterfaces())
            {
                if (iface != typeof(IGameSystem) && iface != typeof(IDisposable)
                    && typeof(IGameSystem).IsAssignableFrom(iface))
                    _systemLookup.TryAdd(iface, system);
            }
        }

        /// <summary>
        /// 初始化：Store.Initialize → LoadAsync → OnReady → System.OnInit → OnStart → GameReadyEvent
        /// 任何步骤失败都会中断初始化并向上抛出异常
        /// </summary>
        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
            {
                GF.LogWarning("[GameContext] 已经初始化，跳过");
                return;
            }

            GF.Log("[GameContext] 开始初始化...");

            foreach (var store in _storeList)
            {
                ct.ThrowIfCancellationRequested();
                store.Initialize(this);
            }

            foreach (var store in _storeList)
            {
                ct.ThrowIfCancellationRequested();
                await store.LoadAsync();
            }

            foreach (var store in _storeList)
                store.OnReady();

            foreach (var system in _systems)
            {
                ct.ThrowIfCancellationRequested();
                await system.OnInit(this, ct);
            }

            foreach (var system in _systems)
                system.OnStart();

            _initialized = true;
            GF.Log($"[GameContext] 初始化完成 (Stores={_stores.Count}, Systems={_systems.Count})");
            GF.Event.Publish(new GameReadyEvent());
        }

        public void Update(float deltaTime)
        {
            if (!_initialized) return;

            for (var i = 0; i < _systems.Count; i++)
            {
                try { _systems[i].OnUpdate(deltaTime); }
                catch (Exception ex) { GF.LogError($"[GameContext] {_systems[i].Name}.OnUpdate 异常: {ex.Message}"); }
            }
        }

        public void LateUpdate(float deltaTime)
        {
            if (!_initialized) return;

            for (int i = 0; i < _systems.Count; i++)
            {
                try { _systems[i].OnLateUpdate(deltaTime); }
                catch (Exception ex) { GF.LogError($"[GameContext] {_systems[i].Name}.OnLateUpdate 异常: {ex.Message}"); }
            }
        }

        public async UniTask ShutdownAsync()
        {
            if (!_initialized) return;
            GF.Log("[GameContext] 开始关闭...");

            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { await _systems[i].OnShutdown(); }
                catch (Exception ex) { GF.LogError($"[GameContext] {_systems[i].Name}.OnShutdown: {ex.Message}"); }
            }

            ShutdownDispose();
        }

        #region ICommandContext / IGameContext

        public T Query<T>() where T : class, IStoreQueries
        {
            if (_storeQueryRegistry.TryGetValue(typeof(T), out var store))
                return store as T;

            GF.LogError($"[GameContext] Query<{typeof(T).Name}> 未注册");
            return null;
        }

        public T GetStore<T>() where T : class, IStore
        {
            if (_stores.TryGetValue(typeof(T), out var store))
                return (T)store;

            GF.LogError($"[GameContext] GetStore<{typeof(T).Name}> 未注册");
            return null;
        }

        public T GetSystem<T>() where T : class, IGameSystem
        {
            if (_systemLookup.TryGetValue(typeof(T), out var system))
                return (T)system;

            GF.LogError($"[GameContext] GetSystem<{typeof(T).Name}> 未注册");
            return null;
        }

        public async UniTask<CommandResult> Execute<TCommand>(TCommand command) where TCommand : ICommand
        {
            try
            {
                return await command.Execute(this);
            }
            catch (Exception ex)
            {
                GF.LogError($"[GameContext] Command {typeof(TCommand).Name} 执行异常: {ex}");
                return CommandResult.Fail($"Command execution failed: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// 同步关闭（用于 Create 重建和 Destroy 退出场景，System 的异步关闭会被 Forget）
        /// </summary>
        private void Shutdown()
        {
            if (!_initialized) return;
            GF.Log("[GameContext] 开始关闭...");

            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { _systems[i].OnShutdown().Forget(); }
                catch (Exception ex) { GF.LogError($"[GameContext] {_systems[i].Name}.OnShutdown: {ex.Message}"); }
            }

            ShutdownDispose();
        }

        /// <summary>
        /// Shutdown/ShutdownAsync 共享的后半段：Dispose System → Shutdown Store → 清理
        /// </summary>
        private void ShutdownDispose()
        {
            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { _systems[i].Dispose(); }
                catch (Exception ex) { GF.LogError($"[GameContext] {_systems[i].Name}.Dispose: {ex.Message}"); }
            }

            for (var i = _storeList.Count - 1; i >= 0; i--)
            {
                try { _storeList[i].Shutdown(); }
                catch (Exception ex) { GF.LogError($"[GameContext] Store.Shutdown: {ex.Message}"); }
            }

            _stores.Clear();
            _storeList.Clear();
            _storeQueryRegistry.Clear();
            _systems.Clear();
            _systemLookup.Clear();
            _initialized = false;

            GF.Log("[GameContext] 已关闭");
        }
    }
}