using JulyCore;
using JulyCore.Provider.Scene.Events;
using UnityEngine.SceneManagement;

namespace JulyArch
{
    /// <summary>
    /// 场景切换协调器
    /// 
    /// 职责：协调场景切换时"所有场景都需要"的统一清理行为
    /// 
    /// 设计原则：
    /// - 只处理对所有场景切换都相同的行为（关 UI、停 SFX 等）
    /// - 因场景而异的行为（播 BGM、开 HUD）由各场景的 View 负责
    /// - UIModule / AudioModule / SceneModule 都是底层框架模块，彼此独立
    /// - 本类处于上层，统一协调下层模块，符合依赖方向
    /// </summary>
    public static class SceneTransitionHandler
    {
        /// <summary>
        /// 初始化（在 GameEntry.OnGameInitialized 中调用，早于任何场景加载）
        /// </summary>
        public static void Initialize()
        {
            GF.Event.Subscribe<SceneLoadStartEvent>(OnSceneLoadStart, typeof(SceneTransitionHandler));
            GF.Log("[SceneTransitionHandler] 已初始化");
        }

        /// <summary>
        /// 关闭
        /// </summary>
        public static void Shutdown()
        {
            GF.Event.UnsubscribeAll(typeof(SceneTransitionHandler));
        }

        private static void OnSceneLoadStart(SceneLoadStartEvent evt)
        {
            if (evt.LoadMode != LoadSceneMode.Single) return;

            GF.Log($"[SceneTransitionHandler] 场景切换 → {evt.SceneName}，执行统一清理");

            GF.UI.CloseAll();
            GF.Audio.StopAllSFX();
        }
    }
}
