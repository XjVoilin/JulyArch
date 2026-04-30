using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyArch
{
    /// <summary>
    /// 游戏上下文 — 上层架构的统一协调中心
    /// 管理 Store 注册与生命周期、System 注册与帧驱动、Mutation 分派
    /// 支持多实例共存，通过 Activate/Deactivate 切换活跃 Context
    /// </summary>
    public sealed class GameContext : IMutationContext
    {
        #region 静态 — Active Context

        private static GameContext _active;

        public static GameContext Active => _active;

        internal static IGameContext ActiveAsContext
        {
            get
            {
                if (_active == null)
                    throw new InvalidOperationException(
                        "[GameContext] 无活跃实例，请确保已调用 Activate()");
                return _active;
            }
        }

        internal static IMutationContext ActiveAsMutationContext => _active;

        public void Activate() => _active = this;

        public void Deactivate()
        {
            if (_active == this) _active = null;
        }

        #region 静态事件快捷方法（转发到 Active Context 的 EventBus）

        public static void Publish<T>(T eventData) => _active?.Event.Publish(eventData);

        public static void Subscribe<T>(Action<T> handler, object owner)
            => _active?.Event.Subscribe(handler, owner);

        public static void Unsubscribe<T>(Action<T> handler)
            => _active?.Event.Unsubscribe(handler);

        public static void UnsubscribeAll(object owner)
            => _active?.Event.UnsubscribeAll(owner);

        #endregion

#if JULYGF_DEBUG || UNITY_EDITOR
        public static IMutationContext DebugContext => _active;
#endif

        #endregion

        private readonly Dictionary<Type, IStore> _stores = new();
        private readonly Dictionary<Type, IStore> _storeQueryRegistry = new();
        private readonly List<IStore> _storeList = new();

        private readonly List<IGameSystem> _systems = new();
        private readonly Dictionary<Type, IGameSystem> _systemLookup = new();

        private bool _initialized;

        public IEventBus Event { get; }

        public GameContext()
        {
            Event = new EventBus();
        }

        public void RegisterStore(IStore store)
        {
            if (_initialized)
            {
                ArchServices.Logger.LogError(
                    $"[GameContext] 初始化后不允许注册 Store: {store?.GetType().Name}");
                return;
            }

            if (store == null) throw new ArgumentNullException(nameof(store));

            var type = store.GetType();
            if (!_stores.TryAdd(type, store))
            {
                ArchServices.Logger.LogWarning(
                    $"[GameContext] Store {type.Name} 已注册，跳过");
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
                ArchServices.Logger.LogError(
                    $"[GameContext] 初始化后不允许注册 System: {system?.GetType().Name}");
                return;
            }

            if (system == null) throw new ArgumentNullException(nameof(system));

            var type = system.GetType();
            if (!_systemLookup.TryAdd(type, system))
            {
                ArchServices.Logger.LogWarning(
                    $"[GameContext] System {type.Name} 已注册，跳过");
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
        /// </summary>
        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
            {
                ArchServices.Logger.LogWarning("[GameContext] 已经初始化，跳过");
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
            Event.Publish(new GameReadyEvent());
        }

        public void Update(float deltaTime)
        {
            if (!_initialized) return;

            for (var i = 0; i < _systems.Count; i++)
            {
                try { _systems[i].OnUpdate(deltaTime); }
                catch (Exception ex) { ArchServices.Logger.LogException(ex); }
            }
        }

        public void LateUpdate(float deltaTime)
        {
            if (!_initialized) return;

            for (int i = 0; i < _systems.Count; i++)
            {
                try { _systems[i].OnLateUpdate(deltaTime); }
                catch (Exception ex) { ArchServices.Logger.LogException(ex); }
            }
        }

        #region IMutationContext / IGameContext

        public T Query<T>() where T : class, IStoreQueries
        {
            if (_storeQueryRegistry.TryGetValue(typeof(T), out var store))
                return store as T;

            ArchServices.Logger.LogError($"[GameContext] Query<{typeof(T).Name}> 未注册");
            return null;
        }

        public T GetStore<T>() where T : class, IStore
        {
            if (_stores.TryGetValue(typeof(T), out var store))
                return (T)store;

            ArchServices.Logger.LogError($"[GameContext] GetStore<{typeof(T).Name}> 未注册");
            return null;
        }

        public T GetSystem<T>() where T : class, IGameSystem
        {
            if (_systemLookup.TryGetValue(typeof(T), out var system))
                return (T)system;

            ArchServices.Logger.LogError($"[GameContext] GetSystem<{typeof(T).Name}> 未注册");
            return null;
        }

        public MutationResult Mutate<TMutation>(TMutation mutation) where TMutation : IMutation
        {
            try
            {
                return mutation.Execute(this);
            }
            catch (Exception ex)
            {
                ArchServices.Logger.LogException(ex);
                return MutationResult.Fail($"Mutation execution failed: {ex.Message}");
            }
        }

        #endregion

        public void Shutdown()
        {
            if (!_initialized) return;

            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { _systems[i].OnShutdown(); }
                catch (Exception ex) { ArchServices.Logger.LogException(ex); }
            }

            ShutdownDispose();
        }

        private void ShutdownDispose()
        {
            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { _systems[i].Dispose(); }
                catch (Exception ex) { ArchServices.Logger.LogException(ex); }
            }

            for (var i = _storeList.Count - 1; i >= 0; i--)
            {
                try { _storeList[i].Shutdown(); }
                catch (Exception ex) { ArchServices.Logger.LogException(ex); }
            }

            _stores.Clear();
            _storeList.Clear();
            _storeQueryRegistry.Clear();
            _systems.Clear();
            _systemLookup.Clear();
            _initialized = false;

            Event.Dispose();
            Deactivate();
        }
    }
}
