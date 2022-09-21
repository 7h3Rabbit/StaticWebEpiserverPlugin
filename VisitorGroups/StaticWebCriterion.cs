using EPiServer.Personalization.VisitorGroups;
using StaticWebEpiserverPlugin.Channels;
using System.Security.Principal;

namespace StaticWebEpiserverPlugin.VisitorGroups
{
    [VisitorGroupCriterion(
       Category = "Technical",
       DisplayName = "StaticWeb User",
       Description = "Checks if currently visited by StaticWebEpiServerPlugin for page caching")]
    public class StaticWebCriterion : CriterionBase<StaticWebCriterionSettings>
    {
        public override bool IsMatch(IPrincipal principal, HttpContextBase httpContext)
        {
            return new StaticWebChannel().IsActive(httpContext);
        }
    }
}
