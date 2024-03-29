﻿using StaticWebEpiserverPlugin.Models;
using System.Configuration;

namespace StaticWebEpiserverPlugin.Configuration
{
    public class AllowedResourceTypeConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("fileExtension", DefaultValue = "", IsRequired = true)]
        public string FileExtension
        {
            get { return (string)this["fileExtension"]; }
            set { this["fileExtension"] = value; }
        }

        [ConfigurationProperty("mimeType", DefaultValue = "", IsRequired = true, IsKey = true)]
        public string MimeType
        {
            get { return (string)this["mimeType"]; }
            set { this["mimeType"] = value; }
        }

        [ConfigurationProperty("useResourceUrl", DefaultValue = false, IsRequired = false)]
        public bool UseResourceUrl
        {
            get
            {
                bool? config = (bool?)this["useResourceUrl"];
                return config ?? false;
            }
            set { this["useResourceUrl"] = value; }
        }

        [ConfigurationProperty("useResourceFolder", DefaultValue = true, IsRequired = false)]
        public bool UseResourceFolder
        {
            get
            {
                bool? config = (bool?)this["useResourceFolder"];
                return config ?? true;
            }
            set { this["useResourceFolder"] = value; }
        }


        [ConfigurationProperty("useHash", DefaultValue = true, IsRequired = false)]
        public bool UseHash
        {
            get
            {
                bool? config = (bool?)this["useHash"];
                return config ?? true;
            }
            set { this["useHash"] = value; }
        }

        [ConfigurationProperty("defaultName", DefaultValue = "index", IsRequired = false)]
        public string DefaultName
        {
            get { return (string)this["defaultName"]; }
            set { this["defaultName"] = value; }
        }

        [ConfigurationProperty("denendencyLookup", DefaultValue = ResourceDependencyLookup.None, IsRequired = false)]
        public ResourceDependencyLookup DenendencyLookup
        {
            get { return (ResourceDependencyLookup)this["denendencyLookup"]; }
            set { this["denendencyLookup"] = value; }
        }


        public AllowedResourceTypeConfigurationElement()
        {
            if (!ValidateResourceNaming())
            {
                throw new ConfigurationErrorsException($"One of the properties {nameof(UseHash)} and {nameof(UseResourceUrl)} has to be 'True'.");
            }
        }

        private bool ValidateResourceNaming()
        {
            if (!UseResourceUrl && !UseHash)
            {
                // One of them needs to be true, use hashing as default option
                //UseHash = true;
                return false;
            }
            return true;
        }
    }
}
