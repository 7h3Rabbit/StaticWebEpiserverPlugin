﻿using EPiServer;
using EPiServer.Core;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using StaticWebEpiserverPlugin.Interfaces;
using StaticWebEpiserverPlugin.Services;
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


            var events = ServiceLocator.Current.GetInstance<IContentEvents>();
            events.PublishedContent += OnPublishedContent;
        }

        private void OnPublishedContent(object sender, ContentEventArgs e)
        {
            if (e.Content is IStaticWebIgnoreGenerate)
            {
                return;
            }

            if (e.Content is PageData)
            {
                var contentLink = e.ContentLink;
                var page = e.Content as PageData;

                var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();
                staticWebService.GeneratePage(contentLink, page.Language);
            }
            else if (e.Content is BlockData)
            {
                var block = e.Content as BlockData;
                var staticWebService = ServiceLocator.Current.GetInstance<IStaticWebService>();
                staticWebService.GeneratePagesDependingOnBlock(e.ContentLink);
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