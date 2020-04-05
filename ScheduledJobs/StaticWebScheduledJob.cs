using EPiServer;
using EPiServer.Core;
using EPiServer.PlugIn;
using EPiServer.Scheduler;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using EPiServer.Web.Routing;
using StaticWebEpiserverPlugin.Services;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace StaticWebEpiserverPlugin.ScheduledJobs
{
    [ScheduledPlugIn(DisplayName = "Generate StaticWeb", GUID = "da758e76-02ec-449e-8b34-999769cafb68")]
    public class StaticWebScheduledJob : ScheduledJobBase
    {
        protected bool _stopSignaled;
        protected IStaticWebService _staticWebService;
        protected IContentRepository _contentRepository;
        protected UrlResolver _urlResolver;
        protected long _numberOfPages = 0;
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
            _numberOfPages = 0;
            _generatedPages = new Dictionary<int, string>();
            _generatedResources = new Dictionary<string, string>();

            //Add implementation
            var startPage = SiteDefinition.Current.StartPage.ToReferenceWithoutVersion();

            var page = _contentRepository.Get<PageData>(startPage);
            GeneratePageInAllLanguages(page);

            return $"{_numberOfPages} of pages where generated with all depending resources.";
        }

        protected void GeneratePageInAllLanguages(PageData page)
        {
            // Only add pages once (have have this because of how websites can be setup to have a circle reference
            if (page.ContentLink == null || _generatedPages.ContainsKey(page.ContentLink.ID))
            {
                return;
            }
            _generatedPages.Add(page.ContentLink.ID, null);

            var languages = page.ExistingLanguages;
            foreach (var lang in languages)
            {
                var langPage = _contentRepository.Get<PageData>(page.ContentLink.ToReferenceWithoutVersion(), lang);

                UpdateScheduledJobStatus(page, lang);

                var langContentLink = langPage.ContentLink.ToReferenceWithoutVersion();

                _staticWebService.GeneratePage(langContentLink, lang, _generatedResources);
                _numberOfPages++;

                var children = _contentRepository.GetChildren<PageData>(langContentLink, lang);
                foreach (PageData child in children)
                {
                    GeneratePageInAllLanguages(child);

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

        protected void UpdateScheduledJobStatus(PageData page, CultureInfo lang)
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

            OnStatusChanged($"Generating page -  {orginalUrl}");
        }
    }
}