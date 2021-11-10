using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction.Internal;
using EPiServer.Filters;
using EPiServer.ServiceLocation;
using EPiServer.Web.Routing;
using StaticWebEpiserverPlugin.Configuration;
using StaticWebEpiserverPlugin.Events;
using StaticWebEpiserverPlugin.Interfaces;
using StaticWebEpiserverPlugin.Models;
using StaticWebEpiserverPlugin.Routing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace StaticWebEpiserverPlugin.Services
{
    public class StaticWebService : IStaticWebService
    {
        public event EventHandler<StaticWebGeneratePageEventArgs> BeforeGeneratePage;
        public event EventHandler<StaticWebGeneratePageEventArgs> BeforeGetPageContent;
        public event EventHandler<StaticWebGeneratePageEventArgs> AfterGetPageContent;
        public event EventHandler<StaticWebGeneratePageEventArgs> BeforeTryToFixLinkUrls;
        public event EventHandler<StaticWebGeneratePageEventArgs> BeforeEnsurePageResources;
        public event EventHandler<StaticWebGeneratePageEventArgs> AfterEnsurePageResources;
        public event EventHandler<StaticWebGeneratePageEventArgs> BeforeGeneratePageWrite;
        public event EventHandler<StaticWebGeneratePageEventArgs> AfterGeneratePageWrite;
        public event EventHandler<StaticWebGeneratePageEventArgs> AfterGeneratePage;

        public StaticWebService()
        {
        }

        public void RemoveGeneratedPage(SiteConfigurationElement configuration, ContentReference contentLink, CultureInfo language)
        {
            bool removeSubFolders = false;
            RemoveGeneratedPage(configuration, contentLink, language, removeSubFolders);
        }

        public void RemoveGeneratedPage(SiteConfigurationElement configuration, ContentReference contentLink, CultureInfo language, bool removeSubFolders)
        {
            string orginalUrl = GetPageUrl(contentLink, language);
            if (orginalUrl == null)
            {
                return;
            }

            RemoveGeneratedPage(configuration, orginalUrl, removeSubFolders);
        }


        public void RemoveGeneratedPage(SiteConfigurationElement configuration, string orginalUrl, bool removeSubFolders = false)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            string relativePath = GetPageRelativePath(orginalUrl);

            if (!Directory.Exists(configuration.OutputPath + relativePath))
            {
                // Directory doesn't exist, nothing to remove :)
                return;
            }

            if (!File.Exists(configuration.OutputPath + relativePath + "index.html"))
            {
                // File doesn't exist, nothing to remove :)
                return;
            }

            File.Delete(configuration.OutputPath + relativePath + "index.html");

            var hasFiles = false;
            var hasDirectories = false;
            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(configuration.OutputPath + relativePath);
                hasFiles = directoryInfo.GetFiles().Length > 0;
                hasDirectories = directoryInfo.GetDirectories().Length > 0;
            }
            catch (Exception)
            {
                // something was wrong, but this is just extra cleaning so ignore it
                return;
            }

            // Directory is empty, remove empty directory
            if (removeSubFolders || (!hasFiles && !hasDirectories))
            {
                Directory.Delete(configuration.OutputPath + relativePath, removeSubFolders);
            }
        }

        public void CreateRedirectPages(SiteConfigurationElement configuration, string oldUrl, string newUrl)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            if (!Directory.Exists(configuration.OutputPath + oldUrl))
            {
                // Directory doesn't exist, nothing to remove :)
                return;
            }

            if (!File.Exists(configuration.OutputPath + oldUrl + "index.html"))
            {
                // File doesn't exist, nothing to remove :)
                return;
            }

            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(configuration.OutputPath + oldUrl);
                var fileInfos = directoryInfo.GetFiles("index.html", SearchOption.AllDirectories);

                foreach (FileInfo info in fileInfos)
                {
                    var tempPath = info.FullName;
                    // c:\websites\A\
                    tempPath = "/" + tempPath.Replace(configuration.OutputPath, "").Replace("\\", "/");
                    // /old/
                    tempPath = tempPath.Replace(oldUrl, "");

                    // index.html
                    tempPath = tempPath.Replace("index.html", "");

                    // /new/
                    tempPath = newUrl + tempPath;

                    // Create redirect html file
                    var redirectHtml = $"<!doctype html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"refresh\" content=\"0; URL='{tempPath}'\" /></head><body><a href=\"{tempPath}\">{tempPath}</a></body></html>";
                    File.WriteAllText(info.FullName, redirectHtml);
                }
            }
            catch (Exception)
            {
                // something was wrong, but this is just extra cleaning so ignore it
                return;
            }
        }
        public void GeneratePage(SiteConfigurationElement configuration, PageData page, CultureInfo lang, bool? useTemporaryAttribute, ConcurrentDictionary<string, string> generatedResources = null)
        {
            string pageUrl, simpleAddress;
            GetUrlsForPage(page, lang, out pageUrl, out simpleAddress);
            GeneratePage(configuration, pageUrl, useTemporaryAttribute, simpleAddress, generatedResources);
        }

        public void GetUrlsForPage(PageData page, CultureInfo lang, out string pageUrl, out string simpleAddress)
        {
            simpleAddress = string.IsNullOrEmpty(page.ExternalURL) ? null : "/" + page.ExternalURL;
            pageUrl = GetPageUrl(page.ContentLink.ToReferenceWithoutVersion(), lang);
        }

        public void GeneratePage(SiteConfigurationElement configuration, string pageUrl, bool? useTemporaryAttribute, string simpleAddress = null, ConcurrentDictionary<string, string> generatedResources = null)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            //string orginalUrl = GetPageUrl(contentLink, language);
            if (pageUrl == null)
                return;

            string relativePath = GetPageRelativePath(pageUrl);
            string relativeSimplePath = GetPageRelativePath(simpleAddress);
            var hasSimpleAddress = !string.IsNullOrEmpty(simpleAddress);
            var fullPageUrl = configuration.Url + pageUrl;
            var fullSimpeAddress = configuration.Url + simpleAddress;

            var generatePageEvent = new StaticWebGeneratePageEventArgs(fullPageUrl, simpleAddress);
            if (generatedResources != null)
            {
                generatePageEvent.Resources = generatedResources;
            }
            else
            {
                generatePageEvent.Resources = new ConcurrentDictionary<string, string>();
            }

            BeforeGeneratePage?.Invoke(this, generatePageEvent);
            // someone wants to cancel this generation of this event.
            if (generatePageEvent.CancelAction)
            {
                return;
            }

            BeforeGetPageContent?.Invoke(this, generatePageEvent);

            string html = null;

            if (configuration.UseRouting)
            {
                StaticWebRouting.Remove(relativePath);
                if (hasSimpleAddress)
                {
                    StaticWebRouting.Remove(relativeSimplePath);
                }
            }

            string pageExtension = ".html";
            // someone wants to cancel this generation of this event.
            if (!generatePageEvent.CancelAction)
            {
                var resourceInfo = DownloadResource(configuration.Url, pageUrl, null, false);
                if (resourceInfo != null)
                {
                    if (!string.IsNullOrEmpty(resourceInfo.Extension))
                    {
                        pageExtension = resourceInfo.Extension;
                    }

                    if (resourceInfo.Data != null)
                    {
                        html = Encoding.UTF8.GetString(resourceInfo.Data);
                    }
                }
            }
            generatePageEvent.Content = html;

            // We don't care about a cancel action here
            AfterGetPageContent?.Invoke(this, generatePageEvent);

            if (generatePageEvent.Content == null)
                return;

            // reset cancel action and reason
            generatePageEvent.CancelAction = false;
            generatePageEvent.CancelReason = null;

            BeforeTryToFixLinkUrls?.Invoke(this, generatePageEvent);
            // someone wants to cancel/ignore ensuring resources.
            if (!generatePageEvent.CancelAction)
            {
                generatePageEvent.Content = TryToFixLinkUrls(generatePageEvent.Content);
            }

            // reset cancel action and reason
            generatePageEvent.CancelAction = false;
            generatePageEvent.CancelReason = null;

            BeforeEnsurePageResources?.Invoke(this, generatePageEvent);
            // someone wants to cancel/ignore ensuring resources.
            if (!generatePageEvent.CancelAction)
            {
                generatePageEvent.Content = EnsurePageResources(configuration, generatePageEvent.Content, useTemporaryAttribute, generatePageEvent.CurrentResources, generatePageEvent.Resources);
            }

            // reset cancel action and reason
            generatePageEvent.CancelAction = false;
            generatePageEvent.CancelReason = null;

            // We don't care about a cancel action here
            AfterEnsurePageResources?.Invoke(this, generatePageEvent);

            string filePath = configuration.OutputPath + relativePath + "index" + pageExtension;
            var filePaths = new List<string>
            {
                filePath
            };
            if (hasSimpleAddress)
            {
                string filePath2 = configuration.OutputPath + relativeSimplePath + "index" + pageExtension;
                filePaths.Add(filePath2);
            }
            generatePageEvent.FilePaths = filePaths;

            // reset cancel action and reason
            generatePageEvent.CancelAction = false;
            generatePageEvent.CancelReason = null;

            BeforeGeneratePageWrite?.Invoke(this, generatePageEvent);
            // someone wants to cancel this page write.
            if (!generatePageEvent.CancelAction)
            {
                // only write and route content if it is not empty
                if (!string.IsNullOrEmpty(generatePageEvent.Content))
                {
                    foreach (string outputFilePath in generatePageEvent.FilePaths)
                    {
                        var directory = Path.GetDirectoryName(outputFilePath);
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        WriteFile(outputFilePath, Encoding.UTF8.GetBytes(generatePageEvent.Content), useTemporaryAttribute);

                        if (configuration.UseRouting)
                        {
                            StaticWebRouting.Add(relativePath);
                            if (hasSimpleAddress)
                            {
                                StaticWebRouting.Add(relativeSimplePath);
                            }
                        }
                    }
                }
            }

            AfterGeneratePageWrite?.Invoke(this, generatePageEvent);
            // someone wants to cancel AFTER page write.
            if (generatePageEvent.CancelAction)
                return;

            AfterGeneratePage?.Invoke(this, generatePageEvent);
        }


        protected static string GetPageRelativePath(string orginalUrl)
        {
            if (string.IsNullOrEmpty(orginalUrl))
            {
                return null;
            }

            var relativePath = orginalUrl.Replace("/", @"\");
            if (!relativePath.StartsWith(@"\"))
            {
                relativePath = @"\" + relativePath;
            }
            if (!relativePath.EndsWith(@"\"))
            {
                relativePath = relativePath + @"\";
            }

            return relativePath;
        }

        public string GetPageUrl(ContentReference contentLink, CultureInfo language = null)
        {
            var urlResolver = ServiceLocator.Current.GetInstance<UrlResolver>();
            string orginalUrl = null;
            if (language != null)
            {
                orginalUrl = urlResolver.GetUrl(contentLink, language.Name);
            }
            else
            {
                orginalUrl = urlResolver.GetUrl(contentLink);
            }

            if (orginalUrl == null)
                return null;

            if (orginalUrl.StartsWith("//"))
            {
                return null;
            }

            // NOTE: If publishing event comes from scheduled publishing (orginalUrl includes protocol, domain and port number)
            if (!orginalUrl.StartsWith("/"))
            {
                orginalUrl = new Uri(orginalUrl).AbsolutePath;
            }

            return orginalUrl;
        }

        public void GeneratePagesDependingOnBlock(SiteConfigurationElement configuration, ContentReference contentLink, bool? useTemporaryAttribute)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            var repository = ServiceLocator.Current.GetInstance<IContentRepository>();
            var pages = GetPageReferencesToContent(repository, contentLink);

            foreach (var page in pages)
            {
                // This page or block type should be ignored
                if (page is IStaticWebIgnoreGenerate)
                {
                    continue;
                }

                // This page type has a conditional for when we should generate it
                if (page is IStaticWebIgnoreGenerateDynamically generateDynamically)
                {
                    if (!generateDynamically.ShouldGenerate())
                    {
                        if (generateDynamically.ShouldDeleteGenerated())
                        {
                            RemoveGeneratedPage(configuration, contentLink, page.Language);
                        }

                        // This page should not be generated at this time, ignore it.
                        continue;
                    }
                }

                var languages = page.ExistingLanguages;
                foreach (var lang in languages)
                {
                    GeneratePage(configuration, page, lang, useTemporaryAttribute);
                }
            }
        }

        public Dictionary<string, string> GetPageLanguageUrls(IContentRepository contentRepository, ContentReference contentReference)
        {
            Dictionary<string, string> languageUrls = new Dictionary<string, string>();
            PageData page;
            if (contentRepository.TryGet<PageData>(contentReference, out page))
            {
                var languages = page.ExistingLanguages;
                foreach (var lang in languages)
                {
                    var oldLangUrl = GetPageUrl(contentReference, lang);
                    languageUrls.Add(lang.Name, oldLangUrl);
                }
            }
            return languageUrls;
        }

        public void RemovePageInAllLanguages(IContentRepository contentRepository, SiteConfigurationElement configuration, ContentReference contentReference)
        {
            var page = contentRepository.Get<PageData>(contentReference);
            var languages = page.ExistingLanguages;
            foreach (var lang in languages)
            {
                var langPage = contentRepository.Get<PageData>(page.ContentLink, lang);

                var langContentLink = langPage.ContentLink;

                var removeSubFolders = true;
                RemoveGeneratedPage(configuration, langContentLink, lang, removeSubFolders);
            }
        }

        public void GeneratePageInAllLanguages(IContentRepository contentRepository, SiteConfigurationElement configuration, PageData page)
        {
            // This page type should be ignored
            if (page is IStaticWebIgnoreGenerate)
            {
                return;
            }

            var languages = page.ExistingLanguages;
            foreach (var lang in languages)
            {
                var langPage = contentRepository.Get<PageData>(page.ContentLink.ToReferenceWithoutVersion(), lang);

                var langContentLink = langPage.ContentLink.ToReferenceWithoutVersion();

                // This page type has a conditional for when we should generate it
                if (langPage is IStaticWebIgnoreGenerateDynamically generateDynamically)
                {
                    if (!generateDynamically.ShouldGenerate())
                    {
                        if (generateDynamically.ShouldDeleteGenerated())
                        {
                            RemoveGeneratedPage(configuration, langContentLink, lang);
                        }

                        // This page should not be generated at this time, ignore it.
                        continue;
                    }
                }

                bool? useTemporaryAttribute = configuration.UseTemporaryAttribute.HasValue ? false : configuration.UseTemporaryAttribute;
                GeneratePage(configuration, langPage, lang, useTemporaryAttribute);
            }
        }

        public void GeneratePage(ContentReference contentReference, IContent content, bool? useTemporaryAttribute)
        {
            // This page or block type should be ignored
            if (content is IStaticWebIgnoreGenerate)
            {
                return;
            }

            var configuration = StaticWebConfiguration.CurrentSite;
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            if (content is PageData)
            {
                var page = content as PageData;

                // This page type has a conditional for when we should generate it
                if (content is IStaticWebIgnoreGenerateDynamically generateDynamically)
                {
                    if (!generateDynamically.ShouldGenerate())
                    {
                        if (generateDynamically.ShouldDeleteGenerated())
                        {
                            RemoveGeneratedPage(configuration, contentReference, page.Language);
                        }

                        // This page should not be generated at this time, ignore it.
                        return;
                    }
                }

                GeneratePage(configuration, page, page.Language, useTemporaryAttribute, null);
            }
            else if (content is BlockData)
            {
                var block = content as BlockData;
                GeneratePagesDependingOnBlock(configuration, contentReference, useTemporaryAttribute);
            }
        }

        public void GeneratePagesDependingOnContent(ContentReference contentReference, bool? useTemporaryAttribute)
        {
            var contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();
            var references = contentRepository.GetReferencesToContent(contentReference, false).GroupBy(x => x.OwnerID + "-" + x.OwnerLanguage);

            foreach (var group in references)
            {
                var item = group.FirstOrDefault();
                if (item == null)
                    continue;

                if (item.ReferenceType != (int)EPiServer.DataAbstraction.ReferenceType.PageLinkReference)
                    continue;

                if (item.OwnerID == null)
                    continue;

                var referencedContent = contentRepository.Get<PageData>(item.OwnerID.ToReferenceWithoutVersion(), item.OwnerLanguage);
                GeneratePage(item.OwnerID, referencedContent, useTemporaryAttribute);
            }
        }

        protected static string TryToFixLinkUrls(string html)
        {
            var urlResolver = ServiceLocator.Current.GetInstance<UrlResolver>();

            var matches = Regex.Matches(html, "href=[\"|'](?<resource>\\/link\\/[0-9a-f]{32}.aspx)[\"|']");
            foreach (Match match in matches)
            {
                var group = match.Groups["resource"];
                if (group.Success)
                {
                    var resourceUrl = group.Value;
                    var correctUrl = urlResolver.GetUrl(resourceUrl);

                    if (correctUrl.StartsWith("//"))
                    {
                        continue;
                    }

                    // NOTE: If publishing event comes from scheduled publishing (correctUrl includes protocol, domain and port number)
                    if (!correctUrl.StartsWith("/"))
                    {
                        correctUrl = new Uri(correctUrl).AbsolutePath;
                    }

                    html = html.Replace(resourceUrl, correctUrl);
                }
            }
            return html;
        }

        protected static string EnsurePageResources(SiteConfigurationElement configuration, string html, bool? useTemporaryAttribute, Dictionary<string, string> currentPageResourcePairs = null, ConcurrentDictionary<string, string> replaceResourcePairs = null)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return html;
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
            EnsureScriptAndLinkAndImgAndATagSupport(configuration, ref html, ref currentPageResourcePairs, ref replaceResourcePairs, useTemporaryAttribute);

            // make sure we have all source resources for current page
            // <(source).*(srcset)="(?<resource>[^"]+)"
            EnsureSourceTagSupport(configuration, ref html, ref currentPageResourcePairs, ref replaceResourcePairs, useTemporaryAttribute);

            // TODO: make sure we have all meta resources for current page
            // Below matches ALL meta content that is a URL
            // <(meta).*(content)="(?<resource>(http:\/\/|https:\/\/|\/)[^"]+)"
            // Below matches ONLY known properties
            // <(meta).*(property|name)="(twitter:image|og:image)".*(content)="(?<resource>[http:\/\/|https:\/\/|\/][^"]+)"


            var sbHtml = new StringBuilder(html);
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

        protected static void EnsureSourceTagSupport(SiteConfigurationElement configuration, ref string html, ref Dictionary<string, string> currentPageResourcePairs, ref ConcurrentDictionary<string, string> replaceResourcePairs, bool? useTemporaryAttribute)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            var matches = Regex.Matches(html, "<(source).*(srcset)=[\"|'](?<resource>[^\"|']+)[\"|']");
            foreach (Match match in matches)
            {
                var group = match.Groups["resource"];
                if (group.Success)
                {
                    var value = group.Value;
                    // Take into account that we can have many resource rules, for example: logo-768.png 768w, logo-768-1.5x.png 1.5x
                    var resourceRules = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string resourceRule in resourceRules)
                    {
                        // Take into account that we can have rules here, not just resource url, for example: logo-768.png 768w
                        var resourceInfo = resourceRule.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (resourceInfo.Length < 1)
                            continue;

                        var resourceUrl = resourceInfo[0];

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

                        var newResourceUrl = EnsureResource(configuration.Url, configuration.OutputPath, configuration.ResourceFolder, resourceUrl, currentPageResourcePairs, replaceResourcePairs, useTemporaryAttribute, configuration.UseHash, configuration.UseResourceUrl);
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

        protected static void EnsureScriptAndLinkAndImgAndATagSupport(SiteConfigurationElement configuration, ref string html, ref Dictionary<string, string> currentPageResourcePairs, ref ConcurrentDictionary<string, string> replaceResourcePairs, bool? useTemporaryAttribute)
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

                    var newResourceUrl = EnsureResource(configuration.Url, configuration.OutputPath, configuration.ResourceFolder, resourceUrl, currentPageResourcePairs, replaceResourcePairs, configuration.UseHash, configuration.UseResourceUrl);
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

        protected static string EnsureResource(string rootUrl, string rootPath, string resourcePath, string resourceUrl, Dictionary<string, string> currentPageResourcePairs, ConcurrentDictionary<string, string> replaceResourcePairs, bool? useTemporaryAttribute, bool useHash = true, bool useResourceUrl = false)
        {
            bool preventDownload = IsDownloadPrevented(resourceUrl, useHash);
            if (preventDownload)
            {
                // don't download resource as it is of a known type we don't want to download
                return null;
            }

            var extension = GetExtension(resourceUrl);

            // Support special edge case width axd resources (only supported when using hash naming)
            if (extension == ".axd")
            {
                extension = null;
            }

            var resourceInfo = DownloadResource(rootUrl, resourceUrl, extension, useHash);
            if (resourceInfo == null)
            {
                // error occured, ignore
                return null;
            }

            if (string.IsNullOrEmpty(resourceInfo.Extension))
            {
                // Extension of resource is not allowed, ignore
                return null;
            }

            switch (resourceInfo.Extension)
            {
                case ".css":
                    // Do more work for this type of resource
                    var content = Encoding.UTF8.GetString(resourceInfo.Data);
                    var newCssResourceUrl = EnsureCssResources(rootUrl, rootPath, resourcePath, resourceUrl, content, currentPageResourcePairs, replaceResourcePairs, useTemporaryAttribute, useHash, useResourceUrl);
                    return newCssResourceUrl;
                default:
                    // For approved file extensions that we don't need to do any changes on
                    string newResourceUrl = GetNewResourceUrl(resourcePath, resourceUrl, resourceInfo.Extension, resourceInfo.Data, useHash, useResourceUrl);

                    var hasContent = resourceInfo.Data != null && resourceInfo.Data.LongLength > 0;
                    if (newResourceUrl != null && hasContent)
                    {
                        var filepath = rootPath + newResourceUrl.Replace("/", "\\");
                        if (!useResourceUrl)
                        {
                            // We are using hash ONLY as file name so no need to replace file that already exists
                            if (File.Exists(filepath))
                                return newResourceUrl;
                        }

                        WriteFile(filepath, resourceInfo.Data, useTemporaryAttribute);
                        return newResourceUrl;
                    }
                    else
                    {
                        // Resource is not valid, return null
                        return null;
                    }
            }
        }

        protected static bool IsDownloadPrevented(string resourceUrl, bool useHash)
        {
            if (string.IsNullOrEmpty(resourceUrl))
            {
                return true;
            }

            if (resourceUrl.StartsWith("//"))
            {
                return true;
            }

            if (!resourceUrl.StartsWith("/"))
            {
                return true;
            }

            var extension = GetExtension(resourceUrl);
            switch (extension)
            {
                case ".axd": // Assembly Web Resouces (Will validate against content-type)
                             // We only allow .axd when using hash (else we will overwrite same file with different content)
                    return !useHash;
                case "":
                    // missing extension, download and look at contenttype
                    // this happens for script and css bundles for examples
                    return false;
                default:
                    var allowedExtensionConfig = StaticWebConfiguration.AllowedResourceTypes.FirstOrDefault(type => type.FileExtension == extension);
                    if (allowedExtensionConfig == null)
                    {
                        // unkown extension, ignore
                        return true;
                    }
                    // extension is allowed, download it
                    return false;
            }
        }

        protected static StaticWebDownloadResult DownloadResource(string rootUrl, string resourceUrl, string extension, bool useHash)
        {
            StaticWebDownloadResult result = new StaticWebDownloadResult();

            // We have no extension to go on, look at content-type
            WebClient referencableClient = new WebClient();
            referencableClient.Headers.Set(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.149 Safari/537.36 StaticWebPlugin/0.1");
            referencableClient.Encoding = Encoding.UTF8;

            byte[] data = null;
            try
            {
                data = referencableClient.DownloadData(rootUrl + resourceUrl);
                result.Data = data;
            }
            catch (WebException)
            {
                return null;
            }

            var contentTypeResponse = referencableClient.ResponseHeaders[HttpResponseHeader.ContentType];
            if (string.IsNullOrEmpty(contentTypeResponse))
                return null;

            string contentType = contentTypeResponse.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(contentType))
                return null;

            result.ContentType = contentType;

            if (string.IsNullOrEmpty(extension))
            {
                var tmpExtension = GetExtensionForKnownContentType(result.ContentType);
                if (!IsDownloadPrevented(resourceUrl + tmpExtension, useHash))
                {
                    result.Extension = tmpExtension;
                }
            }
            else
            {
                result.Extension = extension;
            }

            return result;
        }

        protected static string GetExtension(string url)
        {
            // remove '?' and '#' info from url
            url = EnsureUrlWithoutParams(url);

            var extension = Path.GetExtension(url).ToLower();
            return extension;
        }

        protected static string GetExtensionForKnownContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return null;
            }

            contentType = contentType.Trim().ToLower();

            var typeConfig = StaticWebConfiguration.AllowedResourceTypes.FirstOrDefault(type => type.MimeType == contentType);
            if (typeConfig == null)
                return null;

            return typeConfig.FileExtension;
        }

        protected static string GetNewResourceUrl(string resourcePath, string resourceUrl, string extension, byte[] data, bool useHash = true, bool useResourceUrl = false)
        {
            if (useResourceUrl)
            {
                if (string.IsNullOrEmpty(resourceUrl))
                {
                    resourceUrl = "";
                }

                /* Ugly hack: remove as soon as possible
                 * make sure to not get a folder name with a file name, for example: /globalassets/alloy-plan/alloyplan.png/size700.png
                 * alloyplan.png here would cause error (IF we also have the orginal image) as we can't have a file and a folder with the same name.
                 */
                resourceUrl = resourceUrl.Replace(extension, "");

                resourceUrl = EnsureUrlWithoutParams(resourceUrl);
            }

            // If we have disabled usage of resourceUrl, force usage of hash
            string hash = "";
            if (!useResourceUrl || useHash)
            {
                // We can't calculate hash on null, abort
                if (data == null)
                    return null;

                using (var sha256 = new System.Security.Cryptography.SHA256Managed())
                {
                    var hashData = sha256.ComputeHash(data);
                    hash = Convert.ToBase64String(hashData, Base64FormattingOptions.None).Replace("/", "-").Replace("=", "_").Replace("+", ".");
                }
            }

            if (useResourceUrl && useHash)
            {
                return ("/" + resourcePath.Replace(@"\", "/") + "/" + EnsureFileSystemValid(resourceUrl + "-" + hash + extension)).Replace("//", "/");
            }
            else if (useResourceUrl)
            {
                return ("/" + resourcePath.Replace(@"\", "/") + "/" + EnsureFileSystemValid(resourceUrl + extension)).Replace("//", "/");
            }
            else
            {
                return ("/" + resourcePath.Replace(@"\", "/") + "/" + EnsureFileSystemValid(hash + extension)).Replace("//", "/");
            }
        }

        protected static string EnsureCssResources(string rootUrl, string rootPath, string resourcePath, string url, string content, Dictionary<string, string> currentPageResourcePairs, ConcurrentDictionary<string, string> replaceResourcePairs, bool? useTemporaryAttribute, bool useHash = true, bool useResourceUrl = false)
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
                    var directory = url.Substring(0, url.LastIndexOf('/'));
                    var changedDir = false;
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

                    string newResourceUrl = EnsureResource(rootUrl, rootPath, resourcePath, resourceUrl, currentPageResourcePairs, replaceResourcePairs, useTemporaryAttribute, useHash, useResourceUrl);
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
                        content = content.Replace(orginalUrl, "/" + resourcePath.Replace(@"\", "/") + resourceUrl);
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

            var data = Encoding.UTF8.GetBytes(content);
            string newCssResourceUrl = GetNewResourceUrl(resourcePath, url, ".css", data, useHash, useResourceUrl);

            var hasContent = data != null && data.LongLength > 0;
            if (newCssResourceUrl != null && hasContent)
            {
                var filepath = rootPath + newCssResourceUrl.Replace("/", "\\");
                if (!useResourceUrl)
                {
                    // We are using hash ONLY as file name so no need to replace file that already exists
                    if (File.Exists(filepath))
                        return newCssResourceUrl;
                }

                WriteFile(filepath, data, useTemporaryAttribute);
                return newCssResourceUrl;
            }
            else
            {
                // Resource is not valid, return null
                return null;
            }
        }

        protected static string EnsureUrlWithoutParams(string url)
        {
            int queryIndex, hashIndex;
            queryIndex = url.IndexOf("?");
            hashIndex = url.IndexOf("#");
            var hashIsValid = hashIndex >= 0;
            var queryIsValid = queryIndex >= 0;

            if (queryIsValid || hashIsValid)
            {
                if (queryIsValid && hashIsValid)
                {
                    if (queryIndex < hashIndex)
                    {
                        url = url.Substring(0, queryIndex);
                    }
                    else
                    {
                        url = url.Substring(0, hashIndex);
                    }
                }
                else
                {
                    if (queryIsValid)
                    {
                        url = url.Substring(0, queryIndex);
                    }
                    else
                    {
                        url = url.Substring(0, hashIndex);
                    }
                }
            }

            return url;
        }

        protected static string EnsureFileSystemValid(string filepath)
        {
            return EnsureUrlWithoutParams(filepath);
        }

        protected static void WriteFile(string filepath, byte[] data, bool? useTemporaryAttribute)
        {
            filepath = EnsureFileSystemValid(filepath);
            var directory = Path.GetDirectoryName(filepath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!useTemporaryAttribute.HasValue)
            {
                using (FileStream fs = File.Open(filepath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                {
                    fs.Write(data, 0, data.Length);
                    fs.SetLength(data.Length);
                }
            }
            else if (useTemporaryAttribute.Value)
            {
                using (FileStream fs = File.Open(filepath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                {
                    File.SetAttributes(filepath, FileAttributes.Normal);
                    fs.Write(data, 0, data.Length);
                    fs.SetLength(data.Length);
                }
            }
            else
            {
                using (FileStream fs = File.Open(filepath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                {
                    File.SetAttributes(filepath, FileAttributes.Temporary);
                    fs.Write(data, 0, data.Length);
                    fs.SetLength(data.Length);
                }
            }
        }

        protected List<PageData> GetPageReferencesToContent(IContentRepository repository, ContentReference contentReference)
        {
            var list = GetPagesRecursively(repository, contentReference)
                .Filter(new FilterTemplate()) // exclude container pages
                .Filter(new FilterPublished()) // exclude unpublished pages
                .Distinct()
                .ToList();

            return list;
        }

        protected IEnumerable<PageData> GetPagesRecursively(IContentRepository repository, ContentReference contentReference)
        {
            var references = repository.GetReferencesToContent(contentReference, true).ToList();
            foreach (var reference in references)
            {
                var content = repository.Get<IContent>(reference.OwnerID);

                // if content is PageData, return it
                var page = content as PageData;
                if (page != null)
                {
                    yield return page;
                }

                // if content is BlockData, return all pages where this block is used
                var block = content as BlockData;
                if (block != null)
                {
                    foreach (var x in GetPagesRecursively(repository, content.ContentLink))
                    {
                        yield return x;
                    }
                }
            }
        }
    }
}