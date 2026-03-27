namespace Skua.Core.Interfaces;

/// <summary>
/// Provides access to AQW wiki data for items, classes, and other game entities.
/// </summary>
public interface IWikiService
{
    /// <summary>
    /// Fetches the wiki URL for the given entity name.
    /// </summary>
    /// <param name="name">The name of the entity to look up.</param>
    /// <returns>A URL string to the wiki page, or <see langword="null"/> if not found.</returns>
    Task<string?> GetWikiUrlAsync(string name);

    /// <summary>
    /// Opens the wiki page for the given entity name in the default browser.
    /// </summary>
    /// <param name="name">The name of the entity to look up.</param>
    Task OpenWikiPageAsync(string name);
}
