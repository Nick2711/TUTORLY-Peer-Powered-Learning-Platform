namespace Tutorly.Server.Services
{
    public interface IContentFilterService
    {
        Task<ContentFilterResult> CheckContentAsync(string content);
    }

    public class ContentFilterResult
    {
        public bool IsClean { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> BlockedWords { get; set; } = new();
    }
}

