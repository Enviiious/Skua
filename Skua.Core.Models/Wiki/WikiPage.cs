namespace Skua.Core.Models.Wiki;

public class WikiPage
{
    public string Slug    { get; set; } = string.Empty;
    public string Title   { get; set; } = string.Empty;
    public string Url     { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Text    { get; set; } = string.Empty;
    public string Html    { get; set; } = string.Empty;
    public List<List<List<string>>> Tables { get; set; } = new();
    public string Scraped { get; set; } = string.Empty;

    /// <summary>Whether this page has been fully loaded (Text/Html) or is just an index entry.</summary>
    public bool IsLoaded => !string.IsNullOrEmpty(Text);
}
