using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyEvents;
using UnityEngine;

namespace JulyArch
{
    public sealed class ArchContext
    {
        private readonly Dictionary<Type, StoreBase> _stores = new();
        private readonly List<StoreBase> _storeList = new();

        private readonly List<SystemBase> _systems = new();
        private readonly List<IUpdatableSystem> _updateSystems = new();
        private readonly Dictionary<Type, SystemBase> _systemLookup = new();

        // View 是场景绑定的瞬态角色，随场景生灭动态注册/注销，不受 _initialized 限制。
        private readonly Dictionary<Type, GameView> _views = new();

        private bool _initialized;

        public bool Initialized => _initialized;

        public IEventBus Event { get; }

        public static ArchContext Current { get; private set; }

        public ArchContext()
        {
            Event = new EventBus();
            if (Current == null) Current = this;
        }

        public void RegisterStore(StoreBase store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));

            var type = store.GetType();
            if (!_stores.TryAdd(type, store))
            {
                Debug.LogWarning(
                    $"[ArchContext] Store {type.Name} 已注册，跳过");
                return;
            }

            store.SetContext(this);
            _storeList.Add(store);

            if (_initialized)
            {
                if (store.IsAsyncLoadable)
                    Debug.LogWarning($"[ArchContext] Store {type.Name} 异步 Store 在初始化后注册，仅同步 Load");
                store.Load();
                store.Ready();
            }
        }

        public void UnregisterStore(StoreBase store)
        {
            if (store == null) return;
            var type = store.GetType();
            if (!_stores.TryGetValue(type, out var existing) || existing != store) return;

            if (_initialized)
            {
                try { store.Shutdown(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            _stores.Remove(type);
            _storeList.Remove(store);
        }

        public void RegisterSystem(SystemBase system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));

            var concreteType = system.GetType();
            if (!_systemLookup.TryAdd(concreteType, system))
            {
                Debug.LogWarning(
                    $"[ArchContext] System {concreteType.Name} 已注册，跳过");
                return;
            }

            var baseType = concreteType.BaseType;
            while (baseType != null && baseType != typeof(SystemBase))
            {
                _systemLookup.TryAdd(baseType, system);
                baseType = baseType.BaseType;
            }

            foreach (var iface in concreteType.GetInterfaces())
            {
                if (typeof(IArchNode).IsAssignableFrom(iface)) continue;
                _systemLookup.TryAdd(iface, system);
            }

            system.SetContext(this);
            _systems.Add(system);

            if (system is IUpdatableSystem updatable) _updateSystems.Add(updatable);

            if (_initialized)
            {
                system.Initialize();
                system.Start();
            }
        }

        public void UnregisterSystem(SystemBase system)
        {
            if (system == null) return;
            var concreteType = system.GetType();
            if (!_systemLookup.TryGetValue(concreteType, out var existing) || existing != system) return;

            if (_initialized)
            {
                try { system.Shutdown(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            var keysToRemove = new List<Type>();
            foreach (var kvp in _systemLookup)
                if (kvp.Value == system) keysToRemove.Add(kvp.Key);
            foreach (var key in keysToRemove)
                _systemLookup.Remove(key);

            _systems.Remove(system);
            if (system is IUpdatableSystem updatable) _updateSystems.Remove(updatable);
        }

        /// <summary>
        /// 注册可定位 View。由 GameView 基类在 Awake 中对实现 ISingletonView 的实例自动调用。
        /// 同类型出现第二个实例 = 误标 ISingletonView 的编程错误：开发期抛异常快速失败，发布期保留先注册者并忽略。
        /// </summary>
        public void RegisterView(GameView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));

            var type = view.GetType();
            if (_views.TryGetValue(type, out var existing) && existing != view)
            {
                Debug.LogError(
                    $"[ArchContext] {type.Name} 实现了 ISingletonView 但出现多个实例，" +
                    "多实例对象不应实现 ISingletonView。");
#if UNITY_EDITOR || JULYGF_DEBUG
                throw new InvalidOperationException(
                    $"[ArchContext] Duplicate ISingletonView: {type.Name}");
#else
                return; // 发布期：保留先注册者，忽略后来者，不覆盖、不崩
#endif
            }

            _views[type] = view;
        }

        /// <summary>
        /// 注销可定位 View。由 GameView 基类在 OnDestroy 中调用。
        /// 身份校验：仅当登记的确实是自己时才移除，避免被误标副本注销掉正主。
        /// </summary>
        public void UnregisterView(GameView view)
        {
            if (view == null) return;

            var type = view.GetType();
            if (_views.TryGetValue(type, out var existing) && existing == view)
                _views.Remove(type);
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

        #region Public API

        public T GetStore<T>() where T : StoreBase
        {
            if (_stores.TryGetValue(typeof(T), out var store))
                return (T)store;

            Debug.LogError($"[ArchContext] GetStore<{typeof(T).Name}> 未注册");
            return null;
        }

        public T GetSystem<T>() where T : class
        {
            if (_systemLookup.TryGetValue(typeof(T), out var system))
                return (T)(object)system;

            Debug.LogError($"[ArchContext] GetSystem<{typeof(T).Name}> 未注册");
            return null;
        }

        public T GetView<T>() where T : GameView
        {
            if (_views.TryGetValue(typeof(T), out var view))
                return (T)view;

            Debug.LogError($"[ArchContext] GetView<{typeof(T).Name}> 未注册（View 需实现 ISingletonView 且其 GameObject 在场景加载时处于 active 以触发 Awake 自注册）");
            return null;
        }

        #endregion

        public async UniTask RunProcedure(ProcedureBase procedure, CancellationToken ct = default)
        {
            if (procedure == null) throw new ArgumentNullException(nameof(procedure));
            if (!_initialized) throw new InvalidOperationException(
                $"[ArchContext] 初始化未完成，无法运行 Procedure: {procedure.GetType().Name}");

            procedure.SetContext(this);
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
            _views.Clear();
            _initialized = false;

            Event.Dispose();

            if (Current == this) Current = null;
        }
    }
}
