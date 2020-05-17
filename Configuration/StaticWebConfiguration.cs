using EPiServer.Web;
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
                UseHash = ConfigurationManager.AppSettings["StaticWeb:UseContentHash"] == "true",
                UseResourceUrl = ConfigurationManager.AppSettings["StaticWeb:UseResourceUrl"] == "true",
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

        public static List<AllowedResourceTypeConfigurationElement> GetFallbackAllowedResourceTypes()
        {
            List<AllowedResourceTypeConfigurationElement> allowedResourceTypes = new List<AllowedResourceTypeConfigurationElement>();

            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".css", MimeType = "text/css" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".js", MimeType = "text/javascript" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".js", MimeType = "application/javascript" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".js", MimeType = "application/x-javascript" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".png", MimeType = "image/png" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".jpg", MimeType = "image/jpg" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".jpe", MimeType = "image/jpe" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".jpeg", MimeType = "image/jpeg" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".gif", MimeType = "image/gif" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".ico", MimeType = "image/vnd.microsoft.icon" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".webp", MimeType = "image/webp" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".svg", MimeType = "image/svg+xml" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".pdf", MimeType = "application/pdf" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".woff", MimeType = "font/woff" });
            allowedResourceTypes.Add(new AllowedResourceTypeConfigurationElement() { FileExtension = ".woff2", MimeType = "font/woff2" });

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

            SiteConfigurationElement currentConfig;
            if (_Cache.TryGetValue(definitionId, out currentConfig))
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
