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

        internal static IGameContext Instance
        {
            get
            {
                if (_instance == null)
                    throw new System.InvalidOperationException("[GameContext] 实例未创建，请确保 GameEntry 已启动");
                return _instance;
            }
        }
        
        /// <summary>
        /// 框架内部用：提供 ICommandContext 访问（供 ArchExtensions.GetStore 调用）
        /// </summary>
        internal static ICommandContext CommandContext => _instance;

        public static GameContext Create()
        {
            if (_instance != null)
            {
                GF.LogWarning("[GameContext] 实例已存在，先销毁旧实例");
                _instance.Shutdown();
            }

            _instance = new GameContext();
            return _instance;
        }

        public static void Destroy()
        {
            _instance?.Shutdown();
            _instance = null;
        }

        /// <summary>
        /// 帧驱动（供 GameEntry.Update 调用）
        /// </summary>
        public static void Tick(float deltaTime) => _instance?.Update(deltaTime);

        /// <summary>
        /// 帧驱动（供 GameEntry.LateUpdate 调用）
        /// </summary>
        public static void LateTick(float deltaTime) => _instance?.LateUpdate(deltaTime);

#if JULYGF_DEBUG || UNITY_EDITOR
        /// <summary>
        /// 仅 Debug / Editor 可用，绕过架构访问控制
        /// </summary>
        public static ICommandContext DebugContext => _instance;
#endif

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
        /// 初始化：Store.Initialize → Load/LoadAsync → OnReady → System.OnInit → OnStart → GameReadyEvent
        /// 同步 Store 立即 Load，异步 Store（IAsyncLoadable）并行 WhenAll，全部就绪后统一 OnReady
        /// </summary>
        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
            {
                GF.LogWarning("[GameContext] 已经初始化，跳过");
                return;
            }

            var asyncTasks = new List<UniTask>();

            foreach (var store in _storeList)
            {
                ct.ThrowIfCancellationRequested();
                store.Initialize();

                if (store is IAsyncLoadable asyncLoadable)
                    asyncTasks.Add(asyncLoadable.LoadAsync());
                else
                    store.Load();
            }

            if (asyncTasks.Count > 0)
                await UniTask.WhenAll(asyncTasks);

            foreach (var store in _storeList)
                store.OnReady();

            foreach (var system in _systems)
                system.OnInit();

            foreach (var system in _systems)
                system.OnStart();

            _initialized = true;
            GF.Event.Publish(new GameReadyEvent());
        }

        public void Update(float deltaTime)
        {
            if (!_initialized) return;

            for (var i = 0; i < _systems.Count; i++)
            {
                try { _systems[i].OnUpdate(deltaTime); }
                catch (Exception ex) { GF.LogException(ex); }
            }
        }

        public void LateUpdate(float deltaTime)
        {
            if (!_initialized) return;

            for (int i = 0; i < _systems.Count; i++)
            {
                try { _systems[i].OnLateUpdate(deltaTime); }
                catch (Exception ex) { GF.LogException(ex); }
            }
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

        public CommandResult Execute<TCommand>(TCommand command) where TCommand : ICommand
        {
            try
            {
                return command.Execute(this);
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                return CommandResult.Fail($"Command execution failed: {ex.Message}");
            }
        }

        #endregion

        private void Shutdown()
        {
            if (!_initialized) return;

            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { _systems[i].OnShutdown(); }
                catch (Exception ex) { GF.LogException(ex); }
            }

            ShutdownDispose();
        }

        /// <summary>
        /// Dispose System → Shutdown Store → 清理
        /// </summary>
        private void ShutdownDispose()
        {
            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { _systems[i].Dispose(); }
                catch (Exception ex) { GF.LogException(ex); }
            }

            for (var i = _storeList.Count - 1; i >= 0; i--)
            {
                try { _storeList[i].Shutdown(); }
                catch (Exception ex) { GF.LogException(ex); }
            }

            _stores.Clear();
            _storeList.Clear();
            _storeQueryRegistry.Clear();
            _systems.Clear();
            _systemLookup.Clear();
            _initialized = false;
        }
    }
}