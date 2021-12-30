using StaticWebEpiserverPlugin.Configuration;
using StaticWebEpiserverPlugin.Interfaces;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StaticWebEpiserverPlugin.Services
{
    public class CssDependencyService : ITextResourceDependencyService
    {
        public string EnsureDependencies(string content, IStaticWebService staticWebService, SiteConfigurationElement configuration, bool? useTemporaryAttribute, bool ignoreHtmlDependencies, Dictionary<string, string> currentPageResourcePairs = null, ConcurrentDictionary<string, string> replaceResourcePairs = null, int callDepth = 0)
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

            content = EnsureUrlReferenceSupport(content, staticWebService, configuration, useTemporaryAttribute, ignoreHtmlDependencies, currentPageResourcePairs, replaceResourcePairs);
            return content;
        }

        private static string EnsureUrlReferenceSupport(string content, IStaticWebService staticWebService, SiteConfigurationElement configuration, bool? useTemporaryAttribute, bool ignoreHtmlDependencies, Dictionary<string, string> currentPageResourcePairs, ConcurrentDictionary<string, string> replaceResourcePairs, int callDepth = 0)
        {
            // Download and ensure files referenced are downloaded also
            var matches = Regex.Matches(content, "url\\([\"|']{0,1}(?<resource>[^[\\)\"|']+)");
            foreach (Match match in matches)
            {
                var group = match.Groups["resource"];
                if (group.Success)
                {
                    var orginalUrl = group.Value;
                    var resourceUrl = orginalUrl;
                    var changedDir = false;
                    var directory = "/";
                    // TODO: Temporary removed subfolder support for css resources, should it be fixed or?
                    //var directory = url.Substring(0, url.LastIndexOf('/'));
                    //while (resourceUrl.StartsWith("../"))
                    //{
                    //    changedDir = true;
                    //    resourceUrl = resourceUrl.Remove(0, 3);
                    //    directory = directory.Substring(0, directory.LastIndexOf('/'));
                    //}

                    if (changedDir)
                    {
                        resourceUrl = directory.Replace(@"\", "/") + "/" + resourceUrl;
                    }

                    string newResourceUrl = staticWebService.EnsureResource(configuration, resourceUrl, currentPageResourcePairs, replaceResourcePairs, useTemporaryAttribute, ignoreHtmlDependencies, callDepth);
                    // TODO: Temporary removed subfolder support for css resources, should it be fixed or?
                    //string newResourceUrl = staticWebService.EnsureResource(rootUrl, rootPath, resourcePath, resourceUrl, currentPageResourcePairs, replaceResourcePairs, useTemporaryAttribute);
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
