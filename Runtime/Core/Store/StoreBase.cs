using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Core;
using JulyCore.Data.Save;

namespace JulyArch
{
    /// <summary>
    /// Store 统一基类
    /// 数据通过 GF.Save 完成持久化，子类通过 SaveKey 指定存储路径
    /// 
    /// 【生命周期钩子（子类可覆盖）】
    /// OnInitialize()       → 初始化时（不依赖存档数据）
    /// OnDataLoaded()       → 本 Store 数据加载完成后
    /// OnReady()            → 所有 Store 数据加载完成后（可安全访问其他 Store）
    /// OnShutdown()         → 应用退出时
    /// </summary>
    /// <typeparam name="TData">存档数据类型（必须实现 ISaveData）</typeparam>
    public abstract class StoreBase<TData> : IStore where TData : class, ISaveData, new()
    {
        private GameContext _context;

        /// <summary>数据实例（仅在数据加载后可用）</summary>
        protected TData Data { get; private set; }

        /// <summary>存储键（子类实现，完全由子类控制路径格式）</summary>
        protected abstract string SaveKey { get; }

        #region IStore 显式实现（由 GameContext 调用，不暴露给业务代码）

        void IStore.Initialize(GameContext context)
        {
            _context = context;
            OnInitialize();
        }

        async UniTask IStore.LoadAsync()
        {
            Data = await GF.Save.LoadAndRegisterAsync<TData>(SaveKey) ?? new TData();
            OnDataLoaded();
        }

        void IStore.OnReady()
        {
            OnReady();
        }

        void IStore.Shutdown()
        {
            OnShutdown();
            Data = default;
        }

        #endregion

        #region 受保护的工具方法

        /// <summary>
        /// 获取其他 Store 的只读查询接口（仅在 OnReady 及之后使用）
        /// </summary>
        protected T Query<T>() where T : class, IStoreQueries
            => _context.Query<T>();

        /// <summary>
        /// 标记数据为脏（通知存档系统数据已变更）
        /// </summary>
        protected void MarkDirty()
        {
            GF.Save.MarkDirty(SaveKey);
        }

        /// <summary>
        /// 发布领域事件
        /// </summary>
        protected void PublishEvent<T>(T eventData) where T : IEvent
        {
            GF.Event.Publish(eventData);
        }

        #endregion

        #region 子类可覆盖的生命周期钩子

        /// <summary>初始化（不依赖存档数据）</summary>
        protected virtual void OnInitialize() { }

        /// <summary>本 Store 数据加载完成</summary>
        protected virtual void OnDataLoaded() { }

        /// <summary>所有 Store 数据加载完成（可安全访问其他 Store 的查询接口）</summary>
        protected virtual void OnReady() { }

        /// <summary>应用退出时的最终清理</summary>
        protected virtual void OnShutdown() { }

        #endregion
    }
}
