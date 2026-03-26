using System.IO.Compression;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Skua.Core.Interfaces;
using Skua.Core.Models.Wiki;

namespace Skua.Core.Services;

public class WikiService : IWikiService
{
    // ── State ─────────────────────────────────────────────────────────────

    public bool    IsLoaded   { get; private set; }
    public int     PageCount  => _bySlug.Count;
    public string? LoadedPath { get; private set; }

    public event EventHandler? Loaded;

    // ── Indexes ───────────────────────────────────────────────────────────

    private Dictionary<string, WikiPage>        _bySlug  = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, WikiPage>        _byTitle = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<WikiPage>>  _byTag   = new(StringComparer.OrdinalIgnoreCase);

    // Pre-built category lists (avoid full-scan at runtime)
    private List<WikiPage> _allEnhancements = new();
    private List<WikiPage> _allItems        = new();
    private List<WikiPage> _allMonsters     = new();
    private List<WikiPage> _allQuests       = new();

    // Prefer the compressed file; fall back to plain JSON for backwards compatibility
    private static readonly string DefaultPathGz   = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Skua", "aqwwiki_full.json.gz");
    private static readonly string DefaultPathJson  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Skua", "aqwwiki_full.json");
    private static readonly string DefaultPath = DefaultPathGz;

    // Tag sets for categorisation
    private static readonly HashSet<string> EnhancementTags = new(StringComparer.OrdinalIgnoreCase)
        { "enhancement", "forge-enhancement", "weapon-enhancement" };
    private static readonly HashSet<string> QuestTags = new(StringComparer.OrdinalIgnoreCase)
        { "quest" };
    private static readonly HashSet<string> MonsterTags = new(StringComparer.OrdinalIgnoreCase)
        { "monster" };
    private static readonly HashSet<string> ItemTags = new(StringComparer.OrdinalIgnoreCase)
        { "item", "armor", "helm", "cape", "weapon", "sword", "dagger", "staff",
          "bow", "axe", "mace", "wand", "gun", "polearm", "pet" };

    // ── Loading ───────────────────────────────────────────────────────────

    public Task<bool> LoadAsync()
    {
        // Prefer compressed; fall back to plain JSON so existing installs keep working
        string path = File.Exists(DefaultPathGz) ? DefaultPathGz : DefaultPathJson;
        return LoadAsync(path);
    }

    public async Task<bool> LoadAsync(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            // Run all IO + parsing on a background thread so the UI stays responsive.
            // Stream the JSON instead of reading 400MB into a string — this halves peak
            // memory usage and avoids the large LOH allocation.
            var result = await Task.Run(() => ParseFile(path)).ConfigureAwait(false);
            if (result == null) return false;

            _bySlug          = result.BySlug;
            _byTitle         = result.ByTitle;
            _byTag           = result.ByTag;
            _allEnhancements = result.Enhancements;
            _allItems        = result.Items;
            _allMonsters     = result.Monsters;
            _allQuests       = result.Quests;
            LoadedPath       = path;
            IsLoaded         = true;
            Loaded?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class ParseResult
    {
        public Dictionary<string, WikiPage>       BySlug       = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, WikiPage>       ByTitle      = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<WikiPage>> ByTag        = new(StringComparer.OrdinalIgnoreCase);
        public List<WikiPage>                     Enhancements = new();
        public List<WikiPage>                     Items        = new();
        public List<WikiPage>                     Monsters     = new();
        public List<WikiPage>                     Quests       = new();
    }

    private ParseResult? ParseFile(string path)
    {
        var r          = new ParseResult();
        var serializer = new JsonSerializer();

        // 64 KB read buffer + SequentialScan hint for large sequential files.
        // For .gz files, decompress on-the-fly through GZipStream — no temp file needed.
        bool isGzip = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        using var fs  = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                       bufferSize: 65536, FileOptions.SequentialScan);
        using var dec = isGzip ? new GZipStream(fs, CompressionMode.Decompress) : (Stream)fs;
        using var sr  = new StreamReader(dec, System.Text.Encoding.UTF8,
                                         detectEncodingFromByteOrderMarks: true,
                                         bufferSize: 65536, leaveOpen: false);
        using var jr  = new JsonTextReader(sr) { CloseInput = false };

        // Expect the root to be an object: { "slug": { page }, ... }
        if (!jr.Read() || jr.TokenType != JsonToken.StartObject)
            return null;

        while (jr.Read() && jr.TokenType != JsonToken.EndObject)
        {
            if (jr.TokenType != JsonToken.PropertyName) continue;
            string slug = (string)jr.Value!;

            jr.Read(); // move into the page object
            var raw = serializer.Deserialize<RawPage>(jr);
            if (raw == null) continue;

            var page = new WikiPage
            {
                Slug   = slug,
                // The JSON title field is always the slug (e.g. "void-of-nulgath").
                // Always convert to a readable title via SlugToTitle.
                Title  = SlugToTitle(!string.IsNullOrWhiteSpace(raw.Title) ? raw.Title : slug),
                Url    = raw.Url    ?? $"http://aqwwiki.wikidot.com/{slug}",
                Tags   = raw.Tags   ?? new(),
                Text   = raw.Text   ?? string.Empty,
                Html   = raw.Html   ?? string.Empty,
                Tables = raw.Tables ?? new(),
            };

            r.BySlug[slug] = page;
            if (!r.ByTitle.ContainsKey(page.Title))
                r.ByTitle[page.Title] = page;

            bool isEnh = false, isItem = false, isMon = false, isQst = false;

            foreach (var tag in page.Tags)
            {
                if (!r.ByTag.TryGetValue(tag, out var bucket))
                    r.ByTag[tag] = bucket = new List<WikiPage>();
                bucket.Add(page);

                if (EnhancementTags.Contains(tag)) isEnh  = true;
                if (ItemTags.Contains(tag))         isItem = true;
                if (MonsterTags.Contains(tag))      isMon  = true;
                if (QuestTags.Contains(tag))        isQst  = true;
            }

            if (!isEnh && IsEnhancement(page)) isEnh = true;

            if (isEnh)  r.Enhancements.Add(page);
            if (isItem) r.Items.Add(page);
            if (isMon)  r.Monsters.Add(page);
            if (isQst)  r.Quests.Add(page);
        }

        return r;
    }

