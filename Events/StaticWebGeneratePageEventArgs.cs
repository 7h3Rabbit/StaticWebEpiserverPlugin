using EPiServer.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace StaticWebEpiserverPlugin.Events
{
    public class StaticWebGeneratePageEventArgs
    {
        public StaticWebGeneratePageEventArgs(string pageUrl, string simpleAddress = null)
        {
            this.PageUrl = pageUrl;
            this.CurrentResources = new Dictionary<string, string>();
            this.Items = new Dictionary<string, object>();
        }

        public string PageUrl { get; set; }
        public string SimpleAddress { get; set; }
        public Dictionary<string, string> CurrentResources { get; set; }
        public ConcurrentDictionary<string, string> Resources { get; set; }
        public IEnumerable<string> FilePaths { get; set; }
        public string Content { get; set; }
        public bool CancelAction { get; set; }
        public string CancelReason { get; set; }
        public IDictionary Items { get; }
    }
}
