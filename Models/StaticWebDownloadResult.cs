using StaticWebEpiserverPlugin.Configuration;

namespace StaticWebEpiserverPlugin.Models
{
    public class StaticWebDownloadResult
    {
        protected AllowedResourceTypeConfigurationElement _typeConfiguration;

        public byte[] Data { get; set; }
        public string ContentType { get; set; }
        public string Extension { get; set; }

        public AllowedResourceTypeConfigurationElement TypeConfiguration
        {
            get
            {
                return _typeConfiguration;
            }
            set
            {
                _typeConfiguration = value;
                Extension = value.FileExtension;
                ContentType = value.MimeType;
            }
        }
    }
}
