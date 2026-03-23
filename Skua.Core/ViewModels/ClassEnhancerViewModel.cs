using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Skua.Core.Interfaces;

namespace Skua.Core.ViewModels;

public partial class ClassEnhancerViewModel : ObservableObject, IManagedWindow
{
    // ── IManagedWindow ───────────────────────────────────────────────────────
    public string Title     => "Class Enhancer";
    public int    Width     => 520;
    public int    Height    => 480;
    public bool   CanResize => true;
    private readonly IScriptInterface _bot;
    private readonly IScriptManager   _scripts;

    // Raw data: className -> { modeName -> EnhancementBuild }
    private Dictionary<string, Dictionary<string, EnhancementBuild>> _data = new();

    public ClassEnhancerViewModel(IScriptInterface bot, IScriptManager scripts)
    {
        _bot     = bot;
        _scripts = scripts;
        LoadData();
    }

    // ── Observable Properties ────────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<string> _classes = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Modes))]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private string? _selectedClass;

    public ObservableCollection<string> Modes
    {
        get
        {
            var modes = new ObservableCollection<string>();
            if (SelectedClass != null && _data.TryGetValue(SelectedClass, out var modeMap))
                foreach (var m in modeMap.Keys)
                    modes.Add(m);
            return modes;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private string? _selectedMode;

    public string Preview
    {
        get
        {
            if (SelectedClass == null || SelectedMode == null) return string.Empty;
            if (!_data.TryGetValue(SelectedClass, out var modeMap)) return string.Empty;
            if (!modeMap.TryGetValue(SelectedMode, out var build)) return string.Empty;

            return $"Class  : {build.Class ?? "—"}\n" +
                   $"Weapon : {build.Weapon ?? "—"}\n" +
                   $"Helm   : {build.Helm ?? "—"}\n" +
                   $"Cape   : {build.Cape ?? "—"}";
        }
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task EnhanceAsync()
    {
        if (SelectedClass == null || SelectedMode == null)
        {
            StatusMessage = "Please select a class and mode first.";
            return;
        }

        if (!_data.TryGetValue(SelectedClass, out var modeMap)
            || !modeMap.TryGetValue(SelectedMode, out var build))
        {
            StatusMessage = "Enhancement data not found for this selection.";
            return;
        }

        if (_scripts.ScriptRunning)
        {
            StatusMessage = "A script is already running. Stop it first.";
            return;
        }

        string script = GenerateScript(build);
        string tempPath = GetTempScriptPath();

        try
        {
            await File.WriteAllTextAsync(tempPath, script);
            _scripts.SetLoadedScript(tempPath);
            var error = await _scripts.StartScript();
            StatusMessage = error == null
                ? $"✅ Enhanced {SelectedClass} — {SelectedMode}"
                : $"❌ Error: {error.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task EquipIdealLoadoutAsync()
    {
        if (SelectedClass == null || SelectedMode == null)
        {
            StatusMessage = "Please select a class and mode first.";
            return;
        }

        if (!_data.TryGetValue(SelectedClass, out var modeMap)
            || !modeMap.TryGetValue(SelectedMode, out var build))
        {
            StatusMessage = "Enhancement data not found for this selection.";
            return;
        }

        if (_scripts.ScriptRunning)
        {
            StatusMessage = "A script is already running. Stop it first.";
            return;
        }

        string script = GenerateEquipScript(build, SelectedClass);
        string tempPath = GetTempScriptPath();

        try
        {
            await File.WriteAllTextAsync(tempPath, script);
            _scripts.SetLoadedScript(tempPath);
            var error = await _scripts.StartScript();
            StatusMessage = error == null
                ? $"✅ Equipped ideal loadout for {SelectedClass} — {SelectedMode}"
                : $"❌ Error: {error.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ {ex.Message}";
        }
    }

    // ── Script Generation ────────────────────────────────────────────────────

    private static string GetTempScriptPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Skua", "Scripts", "_ClassEnhancer_Temp.cs");

    private static string GenerateEquipScript(EnhancementBuild build, string className)
    {
        (string eType, string wSpecial, string hSpecial, string cSpecial) = ParseEnhancements(build);

        string classWants  = ToWantsList(eType);
        string helmWants   = ToWantsList(hSpecial, eType);
        string capeWants   = ToWantsList(cSpecial, eType);
        string weaponWants = ToWeaponWantsList(wSpecial, build.Weapon ?? string.Empty);

        return $$"""
/*
name: _ClassEnhancer_Temp
description: Auto-generated by Class Enhancer - Equip Ideal Loadout
tags: null
*/
//cs_include Scripts/CoreBots.cs
//cs_include Scripts/CoreFarms.cs
//cs_include Scripts/CoreAdvanced.cs
using Skua.Core.Interfaces;
using Skua.Core.Models.Items;

public class _ClassEnhancer_Temp
{
    public IScriptInterface Bot => IScriptInterface.Instance;
    public CoreBots Core => CoreBots.Instance;

    public void ScriptMain(IScriptInterface bot)
    {
        Core.SetOptions(disableClassSwap: true);

        Bot.Log($"[Class Enhancer] Equipping ideal loadout for {{className}}");

        string? Enh(int id) => id switch
        {
            1  => "Adventurer",  2  => "Fighter",      3  => "Thief",
            4  => "Armsman",     5  => "Hybrid",        6  => "Wizard",
            7  => "Healer",      8  => "Spellbreaker",  9  => "Lucky",
            10 => "Forge",       11 => "Absolution",    12 => "Avarice",
            23 => "Depths",      24 => "Vainglory",     25 => "Vim",
            26 => "Examen",      27 => "Pneuma",        28 => "Anima",
            29 => "Penitence",   30 => "Lament",        32 => "Hearty",
            _  => null,
        };

        string? WeaponTrait(int id) => id switch
        {
            2  => "Spiral Carve",      3  => "Awe Blast",
            4  => "Health Vamp",       5  => "Mana Vamp",
            6  => "Powerword Die",     7  => "Lacerate",
            8  => "Smite",             9  => "Valiance",
            10 => "Arcana's Concerto", 11 => "Acheron",
            12 => "Elysium",           13 => "Praxis",
            14 => "Dauntless",         15 => "Ravenous",
            _  => null,
        };

        string Norm(string g) => g?.Trim().ToLowerInvariant() switch
        {
            "weapon"                   => "Weapon",
            "helm" or "he"             => "he",
            "back" or "ba" or "cape"   => "ba",
            "class" or "co"            => "co",
            "pet" or "pe"              => "pe",
            _                          => g ?? string.Empty,
        };

        bool MatchWant(InventoryItem i, string want, string grp)
        {
            if (i == null || string.IsNullOrWhiteSpace(want)) return false;
            var pat = Enh(i.EnhancementPatternID);
            if (!string.IsNullOrEmpty(pat) && pat.Equals(want, StringComparison.OrdinalIgnoreCase))
                return true;
            if (grp.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
            {
                var tr = WeaponTrait(i.ProcID);
                if (!string.IsNullOrEmpty(tr) && tr.Equals(want, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        bool TryEquipBest(string slotGroup, params string[] wants)
        {
            string grp = Norm(slotGroup);
            bool mem   = Bot.Player.IsMember == true;

            foreach (var want in wants)
            {
                if (string.IsNullOrWhiteSpace(want)) continue;

                var hit = Bot.Inventory.Items
                    .Where(i => i != null
                        && i.ItemGroup?.Equals(grp, StringComparison.OrdinalIgnoreCase) == true
                        && (!i.Upgrade || mem)
                        && MatchWant(i, want, grp))
                    .OrderByDescending(i => i.EnhancementLevel)
                    .FirstOrDefault();

                if (hit != null)
                {
                    if (Bot.Inventory.IsEquipped(hit.ID))
                    { Bot.Log($"  ✅ {grp}: {hit.Name} ({want}) already equipped"); return true; }
                    for (int t = 0; t < 3 && !Bot.ShouldExit; t++)
                    {
                        Bot.Inventory.EquipItem(hit.ID);
                        Bot.Sleep(500);
                        if (Bot.Inventory.IsEquipped(hit.ID))
                        { Bot.Log($"  ✅ {grp}: {hit.Name} ({want})"); return true; }
                    }
                }

                var fromBank = Bot.Bank.Items
                    .Where(i => i != null
                        && i.ItemGroup?.Equals(grp, StringComparison.OrdinalIgnoreCase) == true
                        && (!i.Upgrade || mem)
                        && MatchWant(i, want, grp))
                    .OrderByDescending(i => i.EnhancementLevel)
                    .FirstOrDefault();

                if (fromBank != null)
                {
                    Bot.Log($"  🏦 {grp}: pulling {fromBank.Name} ({want}) from bank");
                    Bot.Bank.ToInventory(fromBank.Name);
                    Bot.Sleep(800);
                    var pulled = Bot.Inventory.Items.FirstOrDefault(i => i?.ID == fromBank.ID);
                    if (pulled != null)
                        for (int t = 0; t < 3 && !Bot.ShouldExit; t++)
                        {
                            Bot.Inventory.EquipItem(pulled.ID);
                            Bot.Sleep(500);
                            if (Bot.Inventory.IsEquipped(pulled.ID))
                            { Bot.Log($"  ✅ {grp}: {pulled.Name} ({want})"); return true; }
                        }
                }
            }

            Bot.Log($"  ⚠️ {grp}: no match found for [{string.Join(", ", wants.Where(w => !string.IsNullOrWhiteSpace(w)))}]");
            return false;
        }

        TryEquipBest("co",     {{classWants}});  Bot.Sleep(500);
        TryEquipBest("he",     {{helmWants}});   Bot.Sleep(500);
        TryEquipBest("ba",     {{capeWants}});   Bot.Sleep(500);
        TryEquipBest("Weapon", {{weaponWants}});

        Bot.Log("[Class Enhancer] Loadout complete!");
        Core.SetOptions(false);
    }
}
""";
    }

    private static string ToWantsList(params string[] values)
    {
        var quoted = values
            .Where(v => !string.IsNullOrEmpty(v) && v != "None")
            .Distinct()
            .Select(v => $"\"{v}\"");
        return string.Join(", ", quoted);
    }

    private static string ToWeaponWantsList(string wSpecial, string rawWeapon)
    {
        string specialName = wSpecial switch
        {
            "SpiralCarve"    => "Spiral Carve",
            "AweBlast"       => "Awe Blast",
            "HealthVamp"     => "Health Vamp",
            "ManaVamp"       => "Mana Vamp",
            "PowerwordDIE"   => "Powerword Die",
            "Lacerate"       => "Lacerate",
            "Smite"          => "Smite",
            "Valiance"       => "Valiance",
            "ArcanaConcerto" => "Arcana's Concerto",
            "Acheron"        => "Acheron",
            "Elysium"        => "Elysium",
            "Praxis"         => "Praxis",
            "Dauntless"      => "Dauntless",
            "Ravenous"       => "Ravenous",
            _                => string.Empty
        };

        string baseType = string.Empty;
        foreach (var prefix in new[] { "Lucky", "Wizard", "Fighter", "Healer", "Hybrid" })
            if (rawWeapon.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            { baseType = prefix; break; }

        return ToWantsList(specialName, baseType);
    }

    private static string GenerateScript(EnhancementBuild build)
    {
        // Parse the values from the spreadsheet into CoreAdvanced enum values
        (string eType, string wSpecial, string hSpecial, string cSpecial) = ParseEnhancements(build);

        return $$"""
/*
name: _ClassEnhancer_Temp
description: Auto-generated by Class Enhancer
tags: null
*/
//cs_include Scripts/CoreBots.cs
//cs_include Scripts/CoreFarms.cs
//cs_include Scripts/CoreAdvanced.cs
using Skua.Core.Interfaces;

public class _ClassEnhancer_Temp
{
    public IScriptInterface Bot => IScriptInterface.Instance;
    public CoreBots Core => CoreBots.Instance;
    private static CoreAdvanced Adv
    {
        get => _Adv ??= new CoreAdvanced();
        set => _Adv = value;
    }
    private static CoreAdvanced _Adv;

    public void ScriptMain(IScriptInterface bot)
    {
        Core.SetOptions(disableClassSwap: true);
        Adv.EnhanceEquipped(
            EnhancementType.{{eType}},
            CapeSpecial.{{cSpecial}},
            HelmSpecial.{{hSpecial}},
            WeaponSpecial.{{wSpecial}}
        );
        Core.SetOptions(false);
    }
}
""";
    }

    private static (string eType, string wSpecial, string hSpecial, string cSpecial)
        ParseEnhancements(EnhancementBuild build)
    {
        // ── Enhancement Type (class slot) ────────────────────────────────────
        string eType = (build.Class ?? string.Empty).Trim() switch
        {
            var s when s.StartsWith("Wizard",  StringComparison.OrdinalIgnoreCase) => "Wizard",
            var s when s.StartsWith("Fighter", StringComparison.OrdinalIgnoreCase) => "Fighter",
            var s when s.StartsWith("Healer",  StringComparison.OrdinalIgnoreCase) => "Healer",
            var s when s.StartsWith("Hybrid",  StringComparison.OrdinalIgnoreCase) => "Hybrid",
            var s when s.StartsWith("Thief",   StringComparison.OrdinalIgnoreCase) => "Thief",
            _ => "Lucky"
        };

        // ── Weapon Special ───────────────────────────────────────────────────
        // Strip the base type prefix if present (e.g. "Lucky Spiral Carve" → "Spiral Carve")
        string wRaw = StripBaseType(build.Weapon ?? string.Empty);
        string wSpecial = wRaw switch
        {
            var s when Contains(s, "Spiral Carve")    => "SpiralCarve",
            var s when Contains(s, "Awe Blast")        => "AweBlast",
            var s when Contains(s, "Health Vamp")      => "HealthVamp",
            var s when Contains(s, "Mana Vamp")        => "ManaVamp",
            var s when Contains(s, "Powerword")        => "PowerwordDIE",
            var s when Contains(s, "Lacerate")         => "Lacerate",
            var s when Contains(s, "Smite")            => "Smite",
            var s when Contains(s, "Valiance")         => "Valiance",
            var s when Contains(s, "Arcana")           => "ArcanaConcerto",
            var s when Contains(s, "Acheron")          => "Acheron",
            var s when Contains(s, "Elysium")          => "Elysium",
            var s when Contains(s, "Praxis")           => "Praxis",
            var s when Contains(s, "Dauntless")        => "Dauntless",
            var s when Contains(s, "Ravenous")         => "Ravenous",
            _ => "None"
        };

        // ── Helm Special ─────────────────────────────────────────────────────
        string hRaw = (build.Helm ?? string.Empty).Trim();
        string hSpecial = hRaw switch
        {
            var s when Contains(s, "Anima")   => "Anima",
            var s when Contains(s, "Pneuma")  => "Pneuma",
            var s when Contains(s, "Forge")   => "Forge",
            var s when Contains(s, "Vim")     => "Vim",
            var s when Contains(s, "Examen")  => "Examen",
            var s when Contains(s, "Hearty")  => "Hearty",
            _ => "None"
        };

        // ── Cape Special ─────────────────────────────────────────────────────
        string cRaw = (build.Cape ?? string.Empty).Trim();
        string cSpecial = cRaw switch
        {
            var s when Contains(s, "Vainglory")  => "Vainglory",
            var s when Contains(s, "Absolution") => "Absolution",
            var s when Contains(s, "Penitence")  => "Penitence",
            var s when Contains(s, "Lament")     => "Lament",
            var s when Contains(s, "Avarice")    => "Avarice",
            var s when Contains(s, "Forge")      => "Forge",
            _ => "None"
        };

        return (eType, wSpecial, hSpecial, cSpecial);
    }

    // Strip known base type prefixes from weapon strings
    private static string StripBaseType(string raw)
    {
        foreach (var prefix in new[] { "Lucky ", "Wizard ", "Fighter ", "Healer ", "Hybrid ", "Thief ", "Awe " })
            if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return raw[prefix.Length..];
        return raw;
    }

    private static bool Contains(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    // ── Data Loading ─────────────────────────────────────────────────────────

    private void LoadData()
    {
        try
        {
            // Load from embedded resource
            var asm = Assembly.GetExecutingAssembly();
            string resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("ClassEnhancerData.json")) ?? string.Empty;

            string json;
            if (!string.IsNullOrEmpty(resourceName))
            {
                using var stream = asm.GetManifestResourceStream(resourceName)!;
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }
            else
            {
                // Fallback: load from Scripts folder
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Skua", "Scripts", "ClassEnhancerData.json");
                if (!File.Exists(path)) return;
                json = File.ReadAllText(path);
            }

            _data = JsonConvert.DeserializeObject<
                Dictionary<string, Dictionary<string, EnhancementBuild>>>(json) ?? new();

            foreach (var className in _data.Keys.OrderBy(k => k))
                Classes.Add(className);
        }
        catch { /* silently fail — data just won't load */ }
    }

    // ── Data Model ───────────────────────────────────────────────────────────

    public class EnhancementBuild
    {
        [JsonProperty("class")]
        public string? Class { get; set; }
        [JsonProperty("weapon")]
        public string? Weapon { get; set; }
        [JsonProperty("helm")]
        public string? Helm { get; set; }
        [JsonProperty("cape")]
        public string? Cape { get; set; }
    }
}
