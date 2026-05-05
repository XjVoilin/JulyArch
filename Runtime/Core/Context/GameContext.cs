using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace JulyArch
{
    /// <summary>
    /// 游戏上下文 — 上层架构的统一协调中心
    /// 管理 Store 注册与生命周期、System 注册与帧驱动、Mutation 分派
    /// 支持多实例共存，每个 Store/System 通过 SetArchitecture 绑定到所属 Context
    /// </summary>
    public sealed class GameContext : IMutationContext
    {
        private readonly Dictionary<Type, IStore> _stores = new();
        private readonly Dictionary<Type, IStore> _storeQueryRegistry = new();
        private readonly List<IStore> _storeList = new();

        private readonly List<IGameSystem> _systems = new();
        private readonly List<IUpdatableSystem> _updateSystems = new();
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
                Debug.LogError(
                    $"[GameContext] 初始化后不允许注册 Store: {store?.GetType().Name}");
                return;
            }

            if (store == null) throw new ArgumentNullException(nameof(store));

            var type = store.GetType();
            if (!_stores.TryAdd(type, store))
            {
                Debug.LogWarning(
                    $"[GameContext] Store {type.Name} 已注册，跳过");
                return;
            }

            store.SetArchitecture(this);
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
                Debug.LogError(
                    $"[GameContext] 初始化后不允许注册 System: {system?.GetType().Name}");
                return;
            }

            if (system == null) throw new ArgumentNullException(nameof(system));

            var type = system.GetType();
            if (!_systemLookup.TryAdd(type, system))
            {
                Debug.LogWarning(
                    $"[GameContext] System {type.Name} 已注册，跳过");
                return;
            }

            system.SetArchitecture(this);
            _systems.Add(system);

            if (system is IUpdatableSystem updatable) _updateSystems.Add(updatable);

            foreach (var iface in type.GetInterfaces())
            {
                if (iface != typeof(IGameSystem) && iface != typeof(IDisposable)
                    && typeof(IGameSystem).IsAssignableFrom(iface))
                    _systemLookup.TryAdd(iface, system);
            }
        }

        /// <summary>
        /// 初始化：Store.Load/LoadAsync → OnReady → System.OnInit → OnStart → GameReadyEvent
        /// </summary>
        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
            {
                Debug.LogWarning("[GameContext] 已经初始化，跳过");
                return;
            }

            var asyncTasks = new List<UniTask>();

            foreach (var store in _storeList)
            {
                ct.ThrowIfCancellationRequested();

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

            for (var i = 0; i < _updateSystems.Count; i++)
            {
                try { _updateSystems[i].OnUpdate(deltaTime); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        #region IMutationContext / IGameContext

        public T Query<T>() where T : class, IStoreQueries
        {
            if (_storeQueryRegistry.TryGetValue(typeof(T), out var store))
                return store as T;

            Debug.LogError($"[GameContext] Query<{typeof(T).Name}> 未注册");
            return null;
        }

        public bool TryQuery<T>(out T result) where T : class, IStoreQueries
        {
            if (_storeQueryRegistry.TryGetValue(typeof(T), out var store))
            {
                result = store as T;
                return result != null;
            }

            result = null;
            return false;
        }

        T IMutationContext.GetStore<T>() => GetStoreInternal<T>();

        internal T GetStoreInternal<T>() where T : class, IStore
        {
            if (_stores.TryGetValue(typeof(T), out var store))
                return (T)store;

            Debug.LogError($"[GameContext] GetStore<{typeof(T).Name}> 未注册");
            return null;
        }

        public T GetSystem<T>() where T : class, IGameSystem
        {
            if (_systemLookup.TryGetValue(typeof(T), out var system))
                return (T)system;

            Debug.LogError($"[GameContext] GetSystem<{typeof(T).Name}> 未注册");
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
                Debug.LogException(ex);
                return MutationResult.Fail($"Mutation execution failed: {ex.Message}");
            }
        }

        public MutationResult Mutate<TStore>(Action<TStore> mutation) where TStore : class, IStore
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            try
            {
                var store = GetStoreInternal<TStore>();
                mutation(store);
                return MutationResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return MutationResult.Fail($"Lambda mutation on {typeof(TStore).Name} failed: {ex.Message}");
            }
        }

        #endregion

        public async UniTask RunProcedure(IProcedure procedure, CancellationToken ct = default)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            if (!_initialized) throw new InvalidOperationException(
                $"[GameContext] 初始化未完成，无法运行 Procedure: {procedure.GetType().Name}");

            procedure.SetArchitecture(this);
            await procedure.ExecuteAsync(ct);
        }

        public void Shutdown()
        {
            if (!_initialized) return;

            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { _systems[i].OnShutdown(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            ShutdownDispose();
        }

        private void ShutdownDispose()
        {
            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { _systems[i].Dispose(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            for (var i = _storeList.Count - 1; i >= 0; i--)
            {
                try { _storeList[i].Shutdown(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            _stores.Clear();
            _storeList.Clear();
            _storeQueryRegistry.Clear();
            _systems.Clear();
            _updateSystems.Clear();
            _systemLookup.Clear();
            _initialized = false;

            Event.Dispose();
        }
    }
}
