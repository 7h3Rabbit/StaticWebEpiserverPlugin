using EPiServer.Core;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace StaticWebEpiserverPlugin.Events
{
    public class StaticWebGeneratePageEventArgs
    {
        public StaticWebGeneratePageEventArgs(ContentReference contentLink, CultureInfo cultureInfo, string pageUrl)
        {
            this.ContentLink = contentLink;
            this.CultureInfo = cultureInfo;
            this.PageUrl = pageUrl;
            this.CurrentResources = new Dictionary<string, string>();
        }

        public ContentReference ContentLink { get; set; }
        public CultureInfo CultureInfo { get; set; }
        public string PageUrl { get; set; }
        public Dictionary<string, string> CurrentResources { get; set; }
        public Dictionary<string, string> Resources { get; set; }
        public string FilePath { get; set; }
        public string Content { get; set; }
        public bool CancelAction { get; set; }
        public string CancelReason { get; set; }
        public IDictionary Items { get; }
    }
}
