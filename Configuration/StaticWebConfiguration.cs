using EPiServer.Web;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace StaticWebEpiserverPlugin.Configuration
{
    public class StaticWebConfiguration
    {
        protected static StaticWebConfigurationSection _Config = ConfigurationManager.GetSection("staticweb") as StaticWebConfigurationSection;
        public static List<StaticWebSiteConfigurationElement> AvailableSites = new List<StaticWebSiteConfigurationElement>();
        protected static Dictionary<Guid, StaticWebSiteConfigurationElement> _Cache = new Dictionary<Guid, StaticWebSiteConfigurationElement>();

        static StaticWebConfiguration()
        {
            var sites = GetSites();
            foreach (StaticWebSiteConfigurationElement siteElement in sites)
            {
                if (siteElement.Enabled)
                {
                    AvailableSites.Add(siteElement);
                }
            }

            var fallback = GetFallbakConfiguration();
            if (fallback.Enabled)
            {
                AvailableSites.Add(fallback);
            }
        }

        public static StaticWebSiteConfigurationElement GetFallbakConfiguration()
        {
            return new StaticWebSiteConfigurationElement
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

        public static StaticWebSiteConfigurationElementCollection GetSites()
        {
            return _Config.Sites;
        }

        public static StaticWebSiteConfigurationElement Current
        {
            get
            {
                return Get(SiteDefinition.Current);
            }
        }

        public static StaticWebSiteConfigurationElement Get(SiteDefinition definition)
        {
            if (definition == null)
                return null;

            var definitionId = definition.Id;

            StaticWebSiteConfigurationElement currentConfig;
            if (_Cache.TryGetValue(definitionId, out currentConfig))
            {
                return currentConfig;
            }

            var hosts = definition.Hosts.ToList();
            currentConfig = AvailableSites.FirstOrDefault(s => hosts.Any(h => h.Url != null && h.Url.ToString() == s.Url));
            if (currentConfig == null)
            {
                return null;
            }

            _Cache.Add(definitionId, currentConfig);
            return currentConfig;
        }
    }

    public class StaticWebConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("sites")]
        public StaticWebSiteConfigurationElementCollection Sites
        {
            get { return (StaticWebSiteConfigurationElementCollection)this["sites"]; }
        }
    }

    [ConfigurationCollection(typeof(StaticWebSiteConfigurationElement))]
    public class StaticWebSiteConfigurationElementCollection : ConfigurationElementCollection
    {
        public StaticWebSiteConfigurationElement this[int index]
        {
            get { return (StaticWebSiteConfigurationElement)BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                    BaseRemoveAt(index);

                BaseAdd(index, value);
            }
        }
        protected override ConfigurationElement CreateNewElement()
        {
            return new StaticWebSiteConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((StaticWebSiteConfigurationElement)element).Name;
        }
    }

    public class StaticWebSiteConfigurationElement : ConfigurationElement
    {
        public StaticWebSiteConfigurationElement() { }

        [ConfigurationProperty("enabled", DefaultValue = true, IsRequired = false)]
        public bool Enabled
        {
            get {
                bool? config = (bool?)this["enabled"];
                if (config.HasValue ? config.Value : true)
                {
                    return !string.IsNullOrEmpty(OutputPath + ResourceFolder + Url);
                }
                return false;
            }
        }

        [ConfigurationProperty("name", DefaultValue = "", IsKey = true, IsRequired = true)]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("url", DefaultValue = "", IsRequired = true)]
        public string Url
        {
            get { return (string)this["url"]; }
            set { this["url"] = value; }
        }

        [ConfigurationProperty("outputPath", DefaultValue = "", IsRequired = true)]
        public string OutputPath
        {
            get { return (string)this["outputPath"]; }
            set { this["outputPath"] = value; }
        }

        [ConfigurationProperty("resourceFolder", DefaultValue = "", IsRequired = false)]
        public string ResourceFolder
        {
            get { return (string)this["resourceFolder"]; }
            set { this["resourceFolder"] = value; }
        }

        [ConfigurationProperty("useRouting", DefaultValue = false, IsRequired = false)]
        public bool UseRouting
        {
            get {
                bool? config = (bool?)this["useRouting"];
                return config.HasValue ? config.Value : false;
            }
            set { this["useRouting"] = value; }
        }

        [ConfigurationProperty("useHash", DefaultValue = true, IsRequired = false)]
        public bool UseHash
        {
            get {
                bool? config = (bool?)this["useHash"];
                return config.HasValue ? config.Value : true;
            }
            set { this["useHash"] = value; }
        }
        [ConfigurationProperty("useResourceUrl", DefaultValue = false, IsRequired = false)]
        public bool UseResourceUrl
        {
            get {
                bool? config = (bool?)this["useResourceUrl"];
                return config.HasValue ? config.Value : false;
            }
            set { this["useResourceUrl"] = value; }
        }

        private void ValidateResourceNaming()
        {
            if (!UseResourceUrl)
            {
                // One of them needs to be true, use hashing as default option
                UseHash = true;
            }
        }

        protected void ValidateInputUrl()
        {
            if (string.IsNullOrEmpty(Url))
            {
                throw new ArgumentException("Missing value for 'StaticWeb:InputUrl'", "StaticWeb:InputUrl");
            }

            try
            {
                // Try to parse as Uri to validate value
                var testUrl = new Uri(Url);
            }
            catch (Exception)
            {
                throw new ArgumentException("Invalid value for 'StaticWeb:InputUrl'", "StaticWeb:InputUrl");
            }
        }

        protected void ValidateOutputFolder()
        {
            if (string.IsNullOrEmpty(OutputPath))
            {
                throw new ArgumentException("Missing value for 'StaticWeb:OutputFolder'", "StaticWeb:OutputFolder");
            }

            if (!OutputPath.EndsWith("\\"))
            {
                // Make sure it can be combined with _resourcePath
                OutputPath = OutputPath + "\\";
            }


            if (!Directory.Exists(OutputPath))
            {
                throw new ArgumentException("Folder specified in 'StaticWeb:OutputFolder' doesn't exist", "StaticWeb:OutputFolder");
            }

            try
            {
                var directory = new DirectoryInfo(OutputPath);
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

        protected void ValidateResourceFolder()
        {
            if (string.IsNullOrEmpty(ResourceFolder))
            {
                ResourceFolder = "";
            }

            if (!Directory.Exists(OutputPath + ResourceFolder))
            {
                Directory.CreateDirectory(OutputPath + ResourceFolder);
            }

            try
            {
                var directory = new DirectoryInfo(OutputPath + ResourceFolder);
                var directoryName = directory.FullName;

                // Check if it looks like we are in a EpiServer application
                // - if TRUE, throw exception and tell them this is not allowed (resource folder is required to be a subfolder)
                // - if FALSE, continue as usual.
                var appDirectory = new DirectoryInfo(Environment.CurrentDirectory);
                if (directoryName == appDirectory.FullName)
                {
                    throw new ArgumentException($"'StaticWeb:ResourceFolder' can't be the application folder (read: {appDirectory.FullName}). You can change this by setting 'StaticWeb:ResourceFolder", "StaticWeb:ResourceFolder");
                }
                appDirectory = new DirectoryInfo(AppContext.BaseDirectory);
                if (directoryName == appDirectory.FullName)
                {
                    throw new ArgumentException($"'StaticWeb:ResourceFolder' can't be the application folder (read: {appDirectory.FullName}). You can change this by setting 'StaticWeb:ResourceFolder", "StaticWeb:ResourceFolder");
                }

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
                throw new ArgumentException("Not sufficient permissions for folder specified in 'StaticWeb:ResourceFolder'. Require read, write and modify permissions", "StaticWeb:ResourceFolder");
            }
            catch (Exception)
            {
                throw new ArgumentException("Unknown error when testing write, edit and remove access to folder specified in 'StaticWeb:ResourceFolder'", "StaticWeb:ResourceFolder");
            }
        }

    }
}
