﻿using EPiServer;
using EPiServer.Core;
using StaticWebEpiserverPlugin.Configuration;
using StaticWebEpiserverPlugin.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace StaticWebEpiserverPlugin.Services
{
    public interface IStaticWebService
    {
        /// <summary>
        /// First thing happening when GeneratePage is called.
        /// StaticWebGeneratePageEventArg is populated with following info: ContentLink, CultureInfo.
        /// Resources can also be populated IF GeneratePage method is called from ScheduledJob and pages generated before had resources depending on them.
        /// This event can be canceled by setting CancelAction to true.
        /// </summary>
        event EventHandler<StaticWebGeneratePageEventArgs> BeforeGeneratePage;
        /// <summary>
        /// Called before HTTP Request is triggered.
        /// StaticWebGeneratePageEventArg is populated with following info: ContentLink, CultureInfo and PageUrl.
        /// This event can be canceled by setting CancelAction to true.
        /// It can be used for example if you want to populate Content in a different way then using the default WebClient request to PageUrl.
        /// </summary>
        event EventHandler<StaticWebGeneratePageEventArgs> BeforeGetPageContent;
        /// <summary>
        /// Called after HTTP Request was triggered and we get our result.
        /// StaticWebGeneratePageEventArg is populated with following info: ContentLink, CultureInfo, PageUrl and Content.Event can be used to adjust or populate Content.
        /// This event can't be canceled.
        /// </summary>
        event EventHandler<StaticWebGeneratePageEventArgs> AfterGetPageContent;

        /// <summary>
        /// Called before fixing EpiServer permanent links (As they will point to a 404 if not fixed on a static website).
        /// StaticWebGeneratePageEventArg is populated with following info: ContentLink, CultureInfo, PageUrl and Content.
        /// This event can be canceled by setting CancelAction to true.
        /// It can be used to prohibit fixing EpiServer permanent links if you still want them or if it is some sort of error in it.
        /// </summary>
        event EventHandler<StaticWebGeneratePageEventArgs> BeforeTryToFixLinkUrls;
        /// <summary>
        /// Called before retrieving the resources this page is dependent on.
        /// StaticWebGeneratePageEventArg is populated with following info: ContentLink, CultureInfo, PageUrl and Content.
        /// This event can be canceled by setting CancelAction to true.
        /// It can be used to retrieve resources in a different way then using the default WebClient behavior.
        /// </summary>
        event EventHandler<StaticWebGeneratePageEventArgs> BeforeEnsurePageResources;
        /// <summary>
        /// Called after retrieving the resources this page is dependent on.
        /// StaticWebGeneratePageEventArg is populated with following info: ContentLink, CultureInfo, PageUrl, Content and Resources.
        /// This event can't be canceled.
        /// </summary>
        event EventHandler<StaticWebGeneratePageEventArgs> AfterEnsurePageResources;
        /// <summary>
        /// Called before page is written to disk.
        /// StaticWebGeneratePageEventArg is populated with following info: ContentLink, CultureInfo, PageUrl, Content, Resources and FilePath.
        /// This event can be canceled by setting CancelAction to true.
        /// </summary>
        event EventHandler<StaticWebGeneratePageEventArgs> BeforeGeneratePageWrite;
        /// <summary>
        /// Called after page was written to disk.
        /// StaticWebGeneratePageEventArg is populated with following info: ContentLink, CultureInfo, PageUrl, Content, Resources and FilePath.
        /// This event can't be canceled.
        /// </summary>
        event EventHandler<StaticWebGeneratePageEventArgs> AfterGeneratePageWrite;

        event EventHandler<StaticWebIOEvent> AfterIOWrite;
        event EventHandler<StaticWebIOEvent> AfterIODelete;

        void GeneratePage(ContentReference contentReference, IContent content, bool? useTemporaryAttribute, bool ignoreHtmlDependencies);
        void GeneratePage(SiteConfigurationElement configuration, PageData page, CultureInfo lang, bool? useTemporaryAttribute, bool ignoreHtmlDependencies, ConcurrentDictionary<string, string> generatedResources = null);
        void GeneratePage(SiteConfigurationElement configuration, string pageUrl, bool? useTemporaryAttribute, bool ignoreHtmlDependencies, string simpleAddress = null, ConcurrentDictionary<string, string> generatedResources = null);
        List<string> GetUrlsForPage(SiteConfigurationElement configuration, PageData page, CultureInfo lang, out string simpleAddress);
        void GeneratePagesDependingOnBlock(SiteConfigurationElement configuration, ContentReference contentLink, bool? useTemporaryAttribute, bool ignoreHtmlDependencies);
        void GeneratePagesDependingOnContent(SiteConfigurationElement configuration, ContentReference contentReference, bool? useTemporaryAttribute, bool ignoreHtmlDependencies);
        void GeneratePageInAllLanguages(IContentRepository contentRepository, SiteConfigurationElement configuration, PageData page, bool ignoreHtmlDependencies);
        void RemoveGeneratedPage(SiteConfigurationElement configuration, string orginalUrl, bool removeSubFolders = false);
        void RemoveGeneratedPage(SiteConfigurationElement configuration, ContentReference contentLink, CultureInfo language);
        void RemoveGeneratedPage(SiteConfigurationElement configuration, ContentReference contentLink, CultureInfo language, bool removeSubFolders);
        void RemovePageInAllLanguages(IContentRepository contentRepository, SiteConfigurationElement configuration, ContentReference contentReference);
        string EnsureResource(SiteConfigurationElement siteConfiguration, string resourceUrl, Dictionary<string, string> currentPageResourcePairs, ConcurrentDictionary<string, string> replaceResourcePairs, bool? useTemporaryAttribute, bool ignoreHtmlDependencies, int callDepth = 0);
        string GetPageUrl(ContentReference contentLink, CultureInfo language = null);
        void CreateRedirectPages(SiteConfigurationElement configuration, string oldUrl, string newUrl);
        Dictionary<string, string> GetPageLanguageUrls(IContentRepository contentRepository, ContentReference contentReference);
    }
}