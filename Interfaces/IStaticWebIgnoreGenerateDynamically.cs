namespace StaticWebEpiserverPlugin.Interfaces
{
    public interface IStaticWebIgnoreGenerateDynamically
    {
        bool ShouldGenerate();
        bool ShouldDeleteGenerated();
    }
}