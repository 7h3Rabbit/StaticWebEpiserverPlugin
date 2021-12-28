using StaticWebEpiserverPlugin.Configuration;
using StaticWebEpiserverPlugin.Services;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace StaticWebEpiserverPlugin.Interfaces
{
    public interface ITextResourceDependencyService
    {
        string EnsureDependencies(
            string content,
            IStaticWebService staticWebService,
            SiteConfigurationElement configuration,
            bool? useTemporaryAttribute,
            Dictionary<string, string> currentPageResourcePairs = null,
            ConcurrentDictionary<string, string> replaceResourcePairs = null
            );
    }
}
