using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using JulyArch;
using JulyCore.Core;
using JulyCore.Core.Launch;
using JulyCore.Module.ABTest;
using JulyCore.Module.Activity;
using JulyCore.Module.Analytics;
using JulyCore.Module.Audio;
using JulyCore.Module.Config;
using JulyCore.Module.Guide;
using JulyCore.Module.Localization;
using JulyCore.Module.RedDot;
using JulyCore.Module.Save;
using JulyCore.Module.Task;
using JulyCore.Module.UI;
using JulyCore.Provider.ABTest;
using JulyCore.Provider.Activity;
using JulyCore.Provider.Analytics;
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
    public class RegisterAppModules : ILaunchStep
    {
        public string Name => "Register App Modules";

        public UniTask<bool> ExecuteAsync(LaunchContext ctx)
        {
            ctx.RegisterModule<LocalizationModule>();
            ctx.RegisterModule<UIModule>();
            ctx.RegisterModule<AudioModule>();
            ctx.RegisterModule<SaveModule>();
            ctx.RegisterModule<ConfigModule>();
            ctx.RegisterModule<AnalyticsModule>();
            ctx.RegisterModule<ABTestModule>();
            ctx.RegisterModule<TaskModule>();
            ctx.RegisterModule<RedDotModule>();
            ctx.RegisterModule<GuideModule>();
            ctx.RegisterModule<ActivityModule>();

            var resourceProvider = ctx.Registry.Resolve<IResourceProvider>();
            var poolProvider = ctx.Registry.Resolve<IPoolProvider>();
            var serializeProvider = ctx.Registry.Resolve<ISerializeProvider>();
            var encryptionProvider = ctx.Registry.Resolve<IEncryptionProvider>();

            Register<IAnalyticsProvider>(ctx, new NullAnalyticsProvider());
            Register<IConfigProvider>(ctx, new ConfigProvider(resourceProvider));
            Register<IUIProvider>(ctx, new UIProvider(resourceProvider, poolProvider));
            Register<IAudioProvider>(ctx, new UnityAudioProvider(resourceProvider, poolProvider));
            Register<ISaveProvider>(ctx, new LocalFileSaveProvider(serializeProvider, encryptionProvider));
            Register<ILocalizationProvider>(ctx, new LocalizationProvider(resourceProvider, serializeProvider));
            Register<IABTestProvider>(ctx, new ABTestProvider());
            Register<ITaskProvider>(ctx, new TaskProvider());
            Register<IRedDotProvider>(ctx, new RedDotProvider());
            Register<IGuideProvider>(ctx, new GuideProvider());

            var saveProvider = ctx.Registry.Resolve<ISaveProvider>();
            Register<IActivityProvider>(ctx, new SavedActivityProvider(saveProvider));

            var gameContext = GameContext.Create();
            var registrar = FindRegistrar();
            if (registrar != null)
                registrar.Register(gameContext);

            ctx.Registry.Register(gameContext);
            if (registrar != null)
                ctx.Registry.Register(registrar);

            return UniTask.FromResult(true);
        }

        private static void Register<T>(LaunchContext ctx, T instance) where T : IProvider
        {
            ctx.Registry.Register(instance);
            ctx.TrackProvider(instance);
        }

        private static IHotUpdateRegistrar FindRegistrar()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = Array.FindAll(e.Types, t => t != null);
                }

                foreach (var type in types)
                {
                    if (typeof(IHotUpdateRegistrar).IsAssignableFrom(type)
                        && !type.IsAbstract && !type.IsInterface)
                        return (IHotUpdateRegistrar)Activator.CreateInstance(type);
                }
            }

            return null;
        }
    }
}
