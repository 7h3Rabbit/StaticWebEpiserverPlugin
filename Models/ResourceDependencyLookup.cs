using System;

namespace StaticWebEpiserverPlugin.Models
{
    [Flags]
    public enum ResourceDependencyLookup
    {
        None = 0,
        Html = 1,
        Css = 2,
        Svg = 4
    }
}
