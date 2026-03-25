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
    public int    Height    => 600;
    public bool   CanResize => true;

    private readonly IWikiService       _wiki;
    private readonly IDispatcherService _dispatcher;

    public WikiViewModel(IWikiService wiki, IDispatcherService dispatcher)
    {
        _wiki       = wiki;
        _dispatcher = dispatcher;
        RefreshStatus();
    }

    [ObservableProperty] private string  _searchQuery  = string.Empty;
    [ObservableProperty] private ObservableCollection<WikiPage> _searchResults = new();
    [ObservableProperty] private string  _statusText   = string.Empty;
    [ObservableProperty] private WikiPage? _selectedPage;
    [ObservableProperty] private IReadOnlyList<WikiSection> _parsedContent = Array.Empty<WikiSection>();

    // ── Derived display properties ────────────────────────────────────────────

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
        ParsedContent = value != null ? ParsePage(value) : Array.Empty<WikiSection>();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    public void NavigateTo(string title)
    {
        var page = _wiki.GetByTitle(title)
                ?? _wiki.SearchTitles(title, 1).FirstOrDefault();
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
            if (!ok)
                StatusText = "Could not find wiki file at %AppData%\\Skua\\aqwwiki_full.json.";
            else
                RefreshStatus();
        });
    }

    [RelayCommand]
    private void Search()
    {
        if (!_wiki.IsLoaded)
        {
            StatusText = "Wiki not loaded — click \"Load Wiki\".";
            return;
        }

        string q = SearchQuery.Trim();
        if (string.IsNullOrEmpty(q)) return;

        var results = _wiki.Search(q, 100);
        SearchResults = new(results);
        StatusText = results.Count == 0
            ? $"No results for \"{q}\""
            : $"{results.Count} result{(results.Count == 1 ? "" : "s")} for \"{q}\"";
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (string.IsNullOrEmpty(PageUrl)) return;
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(PageUrl) { UseShellExecute = true });
    }

    // ── Parsing ───────────────────────────────────────────────────────────────

    private IReadOnlyList<WikiSection> ParsePage(WikiPage page)
    {
        var sections = new List<WikiSection>();
        var lines    = page.Text.Split('\n');

        WikiSection? current = null;

        foreach (var rawLine in lines)
        {
            string line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || line == "-") continue;

            // Lines ending with ":" are section headers
            if (line.EndsWith(':') && !line.StartsWith(' ') && line.Length > 1)
            {
                current = new WikiSection(line[..^1]);
                sections.Add(current);
            }
            else if (current != null)
            {
                bool isLink = _wiki.IsLoaded && _wiki.GetByTitle(line.Trim()) != null;
                current.Items.Add(new WikiItem(line.TrimStart(), isLink));
            }
        }

        return sections;
    }

    private void RefreshStatus()
    {
        StatusText = _wiki.IsLoaded
            ? $"{_wiki.PageCount:N0} pages loaded"
            : "Wiki not loaded — click \"Load Wiki\".";
    }

    // ── Data types ────────────────────────────────────────────────────────────

    public class WikiSection(string heading)
    {
        public string Heading { get; } = heading;
        public List<WikiItem> Items { get; } = new();
    }

    public record WikiItem(string Text, bool IsLink);
}
