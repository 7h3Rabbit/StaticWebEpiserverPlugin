﻿using EPiServer;
using EPiServer.Core;
using EPiServer.PlugIn;
using EPiServer.Scheduler;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using EPiServer.Web.Routing;
using StaticWebEpiserverPlugin.Configuration;
using StaticWebEpiserverPlugin.Interfaces;
using StaticWebEpiserverPlugin.Routing;
using StaticWebEpiserverPlugin.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StaticWebEpiserverPlugin.ScheduledJobs
{
    [ScheduledPlugIn(DisplayName = "Generate StaticWeb", SortIndex = 100000, GUID = "da758e76-02ec-449e-8b34-999769cafb68")]
    public class StaticWebScheduledJob : ScheduledJobBase
    {
        // We set this as false to generate all subpages as well
        private const bool IGNORE_HTML_DEPENDENCIES = true;

        protected bool _stopSignaled;
        protected IStaticWebService _staticWebService;
        protected IContentRepository _contentRepository;
        protected UrlResolver _urlResolver;
        protected long _numberOfPages = 0;
        protected long _numberOfObsoletePages = 0;
        protected long _numberOfObsoleteResources = 0;
        protected Dictionary<int, string> _generatedPages;
        protected ConcurrentDictionary<string, string> _generatedResources;
        protected Dictionary<string, ConcurrentDictionary<string, string>> _sitePages;
        protected CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public StaticWebScheduledJob()
        {
            IsStoppable = true;

            _staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();
            _contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();
            _urlResolver = ServiceLocator.Current.GetInstance<UrlResolver>();
        }

        /// <summary>
        /// Called when a user clicks on Stop for a manually started job, or when ASP.NET shuts down.
        /// </summary>
        public override void Stop()
        {
            _stopSignaled = true;
            _cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Called when a scheduled job executes
        /// </summary>
        /// <returns>A status message to be stored in the database log and visible from admin mode</returns>
        public override string Execute()
        {
            _stopSignaled = false;
            _cancellationTokenSource = new CancellationTokenSource();

            //Call OnStatusChanged to periodically notify progress of job for manually started jobs
            OnStatusChanged(String.Format("Starting execution of {0}", this.GetType()));

            _sitePages = new Dictionary<string, ConcurrentDictionary<string, string>>();
            StringBuilder resultMessage = new StringBuilder();


            var siteDefinitionRepository = ServiceLocator.Current.GetInstance<ISiteDefinitionRepository>();
            var siteDefinitions = siteDefinitionRepository.List().ToList();
            var hasAnyMatchingConfiguration = false;
            var numberOfSiteDefinitions = siteDefinitions.Count;
            foreach (var siteDefinition in siteDefinitions)
            {
                _generatedPages = new Dictionary<int, string>();
                //_generatedResources = new ConcurrentDictionary<string, string>();

                _numberOfPages = 0;
                _numberOfObsoletePages = 0;
                _numberOfObsoleteResources = 0;

                var configuration = StaticWebConfiguration.Get(siteDefinition);
                if (configuration == null || !configuration.Enabled)
                {
                    if (configuration != null && !string.IsNullOrEmpty(configuration.Name))
                    {
                        resultMessage.AppendLine($"<div class=\"ui-state-error\"><b>{configuration.Name}</b> - Was ignored because not enabled or missing required settings.<br /></div>");
                    }
                    else
                    {
                        resultMessage.AppendLine($"<div class=\"ui-state-error\"><b>{siteDefinition.Name}</b> - Was ignored because it was not configured.<br /></div>");
                    }
                    continue;
                }
                hasAnyMatchingConfiguration = true;

                // This website has been setup for using StaticWebEpiServerPlugin
                SiteDefinition.Current = siteDefinition;

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = configuration.MaxDegreeOfParallelismForScheduledJob,
                    CancellationToken = _cancellationTokenSource.Token
                };

                // Add Empty placeholder for pages to come
                _sitePages.Add(configuration.Name, new ConcurrentDictionary<string, string>());

                //Add implementation
                var startPage = SiteDefinition.Current.StartPage.ToReferenceWithoutVersion();

                var pages = GetPages(configuration, parallelOptions, startPage);

                GeneratingPages(configuration, parallelOptions, pages);

                if (configuration.UseRouting)
                {
                    OnStatusChanged("Saving routes to file");
                    StaticWebRouting.SaveRoutes();
                }


                if (configuration.RemoveObsoletePages)
                {
                    OnStatusChanged("Looking for obsolete pages");
                    RemoveObsoletePages(configuration);
                }

                if (configuration.RemoveObsoleteResources)
                {
                    OnStatusChanged("Looking for obsolete resources");
                    RemoveObsoleteResources(configuration);
                }

                resultMessage.AppendLine($"<b>{configuration.Name}</b> - {_sitePages[configuration.Name].Count} pages generated.");

                if (_numberOfObsoletePages > 0)
                {
                    resultMessage.AppendLine($" {_numberOfObsoletePages} obsolete pages removed.");
                }
                if (_numberOfObsoleteResources > 0)
                {
                    resultMessage.AppendLine($" {_numberOfObsoleteResources} obsolete resources removed.");
                }

                resultMessage.AppendLine($"<br />");
            }

            if (!hasAnyMatchingConfiguration)
            {
                return "StaticWeb is not enabled! Add 'StaticWeb:InputUrl' and 'StaticWeb:OutputFolder' under 'appSettings' element in web.config";
            }

            return resultMessage.ToString();
        }

        private IEnumerable<KeyValuePair<string, string>> GetPages(SiteConfigurationElement configuration, ParallelOptions parallelOptions, ContentReference startPage)
        {
            var page = _contentRepository.Get<PageData>(startPage);
            AddAllPagesInAllLanguagesForConfiguration(configuration, page);

            // Add pages url(s) into generated resouces from begining to prohibit it for downloading pages as resources
            _generatedResources = new ConcurrentDictionary<string, string>(_sitePages[configuration.Name]);

            // We now probably have most common
            IEnumerable<KeyValuePair<string, string>> pages = SortPages(configuration, _sitePages[configuration.Name].Skip(1));
            return pages;
        }

        private void GeneratingPages(SiteConfigurationElement configuration, ParallelOptions parallelOptions, IEnumerable<KeyValuePair<string, string>> pages)
        {
            OnStatusChanged($"{configuration.Name} - Generating Pages");
            bool? useTemporaryAttribute = configuration.UseTemporaryAttribute.HasValue ? false : configuration.UseTemporaryAttribute;

            // Runt first page first to get most of the common resources
            var firstPageUrl = _sitePages[configuration.Name].FirstOrDefault();

            _staticWebService.GeneratePage(configuration, firstPageUrl.Key, useTemporaryAttribute, IGNORE_HTML_DEPENDENCIES, null, _generatedResources);

            Parallel.ForEach(pages, parallelOptions, (pageInfo, _) =>
            {
                // TODO: Change this to handle SimpleAddress...
                _staticWebService.GeneratePage(configuration, pageInfo.Key, useTemporaryAttribute, IGNORE_HTML_DEPENDENCIES, null, _generatedResources);
            });
            OnStatusChanged($"{configuration.Name} - Generated Pages");
        }

        private IEnumerable<KeyValuePair<string, string>> SortPages(SiteConfigurationElement configuration, IEnumerable<KeyValuePair<string, string>> pages)
        {
            OnStatusChanged($"{configuration.Name} - Sorting Pages {configuration.GenerateOrderForScheduledJob}");
            switch (configuration.GenerateOrderForScheduledJob)
            {
                default:
                case Models.GenerateOrderForScheduledJob.Default:
                    pages = pages.ToList();
                    break;
                case Models.GenerateOrderForScheduledJob.UrlDepthFirst:
                    pages = pages.OrderBy(pair => pair.Key.TrimEnd('/')).ToList();
                    break;
                case Models.GenerateOrderForScheduledJob.UrlBreadthFirst:
                    // Smallest folder structure ordered alphabetic
                    pages = pages.OrderBy(pair => pair.Key.TrimEnd('/').Count(character => character == '/').ToString().PadLeft(5, '0') + '|' + pair.Key).ToList();
                    break;
            }
            OnStatusChanged($"{configuration.Name} - Sorted Pages {configuration.GenerateOrderForScheduledJob}");
            return pages;
        }

        private void RemoveObsoleteResources(SiteConfigurationElement configuration)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            if (!Directory.Exists(configuration.OutputPath + configuration.ResourceFolder))
            {
                // Directory doesn't exist, nothing to remove :)
                return;
            }

            var resources = _generatedResources.Values.ToHashSet<string>();

            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(configuration.OutputPath + configuration.ResourceFolder);
                var fileInfos = directoryInfo.GetFiles("*", SearchOption.AllDirectories);

                var nOfCandidates = fileInfos.Length;
                var candidateIndex = 1;
                foreach (FileInfo info in fileInfos)
                {
                    OnStatusChanged($"Looking for obsolete resources, candidate {candidateIndex} of {nOfCandidates}");

                    var resourcePath = info.FullName;

                    if (resourcePath.EndsWith("index.html"))
                    {
                        // Ignore index.html files (for example when having a empty resource folder name)
                        continue;
                    }

                    resourcePath = "/" + resourcePath.Replace(configuration.OutputPath, "").Replace("\\", "/");

                    if (!resources.Contains(resourcePath))
                    {
                        info.Delete();
                        _numberOfObsoleteResources++;
                    }
                    candidateIndex++;
                }
            }
            catch (Exception)
            {
                // something was wrong, but this is just extra cleaning so ignore it
                return;
            }
        }

        protected void RemoveObsoletePages(SiteConfigurationElement configuration)
        {
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            if (!Directory.Exists(configuration.OutputPath))
            {
                // Directory doesn't exist, nothing to remove :)
                return;
            }

            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(configuration.OutputPath);
                var fileInfos = directoryInfo.GetFiles("index.html", SearchOption.AllDirectories);

                var nOfCandidates = fileInfos.Length;
                var candidateIndex = 1;

                var siteUrl = configuration.Url.TrimEnd('/');
                foreach (FileInfo info in fileInfos)
                {
                    OnStatusChanged($"Looking for obsolete pages, candidate {candidateIndex} of {nOfCandidates}");

                    var url = info.FullName;
                    // c:\websites\A\
                    url = "/" + url.Replace(configuration.OutputPath, "").Replace("\\", "/");

                    // index.html
                    url = url.Replace("index.html", "");

                    IContent contentData = UrlResolver.Current.Route(new UrlBuilder(siteUrl + url));

                    // Does page exists?
                    if (!(contentData is PageData page) || !page.CheckPublishedStatus(PagePublishedStatus.Published))
                    {
                        // remove index.html file as it doesn't exist in EpiServer
                        info.Delete();
                        var dir = info.Directory;

                        // remove folder if no more files in it
                        if (dir.GetFiles().Length == 0 && dir.GetDirectories().Length == 0)
                        {
                            dir.Delete();
                        }

                        _numberOfObsoletePages++;
                    }
                    candidateIndex++;
                }
            }
            catch (Exception)
            {
                // something was wrong, but this is just extra cleaning so ignore it
                return;
            }
        }

        protected void AddAllPagesInAllLanguagesForConfiguration(SiteConfigurationElement configuration, PageData page)
        {
            //For long running jobs periodically check if stop is signaled and if so stop execution
            if (_stopSignaled)
            {
                OnStatusChanged("Stop of job was called");
                return;
            }

            // Only add pages once (have this because of how websites can be setup to have a circle reference
            if (page.ContentLink == null || _generatedPages.ContainsKey(page.ContentLink.ID))
            {
                return;
            }
            _generatedPages.Add(page.ContentLink.ID, null);

            // This page type should be ignored
            var ignorePageType = page is IStaticWebIgnoreGenerate;

            var languages = page.ExistingLanguages;
            foreach (var lang in languages)
            {
                var ignorePage = ignorePageType;
                var langPage = _contentRepository.Get<PageData>(page.ContentLink.ToReferenceWithoutVersion(), lang);

                var langContentLink = langPage.ContentLink.ToReferenceWithoutVersion();

                if (!langPage.CheckPublishedStatus(PagePublishedStatus.Published))
                {
                    ignorePage = true;
                }

                // This page type has a conditional for when we should generate it
                if (langPage is IStaticWebIgnoreGenerateDynamically generateDynamically)
                {
                    if (!generateDynamically.ShouldGenerate())
                    {
                        if (generateDynamically.ShouldDeleteGenerated())
                        {
                            _staticWebService.RemoveGeneratedPage(configuration, langContentLink, lang);
                        }

                        // This page should not be generated at this time, ignore it.
                        ignorePage = true;
                    }
                }

                if (!ignorePage)
                {
                    var urls = _staticWebService.GetUrlsForPage(configuration, page, lang, out string simpleAddress);

                    foreach (var url in urls)
                    {
                        UpdateScheduledJobStatus(configuration, url);
                        _sitePages[configuration.Name].TryAdd(url, null);
                    }
                    if (!string.IsNullOrEmpty(simpleAddress))
                    {
                        _sitePages[configuration.Name].TryAdd(simpleAddress, null);
                    }

                    //_staticWebService.GeneratePage(configuration, langPage, lang, _generatedResources);
                    _numberOfPages++;
                }

                var children = _contentRepository.GetChildren<PageData>(langContentLink, lang);
                foreach (PageData child in children)
                {
                    AddAllPagesInAllLanguagesForConfiguration(configuration, child);

                    //For long running jobs periodically check if stop is signaled and if so stop execution
                    if (_stopSignaled)
                    {
                        OnStatusChanged("Stop of job was called");
                        return;
                    }
                }

                //For long running jobs periodically check if stop is signaled and if so stop execution
                if (_stopSignaled)
                {
                    OnStatusChanged("Stop of job was called");
                    return;
                }
            }
        }

        protected void UpdateScheduledJobStatus(SiteConfigurationElement configuration, string pageUrl)
        {
            if (pageUrl.StartsWith("//"))
            {
                return;
            }

            // NOTE: If publishing event comes from scheduled publishing (orginalUrl includes protocol, domain and port number)
            if (!pageUrl.StartsWith("/"))
            {
                pageUrl = new Uri(pageUrl).AbsolutePath;
            }

            OnStatusChanged($"{configuration.Name} - {_numberOfPages} pages found. currently on: {pageUrl}");
        }
    }
}