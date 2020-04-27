namespace StaticWebEpiserverPlugin.Models
{
    public class StaticWebDownloadResult
    {
        public byte[] Data { get; set; }
        public string ContentType { get; set; }
        public string Extension { get; set; }
    }
}