    // ── Direct Lookup ─────────────────────────────────────────────────────

    public WikiPage? GetBySlug(string slug)
        => _bySlug.TryGetValue(slug, out var p) ? p : null;

    public WikiPage? GetByTitle(string title)
        => _byTitle.TryGetValue(title, out var p) ? p : null;

    // ── Search ────────────────────────────────────────────────────────────

    public List<WikiPage> Search(string query, int limit = 10)
    {
        if (!IsLoaded || string.IsNullOrWhiteSpace(query)) return new();

        string q = query.Trim();

        // Single-pass: collect title and text matches separately to rank title first.
        // Evaluate title-match once per page (no double Contains).
        var titleMatches = new List<WikiPage>();
        var textMatches  = new List<WikiPage>();

        foreach (var page in _bySlug.Values)
        {
            bool inTitle = page.Title.Contains(q, StringComparison.OrdinalIgnoreCase);
            if (inTitle)
            {
                titleMatches.Add(page);
            }
            else if (page.Text.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                textMatches.Add(page);
            }

            // Early-exit once we have plenty of title matches
            if (titleMatches.Count >= limit) break;
        }

        var result = new List<WikiPage>(limit);
        result.AddRange(titleMatches.Take(limit));
        if (result.Count < limit)
            result.AddRange(textMatches.Take(limit - result.Count));
        return result;
    }

    public List<WikiPage> SearchTitles(string query, int limit = 10)
    {
        if (!IsLoaded || string.IsNullOrWhiteSpace(query)) return new();

        string q = query.Trim();
        var results = new List<WikiPage>(limit);

        foreach (var page in _bySlug.Values)
        {
            if (page.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(page);
                if (results.Count == limit) break;
            }
        }

        return results;
    }

    public List<WikiPage> GetByTag(string tag)
        => _byTag.TryGetValue(tag, out var list) ? list : new();

