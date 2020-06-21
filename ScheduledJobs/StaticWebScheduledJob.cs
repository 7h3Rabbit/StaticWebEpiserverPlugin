using EPiServer;
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace StaticWebEpiserverPlugin.ScheduledJobs
{
    [ScheduledPlugIn(DisplayName = "Generate StaticWeb", SortIndex = 100000, GUID = "da758e76-02ec-449e-8b34-999769cafb68")]
    public class StaticWebScheduledJob : ScheduledJobBase
    {
        protected bool _stopSignaled;
        protected IStaticWebService _staticWebService;
        protected IContentRepository _contentRepository;
        protected UrlResolver _urlResolver;
        protected long _numberOfPages = 0;
        protected long _numberOfObsoletePages = 0;
        protected long _numberOfObsoleteResources = 0;
        protected Dictionary<int, string> _generatedPages;
        protected Dictionary<string, string> _generatedResources;

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
        }

        /// <summary>
        /// Called when a scheduled job executes
        /// </summary>
        /// <returns>A status message to be stored in the database log and visible from admin mode</returns>
        public override string Execute()
        {
            //Call OnStatusChanged to periodically notify progress of job for manually started jobs
            OnStatusChanged(String.Format("Starting execution of {0}", this.GetType()));

            // Setting number of pages to start value (0), it is used to show message after job is done
            _generatedPages = new Dictionary<int, string>();
            _generatedResources = new Dictionary<string, string>();
            StringBuilder resultMessage = new StringBuilder();

            var siteDefinitionRepository = ServiceLocator.Current.GetInstance<ISiteDefinitionRepository>();
            var siteDefinitions = siteDefinitionRepository.List().ToList();
            var hasAnyMatchingConfiguration = false;
            var numberOfSiteDefinitions = siteDefinitions.Count;
            foreach (var siteDefinition in siteDefinitions)
            {
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

                //Add implementation
                var startPage = SiteDefinition.Current.StartPage.ToReferenceWithoutVersion();

                var page = _contentRepository.Get<PageData>(startPage);
                GeneratePageInAllLanguages(configuration, page);

                if (configuration.UseRouting)
                {
                    OnStatusChanged("Saving routes to file");
                    StaticWebRouting.SaveRoutes();
                }


                OnStatusChanged("Looking for obsolete pages");
                RemoveObsoletePages(configuration);
                OnStatusChanged("Looking for obsolete resources");
                RemoveObsoleteResources(configuration);

                resultMessage.AppendLine($"<b>{configuration.Name}</b> - {_numberOfPages} pages generated.");

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
                foreach (FileInfo info in fileInfos)
                {
                    OnStatusChanged($"Looking for obsolete pages, candidate {candidateIndex} of {nOfCandidates}");

                    var url = info.FullName;
                    // c:\websites\A\
                    url = "/" + url.Replace(configuration.OutputPath, "").Replace("\\", "/");

                    // index.html
                    url = url.Replace("index.html", "");

                    var urlResolver = ServiceLocator.Current.GetInstance<UrlResolver>();
                    IContent contentData = urlResolver.Route(new UrlBuilder(url));

                    // Does page exists?
                    var pageExists = contentData != null;
                    if (!pageExists)
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

        protected void GeneratePageInAllLanguages(SiteConfigurationElement configuration, PageData page)
        {
            // Only add pages once (have have this because of how websites can be setup to have a circle reference
            if (page.ContentLink == null || _generatedPages.ContainsKey(page.ContentLink.ID))
            {
                return;
            }

            // This page type should be ignored
            if (page is IStaticWebIgnoreGenerate)
            {
                return;
            }


            _generatedPages.Add(page.ContentLink.ID, null);

            var languages = page.ExistingLanguages;
            foreach (var lang in languages)
            {
                var langPage = _contentRepository.Get<PageData>(page.ContentLink.ToReferenceWithoutVersion(), lang);

                UpdateScheduledJobStatus(configuration, page, lang);

                var langContentLink = langPage.ContentLink.ToReferenceWithoutVersion();

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
                        continue;
                    }
                }

                _staticWebService.GeneratePage(configuration, langContentLink, lang, _generatedResources);
                _numberOfPages++;

                var children = _contentRepository.GetChildren<PageData>(langContentLink, lang);
                foreach (PageData child in children)
                {
                    GeneratePageInAllLanguages(configuration, child);

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

        protected void UpdateScheduledJobStatus(SiteConfigurationElement configuration, PageData page, CultureInfo lang)
        {
            var orginalUrl = _urlResolver.GetUrl(page.ContentLink.ToReferenceWithoutVersion(), lang.Name);
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

            OnStatusChanged($"{configuration.Name} - {_numberOfPages} pages generated. currently on: {orginalUrl}");
        }
    }
}