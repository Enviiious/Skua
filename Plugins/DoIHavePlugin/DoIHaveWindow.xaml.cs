using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace DoIHavePlugin_NS;

// ── Value converters ──────────────────────────────────────────────────────────

/// <summary>Converts bool → "✓" / "✗"</summary>
public sealed class BoolToCheckConverter : IValueConverter
{
    public static readonly BoolToCheckConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "✓" : "✗";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts a hex color string to a SolidColorBrush.</summary>
public sealed class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(s);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch { }
        }
        return System.Windows.Media.Brushes.Gray;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverts a bool.</summary>
public sealed class NotBoolConverter : IValueConverter
{
    public static readonly NotBoolConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>Converts bool → Visibility, with optional inversion via ConverterParameter="Inverse".</summary>
public sealed class BoolToVisConverter : IValueConverter
{
    public static readonly BoolToVisConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is true;
        if (parameter is string s && s == "Inverse") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Entry window ──────────────────────────────────────────────────────────────

public partial class DoIHaveWindow : Window
{
    public DoIHaveWindow(IScriptInterface bot)
    {
        InitializeComponent();
        DataContext = new DoIHaveViewModel(bot);
    }
}

// ── Data models ───────────────────────────────────────────────────────────────

public class WikiItemData
{
    public string Name          { get; set; } = "";
    public string Slug          { get; set; } = "";
    public string Category      { get; set; } = "";  // armors / swords / etc.
    public string PriceDisplay  { get; set; } = "";  // "100,000 Gold" / "800 AC" / "Merge"
    public bool   IsRare        { get; set; }
    public bool   IsAC          { get; set; }
    public bool   IsLegend      { get; set; }
    public bool   IsSeasonal    { get; set; }
    public bool   IsPseudoRare  { get; set; }
    public string LocationLine  { get; set; } = "";  // "Shop @ Map"
    public string Description   { get; set; } = "";
    public string FlagDisplay   { get; set; } = "";  // "AC · Legend"
}

public class MergeShopData
{
    public string Name         { get; set; } = "";
    public string Location     { get; set; } = "";
    public string Npc          { get; set; } = "";
    public bool   IsRare       { get; set; }
    public bool   IsPseudoRare { get; set; }
    public List<MergeShopTab> Tabs { get; set; } = new();

    // Flat list of all items across all tabs (built once after parsing)
    public List<MergeItemData> AllItems { get; set; } = new();
}

public class MergeShopTab
{
    public string Name { get; set; } = "";
    public List<MergeItemData> Items { get; set; } = new();
}

public class MergeItemData
{
    public string Name     { get; set; } = "";
    public string TypeIcon { get; set; } = "";
    public List<MergeIngredient> Ingredients { get; set; } = new();
}

public class MergeIngredient
{
    public string Name { get; set; } = "";
    public int    Qty  { get; set; }
}

// ── Search result view-model ──────────────────────────────────────────────────

public class SearchResultVm
{
    public string Name         { get; init; } = "";
    public string Category     { get; init; } = "";
    public string Flags        { get; init; } = "";
    public string Price        { get; init; } = "";
    public string Location     { get; init; } = "";
    public string OwnedText    { get; init; } = "";
    public string OwnedColor   { get; init; } = "#888888";
    public string WikiUrl      { get; init; } = "";
}

// ── Merge shop view-model (for list binding) ──────────────────────────────────

public partial class MergeShopVm : ObservableObject
{
    public string Name         { get; init; } = "";
    public string Location     { get; init; } = "";
    public string Npc          { get; init; } = "";
    public string Tags         { get; init; } = "";
    public List<MergeShopTab> Tabs { get; init; } = new();
    public List<MergeItemData> AllItems { get; init; } = new();

    [ObservableProperty] private ObservableCollection<MergeItemVm> _displayItems = new();
}

public class MergeItemVm : ObservableObject
{
    public string Name            { get; init; } = "";
    public string TypeIcon        { get; init; } = "";
    public string IngredientsText { get; set; } = "";
    public bool   CanCraft        { get; set; }
    public string CraftColor      => CanCraft ? "#4CAF50" : "#CF6679";
}

// ── Bank item view-model ──────────────────────────────────────────────────────

public class BankItemVm
{
    public string Name     { get; init; } = "";
    public int    Quantity { get; init; }
    public bool   IsAC     { get; init; }
    public string ACLabel  => IsAC ? "AC" : "";
}

// ── Main ViewModel ────────────────────────────────────────────────────────────

public partial class DoIHaveViewModel : ObservableObject
{
    private readonly IScriptInterface _bot;

