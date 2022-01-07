using System;

namespace StaticWebEpiserverPlugin.Models
{
    [Flags]
    public enum ResourceDependencyLookup
    {
        None = 0,
        Html,
        Css
    }
}
