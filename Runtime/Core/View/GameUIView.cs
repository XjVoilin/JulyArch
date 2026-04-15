using JulyCore.Provider.UI;

namespace JulyArch
{
    /// <summary>
    /// UI 面板 View 基类
    /// 所有需要接入架构的 UI 面板应继承此类
    /// </summary>
    public abstract class GameUIView : UIBase, ICanQuery, ICanGetSystem, ICanMutate
    {
        #region 快捷方法（委托给 ArchExtensions）

        protected T Query<T>() where T : class, IStoreQueries
            => ArchExtensions.Query<T>(this);

        protected T GetSystem<T>() where T : class, IGameSystem
            => ArchExtensions.GetSystem<T>(this);

        protected MutationResult Mutate<TMutation>(TMutation mutation) where TMutation : IMutation
            => ArchExtensions.Mutate(this, mutation);

        #endregion
    }
}