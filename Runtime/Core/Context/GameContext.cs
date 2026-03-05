using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore;

namespace JulyArch
{
    public enum GameContextState
    {
        NotInitialized,
        Ready,
    }

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
                _instance.ShutdownCore(async: false);
            }

            _instance = new GameContext();
            return _instance;
        }

        internal static void Destroy()
        {
            if (_instance != null)
            {
                _instance.ShutdownCore(async: false);
                _instance = null;
            }
        }

        #endregion

        private readonly Dictionary<Type, IStore> _stores = new();
        private readonly Dictionary<Type, IStore> _storeQueryRegistry = new();
        private readonly List<IStore> _storeList = new();

        private readonly List<IGameSystem> _systems = new();
        private readonly Dictionary<Type, IGameSystem> _systemLookup = new();

        private GameContextState _state = GameContextState.NotInitialized;

        private GameContext() { }

        // ================================================================
        // 注册
        // ================================================================

        public void RegisterStore(IStore store)
        {
            if (_state != GameContextState.NotInitialized)
            {
                GF.LogError($"[GameContext] 初始化后不允许注册 Store: {store?.GetType().Name}");
                return;
            }

            if (store == null)
                throw new ArgumentNullException(nameof(store));

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
            if (_state != GameContextState.NotInitialized)
            {
                GF.LogError($"[GameContext] 初始化后不允许注册 System: {system?.GetType().Name}");
                return;
            }

            if (system == null)
                throw new ArgumentNullException(nameof(system));

            var type = system.GetType();
            if (_systemLookup.ContainsKey(type))
            {
                GF.LogWarning($"[GameContext] System {type.Name} 已注册，跳过");
                return;
            }

            _systems.Add(system);
            _systemLookup[type] = system;

            foreach (var iface in type.GetInterfaces())
            {
                if (iface != typeof(IGameSystem) && iface != typeof(IDisposable)
                                                 && typeof(IGameSystem).IsAssignableFrom(iface))
                    _systemLookup.TryAdd(iface, system);
            }
        }

        // ================================================================
        // 生命周期
        // ================================================================

        /// <summary>
        /// 初始化：Store.Initialize → Store.LoadAsync → Store.OnReady → System.OnInit → System.OnStart → GameReadyEvent
        /// </summary>
        public async UniTask InitializeAsync(CancellationToken externalCt = default)
        {
            if (_state != GameContextState.NotInitialized)
            {
                GF.LogWarning("[GameContext] 已经初始化，跳过");
                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = cts.Token;

            GF.Log("[GameContext] 开始初始化...");

            foreach (var store in _storeList)
            {
                ct.ThrowIfCancellationRequested();
                SafeInvoke(store, "Initialize", () => store.Initialize(this));
            }

            foreach (var store in _storeList)
            {
                ct.ThrowIfCancellationRequested();
                await store.LoadAsync();
            }

            foreach (var store in _storeList)
            {
                SafeInvoke(store, "OnReady", () => store.OnReady());
            }

            foreach (var system in _systems)
            {
                ct.ThrowIfCancellationRequested();
                await system.OnInit(this, ct);
            }

            foreach (var system in _systems)
            {
                try { system.OnStart(); }
                catch (Exception ex) { GF.LogError($"[GameContext] {system.Name}.OnStart failed: {ex.Message}"); }
            }

            _state = GameContextState.Ready;
            GF.Log($"[GameContext] 初始化完成 (Stores={_stores.Count}, Systems={_systems.Count})");
            GF.Event.Publish(new GameReadyEvent());
        }

        public void Update(float deltaTime)
        {
            if (_state != GameContextState.Ready) return;

            for (int i = 0; i < _systems.Count; i++)
            {
                try { _systems[i].OnUpdate(deltaTime); }
                catch (Exception ex) { GF.LogError($"[GameContext] {_systems[i].Name}.OnUpdate 异常: {ex.Message}"); }
            }
        }

        public void LateUpdate(float deltaTime)
        {
            if (_state != GameContextState.Ready) return;

            for (int i = 0; i < _systems.Count; i++)
            {
                try { _systems[i].OnLateUpdate(deltaTime); }
                catch (Exception ex) { GF.LogError($"[GameContext] {_systems[i].Name}.OnLateUpdate 异常: {ex.Message}"); }
            }
        }

        /// <summary>异步关闭（能 await 的场景使用）</summary>
        public async UniTask ShutdownAsync()
        {
            await ShutdownCore(async: true);
        }

        // ================================================================
        // ICommandContext / IGameContext
        // ================================================================

        public T Query<T>() where T : class, IStoreQueries
        {
            if (_storeQueryRegistry.TryGetValue(typeof(T), out var store))
                return store as T;

            GF.LogError($"[GameContext] Store query interface {typeof(T).Name} 未注册");
            return null;
        }

        public T GetStore<T>() where T : class, IStore
        {
            if (_stores.TryGetValue(typeof(T), out var store))
                return (T)store;

            GF.LogError($"[GameContext] Store {typeof(T).Name} 未注册");
            return null;
        }

        public T GetSystem<T>() where T : class, IGameSystem
        {
            if (_systemLookup.TryGetValue(typeof(T), out var system))
                return (T)system;

            GF.LogError($"[GameContext] System {typeof(T).Name} 未注册");
            return null;
        }

        public async UniTask<CommandResult> Execute<TCommand>(TCommand command)
            where TCommand : ICommand
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

        // ================================================================
        // 内部
        // ================================================================

        /// <summary>
        /// 统一关闭逻辑。async=true 时 await System.OnShutdown，async=false 时 fire-and-forget
        /// </summary>
        private async UniTask ShutdownCore(bool async)
        {
            if (_state == GameContextState.NotInitialized) return;

            GF.Log("[GameContext] 开始关闭...");

            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (async)
                        await _systems[i].OnShutdown();
                    else
                        _systems[i].OnShutdown().Forget();
                }
                catch (Exception ex)
                {
                    GF.LogError($"[GameContext] {_systems[i].Name}.OnShutdown failed: {ex.Message}");
                }
            }

            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { _systems[i].Dispose(); }
                catch (Exception ex) { GF.LogError($"[GameContext] {_systems[i].Name}.Dispose failed: {ex.Message}"); }
            }

            for (var i = _storeList.Count - 1; i >= 0; i--)
            {
                SafeInvoke(_storeList[i], "Shutdown", () => _storeList[i].Shutdown());
            }

            _stores.Clear();
            _storeList.Clear();
            _storeQueryRegistry.Clear();
            _systems.Clear();
            _systemLookup.Clear();
            _state = GameContextState.NotInitialized;

            GF.Log("[GameContext] 已关闭");
        }

        private void SafeInvoke(IStore store, string operation, Action action)
        {
            try { action(); }
            catch (Exception ex) { GF.LogError($"[GameContext] {store.GetType().Name}.{operation} failed: {ex.Message}"); }
        }
    }
}
