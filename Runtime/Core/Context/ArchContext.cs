using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyEvents;
using UnityEngine;

namespace JulyArch
{
    public sealed class ArchContext : IArchContext
    {
        private readonly Dictionary<Type, StoreBase> _stores = new();
        private readonly List<StoreBase> _storeList = new();

        private readonly List<GameSystemBase> _systems = new();
        private readonly List<IUpdatableSystem> _updateSystems = new();
        private readonly Dictionary<Type, GameSystemBase> _systemLookup = new();

        private bool _initialized;

        public IEventBus Event { get; }

        public ArchContext()
        {
            Event = new EventBus();
        }

        public void RegisterStore(StoreBase store)
        {
            if (_initialized)
            {
                Debug.LogError(
                    $"[ArchContext] 初始化后不允许注册 Store: {store?.GetType().Name}");
                return;
            }

            if (store == null) throw new ArgumentNullException(nameof(store));

            var type = store.GetType();
            if (!_stores.TryAdd(type, store))
            {
                Debug.LogWarning(
                    $"[ArchContext] Store {type.Name} 已注册，跳过");
                return;
            }

            store.SetArchitecture(this);
            _storeList.Add(store);
        }

        public void RegisterSystem(GameSystemBase system)
        {
            if (_initialized)
            {
                Debug.LogError(
                    $"[ArchContext] 初始化后不允许注册 System: {system?.GetType().Name}");
                return;
            }

            if (system == null) throw new ArgumentNullException(nameof(system));

            var type = system.GetType();
            if (!_systemLookup.TryAdd(type, system))
            {
                Debug.LogWarning(
                    $"[ArchContext] System {type.Name} 已注册，跳过");
                return;
            }

            system.SetArchitecture(this);
            _systems.Add(system);

            if (system is IUpdatableSystem updatable) _updateSystems.Add(updatable);
        }

        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
            {
                Debug.LogWarning("[ArchContext] 已经初始化，跳过");
                return;
            }

            var asyncTasks = new List<UniTask>();

            foreach (var store in _storeList)
            {
                ct.ThrowIfCancellationRequested();

                if (store.IsAsyncLoadable)
                    asyncTasks.Add(store.LoadAsync());
                else
                    store.Load();
            }

            if (asyncTasks.Count > 0)
                await UniTask.WhenAll(asyncTasks);

            foreach (var store in _storeList)
                store.Ready();

            foreach (var system in _systems)
                system.Initialize();

            foreach (var system in _systems)
                system.Start();

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

        #region IArchContext

        public T GetStore<T>() where T : StoreBase
        {
            if (_stores.TryGetValue(typeof(T), out var store))
                return (T)store;

            Debug.LogError($"[ArchContext] GetStore<{typeof(T).Name}> 未注册");
            return null;
        }

        public T GetSystem<T>() where T : GameSystemBase
        {
            if (_systemLookup.TryGetValue(typeof(T), out var system))
                return (T)system;

            Debug.LogError($"[ArchContext] GetSystem<{typeof(T).Name}> 未注册");
            return null;
        }

        #endregion

        public async UniTask RunProcedure(ProcedureBase procedure, CancellationToken ct = default)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            if (!_initialized) throw new InvalidOperationException(
                $"[ArchContext] 初始化未完成，无法运行 Procedure: {procedure.GetType().Name}");

            procedure.SetArchitecture(this);
            await procedure.Execute(ct);
        }

        public void Shutdown()
        {
            if (!_initialized) return;

            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                try { _systems[i].Shutdown(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            for (var i = _storeList.Count - 1; i >= 0; i--)
            {
                try { _storeList[i].Shutdown(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            _stores.Clear();
            _storeList.Clear();
            _systems.Clear();
            _updateSystems.Clear();
            _systemLookup.Clear();
            _initialized = false;

            Event.Dispose();
        }
    }
}
