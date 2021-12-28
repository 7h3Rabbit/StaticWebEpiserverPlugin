﻿using StaticWebEpiserverPlugin.Configuration;
using StaticWebEpiserverPlugin.Interfaces;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace StaticWebEpiserverPlugin.Services
{
    public class HtmlDependencyService : ITextResourceDependencyService
    {
        public string EnsureDependencies(string content, IStaticWebService staticWebService, SiteConfigurationElement configuration, bool? useTemporaryAttribute, Dictionary<string, string> currentPageResourcePairs = null, ConcurrentDictionary<string, string> replaceResourcePairs = null)
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
            // <(script|link|img).*(href|src)="(?<resource>[^"]+)
            EnsureScriptAndLinkAndImgAndATagSupport(staticWebService, configuration, ref content, ref currentPageResourcePairs, ref replaceResourcePairs, useTemporaryAttribute);

            // make sure we have all source resources for current page
            // <(source).*(srcset)="(?<resource>[^"]+)"
            EnsureSourceTagSupport(staticWebService, configuration, ref content, ref currentPageResourcePairs, ref replaceResourcePairs, useTemporaryAttribute);

            // TODO: make sure we have all meta resources for current page
            // Below matches ALL meta content that is a URL
            // <(meta).*(content)="(?<resource>(http:\/\/|https:\/\/|\/)[^"]+)"
            // Below matches ONLY known properties
            // <(meta).*(property|name)="(twitter:image|og:image)".*(content)="(?<resource>[http:\/\/|https:\/\/|\/][^"]+)"

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

        protected void EnsureSourceTagSupport(IStaticWebService staticWebService, SiteConfigurationElement configuration, ref string html, ref Dictionary<string, string> currentPageResourcePairs, ref ConcurrentDictionary<string, string> replaceResourcePairs, bool? useTemporaryAttribute)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            var sourceSetMatches = Regex.Matches(html, "<(source).*(srcset)=[\"|'](?<imageCandidates>[^\"|']+)[\"|']");
            foreach (Match sourceSetMatch in sourceSetMatches)
            {
                var imageCandidatesGroup = sourceSetMatch.Groups["imageCandidates"];
                if (imageCandidatesGroup.Success)
                {
                    var imageCandidates = imageCandidatesGroup.Value;
                    // Take into account that we can have many image candidates, for example: logo-768.png 768w, logo-768-1.5x.png 1.5x
                    var resourceMatches = Regex.Matches(imageCandidates, "(?<resource>[^, ]+)( [0-9.]+[w|x][,]{0,1})*");
                    foreach (Match match in resourceMatches)
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
                            var newResourceUrl = staticWebService.EnsureResource(configuration, resourceUrl, currentPageResourcePairs, replaceResourcePairs, useTemporaryAttribute);
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

        protected void EnsureScriptAndLinkAndImgAndATagSupport(IStaticWebService staticWebService, SiteConfigurationElement configuration, ref string html, ref Dictionary<string, string> currentPageResourcePairs, ref ConcurrentDictionary<string, string> replaceResourcePairs, bool? useTemporaryAttribute)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            var matches = Regex.Matches(html, "<(script|link|img|a).*(href|src)=[\"|'](?<resource>[^\"|']+)");
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

                    var newResourceUrl = staticWebService.EnsureResource(configuration, resourceUrl, currentPageResourcePairs, replaceResourcePairs, useTemporaryAttribute);
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
