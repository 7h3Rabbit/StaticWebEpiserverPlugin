using System;

namespace StaticWebEpiserverPlugin.Models
{
    [Flags]
    public enum GenerateOrderForScheduledJob
    {
        Default = 0,
        UrlDepthFirst,
        UrlBreadthFirst
    }
}
