using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using EPiServer.Web.Routing;
using StaticWebEpiserverPlugin.Configuration;
using StaticWebEpiserverPlugin.Interfaces;
using StaticWebEpiserverPlugin.Routing;
using StaticWebEpiserverPlugin.Services;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace StaticWebEpiserverPlugin.Initialization
{
    [InitializableModule]
    [ModuleDependency(typeof(EPiServer.Web.InitializationModule))]
    public class StaticWebInitialization : IInitializableModule, IConfigurableModule
    {
        public void Initialize(InitializationEngine context)
        {
            DependencyResolver.SetResolver(new StaticWebServiceLocatorDependencyResolver(context.Locate.Advanced));

            var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();
            var configuration = StaticWebConfiguration.CurrentSite;
            if (configuration != null && configuration.Enabled && configuration.UseRouting)
            {
                StaticWebRouting.LoadRoutes();
            }

            var events = ServiceLocator.Current.GetInstance<IContentEvents>();
            events.PublishedContent += OnPublishedContent;
            events.PublishingContent += OnPublishingContent;
            events.MovingContent += OnMovingContent;
            events.MovedContent += OnMovedContent;
            events.DeletedContent += OnDeletedContent;

            var contentSecurityRepository = ServiceLocator.Current.GetInstance<IContentSecurityRepository>();
            contentSecurityRepository.ContentSecuritySaved += OnContentSecuritySaved;
        }

        private void OnMovingContent(object sender, ContentEventArgs e)
        {
            var isPage = e.Content is PageData;
            if (isPage)
            {
                var urlResolver = ServiceLocator.Current.GetInstance<IUrlResolver>();
                var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();
                var contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();

                // Get urls for all language (as we are moving them all)
                var contentReference = new ContentReference(e.Content.ContentLink.ID);
                var languageUrls = GetPageLanguageUrls(staticWebService, contentRepository, contentReference);
                e.Items.Add("StaticWeb-OldLanguageUrls", languageUrls);
            }
        }

        private static Dictionary<string,string> GetPageLanguageUrls(IStaticWebService staticWebService, IContentRepository contentRepository, ContentReference contentReference)
        {
            Dictionary<string, string> languageUrls = new Dictionary<string, string>();
            PageData page;
            if (contentRepository.TryGet<PageData>(contentReference, out page))
            {
                var languages = page.ExistingLanguages;
                foreach (var lang in languages)
                {
                    var oldLangUrl = staticWebService.GetPageUrl(contentReference, lang);
                    languageUrls.Add(lang.Name, oldLangUrl);
                }
            }
            return languageUrls;
        }

        private void OnContentSecuritySaved(object sender, ContentSecurityEventArg e)
        {
            //_log.Information($"ContentSecuritySaved fired for content {e.ContentLink.ID}");
            //var action = ContentAction.AccessRightsChanged;
            //var affectedContent = new List<ContentReference>();
            //var contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();
            //var descendants = contentRepository.GetDescendents(e.ContentLink);
            //affectedContent.AddRange(descendants);
            //affectedContent.Add(e.ContentLink);
            //ExtendedContentEvents.Instance.RaiseContentChangedEvent(new ContentChangedEventArgs(e.ContentLink, action, affectedContent));
        }

        private void OnPublishingContent(object sender, ContentEventArgs e)
        {
            var isPage = e.Content is PageData;
            if (isPage)
            {
                var urlResolver = ServiceLocator.Current.GetInstance<IUrlResolver>();
                var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();

                var oldUrl = staticWebService.GetPageUrl(new ContentReference(e.Content.ContentLink.ID));
                e.Items.Add("StaticWeb-OldUrl", oldUrl);
            }
        }

        private void OnDeletedContent(object sender, DeleteContentEventArgs e)
        {
            //var deleteContentEvent = e as DeleteContentEventArgs;
            //var descendents = deleteContentEvent.DeletedDescendents.ToList();
            //foreach (ContentReference contentReference in descendents)
            //{

            //}
        }

        private void OnMovedContent(object sender, ContentEventArgs e)
        {
            var page = e.Content as PageData;
            if (page == null)
            {
                return;
            }

            var moveContentEvent = e as MoveContentEventArgs;

            var movedToWasteBasket = moveContentEvent.TargetLink.ID == ContentReference.WasteBasket.ID;
            var movedFromWasteBasket = moveContentEvent.OriginalParent.ID == ContentReference.WasteBasket.ID;

            // TODO: Sadly this is a syncronized event, look if we can thread this so we are not locking user interface until generation of pages are done

            //GeneratePage(e.ContentLink, e.Content);
            var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();
            var contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();

            var configuration = StaticWebConfiguration.CurrentSite;

            if (!movedToWasteBasket)
            {
                GeneratePageInAllLanguages(staticWebService, contentRepository, configuration, page);

                var descendents = moveContentEvent.Descendents.ToList();
                foreach (ContentReference contentReference in descendents)
                {
                    PageData subPage;
                    if (contentRepository.TryGet<PageData>(contentReference, out subPage))
                    {
                        GeneratePageInAllLanguages(staticWebService, contentRepository, configuration, subPage);
                    }
                }

                var oldUrls = e.Items["StaticWeb-OldLanguageUrls"] as Dictionary<string, string>;
                var newContentReference = new ContentReference(e.Content.ContentLink.ID);
                var newUrls = GetPageLanguageUrls(staticWebService, contentRepository, newContentReference);
                if (oldUrls != null && newUrls != null && oldUrls.Count == newUrls.Count)
                {
                    foreach (KeyValuePair<string, string> pair in oldUrls)
                    {
                        if (newUrls.ContainsKey(pair.Key))
                        {
                            var newUrl = newUrls[pair.Key];
                            staticWebService.CreateRedirectPages(configuration, pair.Value, newUrl);
                        }
                    }
                }
            }
        }

        private void OnPublishedContent(object sender, ContentEventArgs e)
        {
            var isPage = e.Content is PageData;
            var isBlock = e.Content is BlockData;
            if (!isPage && !isBlock)
            {
                // Content is not of type PageData or BlockData, ignore
                return;
            }

            GeneratePage(e.ContentLink, e.Content);

            var configuration = StaticWebConfiguration.CurrentSite;
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            if (isPage)
            {
                // Handle renaming of pages
                var oldUrl = e.Items["StaticWeb-OldUrl"] as string;
                if (oldUrl != null)
                {
                    var urlResolver = ServiceLocator.Current.GetInstance<IUrlResolver>();
                    var url = urlResolver.GetUrl(e.ContentLink);
                    if (url != oldUrl)
                    {
                        // Page has changed url, remove old page(s) and generate new for children.
                        var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();

                        var newUrl = staticWebService.GetPageUrl(new ContentReference(e.Content.ContentLink.ID));
                        staticWebService.CreateRedirectPages(configuration, oldUrl, newUrl);
                    }
                }
            }
        }

        protected void RemovePageInAllLanguages(IStaticWebService staticWebService, IContentRepository contentRepository, SiteConfigurationElement configuration, ContentReference contentReference)
        {
            var page = contentRepository.Get<PageData>(contentReference);
            var languages = page.ExistingLanguages;
            foreach (var lang in languages)
            {
                var langPage = contentRepository.Get<PageData>(page.ContentLink, lang);

                var langContentLink = langPage.ContentLink;

                var removeSubFolders = true;
                staticWebService.RemoveGeneratedPage(configuration, langContentLink, lang, removeSubFolders);
            }
        }


        protected void GeneratePageInAllLanguages(IStaticWebService staticWebService, IContentRepository contentRepository, SiteConfigurationElement configuration, PageData page)
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
                            staticWebService.RemoveGeneratedPage(configuration, langContentLink, lang);
                        }

                        // This page should not be generated at this time, ignore it.
                        continue;
                    }
                }

                staticWebService.GeneratePage(configuration, langContentLink, lang);
            }
        }

        private static void GeneratePage(ContentReference contentReference, IContent content)
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
                var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();

                // This page type has a conditional for when we should generate it
                if (content is IStaticWebIgnoreGenerateDynamically generateDynamically)
                {
                    if (!generateDynamically.ShouldGenerate())
                    {
                        if (generateDynamically.ShouldDeleteGenerated())
                        {
                            staticWebService.RemoveGeneratedPage(configuration, contentReference, page.Language);
                        }

                        // This page should not be generated at this time, ignore it.
                        return;
                    }
                }

                staticWebService.GeneratePage(configuration, contentReference, page.Language);
            }
            else if (content is BlockData)
            {
                var block = content as BlockData;
                var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();
                staticWebService.GeneratePagesDependingOnBlock(configuration, contentReference);
            }
        }

        public void Uninitialize(InitializationEngine context)
        {
        }

        public void ConfigureContainer(ServiceConfigurationContext context)
        {
            context.ConfigurationComplete += (o, e) =>
            {
                //Register custom implementations that should be used in favour of the default implementations
                context.Services.Add(typeof(IStaticWebService), new StaticWebService());
            };
        }
    }
}