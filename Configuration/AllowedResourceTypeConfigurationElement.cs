using System.Configuration;

namespace StaticWebEpiserverPlugin.Configuration
{
    public class AllowedResourceTypeConfigurationElement : ConfigurationElement
    {
        public AllowedResourceTypeConfigurationElement() { }

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
    }
}
