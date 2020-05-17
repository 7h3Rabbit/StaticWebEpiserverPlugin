using System.Configuration;

namespace StaticWebEpiserverPlugin.Configuration
{
    [ConfigurationCollection(typeof(SiteConfigurationElement))]
    public class SiteConfigurationElementCollection : ConfigurationElementCollection
    {
        public SiteConfigurationElement this[int index]
        {
            get { return (SiteConfigurationElement)BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                    BaseRemoveAt(index);

                BaseAdd(index, value);
            }
        }
        protected override ConfigurationElement CreateNewElement()
        {
            return new SiteConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((SiteConfigurationElement)element).Name;
        }
    }
}
