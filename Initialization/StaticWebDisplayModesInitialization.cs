using System.Linq;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using StaticWebEpiserverPlugin.Channels;
using EPiServer.Web;

namespace StaticWebEpiserverPlugin.Initialization
{
    [ModuleDependency(typeof(EPiServer.Web.InitializationModule))]
    public class StaticWebDisplayModesInitialization : IInitializableModule
    {
        public void Initialize(InitializationEngine context)
        {
            var staticWebChannelDisplayMode = new DefaultDisplayMode(StaticWebChannel.Name)
            {
                ContextCondition = IsStaticWebDisplayModeActive
            };
            DisplayModeProvider.Instance.Modes.Insert(0, staticWebChannelDisplayMode);
        }

        private static bool IsStaticWebDisplayModeActive(HttpContextBase httpContext)
        {
            var userAgent = httpContext.GetOverriddenBrowser().Browser;
            if (userAgent != null && userAgent.Contains("StaticWebPlugin"))
            {
                return true;
            }
            var displayChannelService = ServiceLocator.Current.GetInstance<IDisplayChannelService>();
            return displayChannelService.GetActiveChannels(httpContext).Any(x => x.ChannelName == StaticWebChannel.Name);
        }

        public void Uninitialize(InitializationEngine context)
        {
        }

        public void Preload(string[] parameters)
        {
        }
    }
}