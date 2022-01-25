using StaticWebEpiserverPlugin.Configuration;
using StaticWebEpiserverPlugin.Interfaces;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace StaticWebEpiserverPlugin.Services
{
    public class SvgDependencyService : ITextResourceDependencyService
    {
        static readonly Regex REGEX_FIND_USE_URL_REFERENCE = new Regex("<(use).*(xlink:href)=[\"'](?<resource>[^\"#']+)", RegexOptions.Compiled);

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

            // make sure we have all resources from script, link and img tags for current page
            // <(use).*(xlink:href)="(?<resource>[^"]+)
            EnsureUseTagSupport(staticWebService, configuration, ref content, ref currentPageResourcePairs, ref replaceResourcePairs, useTemporaryAttribute, ignoreHtmlDependencies, callDepth);

            var sbHtml = new StringBuilder(content);
            foreach (KeyValuePair<string, string> pair in replaceResourcePairs)
            {
                // We have a value if we want to replace orginal url with a new one
                if (pair.Value != null)
                {
                    sbHtml = sbHtml.Replace(pair.Key, pair.Value);
                }
            }

            return sbHtml.ToString();
        }

        protected void EnsureUseTagSupport(IStaticWebService staticWebService, SiteConfigurationElement configuration, ref string html, ref Dictionary<string, string> currentPageResourcePairs, ref ConcurrentDictionary<string, string> replaceResourcePairs, bool? useTemporaryAttribute, bool ignoreHtmlDependencies, int callDepth = 0)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            var matches = REGEX_FIND_USE_URL_REFERENCE.Matches(html);
            foreach (Match match in matches)
            {
                var group = match.Groups["resource"];
                if (group.Success)
                {
                    var resourceUrl = group.Value;
                    if (replaceResourcePairs.ContainsKey(resourceUrl))
                    {
                        /**
                         * If we have already downloaded resource, we don't need to download it again.
                         * Not only usefull for pages repeating same resource but also in our Scheduled job where we try to generate all pages.
                         **/

                        if (!currentPageResourcePairs.ContainsKey(resourceUrl))
                        {
                            // current page has no info regarding this resource, add it
                            currentPageResourcePairs.Add(resourceUrl, replaceResourcePairs[resourceUrl]);
                        }
                        continue;
                    }

                    var newResourceUrl = staticWebService.EnsureResource(configuration, resourceUrl, currentPageResourcePairs, replaceResourcePairs, useTemporaryAttribute, ignoreHtmlDependencies, callDepth);
                    if (!replaceResourcePairs.ContainsKey(resourceUrl))
                    {
                        replaceResourcePairs.TryAdd(resourceUrl, newResourceUrl);
                    }
                    if (!currentPageResourcePairs.ContainsKey(resourceUrl))
                    {
                        currentPageResourcePairs.Add(resourceUrl, newResourceUrl);
                    }
                }
            }
        }

    }
}
