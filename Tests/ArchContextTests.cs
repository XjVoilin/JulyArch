using System;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JulyArch.Tests
{
    /// <summary>
    /// ArchContext 注册 / 初始化 / Shutdown 顺序 / 动态注册 / 事件清理 的单元测试。
    /// 纯逻辑验证框架骨架，不依赖场景与资源。
    /// </summary>
    [TestFixture]
    public class ArchContextTests
    {
        // 单调递增的逻辑时钟，用于断言生命周期先后顺序（跨 Store/System 共享）。
        private static int s_clock;
        private ArchContext _ctx;

        [SetUp]
        public void SetUp()
        {
            s_clock = 0;
            _ctx = new ArchContext();
        }

        [TearDown]
        public void TearDown()
        {
            _ctx?.Shutdown();
            _ctx = null;
        }

        #region 查询：未注册返回 null

        [Test]
        public void GetStore_NotRegistered_ReturnsNull()
        {
            LogAssert.Expect(LogType.Error, new Regex("GetStore.*未注册"));
            Assert.IsNull(_ctx.GetStore<TestStore>());
        }

        [Test]
        public void GetSystem_NotRegistered_ReturnsNull()
        {
            LogAssert.Expect(LogType.Error, new Regex("GetSystem.*未注册"));
            Assert.IsNull(_ctx.GetSystem<TestSystem>());
        }

        #endregion

        #region 注册 + 初始化顺序

        [Test]
        public void InitializeAsync_StoreLoadBeforeReady_SystemInitBeforeStart()
        {
            var store = new TestStore();
            var system = new TestSystem();
            _ctx.RegisterStore(store);
            _ctx.RegisterSystem(system);

            _ctx.InitializeAsync().GetAwaiter().GetResult();

            Assert.IsTrue(store.LoadCalled, "Store.Load 应在初始化时被调用");
            Assert.IsTrue(store.ReadyCalled, "Store.Ready 应在 Load 之后被调用");
            Assert.Less(store.LoadTick, store.ReadyTick, "Ready 必须晚于 Load");

            Assert.IsTrue(system.InitializeCalled, "System.Initialize 应在初始化时被调用");
            Assert.IsTrue(system.StartCalled, "System.Start 应在 Initialize 之后被调用");
            Assert.Less(system.InitTick, system.StartTick, "Start 必须晚于 Initialize");

            Assert.IsTrue(_ctx.Initialized);
        }

        [Test]
        public void InitializeAsync_CalledTwice_SecondIsNoop()
        {
            var store = new TestStore();
            _ctx.RegisterStore(store);
            _ctx.InitializeAsync().GetAwaiter().GetResult();
            store.LoadCalled = false;

            _ctx.InitializeAsync().GetAwaiter().GetResult();

            Assert.IsFalse(store.LoadCalled, "重复初始化不应再次 Load");
            Assert.IsTrue(_ctx.Initialized);
        }

        [Test]
        public void RunProcedure_ThrowsBeforeInitialize()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _ctx.RunProcedure(new NoopProcedure()).GetAwaiter().GetResult());
        }

        [Test]
        public void RunProcedure_NullArg_Throws()
        {
            _ctx.InitializeAsync().GetAwaiter().GetResult();
            Assert.Throws<ArgumentNullException>(() =>
                _ctx.RunProcedure(null).GetAwaiter().GetResult());
        }

        #endregion

        #region System 多接口键查询

        [Test]
        public void GetSystem_ByInterface_ReturnsImplementation()
        {
            var system = new TestSystem();
            _ctx.RegisterSystem(system);
            _ctx.InitializeAsync().GetAwaiter().GetResult();

            // ITickSystem 是 TestSystem 实现的非框架接口，应能查到同一个实例
            Assert.AreSame(system, _ctx.GetSystem<ITickSystem>());
            // 按基类查询同样命中
            Assert.AreSame(system, _ctx.GetSystem<TestSystemBase>());
        }

        [Test]
        public void RegisterSystem_DuplicateType_SkipsSecond()
        {
            var a = new TestSystem();
            var b = new TestSystem();
            _ctx.RegisterSystem(a);
            _ctx.RegisterSystem(b);

            Assert.AreSame(a, _ctx.GetSystem<TestSystem>(), "重复注册应保留先注册者");
        }

        #endregion

        #region 动态注册（已初始化后）

        [Test]
        public void RegisterSystem_AfterInitialize_InitializesImmediately()
        {
            _ctx.InitializeAsync().GetAwaiter().GetResult();

            var system = new TestSystem();
            _ctx.RegisterSystem(system);

            Assert.IsTrue(system.InitializeCalled, "初始化后注册应立即 Initialize");
            Assert.IsTrue(system.StartCalled, "初始化后注册应立即 Start");
        }

        [Test]
        public void UnregisterSystem_AfterInitialize_ShutsDown()
        {
            var system = new TestSystem();
            _ctx.RegisterSystem(system);
            _ctx.InitializeAsync().GetAwaiter().GetResult();

            _ctx.UnregisterSystem(system);

            Assert.IsTrue(system.ShutdownCalled, "已初始化后注销应触发 Shutdown");
            LogAssert.Expect(LogType.Error, new Regex("GetSystem.*未注册"));
            Assert.IsNull(_ctx.GetSystem<TestSystem>());
        }

        [Test]
        public void RegisterStore_AfterInitialize_LoadsImmediately()
        {
            _ctx.InitializeAsync().GetAwaiter().GetResult();

            var store = new TestStore();
            _ctx.RegisterStore(store);

            Assert.IsTrue(store.LoadCalled, "初始化后注册 Store 应立即 Load");
            Assert.IsTrue(store.ReadyCalled, "初始化后注册 Store 应立即 Ready");
        }

        #endregion

        #region Shutdown 顺序 + System 事件兜底注销

        [Test]
        public void Shutdown_SystemsBeforeStores()
        {
            var store = new TestStore();
            var system = new TestSystem();
            _ctx.RegisterStore(store);
            _ctx.RegisterSystem(system);
            _ctx.InitializeAsync().GetAwaiter().GetResult();

            _ctx.Shutdown();

            Assert.GreaterOrEqual(system.ShutdownTick, 0);
            Assert.GreaterOrEqual(store.ShutdownTick, 0);
            Assert.Less(system.ShutdownTick, store.ShutdownTick,
                "System 应在 Store 之前 Shutdown（逆序注销）");
        }

        [Test]
        public void Shutdown_SystemBaseAutoUnsubscribesEvents()
        {
            // 验证 SystemBase.Shutdown 兜底 UnsubscribeAll(this)：
            // System 订阅事件后不手写 Unsubscribe，注销后不应再收到事件。
            var system = new TestSubscribingSystem();
            _ctx.RegisterSystem(system);
            _ctx.InitializeAsync().GetAwaiter().GetResult();

            _ctx.Event.Publish(new TestEvent());
            Assert.AreEqual(1, system.ReceivedCount, "初始化后事件应路由到 System");

            _ctx.UnregisterSystem(system); // 触发 SystemBase.Shutdown 兜底注销

            _ctx.Event.Publish(new TestEvent());
            Assert.AreEqual(1, system.ReceivedCount,
                "Shutdown 兜底 UnsubscribeAll 后，System 不应再收到事件");
        }

        [Test]
        public void RunProcedure_AutoUnsubscribesAfterExecute()
        {
            // 验证 ArchContext.RunProcedure 执行结束（含正常完成）后兜底注销 Procedure 订阅。
            _ctx.InitializeAsync().GetAwaiter().GetResult();

            var proc = new SubscribingProcedure();
            int received = 0;
            _ctx.Event.Subscribe<TestEvent>(e => received++, this);

            _ctx.RunProcedure(proc).GetAwaiter().GetResult();
            Assert.AreEqual(1, proc.ReceivedCount, "Procedure 执行期间应收到事件");

            // Procedure 执行完毕后应已被兜底注销，再发布不再触发其 handler
            _ctx.Event.Publish(new TestEvent());
            Assert.AreEqual(1, proc.ReceivedCount, "Procedure 结束后不应再收到事件");
            Assert.AreEqual(2, received, "外部订阅者不受影响");
        }

        [Test]
        public void RunProcedure_AutoUnsubscribesEvenOnException()
        {
            _ctx.InitializeAsync().GetAwaiter().GetResult();

            var proc = new ThrowingProcedure();
            Assert.Throws<InvalidOperationException>(() =>
                _ctx.RunProcedure(proc).GetAwaiter().GetResult());

            // 异常路径也应兜底注销，否则订阅泄漏
            _ctx.Event.Publish(new TestEvent());
            Assert.AreEqual(0, proc.ReceivedCount, "异常退出后 Procedure 不应再收到事件");
        }

        [Test]
        public void Shutdown_ReleasesCurrent()
        {
            Assert.AreSame(_ctx, ArchContext.Current);
            _ctx.Shutdown();
            Assert.IsNull(ArchContext.Current, "Shutdown 后 Current 应被清空");
        }

        [Test]
        public void Shutdown_Twice_IsNoop()
        {
            _ctx.InitializeAsync().GetAwaiter().GetResult();
            _ctx.Shutdown();
            Assert.DoesNotThrow(() => _ctx.Shutdown(), "重复 Shutdown 不应抛异常");
        }

        #endregion

        #region View 注册与身份校验

        [Test]
        public void GetView_NotRegistered_ReturnsNull()
        {
            LogAssert.Expect(LogType.Error, new Regex("GetView.*未注册"));
            Assert.IsNull(_ctx.GetView<TestSingletonView>());
        }

        [Test]
        public void RegisterView_UnregisterView_IdentitySafe()
        {
            // view1: active GO → Awake 自动注册
            var go1 = new GameObject("v1");
            var view1 = go1.AddComponent<TestSingletonView>();

            // view2: inactive GO → Awake 不触发，避免 Duplicate ISingletonView 冲突
            var go2 = new GameObject("v2");
            go2.SetActive(false);
            var view2 = go2.AddComponent<TestSingletonView>();
            try
            {
                Assert.AreSame(view1, _ctx.GetView<TestSingletonView>());

                // 身份校验：用 view2 尝试注销，view1 应仍在（仅当登记的是自己才移除）
                _ctx.UnregisterView(view2);
                Assert.AreSame(view1, _ctx.GetView<TestSingletonView>(),
                    "仅当登记的确实是自己时才移除");

                _ctx.UnregisterView(view1);
                LogAssert.Expect(LogType.Error, new Regex("GetView.*未注册"));
                Assert.IsNull(_ctx.GetView<TestSingletonView>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go1);
                UnityEngine.Object.DestroyImmediate(go2);
            }
        }

        #endregion

        #region 异步 Store

        [Test]
        public void InitializeAsync_AsyncStore_LoadedViaLoadAsync()
        {
            var store = new TestAsyncStore();
            _ctx.RegisterStore(store);
            _ctx.InitializeAsync().GetAwaiter().GetResult();

            Assert.IsTrue(store.LoadAsyncCalled, "IAsyncLoadable Store 应通过 LoadAsync 加载");
            Assert.IsFalse(store.SyncLoadCalled, "异步 Store 不应走同步 Load");
            Assert.IsTrue(store.ReadyCalled);
        }

        #endregion

        #region Update 驱动

        [Test]
        public void Update_DrivesUpdatableSystems()
        {
            var system = new TestSystem();
            _ctx.RegisterSystem(system);
            _ctx.InitializeAsync().GetAwaiter().GetResult();

            _ctx.Update(0.1f);
            _ctx.Update(0.2f);

            Assert.AreEqual(2, system.UpdateCount, "IUpdatableSystem 应被 Update 驱动");
            Assert.AreEqual(0.3f, system.TotalDelta, 0.0001f, "deltaTime 应被透传累加");
        }

        [Test]
        public void Update_BeforeInitialize_IsNoop()
        {
            var system = new TestSystem();
            _ctx.RegisterSystem(system);

            Assert.DoesNotThrow(() => _ctx.Update(0.1f));
            Assert.AreEqual(0, system.UpdateCount, "未初始化时 Update 不应驱动 System");
        }

        #endregion

        #region Stubs

        private struct TestEvent { }

        private abstract class TestSystemBase : SystemBase { }

        private interface ITickSystem { void Tick(); }

        private sealed class TestSystem : TestSystemBase, IUpdatableSystem, ITickSystem
        {
            public bool InitializeCalled, StartCalled, ShutdownCalled;
            public int InitTick = -1, StartTick = -1, ShutdownTick = -1;
            public int UpdateCount;
            public float TotalDelta;

            public void Tick() { }

            protected override void OnInitialize()
            {
                InitializeCalled = true;
                InitTick = s_clock++;
            }

            protected override void OnStart()
            {
                StartCalled = true;
                StartTick = s_clock++;
            }

            protected override void OnShutdown()
            {
                ShutdownCalled = true;
                ShutdownTick = s_clock++;
            }

            public void OnUpdate(float deltaTime)
            {
                UpdateCount++;
                TotalDelta += deltaTime;
            }
        }

        private sealed class TestSubscribingSystem : SystemBase
        {
            public int ReceivedCount;

            protected override void OnInitialize()
                => Subscribe<TestEvent>(OnEvent);

            private void OnEvent(TestEvent e) => ReceivedCount++;
        }

        private sealed class TestStoreData { }

        private class TestStore : StoreBase<TestStoreData>
        {
            public bool LoadCalled, ReadyCalled, ShutdownCalled;
            public int LoadTick = -1, ReadyTick = -1, ShutdownTick = -1;

            protected override TestStoreData OnLoad()
            {
                LoadCalled = true;
                LoadTick = s_clock++;
                return new TestStoreData();
            }

            protected override void OnReady()
            {
                ReadyCalled = true;
                ReadyTick = s_clock++;
            }

            protected override void OnShutdown()
            {
                ShutdownCalled = true;
                ShutdownTick = s_clock++;
            }
        }

        private sealed class TestAsyncStore : StoreBase<TestStoreData>, IAsyncLoadable
        {
            public bool LoadAsyncCalled, SyncLoadCalled, ReadyCalled;

            public UniTask OnLoadAsync()
            {
                LoadAsyncCalled = true;
                Data = new TestStoreData();
                return UniTask.CompletedTask;
            }

            // 同步路径不应被触发；若被调用则标记，用于断言异步 Store 没误走同步 Load。
            protected override TestStoreData OnLoad()
            {
                SyncLoadCalled = true;
                return new TestStoreData();
            }

            protected override void OnReady() => ReadyCalled = true;
        }

        private sealed class NoopProcedure : ProcedureBase
        {
            protected override UniTask OnExecuteAsync(CancellationToken ct)
                => UniTask.CompletedTask;
        }

        private sealed class SubscribingProcedure : ProcedureBase
        {
            public int ReceivedCount;

            protected override UniTask OnExecuteAsync(CancellationToken ct)
            {
                Subscribe<TestEvent>(OnEvent);
                ArchContext.Current.Event.Publish(new TestEvent());
                return UniTask.CompletedTask;
            }

            private void OnEvent(TestEvent e) => ReceivedCount++;
        }

        private sealed class ThrowingProcedure : ProcedureBase
        {
            public int ReceivedCount;

            protected override UniTask OnExecuteAsync(CancellationToken ct)
            {
                Subscribe<TestEvent>(OnEvent);
                throw new InvalidOperationException("boom");
            }

            private void OnEvent(TestEvent e) => ReceivedCount++;
        }

        /// <summary>
        /// 测试用可定位 View。GameView 是 MonoBehaviour，须通过 AddComponent 创建。
        /// 这里只验证注册/注销/身份校验逻辑，不触发任何 Unity 生命周期回调。
        /// </summary>
        private sealed class TestSingletonView : GameView, ISingletonView { }

        #endregion
    }
}
