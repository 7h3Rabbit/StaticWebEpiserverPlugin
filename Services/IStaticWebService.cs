using EPiServer.Core;
using System.Collections.Generic;
using System.Globalization;

namespace StaticWebEpiserverPlugin.Services
{
    public interface IStaticWebService
    {
        void GeneratePage(ContentReference contentLink, CultureInfo language, Dictionary<string, string> generatedResources = null);
        void GeneratePagesDependingOnBlock(ContentReference contentLink);
        void RemoveGeneratedPage(ContentReference contentLink, CultureInfo language);
    }
}