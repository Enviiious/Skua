using Skua.Core.Models.Wiki;

namespace Skua.Core.Interfaces;

/// <summary>
/// Provides query access to the locally-loaded AQW Wiki JSON.
/// Set the path to aqwwiki_full.json in Skua Settings → Wiki Path.
/// All methods return null / empty if the wiki is not loaded.
/// </summary>
public interface IWikiService
{
    // ── State ─────────────────────────────────────────────────────────────

    /// <summary>Whether the wiki JSON has been successfully loaded.</summary>
    bool IsLoaded { get; }

    /// <summary>Fired on the thread pool when LoadAsync completes successfully.</summary>
    event EventHandler? Loaded;

    /// <summary>Total number of pages in the loaded wiki.</summary>
    int PageCount { get; }

    /// <summary>Path to the aqwwiki_full.json file currently loaded.</summary>
    string? LoadedPath { get; }

    // ── Loading ───────────────────────────────────────────────────────────

    /// <summary>Load (or reload) the wiki from the given file path.</summary>
    Task<bool> LoadAsync(string path);

    /// <summary>Load from the default path (%appdata%\Skua\aqwwiki_full.json).</summary>
    Task<bool> LoadAsync();

    // ── Direct Lookup ─────────────────────────────────────────────────────

    /// <summary>Get a page by its exact slug (e.g. "acheron", "tempest-dracolich").</summary>
    WikiPage? GetBySlug(string slug);

    /// <summary>Get a page by its exact title.</summary>
    WikiPage? GetByTitle(string title);

    // ── Search ────────────────────────────────────────────────────────────

    /// <summary>Full-text search across all page titles and text. Returns up to <paramref name="limit"/> results.</summary>
    List<WikiPage> Search(string query, int limit = 10);

    /// <summary>Search only page titles.</summary>
    List<WikiPage> SearchTitles(string query, int limit = 10);

    /// <summary>Get all pages with a specific tag.</summary>
    List<WikiPage> GetByTag(string tag);

    /// <summary>Get all pages matching multiple tags (AND logic).</summary>
    List<WikiPage> GetByTags(IEnumerable<string> tags);

    // ── Enhancements ──────────────────────────────────────────────────────

    /// <summary>Get the wiki page for a named enhancement (e.g. "Acheron", "Dauntless").</summary>
    WikiPage? GetEnhancement(string name);

    /// <summary>Get all enhancement pages.</summary>
    List<WikiPage> GetAllEnhancements();

    /// <summary>
    /// Extract the special skill description lines from an enhancement page.
    /// Returns the bullet-point effects list.
    /// </summary>
    List<string> GetEnhancementEffects(string name);

    // ── Quests ────────────────────────────────────────────────────────────

    /// <summary>Get the wiki page for a quest by name.</summary>
    WikiPage? GetQuest(string name);

    /// <summary>Get all pages tagged as quests.</summary>
    List<WikiPage> GetAllQuests();

    /// <summary>
    /// Find which monster and map drop a given item name.
    /// Returns list of (monsterName, mapName) tuples parsed from quest pages.
    /// </summary>
    List<(string Monster, string Map)> FindDropSource(string itemName);

    // ── Monsters ──────────────────────────────────────────────────────────

    /// <summary>Get the wiki page for a monster by name.</summary>
    WikiPage? GetMonster(string name);

    /// <summary>Get all pages tagged as monsters.</summary>
    List<WikiPage> GetAllMonsters();

    /// <summary>
    /// Find what maps a monster appears in.
    /// Parsed from the monster's wiki page location field.
    /// </summary>
    List<string> GetMonsterLocations(string monsterName);

    /// <summary>
    /// Get items dropped by a monster.
    /// Parsed from the monster's wiki page drop table.
    /// </summary>
    List<string> GetMonsterDrops(string monsterName);

    // ── Items ─────────────────────────────────────────────────────────────

    /// <summary>Get the wiki page for an item by name.</summary>
    WikiPage? GetItem(string name);

    /// <summary>Get all pages tagged as items.</summary>
    List<WikiPage> GetAllItems();

    /// <summary>
    /// Find where to obtain an item (shop, quest, drop, merge).
    /// Parsed from the item's wiki page location field.
    /// </summary>
    string GetItemSource(string itemName);
}
