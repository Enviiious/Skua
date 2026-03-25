using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using Skua.Core.Models.Wiki;
using System.Collections.ObjectModel;

namespace Skua.Core.ViewModels;

public partial class WikiViewModel : ObservableObject, IManagedWindow
{
    public string Title     => "Wiki Browser";
    public int    Width     => 960;
    public int    Height    => 620;
    public bool   CanResize => true;

    private readonly IWikiService       _wiki;
    private readonly IDispatcherService _dispatcher;

    public WikiViewModel(IWikiService wiki, IDispatcherService dispatcher)
    {
        _wiki       = wiki;
        _dispatcher = dispatcher;
        // Update status when background auto-load (fired from App startup) completes
        wiki.Loaded += (_, _) => _dispatcher.Invoke(RefreshStatus);
        RefreshStatus();
    }

    [ObservableProperty] private string  _searchQuery  = string.Empty;
    [ObservableProperty] private ObservableCollection<WikiPage> _searchResults = new();
    [ObservableProperty] private string  _statusText   = string.Empty;
    [ObservableProperty] private WikiPage? _selectedPage;

    public string PageTitle => SelectedPage?.Title ?? string.Empty;
    public string PageTags  => SelectedPage?.Tags is { Count: > 0 } t
                                  ? string.Join("  •  ", t)
                                  : string.Empty;
    public string PageUrl   => SelectedPage?.Url ?? string.Empty;
    public bool   HasPage   => SelectedPage != null;

    partial void OnSelectedPageChanged(WikiPage? value)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageTags));
        OnPropertyChanged(nameof(PageUrl));
        OnPropertyChanged(nameof(HasPage));
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>Navigate by page title (used by in-app hyperlinks).</summary>
    public void NavigateTo(string title)
    {
        var page = _wiki.GetByTitle(title)
                ?? _wiki.SearchTitles(title, 1).FirstOrDefault();
        if (page != null)
            _dispatcher.Invoke(() => SelectedPage = page);
    }

    /// <summary>Navigate by slug (used when intercepting wiki URL clicks).</summary>
    public void NavigateToSlug(string slug)
    {
        var page = _wiki.GetBySlug(slug);
        if (page != null)
            _dispatcher.Invoke(() => SelectedPage = page);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadWikiAsync()
    {
        StatusText = "Loading wiki…";
        bool ok = await _wiki.LoadAsync();
        _dispatcher.Invoke(() =>
        {
            StatusText = ok
                ? $"{_wiki.PageCount:N0} pages loaded"
                : "Could not find wiki file at %AppData%\\Skua\\aqwwiki_full.json.";
        });
    }

    [RelayCommand]
    private void Search()
    {
        if (!_wiki.IsLoaded)
        {
            StatusText = "Wiki not loaded — loading in background, try again shortly.";
            return;
        }

        string q = SearchQuery.Trim();
        if (string.IsNullOrEmpty(q)) return;

        // Title-only search, ranked: exact match → starts-with → contains
        var all = _wiki.SearchTitles(q, 200);

        var exact      = new List<Skua.Core.Models.Wiki.WikiPage>();
        var startsWith = new List<Skua.Core.Models.Wiki.WikiPage>();
        var contains   = new List<Skua.Core.Models.Wiki.WikiPage>();

        foreach (var p in all)
        {
            if (p.Title.Equals(q, StringComparison.OrdinalIgnoreCase))
                exact.Add(p);
            else if (p.Title.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                startsWith.Add(p);
            else
                contains.Add(p);
        }

        var ranked = exact.Concat(startsWith).Concat(contains).ToList();
        SearchResults = new(ranked);
        StatusText = ranked.Count == 0
            ? $"No results for \"{q}\""
            : $"{ranked.Count} result{(ranked.Count == 1 ? "" : "s")} for \"{q}\"";
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (string.IsNullOrEmpty(PageUrl)) return;
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(PageUrl) { UseShellExecute = true });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshStatus()
    {
        StatusText = _wiki.IsLoaded
            ? $"{_wiki.PageCount:N0} pages loaded"
            : "Wiki not loaded — click \"Load Wiki\".";
    }
}