    public List<WikiPage> GetByTags(IEnumerable<string> tags)
    {
        if (!IsLoaded) return new();

        // Use tag index intersection: start with smallest bucket, filter against rest
        string[]? tagArray = tags as string[] ?? tags.ToArray();
        if (tagArray.Length == 0) return new();

        List<WikiPage>? smallest = null;
        string? smallestTag = null;

        foreach (var t in tagArray)
        {
            if (_byTag.TryGetValue(t, out var bucket))
            {
                if (smallest == null || bucket.Count < smallest.Count)
                {
                    smallest    = bucket;
                    smallestTag = t;
                }
            }
            else return new(); // No pages have this tag at all
        }

        if (smallest == null) return new();

        // Filter the smallest bucket against the remaining tags
        var otherTags = tagArray.Where(t => !string.Equals(t, smallestTag, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (otherTags.Length == 0) return new List<WikiPage>(smallest);

        return smallest
            .Where(p => otherTags.All(t => p.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    // ── Enhancements ──────────────────────────────────────────────────────

    public WikiPage? GetEnhancement(string name)
    {
        var page = GetByTitle(name)
                ?? GetBySlug(name.ToLower().Replace(" ", "-"));

        if (page != null && IsEnhancement(page)) return page;

        return _allEnhancements
            .FirstOrDefault(p => p.Title.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    public List<WikiPage> GetAllEnhancements() => _allEnhancements;

    public List<string> GetEnhancementEffects(string name)
    {
        var page = GetEnhancement(name);
        if (page == null) return new();
        return ExtractBulletPoints(page.Text);
    }

    private static bool IsEnhancement(WikiPage p)
        => p.Tags.Any(t => EnhancementTags.Contains(t))
        || p.Text.Contains("Special skill on", StringComparison.OrdinalIgnoreCase)
        || p.Text.Contains("forge weapon enhancement", StringComparison.OrdinalIgnoreCase);

    // ── Quests ────────────────────────────────────────────────────────────

    public WikiPage? GetQuest(string name)
        => GetByTitle(name)
        ?? _allQuests.FirstOrDefault(p =>
               p.Title.Contains(name, StringComparison.OrdinalIgnoreCase));

    public List<WikiPage> GetAllQuests() => _allQuests;

    public List<(string Monster, string Map)> FindDropSource(string itemName)
    {
        if (!IsLoaded) return new();

        var results = new List<(string, string)>();

        var candidates = Search(itemName, 20);
        foreach (var page in candidates)
        {
            var matches = Regex.Matches(page.Text,
                @"[Dd]ropped? by ([^\n\r/]+?) (?:in |at )?/?([\w]+)",
                RegexOptions.IgnoreCase);

            foreach (Match m in matches)
            {
                string monster = m.Groups[1].Value.Trim();
                string map     = m.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(monster) && !string.IsNullOrEmpty(map))
                    results.Add((monster, map));
            }

            foreach (var table in page.Tables)
                foreach (var row in table)
                {
                    if (row.Any(cell => cell.Contains(itemName, StringComparison.OrdinalIgnoreCase)))
                    {
                        string combined = string.Join(" ", row);
                        var mm = Regex.Match(combined, @"([\w\s]+?) in /?([\w]+)");
                        if (mm.Success)
                            results.Add((mm.Groups[1].Value.Trim(), mm.Groups[2].Value.Trim()));
                    }
                }
        }

        return results.Distinct().ToList();
    }

    // ── Monsters ──────────────────────────────────────────────────────────

    public WikiPage? GetMonster(string name)
        => GetByTitle(name)
        ?? GetByTitle(name + " (Monster)")
        ?? _allMonsters.FirstOrDefault(p =>
               p.Title.Contains(name, StringComparison.OrdinalIgnoreCase));

    public List<WikiPage> GetAllMonsters() => _allMonsters;

    public List<string> GetMonsterLocations(string monsterName)
    {
        var page = GetMonster(monsterName);
        if (page == null) return new();

        var locations = new List<string>();
        var matches   = Regex.Matches(page.Text,
            @"[Ll]ocation[s]?:?\s*([^\n\r]+)",
            RegexOptions.IgnoreCase);

        foreach (Match m in matches)
            locations.Add(m.Groups[1].Value.Trim());

        return locations.Distinct().ToList();
    }

    public List<string> GetMonsterDrops(string monsterName)
    {
        var page = GetMonster(monsterName);
        if (page == null) return new();

        var drops   = new List<string>();
        var matches = Regex.Matches(page.Text,
            @"[Dd]rop[s]?:?\s*([^\n\r]+)",
            RegexOptions.IgnoreCase);

        foreach (Match m in matches)
            drops.Add(m.Groups[1].Value.Trim());

        foreach (var table in page.Tables)
            foreach (var row in table)
                if (row.Count > 0 && !string.IsNullOrWhiteSpace(row[0]))
                    drops.Add(row[0].Trim());

        return drops.Distinct().ToList();
    }

    // ── Items ─────────────────────────────────────────────────────────────

    public WikiPage? GetItem(string name)
        => GetByTitle(name)
        ?? _allItems.FirstOrDefault(p =>
               p.Title.Contains(name, StringComparison.OrdinalIgnoreCase));

    public List<WikiPage> GetAllItems() => _allItems;

    public string GetItemSource(string itemName)
    {
        var page = GetItem(itemName);
        if (page == null) return string.Empty;

        var m = Regex.Match(page.Text,
            @"[Ll]ocation[s]?:?\s*([^\n\r]+)",
            RegexOptions.IgnoreCase);

        return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Convert "diamond-of-nulgath" → "Diamond Of Nulgath" when no title is in the JSON.</summary>
    private static string SlugToTitle(string slug) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo
              .ToTitleCase(slug.Replace('-', ' ').Replace('_', ' '));

    private static List<string> ExtractBulletPoints(string text)
    {
        var results = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            string t = line.Trim();
            if (t.StartsWith("-") || t.StartsWith("•") || t.StartsWith("*"))
            {
                string item = t.TrimStart('-', '•', '*', ' ');
                if (!string.IsNullOrWhiteSpace(item))
                    results.Add(item);
            }
        }
        return results;
    }

    // ── Raw deserialization model ─────────────────────────────────────────

    private class RawPage
    {
        [JsonProperty("title")]  public string?                  Title  { get; set; }
        [JsonProperty("url")]    public string?                  Url    { get; set; }
        [JsonProperty("tags")]   public List<string>?            Tags   { get; set; }
        [JsonProperty("text")]   public string?                  Text   { get; set; }
        [JsonProperty("html")]   public string?                  Html   { get; set; }
        [JsonProperty("tables")] public List<List<List<string>>>? Tables { get; set; }
        // "scraped" intentionally omitted — not used anywhere, saves parse time
    }
}
