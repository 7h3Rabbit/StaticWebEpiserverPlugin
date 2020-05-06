using EPiServer.Personalization.VisitorGroups;

namespace StaticWebEpiserverPlugin.VisitorGroups
{
    public class StaticWebCriterionSettings : CriterionModelBase
    {
        public override ICriterionModel Copy()
        {
            return ShallowCopy();
        }
    }
}
