using EPiServer.Web;
using StaticWebEpiserverPlugin.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace StaticWebEpiserverPlugin.Configuration
{
    public class StaticWebConfiguration
    {
        protected static StaticWebConfigurationSection _Config = ConfigurationManager.GetSection("staticweb") as StaticWebConfigurationSection;
        public static List<SiteConfigurationElement> AvailableSites = new List<SiteConfigurationElement>();
        public static List<AllowedResourceTypeConfigurationElement> AllowedResourceTypes = new List<AllowedResourceTypeConfigurationElement>();
        protected static Dictionary<Guid, SiteConfigurationElement> _Cache = new Dictionary<Guid, SiteConfigurationElement>();

        static StaticWebConfiguration()
        {
            var sites = GetSites();
            foreach (SiteConfigurationElement siteElement in sites)
            {
                if (siteElement.Enabled)
                {
                    AvailableSites.Add(siteElement);
                }
            }

            var fallback = GetFallbakSiteConfiguration();
            if (fallback.Enabled)
            {
                AvailableSites.Add(fallback);
            }

            var allowedTypes = GetAllowedResourceTypes();
            if (!allowedTypes.EmitClear || allowedTypes.Count == 0)
            {
                AllowedResourceTypes.AddRange(GetFallbackAllowedResourceTypes());
            }

            foreach (AllowedResourceTypeConfigurationElement typeElement in allowedTypes)
            {
                AllowedResourceTypes.Add(typeElement);
            }
        }

        public static SiteConfigurationElement GetFallbakSiteConfiguration()
        {
            return new SiteConfigurationElement
            {
                Name = "fallback",
                OutputPath = ConfigurationManager.AppSettings["StaticWeb:OutputFolder"],
                ResourceFolder = ConfigurationManager.AppSettings["StaticWeb:ResourceFolder"],
                Url = ConfigurationManager.AppSettings["StaticWeb:InputUrl"],
                UseRouting = ConfigurationManager.AppSettings["StaticWeb:UseRouting"] == "true"
            };
        }

        public static SiteConfigurationElementCollection GetSites()
        {
            if (_Config == null)
            {
                return new SiteConfigurationElementCollection();
            }
            return _Config.Sites;
        }

        public static string SpecialMimeType = "*";

        public static List<AllowedResourceTypeConfigurationElement> GetFallbackAllowedResourceTypes()
        {
            List<AllowedResourceTypeConfigurationElement> allowedResourceTypes = new List<AllowedResourceTypeConfigurationElement>
            {
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".html", MimeType = "text/html", UseHash = false, UseResourceUrl = true, UseResourceFolder = false, DenendencyLookup = ResourceDependencyLookup.Html | ResourceDependencyLookup.Svg },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".xml", MimeType = "application/xml", UseHash = false, UseResourceUrl = true, UseResourceFolder = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".json", MimeType = "application/json", UseHash = false, UseResourceUrl = true, UseResourceFolder = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".txt", MimeType = "text/plain", UseHash = false, UseResourceUrl = true, UseResourceFolder = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".axd", MimeType = "*.axd", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = "", MimeType = "*", UseResourceFolder = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".css", MimeType = "text/css", UseHash = true, UseResourceUrl = false, DenendencyLookup = ResourceDependencyLookup.Css },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".js", MimeType = "text/javascript", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".js", MimeType = "application/javascript", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".js", MimeType = "application/x-javascript", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".png", MimeType = "image/png", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".jpg", MimeType = "image/jpg", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".jpe", MimeType = "image/jpe", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".jpeg", MimeType = "image/jpeg", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".gif", MimeType = "image/gif", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".ico", MimeType = "image/vnd.microsoft.icon", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".webp", MimeType = "image/webp", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".svg", MimeType = "image/svg+xml", UseHash = true, UseResourceUrl = false, DenendencyLookup = ResourceDependencyLookup.Svg },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".pdf", MimeType = "application/pdf", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".woff", MimeType = "font/woff", UseHash = true, UseResourceUrl = false },
                new AllowedResourceTypeConfigurationElement() { FileExtension = ".woff2", MimeType = "font/woff2", UseHash = true, UseResourceUrl = false }
            };

            return allowedResourceTypes;
        }

        public static AllowedResourceTypeConfigurationElementCollection GetAllowedResourceTypes()
        {
            if (_Config == null)
                return new AllowedResourceTypeConfigurationElementCollection();

            return _Config.AllowedResourceTypes;
        }


        public static SiteConfigurationElement CurrentSite
        {
            get
            {
                return Get(SiteDefinition.Current);
            }
        }

        public static SiteConfigurationElement Get(SiteDefinition definition)
        {
            if (definition == null)
                return null;

            var definitionId = definition.Id;

            if (_Cache.TryGetValue(definitionId, out SiteConfigurationElement currentConfig))
            {
                return currentConfig;
            }

            var hosts = definition.Hosts.ToList();
            currentConfig = AvailableSites.FirstOrDefault(s => hosts.Any(h => h.Url != null && h.Url.ToString() == s.Url + "/"));
            if (currentConfig == null)
            {
                return null;
            }

            _Cache.Add(definitionId, currentConfig);
            return currentConfig;
        }
    }
}
