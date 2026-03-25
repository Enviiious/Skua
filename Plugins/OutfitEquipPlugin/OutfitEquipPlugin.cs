using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Models;

/// <summary>
/// Outfit Equip Plugin
///
/// Bridges the gap between the outfit-based CoreBots loadout UI and the
/// actual CoreBots script, which reads class names from CBO_Storage.
///
/// How it works:
///   1. On login and on every script start, reads SoloOutfit/FarmOutfit/
///      DodgeOutfit/BossOutfit from CBO_Storage, resolves the class name
///      for each outfit, and writes SoloClassSelect/FarmClassSelect etc.
///      back to the file so CoreBots finds the right class name when it
///      calls SetOptions().
///
///   2. A background task monitors the player's equipped class every 300ms.
///      When the class changes, it looks up the matching outfit name and
///      sends the equipLoadout packet — equipping the full outfit (armor,
///      cape, weapon, pet, etc.) to match the class CoreBots just swapped to.
/// </summary>
public class OutfitEquipPlugin : ISkuaPlugin
{
    public string Name        => "Outfit Equip";
    public string Author      => "Ty";
    public string Description => "Equips the correct outfit when CoreBots swaps class";
    public List<IOption>? Options => null;

    private IScriptInterface? _bot;
    private string _lastClass = string.Empty;
    private bool _wasLoggedIn = false;

    // Maps class name (lower) -> outfit name, rebuilt on each login/script start
    private readonly Dictionary<string, string> _classToOutfit =
        new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _cts;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void Load(IServiceProvider provider, IPluginHelper helper)
    {
        _bot = provider.GetRequiredService<IScriptInterface>();

        // Update CBO file when player logs in (before any script runs)
        StrongReferenceMessenger.Default.Register<OutfitEquipPlugin, LoginMessage, int>(
            this, (int)MessageChannels.GameEvents,
            (r, _) => r.UpdateCBOAndRebuildMappings());

        // Re-sync before each script run in case CBO settings changed
        StrongReferenceMessenger.Default.Register<OutfitEquipPlugin, ScriptStartedMessage, int>(
            this, (int)MessageChannels.ScriptStatus,
            (r, _) => r.UpdateCBOAndRebuildMappings());

        // Clear last-class on logout so next login triggers a fresh equip
        StrongReferenceMessenger.Default.Register<OutfitEquipPlugin, LogoutMessage, int>(
            this, (int)MessageChannels.GameEvents,
            (r, _) => { r._lastClass = string.Empty; r._wasLoggedIn = false; });

        // Background loop: send outfit packet when class changes
        _cts = new CancellationTokenSource();
        Task.Run(() => ClassMonitorLoop(_cts.Token));

        // Manual refresh button in the Plugins menu
        helper.AddMenuButton("Refresh Outfit Mappings", UpdateCBOAndRebuildMappings);

        // If already logged in when plugin loads, sync immediately
        if (_bot.Player.Playing)
            UpdateCBOAndRebuildMappings();
    }

    public void Unload()
    {
        StrongReferenceMessenger.Default.UnregisterAll(this);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    // ── Core logic ───────────────────────────────────────────────────────────

    /// <summary>
    /// Reads outfit keys from CBO_Storage, resolves class names via
    /// GetOutfitClassName, writes SoloClassSelect / FarmClassSelect /
    /// DodgeClassSelect / BossClassSelect and the matching ModeSelect
    /// back to the file, and rebuilds the class->outfit dictionary.
    /// </summary>
    private void UpdateCBOAndRebuildMappings()
    {
        if (_bot == null || !_bot.Player.Playing)
            return;

        _classToOutfit.Clear();

        string cboPath = GetCBOPath();
        if (!File.Exists(cboPath))
            return;

        List<string> lines = File.ReadAllLines(cboPath).ToList();

        // Build a quick lookup dict from the file (last occurrence wins — tolerates duplicate keys)
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines)
        {
            if (!line.Contains(": ")) continue;
            string[] parts = line.Split(new[] { ": " }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
                dict[parts[0].Trim()] = parts[1].Trim();
        }

        // (outfitKey, classSelectKey, modeSelectKey, modeSourceKey)
        var mappings = new[]
        {
            ("SoloOutfit",  "SoloClassSelect",  "SoloModeSelect",  "SoloOutfitMode"),
            ("FarmOutfit",  "FarmClassSelect",  "FarmModeSelect",  "FarmOutfitMode"),
            ("DodgeOutfit", "DodgeClassSelect", "DodgeModeSelect", "DodgeOutfitMode"),
            ("BossOutfit",  "BossClassSelect",  "BossModeSelect",  "BossOutfitMode"),
        };

        bool changed = false;

        foreach (var (outfitKey, classKey, modeKey, modeSourceKey) in mappings)
        {
            if (!dict.TryGetValue(outfitKey, out string? outfitName)
                || string.IsNullOrEmpty(outfitName))
                continue;

            // Resolve class name from outfit — requires inventory to be loaded
            string? className = _bot.Inventory.GetOutfitClassName(outfitName);
            if (string.IsNullOrEmpty(className))
                continue;

            // Build mapping for background gear equip
            _classToOutfit[className] = outfitName;

            // Write SoloClassSelect = "Chrono ShadowHunter" etc.
            SetLine(lines, classKey, className);

            // Write SoloModeSelect from SoloOutfitMode
            if (dict.TryGetValue(modeSourceKey, out string? mode)
                && !string.IsNullOrEmpty(mode))
                SetLine(lines, modeKey, mode);

            changed = true;
        }

        if (changed)
            File.WriteAllLines(cboPath, lines);
    }

    /// <summary>
    /// Background loop: every 300ms, check if the equipped class changed.
    /// When it does, send the equipLoadout packet for the matching outfit
    /// so that armor, cape, weapon, pet etc. all swap with the class.
    /// </summary>
    private async Task ClassMonitorLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                bool playing = _bot?.Player.Playing == true;

                // Detect fresh login so we sync before the first script
                if (playing && !_wasLoggedIn)
                {
                    _wasLoggedIn = true;
                    UpdateCBOAndRebuildMappings();
                }
                else if (!playing)
                {
                    _wasLoggedIn = false;
                    _lastClass = string.Empty;
                }

                if (playing && _bot != null)
                {
                    string current = _bot.Player.CurrentClass?.Name ?? string.Empty;
                    if (!string.IsNullOrEmpty(current)
                        && !string.Equals(current, _lastClass, StringComparison.OrdinalIgnoreCase))
                    {
                        _lastClass = current;

                        if (_classToOutfit.TryGetValue(current, out string? outfitName))
                            _bot.Inventory.EquipOutfit(outfitName);
                    }
                }
            }
            catch { /* swallow — never crash the background loop */ }

            await Task.Delay(300, ct).ConfigureAwait(false);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void SetLine(List<string> lines, string key, string value)
    {
        string newLine = $"{key}: {value}";
        int idx = lines.FindIndex(l =>
            l.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
            lines[idx] = newLine;
        else
            lines.Add(newLine);
    }

    private string GetCBOPath()
    {
        string username = _bot?.Player.Username ?? string.Empty;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Skua", "options", $"CBO_Storage({username}).txt");
    }
}