    // Inventory caches (name → qty), refreshed by user
    private Dictionary<string, int> _invCache  = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _bankCache = new(StringComparer.OrdinalIgnoreCase);

    // Loaded data
    private Dictionary<string, WikiItemData>? _wikiItems;
    private List<MergeShopData>?               _mergeShops;
    private bool _dataLoading;

    // Debounce timer for search
    private readonly DispatcherTimer _searchDebounce;

    public DoIHaveViewModel(IScriptInterface bot)
    {
        _bot = bot;

        _searchDebounce = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            _ = ExecuteSearchAsync();
        };

        StatusText = "Ready. Type an item name to search, or open a tab.";
        _ = EnsureDataLoadedAsync();
        _ = AutoLoadAsync();
    }

    private async Task AutoLoadAsync()
    {
        if (!_bot.Player.Playing) return;

        IsLoading  = true;
        StatusText = "Loading inventory…";

        try
        {
            await Task.Run(() =>
            {
                var inv = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in _bot.Inventory.Items)
                    inv[item.Name] = item.Quantity;
                _invCache = inv;
            });

            StatusText = $"Inventory loaded ({_invCache.Count:N0} items). Loading bank…";

            await Task.Run(() =>
            {
                _bot.Bank.Load(waitForLoad: true);
                var bank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in _bot.Bank.Items)
                    bank[item.Name] = item.Quantity;
                _bankCache = bank;
            });

