using EPiServer;
using EPiServer.Core;
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

        public event EventHandler<StaticWebIOEvent> AfterIOWrite;
        public event EventHandler<StaticWebIOEvent> AfterIODelete;

        public ITextResourceDependencyService _htmlDependencyService = new HtmlDependencyService();
        public ITextResourceDependencyService _cssDependencyService = new CssDependencyService();

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

            AfterIODelete?.Invoke(this, new StaticWebIOEvent { FilePath = relativePath + "index.html" });

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

                    AfterIOWrite?.Invoke(this, new StaticWebIOEvent { FilePath = info.FullName, Data = Encoding.UTF8.GetBytes(redirectHtml) });
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


            string html = null;

            var pageTypeConfiguration = StaticWebConfiguration.AllowedResourceTypes.FirstOrDefault(r => r.FileExtension == "");
            if (pageTypeConfiguration == null)
            {
                // don't download resource as it is of a known type we don't want to download
                generatePageEvent.CancelAction = true;
                generatePageEvent.CancelReason = "AllowedResourceTypes configuration for file extension '' is missing (read: used by page).";
            }
            else
            {
                generatePageEvent.TypeConfiguration = pageTypeConfiguration;
            }


            BeforeGeneratePage?.Invoke(this, generatePageEvent);
            // someone wants to cancel this generation of this event.
            if (generatePageEvent.CancelAction)
            {
                return;
            }

            BeforeGetPageContent?.Invoke(this, generatePageEvent);

            if (configuration.UseRouting)
            {
                StaticWebRouting.Remove(relativePath);
                if (hasSimpleAddress)
                {
                    StaticWebRouting.Remove(relativeSimplePath);
                }
            }

            // someone wants to cancel this generation of this event.
            if (!generatePageEvent.CancelAction)
            {
                var resourceInfo = DownloadResource(configuration.Url, pageUrl, generatePageEvent.TypeConfiguration);
                if (resourceInfo != null)
                {
                    if (generatePageEvent.TypeConfiguration.MimeType.StartsWith("*"))
                    {
                        generatePageEvent.TypeConfiguration = resourceInfo.TypeConfiguration;
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
                generatePageEvent.Content = EnsureDependencies(generatePageEvent.Content, configuration, useTemporaryAttribute, generatePageEvent.TypeConfiguration, generatePageEvent.CurrentResources, generatePageEvent.Resources);
            }

            // reset cancel action and reason
            generatePageEvent.CancelAction = false;
            generatePageEvent.CancelReason = null;

            // We don't care about a cancel action here
            AfterEnsurePageResources?.Invoke(this, generatePageEvent);

            string filePath = configuration.OutputPath + relativePath + generatePageEvent.TypeConfiguration.DefaultName + generatePageEvent.TypeConfiguration.FileExtension;
            var filePaths = new List<string>
            {
                filePath
            };
            if (hasSimpleAddress)
            {
                string filePath2 = configuration.OutputPath + relativeSimplePath + generatePageEvent.TypeConfiguration.DefaultName + generatePageEvent.TypeConfiguration.FileExtension;
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

        public void GeneratePagesDependingOnContent(SiteConfigurationElement configuration, ContentReference contentReference, bool? useTemporaryAttribute)
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

                var referencedContent = contentRepository.Get<IContent>(item.OwnerID.ToReferenceWithoutVersion(), item.OwnerLanguage);
                if (referencedContent is PageData)
                {
                    GeneratePage(item.OwnerID, referencedContent, useTemporaryAttribute);
                }
                else if (referencedContent is BlockData)
                {
                    GeneratePagesDependingOnBlock(configuration, item.OwnerID, useTemporaryAttribute);
                }
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


        protected string EnsureDependencies(string content, SiteConfigurationElement siteConfiguration, bool? useTemporaryAttribute, AllowedResourceTypeConfigurationElement typeConfiguration, Dictionary<string, string> currentPageResourcePairs = null, ConcurrentDictionary<string, string> replaceResourcePairs = null)
        {
            switch (typeConfiguration.DenendencyLookup)
            {
                case ResourceDependencyLookup.Html:
                    return _htmlDependencyService.EnsureDependencies(content, this, siteConfiguration, useTemporaryAttribute, currentPageResourcePairs, replaceResourcePairs);
                case ResourceDependencyLookup.Css:
                    return _cssDependencyService.EnsureDependencies(content, this, siteConfiguration, useTemporaryAttribute, currentPageResourcePairs, replaceResourcePairs);
                case ResourceDependencyLookup.None:
                default:
                    return content;
            }
        }

        public string EnsureResource(SiteConfigurationElement siteConfiguration, string resourceUrl, Dictionary<string, string> currentPageResourcePairs, ConcurrentDictionary<string, string> replaceResourcePairs, bool? useTemporaryAttribute)
        {
            var extension = GetExtension(resourceUrl);
            bool preventDownload = IsDownloadPrevented(resourceUrl + extension);
            if (preventDownload)
            {
                // don't download resource as it is of a known type we don't want to download
                return null;
            }

            var fileExtensionConfiguration = StaticWebConfiguration.AllowedResourceTypes.FirstOrDefault(r => r.FileExtension == extension);
            if (fileExtensionConfiguration == null)
            {
                // don't download resource as it is of a known type we don't want to download
                return null;
            }

            string oldResultingResourceUrl;
            if (replaceResourcePairs.TryGetValue(resourceUrl, out oldResultingResourceUrl))
            {
                return oldResultingResourceUrl;
            }

            var resourceInfo = DownloadResource(siteConfiguration.Url, resourceUrl, fileExtensionConfiguration);
            if (resourceInfo == null)
            {
                // error occured OR resource type not allowed, ignore
                return null;
            }

            // For approved file extensions that we don't need to do any changes on
            string newResourceUrl = GetNewResourceUrl(siteConfiguration.ResourceFolder, resourceUrl, resourceInfo.Data, resourceInfo.TypeConfiguration);

            if (!replaceResourcePairs.ContainsKey(resourceUrl))
            {
                replaceResourcePairs.TryAdd(resourceUrl, newResourceUrl);
            }
            if (!currentPageResourcePairs.ContainsKey(resourceUrl))
            {
                currentPageResourcePairs.Add(resourceUrl, newResourceUrl);
            }

            if (resourceInfo.TypeConfiguration.DenendencyLookup != ResourceDependencyLookup.None)
            {
                var content = Encoding.UTF8.GetString(resourceInfo.Data);
                content = EnsureDependencies(content, siteConfiguration, useTemporaryAttribute, resourceInfo.TypeConfiguration, currentPageResourcePairs, replaceResourcePairs);
                resourceInfo.Data = Encoding.UTF8.GetBytes(content);
            }

            var hasContent = resourceInfo.Data != null && resourceInfo.Data.LongLength > 0;
            if (newResourceUrl != null && hasContent)
            {
                var filepath = siteConfiguration.OutputPath + newResourceUrl.Replace("/", "\\");
                if (resourceInfo.TypeConfiguration.UseHash)
                {
                    // We are using hash ONLY as file name so no need to replace file that already exists
                    if (File.Exists(filepath))
                        return newResourceUrl;
                }

                var shouldMaintainUrl = !resourceInfo.TypeConfiguration.UseHash && !resourceInfo.TypeConfiguration.UseResourceFolder && resourceInfo.TypeConfiguration.UseResourceUrl;
                if (filepath.EndsWith("\\") && shouldMaintainUrl)
                {
                    filepath = filepath + resourceInfo.TypeConfiguration.DefaultName + resourceInfo.TypeConfiguration.FileExtension;
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

        protected static bool IsDownloadPrevented(string resourceUrl)
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
            var allowedExtensionConfig = StaticWebConfiguration.AllowedResourceTypes.FirstOrDefault(type => type.FileExtension == extension);
            return (allowedExtensionConfig == null);
        }

        protected static StaticWebDownloadResult DownloadResource(string rootUrl, string resourceUrl, AllowedResourceTypeConfigurationElement typeConfiguration)
        {
            StaticWebDownloadResult result = new StaticWebDownloadResult();

            // We have no extension to go on, look at content-type
            WebClient referencableClient = new WebClient();
            referencableClient.Headers.Set(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.149 Safari/537.36 StaticWebPlugin/0.1");
            referencableClient.Encoding = Encoding.UTF8;

            try
            {
                byte[] data = referencableClient.DownloadData(rootUrl + resourceUrl);
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

            if (typeConfiguration.MimeType.StartsWith("*"))
            {
                var extensionConfig = GetConfigurationForKnownContentType(contentType);
                if (extensionConfig != null && !IsDownloadPrevented(resourceUrl + extensionConfig.FileExtension))
                {
                    result.TypeConfiguration = extensionConfig;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                result.TypeConfiguration = typeConfiguration;
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

        protected static string GetDefaultNameForKnownContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return null;
            }

            contentType = contentType.Trim().ToLower();

            var typeConfig = StaticWebConfiguration.AllowedResourceTypes.FirstOrDefault(type => type.MimeType == contentType);
            if (typeConfig == null)
                return null;

            return typeConfig.DefaultName;
        }

        protected static AllowedResourceTypeConfigurationElement GetConfigurationForKnownContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return null;
            }

            contentType = contentType.Trim().ToLower();

            var typeConfig = StaticWebConfiguration.AllowedResourceTypes.FirstOrDefault(type => type.MimeType == contentType);
            if (typeConfig == null)
                return null;

            return typeConfig;
        }

        protected static string GetNewResourceUrl(string resourcePath, string resourceUrl, byte[] data, AllowedResourceTypeConfigurationElement typeConfiguration)
        {
            if (typeConfiguration == null)
                return null;

            if (typeConfiguration.UseResourceFolder)
            {
                resourcePath = "/" + resourcePath.Replace(@"\", "/") + "/";
            }
            else
            {
                resourcePath = "/";
            }

            if (typeConfiguration.UseResourceUrl)
            {
                if (string.IsNullOrEmpty(resourceUrl))
                {
                    resourceUrl = "";
                }

                /* Ugly hack: remove as soon as possible
                 * make sure to not get a folder name with a file name, for example: /globalassets/alloy-plan/alloyplan.png/size700.png
                 * alloyplan.png here would cause error (IF we also have the orginal image) as we can't have a file and a folder with the same name.
                 */
                resourceUrl = resourceUrl.Replace(typeConfiguration.FileExtension, "");

                resourceUrl = EnsureUrlWithoutParams(resourceUrl);
            }

            // If we have disabled usage of resourceUrl, force usage of hash
            string hash = "";
            if (!typeConfiguration.UseResourceUrl || typeConfiguration.UseHash)
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

            if (typeConfiguration.UseResourceUrl && typeConfiguration.UseHash)
            {
                return (resourcePath + EnsureFileSystemValid(resourceUrl + "-" + hash + typeConfiguration.FileExtension)).Replace("//", "/");
            }
            else if (typeConfiguration.UseHash)
            {
                return (resourcePath + EnsureFileSystemValid(hash + typeConfiguration.FileExtension)).Replace("//", "/");
            }
            else
            {
                if (resourceUrl.EndsWith("/"))
                {
                    return (resourcePath + EnsureFileSystemValid(resourceUrl)).Replace("//", "/");
                }
                else
                {
                    return (resourcePath + EnsureFileSystemValid(resourceUrl + typeConfiguration.FileExtension)).Replace("//", "/");
                }
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

        protected void WriteFile(string filepath, byte[] data, bool? useTemporaryAttribute)
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

            AfterIOWrite?.Invoke(this, new StaticWebIOEvent { FilePath = filepath, Data = data });
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