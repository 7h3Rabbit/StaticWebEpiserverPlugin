using System.Configuration;

namespace StaticWebEpiserverPlugin.Configuration
{
    public class StaticWebConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("sites")]
        public SiteConfigurationElementCollection Sites
        {
            get { return (SiteConfigurationElementCollection)this["sites"]; }
        }

        [ConfigurationProperty("allowedResourceTypes")]
        public AllowedResourceTypeConfigurationElementCollection AllowedResourceTypes
        {
            get { return (AllowedResourceTypeConfigurationElementCollection)this["allowedResourceTypes"]; }
        }

    }
}
