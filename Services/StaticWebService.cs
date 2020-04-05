using EPiServer;
using EPiServer.Core;
using EPiServer.Filters;
using EPiServer.ServiceLocation;
using EPiServer.Web.Routing;
using System;
using System.Collections.Generic;
using System.Configuration;
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
        protected string _rootUrl = null;
        protected string _rootPath = null;

        public StaticWebService()
        {
            _rootPath = ConfigurationManager.AppSettings["StaticWeb:OutputFolder"];
            ValidateOutputFolder();

            _rootUrl = ConfigurationManager.AppSettings["StaticWeb:InputUrl"];
            ValidateInputUrl();
        }

        private void ValidateInputUrl()
        {
            if (string.IsNullOrEmpty(_rootUrl))
            {
                throw new ArgumentException("Missing value for 'StaticWeb:InputUrl'", "StaticWeb:InputUrl");
            }

            try
            {
                // Try to parse as Uri to validate value
                var testUrl = new Uri(_rootUrl);
            }
            catch (Exception)
            {
                throw new ArgumentException("Invalid value for 'StaticWeb:InputUrl'", "StaticWeb:InputUrl");
            }
        }

        protected void ValidateOutputFolder()
        {
            if (string.IsNullOrEmpty(_rootPath))
            {
                throw new ArgumentException("Missing value for 'StaticWeb:OutputFolder'", "StaticWeb:OutputFolder");
            }

            if (!Directory.Exists(_rootPath))
            {
                throw new ArgumentException("Folder specified in 'StaticWeb:OutputFolder' doesn't exist", "StaticWeb:OutputFolder");
            }

            try
            {
                var directory = new DirectoryInfo(_rootPath);
                var directoryName = directory.FullName;
                var fileName = directoryName + Path.DirectorySeparatorChar + ".staticweb-access-test";

                // verifying write access
                File.WriteAllText(fileName, "Verifying write access to folder");
                // verify modify access
                File.WriteAllText(fileName, "Verifying modify access to folder");
                // verify delete access
                File.Delete(fileName);

            }
            catch (UnauthorizedAccessException)
            {
                throw new ArgumentException("Not sufficient permissions for folder specified in 'StaticWeb:OutputFolder'. Require read, write and modify permissions", "StaticWeb:OutputFolder");
            }
            catch (Exception)
            {
                throw new ArgumentException("Unknown error when testing write, edit and remove access to folder specified in 'StaticWeb:OutputFolder'", "StaticWeb:OutputFolder");
            }
        }

        public void GeneratePage(ContentReference contentLink, CultureInfo language, Dictionary<string, string> generatedResources = null)
        {
            var urlResolver = ServiceLocator.Current.GetInstance<UrlResolver>();
            var orginalUrl = urlResolver.GetUrl(contentLink, language.Name);
            if (orginalUrl == null)
                return;

            if (orginalUrl.StartsWith("//"))
            {
                return;
            }

            // NOTE: If publishing event comes from scheduled publishing (orginalUrl includes protocol, domain and port number)
            if (!orginalUrl.StartsWith("/"))
            {
                orginalUrl = new Uri(orginalUrl).AbsolutePath;
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

            string html = null;
            WebClient webClient = new WebClient();
            webClient.Headers.Set(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.149 Safari/537.36 StaticWebPlugin/0.1");
            webClient.Encoding = Encoding.UTF8;
            try
            {
                html = webClient.DownloadString(_rootUrl + orginalUrl);
            }
            catch (WebException)
            {
                // Ignore web exception, for example 404
            }

            if (html == null)
                return;

            html = TryToFixLinkUrls(html);

            html = EnsurePageResources(_rootUrl, _rootPath, html, generatedResources);

            if (!Directory.Exists(_rootPath + relativePath))
            {
                Directory.CreateDirectory(_rootPath + relativePath);
            }

            File.WriteAllText(_rootPath + relativePath + "index.html", html);
        }

        public void GeneratePagesDependingOnBlock(ContentReference contentLink)
        {
            var repository = ServiceLocator.Current.GetInstance<IContentRepository>();
            var pages = GetPageReferencesToContent(repository, contentLink);

            foreach (var page in pages)
            {
                var languages = page.ExistingLanguages;
                foreach (var lang in languages)
                {
                    GeneratePage(page.ContentLink, lang);
                }
            }

        }

        protected static string TryToFixLinkUrls(string html)
        {
            var urlResolver = ServiceLocator.Current.GetInstance<UrlResolver>();

            var matches = Regex.Matches(html, "href=\"(?<resource>\\/link\\/[0-9a-f]{32}.aspx)\"");
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

        protected static string EnsurePageResources(string rootUrl, string rootPath, string html, Dictionary<string, string> replaceResourcePairs = null)
        {
            if (replaceResourcePairs == null)
            {
                replaceResourcePairs = new Dictionary<string, string>();
            }

            // make sure we have all resources from script, link and img tags for current page
            // <(script|link|img).*(href|src)="(?<resource>[^"]+)
            EnsureScriptAndLinkAndImgTagSupport(rootUrl, rootPath, ref html, ref replaceResourcePairs);

            // make sure we have all source resources for current page
            // <(source).*(srcset)="(?<resource>[^"]+)"
            EnsureSourceTagSupport(rootUrl, rootPath, ref html, ref replaceResourcePairs);

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

        protected static void EnsureSourceTagSupport(string rootUrl, string rootPath, ref string html, ref Dictionary<string, string> replaceResourcePairs)
        {
            var matches = Regex.Matches(html, "<(source).*(srcset)=\"(?<resource>[^\"]+)\"");
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
                            continue;
                        }

                        var newResourceUrl = EnsureResource(rootUrl, rootPath, resourceUrl);
                        if (!string.IsNullOrEmpty(newResourceUrl))
                        {
                            if (!replaceResourcePairs.ContainsKey(resourceUrl))
                            {
                                replaceResourcePairs.Add(resourceUrl, newResourceUrl);
                            }
                        }
                        else
                        {
                            if (!replaceResourcePairs.ContainsKey(resourceUrl))
                            {
                                replaceResourcePairs.Add(resourceUrl, null);
                            }
                        }
                    }
                }
            }
        }

        protected static void EnsureScriptAndLinkAndImgTagSupport(string rootUrl, string rootPath, ref string html, ref Dictionary<string, string> replaceResourcePairs)
        {
            var matches = Regex.Matches(html, "<(script|link|img).*(href|src)=\"(?<resource>[^\"]+)");
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
                        continue;
                    }

                    var newResourceUrl = EnsureResource(rootUrl, rootPath, resourceUrl);
                    if (!string.IsNullOrEmpty(newResourceUrl))
                    {
                        if (!replaceResourcePairs.ContainsKey(resourceUrl))
                        {
                            replaceResourcePairs.Add(resourceUrl, newResourceUrl);
                        }
                    }
                    else
                    {
                        if (!replaceResourcePairs.ContainsKey(resourceUrl))
                        {
                            replaceResourcePairs.Add(resourceUrl, null);
                        }
                    }
                }
            }
        }

        protected static string EnsureResource(string rootUrl, string rootPath, string resourceUrl)
        {
            if (resourceUrl.StartsWith("//"))
            {
                return null;
            }

            if (resourceUrl.StartsWith("/"))
            {
                switch (Path.GetExtension(resourceUrl).ToLower())
                {
                    case ".css":
                        EnsureCssResources(rootUrl, rootPath, resourceUrl);
                        break;
                    case ".js":
                    case ".woff":
                    case ".woff2":
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".jpe":
                    case ".gif":
                    case ".ico":
                    case ".pdf":
                        // For approved file extensions that we don't need to do any changes on
                        var downloadUrl = rootUrl + resourceUrl;
                        var filepath = rootPath + resourceUrl.Replace("/", "\\");

                        DownloadFile(downloadUrl, filepath);
                        break;
                    case ".bmp":
                    case ".mp4":
                    case ".flv":
                    case ".webm":
                        // don't download of this extensions
                        break;
                    case ".html":
                    case ".htm":
                        // don't download web pages
                        break;
                    default:
                        // We have no extension to go on, look at content-type
                        WebClient referencableClient = new WebClient();
                        referencableClient.Headers.Set(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.149 Safari/537.36 StaticWebPlugin/0.1");
                        referencableClient.Encoding = Encoding.UTF8;
                        byte[] data = referencableClient.DownloadData(rootUrl + resourceUrl);

                        var contentTypeResponse = referencableClient.ResponseHeaders[HttpResponseHeader.ContentType];
                        if (string.IsNullOrEmpty(contentTypeResponse))
                            return null;

                        var contentType = contentTypeResponse.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        if (string.IsNullOrEmpty(contentType))
                            return null;

                        contentType = contentType.Trim().ToLower();

                        switch (contentType)
                        {
                            case "text/css":
                                var contentCss = Encoding.UTF8.GetString(data);
                                string newCssResourceUrl = GetNewResourceUrl(resourceUrl, ".css");

                                EnsureCssResources(rootUrl, rootPath, newCssResourceUrl, contentCss);
                                return newCssResourceUrl;
                            case "text/javascript":
                            case "application/javascript":
                                var contentJs = Encoding.UTF8.GetString(data);
                                string newJsResourceUrl = GetNewResourceUrl(resourceUrl, ".js");

                                var jsFilepath = rootPath + newJsResourceUrl.Replace("/", "\\");
                                WriteFile(jsFilepath, contentJs);
                                return newJsResourceUrl;
                            case "image/png":
                            case "image/jpg":
                            case "image/jpe":
                            case "image/jpeg":
                            case "image/gif":
                            case "image/webp":
                            case "application/pdf":
                                // Let us get file extension (for example: .png)
                                var fileExtension = "." + contentType.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries)[1];

                                string newBinaryResourceUrl = GetNewResourceUrl(resourceUrl, fileExtension);

                                var binaryFilepath = rootPath + newBinaryResourceUrl.Replace("/", "\\");
                                WriteFile(binaryFilepath, data);
                                return newBinaryResourceUrl;
                            default:
                                // don't download unknown content type
                                break;
                        }

                        break;


                }
            }
            return null;
        }

        protected static string GetNewResourceUrl(string resourceUrl, string extension)
        {
            /* Ugly hack: remove as soon as possible
             * make sure to not get a folder name with a file name, for example: /globalassets/alloy-plan/alloyplan.png/size700.png
             * alloyplan.png here would cause error (IF we also have the orginal image) as we can't have a file and a folder with the same name.
             */
            resourceUrl = resourceUrl.Replace(".", "-");

            int queryIndex, hashIndex;
            queryIndex = resourceUrl.IndexOf("?");
            hashIndex = resourceUrl.IndexOf("#");

            string newResourceUrl = resourceUrl + extension;
            if (queryIndex >= 0)
            {
                newResourceUrl = resourceUrl.Substring(0, queryIndex) + extension + resourceUrl.Substring(queryIndex);
            }
            else if (hashIndex >= 0)
            {
                newResourceUrl = resourceUrl.Substring(0, hashIndex) + extension + resourceUrl.Substring(hashIndex);
            }
            return newResourceUrl;
        }

        protected static void EnsureCssResources(string rootUrl, string rootPath, string url)
        {
            try
            {
                // Download and ensure files referenced are downloaded also
                WebClient referencableClient = new WebClient();
                referencableClient.Headers.Set(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.149 Safari/537.36 StaticWebPlugin/0.1");
                referencableClient.Encoding = Encoding.UTF8;
                byte[] data = referencableClient.DownloadData(rootUrl + url);
                var content = Encoding.UTF8.GetString(data);

                EnsureCssResources(rootUrl, rootPath, url, content);
            }
            catch (WebException)
            {
                // Ignore web exceptions, for example 404
            }
        }

        protected static void EnsureCssResources(string rootUrl, string rootPath, string url, string content)
        {
            // Download and ensure files referenced are downloaded also
            var matches = Regex.Matches(content, "url\\([\"|']{0,1}(?<resource>[^[\\)\"|']+)");
            foreach (Match match in matches)
            {
                var group = match.Groups["resource"];
                if (group.Success)
                {
                    var resourceUrl = group.Value;
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

                    DownloadFile(rootUrl + resourceUrl, rootPath + resourceUrl.Replace("/", @"\"));
                }
            }

            var filepath = rootPath + url.Replace("/", "\\");
            WriteFile(filepath, content);
        }

        protected static string EnsureFileSystemValid(string filepath)
        {
            int queryIndex, hashIndex;
            queryIndex = filepath.IndexOf("?");
            hashIndex = filepath.IndexOf("#");
            var hashIsValid = hashIndex >= 0;
            var queryIsValid = queryIndex >= 0;

            if (queryIsValid || hashIsValid)
            {
                if (queryIsValid && hashIsValid)
                {
                    if (queryIndex < hashIndex)
                    {
                        filepath = filepath.Substring(0, queryIndex);
                    }
                    else
                    {
                        filepath = filepath.Substring(0, hashIndex);
                    }
                }
                else
                {
                    if (queryIsValid)
                    {
                        filepath = filepath.Substring(0, queryIndex);
                    }
                    else
                    {
                        filepath = filepath.Substring(0, hashIndex);
                    }
                }
            }

            return filepath;
        }

        protected static void WriteFile(string filepath, string content)
        {
            filepath = EnsureFileSystemValid(filepath);
            var directory = Path.GetDirectoryName(filepath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filepath, content);
        }

        protected static void WriteFile(string filepath, byte[] data)
        {
            filepath = EnsureFileSystemValid(filepath);
            var directory = Path.GetDirectoryName(filepath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(filepath, data);
        }

        protected static void DownloadFile(string downloadUrl, string filepath)
        {
            filepath = EnsureFileSystemValid(filepath);
            using (WebClient resourceClient = new WebClient())
            {
                resourceClient.Headers.Set(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.149 Safari/537.36 StaticWebPlugin/0.1");
                resourceClient.Encoding = Encoding.UTF8;

                var directory = Path.GetDirectoryName(filepath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                try
                {
                    resourceClient.DownloadFile(downloadUrl, filepath);
                }
                catch (WebException)
                {
                    // Ignore web exceptions like 404 errors
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