using System.Configuration;

namespace StaticWebEpiserverPlugin.Configuration
{
    [ConfigurationCollection(typeof(AllowedResourceTypeConfigurationElement))]
    public class AllowedResourceTypeConfigurationElementCollection : ConfigurationElementCollection
    {
        public AllowedResourceTypeConfigurationElement this[int index]
        {
            get { return (AllowedResourceTypeConfigurationElement)BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                    BaseRemoveAt(index);

                BaseAdd(index, value);
            }
        }
        protected override ConfigurationElement CreateNewElement()
        {
            return new AllowedResourceTypeConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((AllowedResourceTypeConfigurationElement)element).MimeType;
        }
    }
}