            FilterBankItems();
            StatusText = $"Ready — {_invCache.Count:N0} inventory · {_bankCache.Count:N0} bank items loaded.";
        }
        catch (Exception ex)
        {
            StatusText = $"Auto-load partial: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Observable state ──────────────────────────────────────────────────────

    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private string _mergeSearchQuery = "";
    [ObservableProperty] private string _bankSearchQuery = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool   _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedShop))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedShop))]
    private MergeShopVm? _selectedShop;

    public bool HasSelectedShop   => SelectedShop is not null;
    public bool HasNoSelectedShop => SelectedShop is null;

    public ObservableCollection<SearchResultVm> SearchResults { get; } = new();
    public ObservableCollection<MergeShopVm>    FilteredShops { get; } = new();
    public ObservableCollection<BankItemVm>     BankItems     { get; } = new();

    // ── Property change reactions ──────────────────────────────────────────────

    partial void OnSearchQueryChanged(string value)
    {
        _searchDebounce.Stop();
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            return;
        }
        _searchDebounce.Start();
    }

    partial void OnMergeSearchQueryChanged(string value)
    {
        FilterMergeShops();
    }

    partial void OnBankSearchQueryChanged(string value)
    {
        FilterBankItems();
    }

    partial void OnSelectedShopChanged(MergeShopVm? value)
    {
        if (value is null) return;
        RefreshShopItems(value);
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    private static string SkuaDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skua");

    private static string MergeShopsPath =>
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "data", "merge_shops.json");

    private async Task EnsureDataLoadedAsync()
    {
        if (_wikiItems is not null && _mergeShops is not null) return;
        if (_dataLoading) return;
        _dataLoading = true;

        IsLoading  = true;
        StatusText = "Loading item database…";

        try
        {
            var (wiki, shops) = await Task.Run(() =>
            {
                var w = LoadWikiItems();
                var s = LoadMergeShops();
                return (w, s);
            });

            _wikiItems  = wiki;
            _mergeShops = shops;

            int wikiCount = wiki?.Count ?? 0;
            int shopCount = shops?.Count ?? 0;

            var parts = new List<string>();
            if (wikiCount > 0) parts.Add($"{wikiCount:N0} items");
            if (shopCount > 0) parts.Add($"{shopCount:N0} merge shops");

            if (parts.Count == 0)
            {
                StatusText = $"⚠ aqwwiki_full.json not found in {SkuaDir}";
            }
            else
            {
                StatusText = $"Loaded {string.Join(" · ", parts)}. Ready.";
                BuildMergeShopVms();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading data: {ex.Message}";
        }
        finally
        {
            IsLoading    = false;
            _dataLoading = false;
        }
    }

    // Reads aqwwiki_full.json from %AppData%\Skua\ — the same file Skua's wiki plugin uses.
    // Format: { "slug": { title, slug, url, tags: [...], text: "..." } }
    private static Dictionary<string, WikiItemData>? LoadWikiItems()
    {
        string path = Path.Combine(SkuaDir, "aqwwiki_full.json");
        if (!File.Exists(path)) return null;

        var result = new Dictionary<string, WikiItemData>(StringComparer.OrdinalIgnoreCase);

        using var stream = File.OpenRead(path);
        using var doc    = JsonDocument.Parse(stream);

        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var item = ParseWikiEntry(entry.Name, entry.Value);
            if (item is not null)
                result[item.Name] = item;
        }

        return result;
    }

    private static WikiItemData? ParseWikiEntry(string slug, JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;

        var item = new WikiItemData
        {
            Slug = slug,
            Name = SlugToName(slug),
        };

        // Tags → flags + category
        if (el.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tagsEl.EnumerateArray())
                if (t.ValueKind == JsonValueKind.String)
                    tags.Add(t.GetString() ?? "");

            item.IsAC        = tags.Contains("ac");
            item.IsRare      = tags.Contains("rare");
            item.IsLegend    = tags.Contains("legend");
            item.IsSeasonal  = tags.Contains("seasonal");
            item.IsPseudoRare = tags.Contains("pseudo-rare");
            item.Category    = InferCategory(tags);
        }

        // Raw wiki text → location, price, description
        if (el.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
        {
            string text = textEl.GetString() ?? "";
            item.LocationLine = ParseTextField(text, "Location", maxLines: 2);
            item.PriceDisplay = ParseTextField(text, "Price",    maxLines: 1);
            item.Description  = ParseTextField(text, "Description", maxLines: 1);
        }

        var flags = new List<string>();
        if (item.IsAC)         flags.Add("AC");
        if (item.IsLegend)     flags.Add("Legend");
        if (item.IsRare)       flags.Add("Rare");
        if (item.IsPseudoRare) flags.Add("Pseudo-Rare");
        if (item.IsSeasonal)   flags.Add("Seasonal");
        item.FlagDisplay = string.Join(" · ", flags);

        return item;
    }

    /// <summary>Converts "void-highlord-armor" → "Void Highlord Armor"</summary>
    private static string SlugToName(string slug)
    {
        var sb = new System.Text.StringBuilder(slug.Length + 4);
        bool cap = true;
        foreach (char c in slug)
        {
            if (c == '-') { sb.Append(' '); cap = true; }
            else if (cap) { sb.Append(char.ToUpperInvariant(c)); cap = false; }
            else          { sb.Append(c); }
        }
        return sb.ToString();
    }

    private static string InferCategory(HashSet<string> tags)
    {
        if (tags.Contains("armor"))   return "armor";
        if (tags.Contains("class"))   return "class";
        if (tags.Contains("sword"))   return "sword";
        if (tags.Contains("dagger"))  return "dagger";
        if (tags.Contains("staff"))   return "staff";
        if (tags.Contains("wand"))    return "wand";
        if (tags.Contains("axe"))     return "axe";
        if (tags.Contains("mace"))    return "mace";
        if (tags.Contains("polearm")) return "polearm";
        if (tags.Contains("gun"))     return "gun";
        if (tags.Contains("bow"))     return "bow";
        if (tags.Contains("cape"))    return "cape";
        if (tags.Contains("helm"))    return "helm";
        if (tags.Contains("hat"))     return "helm";
        if (tags.Contains("pet"))     return "pet";
        if (tags.Contains("misc"))    return "misc";
        if (tags.Contains("house"))   return "house";
        return "";
    }

    /// <summary>
    /// Extracts the value of a named field from raw wiki text.
    /// Text format: "FieldName:\nvalue line 1\n-\nvalue line 2\n..."
    /// </summary>
    private static string ParseTextField(string text, string fieldName, int maxLines)
    {
        string marker = fieldName + ":";
        int idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";

        var lines  = text[(idx + marker.Length)..].Split('\n');
        var parts  = new List<string>(maxLines);

        foreach (var raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line == "-") continue;

            // Stop when we hit the next field header (short line ending with ":")
            if (line.EndsWith(':') && line.Length < 25 && !line.Contains('(')) break;

            parts.Add(line);
            if (parts.Count >= maxLines) break;
        }

        return string.Join("  ·  ", parts);
    }

    private static List<MergeShopData>? LoadMergeShops()
    {
        string path = MergeShopsPath;
        if (!File.Exists(path)) return null;

        var result = new List<MergeShopData>();

        using var stream = File.OpenRead(path);
        using var doc    = JsonDocument.Parse(stream);

        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var shop = ParseMergeShop(entry.Value);
            if (shop is not null)
                result.Add(shop);
        }

        return result;
    }

    private static MergeShopData? ParseMergeShop(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;

        var shop = new MergeShopData();
        if (el.TryGetProperty("name", out var n))     shop.Name     = n.GetString() ?? "";
        if (el.TryGetProperty("rare", out var r))     shop.IsRare   = r.ValueKind == JsonValueKind.True;
        if (el.TryGetProperty("pseudo_rare", out var pr)) shop.IsPseudoRare = pr.ValueKind == JsonValueKind.True;

        if (el.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object)
            if (loc.TryGetProperty("name", out var ln)) shop.Location = ln.GetString() ?? "";

        if (el.TryGetProperty("npc", out var npc) && npc.ValueKind == JsonValueKind.Object)
            if (npc.TryGetProperty("name", out var nn)) shop.Npc = nn.GetString() ?? "";

        if (el.TryGetProperty("tabs", out var tabs) && tabs.ValueKind == JsonValueKind.Array)
        {
            foreach (var tabEl in tabs.EnumerateArray())
            {
                var tab = new MergeShopTab();
                if (tabEl.TryGetProperty("name", out var tn)) tab.Name = tn.GetString() ?? "";

                if (tabEl.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var itemEl in items.EnumerateArray())
                    {
                        var mi = ParseMergeItem(itemEl);
                        if (mi is not null) tab.Items.Add(mi);
                    }
                }

                if (tab.Items.Count > 0)
                    shop.Tabs.Add(tab);
            }
        }

        // Build flat list
        foreach (var tab in shop.Tabs)
            shop.AllItems.AddRange(tab.Items);

        return shop.AllItems.Count > 0 ? shop : null;
    }

    private static MergeItemData? ParseMergeItem(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        var mi = new MergeItemData();
        if (el.TryGetProperty("name", out var n))      mi.Name     = n.GetString() ?? "";
        if (el.TryGetProperty("type_icon", out var t)) mi.TypeIcon = t.GetString() ?? "";

        if (el.TryGetProperty("ingredients", out var ings) && ings.ValueKind == JsonValueKind.Array)
        {
            foreach (var ing in ings.EnumerateArray())
            {
                var ingData = new MergeIngredient();
                if (ing.TryGetProperty("name", out var inm)) ingData.Name = inm.GetString() ?? "";
                if (ing.TryGetProperty("qty",  out var iq))  ingData.Qty  = iq.GetInt32();
                if (!string.IsNullOrEmpty(ingData.Name))
                    mi.Ingredients.Add(ingData);
            }
        }

        return string.IsNullOrEmpty(mi.Name) ? null : mi;
    }

    // ── Build merge-shop VMs ──────────────────────────────────────────────────

    private void BuildMergeShopVms()
    {
        if (_mergeShops is null) return;

        FilteredShops.Clear();
        foreach (var s in _mergeShops)
        {
            var vm = new MergeShopVm
            {
                Name     = s.Name,
                Location = s.Location,
                Npc      = s.Npc,
                Tags     = BuildShopTags(s),
                Tabs     = s.Tabs,
                AllItems = s.AllItems,
            };
            FilteredShops.Add(vm);
        }
    }

    private static string BuildShopTags(MergeShopData s)
    {
        var tags = new List<string>();
        if (s.IsRare)       tags.Add("Rare");
        if (s.IsPseudoRare) tags.Add("Pseudo-Rare");
        return string.Join(" · ", tags);
    }

    private void FilterMergeShops()
    {
        if (_mergeShops is null) return;
        string q = MergeSearchQuery.Trim();

        FilteredShops.Clear();
        foreach (var s in _mergeShops)
        {
            if (!string.IsNullOrEmpty(q)
                && !s.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !s.Location.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredShops.Add(new MergeShopVm
            {
                Name     = s.Name,
                Location = s.Location,
                Npc      = s.Npc,
                Tags     = BuildShopTags(s),
                Tabs     = s.Tabs,
                AllItems = s.AllItems,
            });
        }
        // DisplayItems are populated lazily when a shop is selected
    }

    private void RefreshShopItems(MergeShopVm vm)
    {
        vm.DisplayItems.Clear();
        foreach (var mi in vm.AllItems)
        {
            bool canCraft = true;
            var  parts    = new List<string>();

            foreach (var ing in mi.Ingredients)
            {
                int have = _invCache.TryGetValue(ing.Name, out var q) ? q : 0;
                if (_bankCache.TryGetValue(ing.Name, out var bq)) have += bq;
                parts.Add($"{ing.Name} {have}/{ing.Qty}");
                if (have < ing.Qty) canCraft = false;
            }

            vm.DisplayItems.Add(new MergeItemVm
            {
                Name            = mi.Name,
                TypeIcon        = mi.TypeIcon,
                IngredientsText = string.Join("  |  ", parts),
                CanCraft        = canCraft,
            });
        }
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private async Task ExecuteSearchAsync()
    {
        if (_wikiItems is null)
        {
            await EnsureDataLoadedAsync();
            if (_wikiItems is null) return;
        }

        string q = SearchQuery.Trim();
        if (string.IsNullOrEmpty(q)) return;

        IsLoading = true;

        var results = await Task.Run(() =>
        {
            var list = new List<SearchResultVm>();
            int count = 0;

            foreach (var kvp in _wikiItems)
            {
                if (!kvp.Key.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                if (count >= 150) break;
                count++;

                var wiki = kvp.Value;

                _invCache.TryGetValue(kvp.Key, out int invQty);
                _bankCache.TryGetValue(kvp.Key, out int bankQty);

                string ownedText;
                string ownedColor;
                if (invQty > 0 && bankQty > 0)
                {
                    ownedText  = $"✓ Inv ×{invQty}  ·  Bank ×{bankQty}";
                    ownedColor = "#4CAF50";
                }
                else if (invQty > 0)
                {
                    ownedText  = $"✓ In Inventory  ×{invQty}";
                    ownedColor = "#4CAF50";
                }
                else if (bankQty > 0)
                {
                    ownedText  = $"✓ In Bank  ×{bankQty}";
                    ownedColor = "#2196F3";
                }
                else
                {
                    ownedText  = "Not Owned";
                    ownedColor = "#888888";
                }

                list.Add(new SearchResultVm
                {
                    Name       = wiki.Name,
                    Category   = wiki.Category,
                    Flags      = wiki.FlagDisplay,
                    Price      = wiki.PriceDisplay,
                    Location   = wiki.LocationLine,
                    OwnedText  = ownedText,
                    OwnedColor = ownedColor,
                    WikiUrl    = $"https://aqwwiki.wikidot.com{wiki.Slug}",
                });
            }

            return list;
        });

        SearchResults.Clear();
        foreach (var r in results)
            SearchResults.Add(r);

        int total = _wikiItems.Count(kvp =>
            kvp.Key.Contains(q, StringComparison.OrdinalIgnoreCase));
        StatusText = results.Count == 0
            ? $"No results for \"{q}\"."
            : total > 150
                ? $"Showing first 150 of {total:N0} matches for \"{q}\"."
                : $"{total:N0} result(s) for \"{q}\".";

        IsLoading = false;
    }

    // ── Inventory refresh ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshInventoryAsync()
    {
        if (!_bot.Player.Playing)
        {
            StatusText = "Not logged in — please log in first.";
            return;
        }

        IsLoading  = true;
        StatusText = "Refreshing inventory…";

        await Task.Run(() =>
        {
            var inv = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _bot.Inventory.Items)
                inv[item.Name] = item.Quantity;
            _invCache = inv;
        });

        StatusText = $"Inventory refreshed — {_invCache.Count:N0} items.";
        IsLoading  = false;

        // Re-run search with new ownership data
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            await ExecuteSearchAsync();

        // Refresh selected shop items
        if (SelectedShop is not null)
            RefreshShopItems(SelectedShop);
    }

    // ── Bank ──────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadBankAsync()
    {
        if (!_bot.Player.Playing)
        {
            StatusText = "Not logged in — please log in first.";
            return;
        }

        IsLoading  = true;
        StatusText = "Loading bank…";

        try
        {
            await Task.Run(() =>
            {
                _bot.Bank.Load(waitForLoad: true);
                var bank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in _bot.Bank.Items)
                    bank[item.Name] = item.Quantity;
                _bankCache = bank;
            });

            StatusText = $"Bank loaded — {_bankCache.Count:N0} items.";
            FilterBankItems();

            // Refresh search/shop with bank data
            if (!string.IsNullOrWhiteSpace(SearchQuery))
                await ExecuteSearchAsync();

            if (SelectedShop is not null)
                RefreshShopItems(SelectedShop);
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading bank: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void FilterBankItems()
    {
        string q = BankSearchQuery.Trim();
        BankItems.Clear();

        foreach (var kvp in _bankCache)
        {
            if (!string.IsNullOrEmpty(q) && !kvp.Key.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;

            bool isAC = false;
            if (_wikiItems?.TryGetValue(kvp.Key, out var wd) == true)
                isAC = wd.IsAC;

            BankItems.Add(new BankItemVm
            {
                Name     = kvp.Key,
                Quantity = kvp.Value,
                IsAC     = isAC,
            });
        }
    }

    // ── Open wiki URL ─────────────────────────────────────────────────────────

    [RelayCommand]
    private static void OpenWiki(SearchResultVm? result)
    {
        if (result is null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = result.WikiUrl,
                UseShellExecute = true
            });
        }
        catch { /* ignore */ }
    }
}
