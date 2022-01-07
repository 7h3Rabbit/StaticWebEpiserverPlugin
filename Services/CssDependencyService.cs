using StaticWebEpiserverPlugin.Configuration;
using StaticWebEpiserverPlugin.Interfaces;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StaticWebEpiserverPlugin.Services
{
    public class CssDependencyService : ITextResourceDependencyService
    {
        static readonly Regex REGEX_FIND_URL_REFERENCE = new Regex("url\\([\"|']{0,1}(?<resource>[^[\\)\"|']+)", RegexOptions.Compiled);

        public string EnsureDependencies(string referencingUrl, string content, IStaticWebService staticWebService, SiteConfigurationElement configuration, bool? useTemporaryAttribute, bool ignoreHtmlDependencies, Dictionary<string, string> currentPageResourcePairs = null, ConcurrentDictionary<string, string> replaceResourcePairs = null, int callDepth = 0)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return content;
            }

            if (currentPageResourcePairs == null)
            {
                currentPageResourcePairs = new Dictionary<string, string>();
            }

            if (replaceResourcePairs == null)
            {
                replaceResourcePairs = new ConcurrentDictionary<string, string>();
            }

            content = EnsureUrlReferenceSupport(referencingUrl, content, staticWebService, configuration, useTemporaryAttribute, ignoreHtmlDependencies, currentPageResourcePairs, replaceResourcePairs);
            return content;
        }

        private static string EnsureUrlReferenceSupport(string referencingUrl, string content, IStaticWebService staticWebService, SiteConfigurationElement configuration, bool? useTemporaryAttribute, bool ignoreHtmlDependencies, Dictionary<string, string> currentPageResourcePairs, ConcurrentDictionary<string, string> replaceResourcePairs, int callDepth = 0)
        {
            // Download and ensure files referenced are downloaded also
            var matches = REGEX_FIND_URL_REFERENCE.Matches(content);
            foreach (Match match in matches)
            {
                var group = match.Groups["resource"];
                if (group.Success)
                {
                    var orginalUrl = group.Value;
                    var resourceUrl = orginalUrl;
                    var changedDir = false;
                    var directory = referencingUrl.Substring(0, referencingUrl.LastIndexOf('/'));
                    while (resourceUrl.StartsWith("../"))
                    {
                        changedDir = true;
                        resourceUrl = resourceUrl.Remove(0, 3);
                        directory = directory.Substring(0, directory.LastIndexOf('/'));
                    }

                    if (changedDir)
                    {
                        resourceUrl = directory.Replace(@"\", "/") + "/" + resourceUrl;
                    }

                    string newResourceUrl = staticWebService.EnsureResource(configuration, resourceUrl, currentPageResourcePairs, replaceResourcePairs, useTemporaryAttribute, ignoreHtmlDependencies, callDepth);
                    if (!string.IsNullOrEmpty(newResourceUrl))
                    {
                        content = content.Replace(orginalUrl, newResourceUrl);
                        if (!replaceResourcePairs.ContainsKey(resourceUrl))
                        {
                            replaceResourcePairs.TryAdd(resourceUrl, newResourceUrl);
                        }
                        if (!currentPageResourcePairs.ContainsKey(resourceUrl))
                        {
                            currentPageResourcePairs.Add(resourceUrl, newResourceUrl);
                        }
                    }
                    else
                    {
                        content = content.Replace(orginalUrl, "/" + configuration.ResourceFolder.Replace(@"\", "/") + resourceUrl);
                        if (!replaceResourcePairs.ContainsKey(resourceUrl))
                        {
                            replaceResourcePairs.TryAdd(resourceUrl, null);
                        }
                        if (!currentPageResourcePairs.ContainsKey(resourceUrl))
                        {
                            currentPageResourcePairs.Add(resourceUrl, null);
                        }
                    }
                }
            }

            return content;
        }
    }
}
