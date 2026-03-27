using Skua.Core.Interfaces;

namespace Skua.Core.Services;

public class WikiService : IWikiService
{
    private const string _wikiBaseUrl = "https://aqwwiki.wikidot.com/";

    public Task<string?> GetWikiUrlAsync(string name)
    {
        string slug = name.ToLowerInvariant().Replace(' ', '-');
        return Task.FromResult<string?>(_wikiBaseUrl + slug);
    }

    public Task OpenWikiPageAsync(string name)
    {
        string slug = name.ToLowerInvariant().Replace(' ', '-');
        string url = _wikiBaseUrl + slug;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
