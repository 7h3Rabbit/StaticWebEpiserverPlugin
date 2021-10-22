using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using EPiServer.Web.Routing;
using StaticWebEpiserverPlugin.Configuration;
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
            events.DeletingContent += OnDeletingContent;
            events.DeletedContent += OnDeletedContent;

            var contentSecurityRepository = ServiceLocator.Current.GetInstance<IContentSecurityRepository>();
            contentSecurityRepository.ContentSecuritySaved += OnContentSecuritySaved;
        }

        private void OnDeletingContent(object sender, DeleteContentEventArgs e)
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
                var languageUrls = staticWebService.GetPageLanguageUrls(contentRepository, contentReference);
                e.Items.Add("StaticWeb-OldLanguageUrls", languageUrls);
            }
        }

        private void OnContentSecuritySaved(object sender, ContentSecurityEventArg e)
        {
            //e.ContentSecurityDescriptor.HasAccess()
            //$"ContentSecuritySaved fired for content {e.ContentLink.ID}";
            //var affectedContent = new List<ContentReference>();
            //var contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();
            //var descendants = contentRepository.GetDescendents(e.ContentLink);
            //affectedContent.AddRange(descendants);
            //affectedContent.Add(e.ContentLink);
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
            var isPage = e.Content is PageData;
            var isBlock = e.Content is BlockData;
            if (!isPage)
            {
                // Content is not of type PageData or BlockData, ignore
                return;
            }

            var configuration = StaticWebConfiguration.CurrentSite;
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            bool? useTemporaryAttribute = configuration.UseTemporaryAttribute.HasValue ? false : configuration.UseTemporaryAttribute;
            var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();
            staticWebService.GeneratePage(e.ContentLink, e.Content, useTemporaryAttribute);

            if (isPage)
            {
                // Handle renaming of pages
                var oldUrl = e.Items["StaticWeb-OldUrl"] as string;
                if (oldUrl != null)
                {
                    var removeSubFolders = true;
                    staticWebService.RemoveGeneratedPage(configuration, oldUrl, removeSubFolders);
                }
            }
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

            var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();
            var contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();

            var configuration = StaticWebConfiguration.CurrentSite;

            if (!movedToWasteBasket)
            {
                // Moved somewhere, generate pages again
                staticWebService.GeneratePageInAllLanguages(contentRepository, configuration, page);

                var descendents = moveContentEvent.Descendents.ToList();
                foreach (ContentReference contentReference in descendents)
                {
                    PageData subPage;
                    if (contentRepository.TryGet<PageData>(contentReference, out subPage))
                    {
                        staticWebService.GeneratePageInAllLanguages(contentRepository, configuration, subPage);
                    }
                }

                // create redirect pages in old location(s)
                var oldUrls = e.Items["StaticWeb-OldLanguageUrls"] as Dictionary<string, string>;
                var newContentReference = new ContentReference(e.Content.ContentLink.ID);
                var newUrls = staticWebService.GetPageLanguageUrls(contentRepository, newContentReference);
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
            else
            {
                // Remove page as it was added to WasteBasket
                var oldUrls = e.Items["StaticWeb-OldLanguageUrls"] as Dictionary<string, string>;
                if (oldUrls != null)
                {
                    foreach (KeyValuePair<string, string> pair in oldUrls)
                    {
                        staticWebService.RemoveGeneratedPage(configuration, pair.Value, true);
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

            var configuration = StaticWebConfiguration.CurrentSite;
            if (configuration == null || !configuration.Enabled)
            {
                return;
            }

            bool? useTemporaryAttribute = configuration.UseTemporaryAttribute.HasValue ? false : configuration.UseTemporaryAttribute;
            var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();
            staticWebService.GeneratePage(e.ContentLink, e.Content, useTemporaryAttribute);

            if (isPage)
            {
                // Handle renaming of pages
                var oldUrl = e.Items["StaticWeb-OldUrl"] as string;
                if (oldUrl != null)
                {
                    var urlResolver = ServiceLocator.Current.GetInstance<IUrlResolver>();
                    var url = staticWebService.GetPageUrl(e.ContentLink);
                    if (url != oldUrl)
                    {
                        // Page has changed url, remove old page(s) and generate new for children.
                        var newUrl = staticWebService.GetPageUrl(new ContentReference(e.Content.ContentLink.ID));
                        staticWebService.CreateRedirectPages(configuration, oldUrl, newUrl);
                    }
                }
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