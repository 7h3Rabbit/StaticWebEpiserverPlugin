using EPiServer.Web;

namespace StaticWebEpiserverPlugin.Channels
{
    public class StaticWebChannel : DisplayChannel
    {
        public const string Name = "staticweb";

        public override string DisplayName => "StaticWeb";

        public override string ChannelName
        {
            get
            {
                return Name;
            }
        }

        public override bool IsActive(HttpContextBase context)
        {
            var userAgent = context.Request.UserAgent;
            return userAgent != null && userAgent.Contains("StaticWebPlugin");
        }
    }
}