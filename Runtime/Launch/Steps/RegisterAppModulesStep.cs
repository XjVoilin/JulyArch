using Cysharp.Threading.Tasks;
using JulyArch;
using JulyCore.Core;
using JulyCore.Core.Launch;
using JulyCore.Module.ABTest;
using JulyCore.Module.Activity;
using JulyCore.Module.Audio;
using JulyCore.Module.Config;
using JulyCore.Module.Guide;
using JulyCore.Module.Localization;
using JulyCore.Module.RedDot;
using JulyCore.Module.Resource;
using JulyCore.Module.Save;
using JulyCore.Module.Scene;
using JulyCore.Module.Task;
using JulyCore.Module.UI;
using JulyCore.Provider.ABTest;
using JulyCore.Provider.Activity;
using JulyCore.Provider.Audio;
using JulyCore.Provider.Config;
using JulyCore.Provider.Data;
using JulyCore.Provider.Encryption;
using JulyCore.Provider.Guide;
using JulyCore.Provider.Localization;
using JulyCore.Provider.Pool;
using JulyCore.Provider.RedDot;
using JulyCore.Provider.Resource;
using JulyCore.Provider.Save;
using JulyCore.Provider.Task;
using JulyCore.Provider.UI;

namespace JulyArch.Launch.Steps
{
    public class RegisterAppModulesStep : ILaunchStep
    {
        public string Name => "Register App Modules";

        public UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
            ctx.RegisterModule<ResourceModule>();
            ctx.RegisterModule<SceneModule>();
            ctx.RegisterModule<LocalizationModule>();
            ctx.RegisterModule<UIModule>();
            ctx.RegisterModule<AudioModule>();
            ctx.RegisterModule<SaveModule>();
            ctx.RegisterModule<ConfigModule>();
            ctx.RegisterModule<ABTestModule>();
            ctx.RegisterModule<TaskModule>();
            ctx.RegisterModule<RedDotModule>();
            ctx.RegisterModule<GuideModule>();
            ctx.RegisterModule<ActivityModule>();

            var resourceProvider = ctx.Registry.Resolve<IResourceProvider>();
            var poolProvider = ctx.Registry.Resolve<IPoolProvider>();
            var serializeProvider = ctx.Registry.Resolve<ISerializeProvider>();
            var encryptionProvider = ctx.Registry.Resolve<IEncryptionProvider>();

            ctx.RegisterProvider<IConfigProvider>(new ConfigProvider(resourceProvider));
            ctx.RegisterProvider<IUIProvider>(new UIProvider(resourceProvider, poolProvider));
            ctx.RegisterProvider<IAudioProvider>(new UnityAudioProvider(resourceProvider, poolProvider));
            ctx.RegisterProvider<ISaveProvider>(new LocalFileSaveProvider(serializeProvider, encryptionProvider));
            ctx.RegisterProvider<ILocalizationProvider>(new LocalizationProvider(resourceProvider, serializeProvider));
            ctx.RegisterProvider<IABTestProvider>(new ABTestProvider());
            ctx.RegisterProvider<ITaskProvider>(new TaskProvider());
            ctx.RegisterProvider<IRedDotProvider>(new RedDotProvider());
            ctx.RegisterProvider<IGuideProvider>(new GuideProvider());

            var saveProvider = ctx.Registry.Resolve<ISaveProvider>();
            ctx.RegisterProvider<IActivityProvider>(new SavedActivityProvider(saveProvider));

            var gameContext = GameContext.Create();
            var registrar = FindRegistrar();
            if (registrar != null)
                registrar.Register(gameContext);

            ctx.Registry.Register(gameContext);
            if (registrar != null)
                ctx.Registry.Register(registrar);

            return UniTask.FromResult(true);
        }

        private static IHotUpdateRegistrar FindRegistrar()
        {
            const string TypeFullName = "GooseMarket.HotUpdateRegistrar";
            try
            {
                var assembly = System.Reflection.Assembly.Load("Assembly-CSharp");
                var type = assembly.GetType(TypeFullName);
                if (type != null)
                    return (IHotUpdateRegistrar)System.Activator.CreateInstance(type);
            }
            catch (System.Exception e)
            {
                JLogger.LogWarning($"[RegisterApp] HotUpdateRegistrar not found: {e.Message}");
            }
            return null;
        }
    }
}
