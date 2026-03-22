using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using Skua.Core.Models.Skills;
using System.Text;

namespace Skua.Core.ViewModels;

public partial class CBOClassEquipmentViewModel : ObservableObject, IManageCBOptions
{
    public CBOClassEquipmentViewModel(IScriptInventory inventory, IAdvancedSkillContainer advancedSkills)
    {
        _inventory = inventory;
        _advancedSkills = advancedSkills;
    }

    private readonly IScriptInventory _inventory;
    private readonly IAdvancedSkillContainer _advancedSkills;

    /// <summary>
    /// The list of outfit names fetched from the player's in-game Wardrobe.
    /// Populated when the user opens any outfit dropdown or clicks Refresh Outfits.
    /// </summary>
    public List<string> Outfits { get; private set; } = new();

    // ── Solo ────────────────────────────────────────────────

    private string? _selectedSoloOutfit;
    public string? SelectedSoloOutfit
    {
        get => _selectedSoloOutfit;
        set
        {
            if (SetProperty(ref _selectedSoloOutfit, value) && value is not null)
                UpdateModes(value, ref _soloUseModes, ref _selectedSoloUseMode,
                    nameof(SoloUseModes), nameof(SelectedSoloUseMode));
        }
    }

    [ObservableProperty]
    private List<ClassUseMode> _soloUseModes = new() { ClassUseMode.Base };

    [ObservableProperty]
    private ClassUseMode? _selectedSoloUseMode;

    // ── Farm ────────────────────────────────────────────────

    private string? _selectedFarmOutfit;
    public string? SelectedFarmOutfit
    {
        get => _selectedFarmOutfit;
        set
        {
            if (SetProperty(ref _selectedFarmOutfit, value) && value is not null)
                UpdateModes(value, ref _farmUseModes, ref _selectedFarmUseMode,
                    nameof(FarmUseModes), nameof(SelectedFarmUseMode));
        }
    }

    [ObservableProperty]
    private List<ClassUseMode> _farmUseModes = new() { ClassUseMode.Base };

    [ObservableProperty]
    private ClassUseMode? _selectedFarmUseMode;

    // ── Dodge ───────────────────────────────────────────────

    private string? _selectedDodgeOutfit;
    public string? SelectedDodgeOutfit
    {
        get => _selectedDodgeOutfit;
        set
        {
            if (SetProperty(ref _selectedDodgeOutfit, value) && value is not null)
                UpdateModes(value, ref _dodgeUseModes, ref _selectedDodgeUseMode,
                    nameof(DodgeUseModes), nameof(SelectedDodgeUseMode));
        }
    }

    [ObservableProperty]
    private List<ClassUseMode> _dodgeUseModes = new() { ClassUseMode.Base };

    [ObservableProperty]
    private ClassUseMode? _selectedDodgeUseMode;

    // ── Boss ────────────────────────────────────────────────

    private string? _selectedBossOutfit;
    public string? SelectedBossOutfit
    {
        get => _selectedBossOutfit;
        set
        {
            if (SetProperty(ref _selectedBossOutfit, value) && value is not null)
                UpdateModes(value, ref _bossUseModes, ref _selectedBossUseMode,
                    nameof(BossUseModes), nameof(SelectedBossUseMode));
        }
    }

    [ObservableProperty]
    private List<ClassUseMode> _bossUseModes = new() { ClassUseMode.Base };

    [ObservableProperty]
    private ClassUseMode? _selectedBossUseMode;

    // ── Core logic ──────────────────────────────────────────

    /// <summary>
    /// When an outfit is selected, reads the class name from its data (without equipping),
    /// then populates the mode dropdown with every ClassUseMode that has a saved skill set
    /// for that class. Falls back to just Base if nothing is found.
    /// </summary>
    private void UpdateModes(
        string outfitName,
        ref List<ClassUseMode> modeList,
        ref ClassUseMode? selectedMode,
        string modeListPropName,
        string selectedModePropName)
    {
        string? className = _inventory.GetOutfitClassName(outfitName);

        List<ClassUseMode> modes;
        if (!string.IsNullOrEmpty(className))
        {
            HashSet<ClassUseMode> found = new();
            foreach (AdvancedSkill skill in _advancedSkills.LoadedSkills)
            {
                if (string.Equals(skill.ClassName, className, StringComparison.OrdinalIgnoreCase))
                    found.Add(skill.ClassUseMode);
            }
            modes = found.Count > 0
                ? found.OrderBy(m => m).ToList()
                : new List<ClassUseMode> { ClassUseMode.Base };
        }
        else
        {
            // Player not logged in or outfit not found — show all modes
            modes = Enum.GetValues<ClassUseMode>().ToList();
        }

        modeList = modes;
        OnPropertyChanged(modeListPropName);

        // Keep existing selection if it's still valid, otherwise default to first
        if (selectedMode == null || !modes.Contains(selectedMode.Value))
            selectedMode = modes.FirstOrDefault();
        OnPropertyChanged(selectedModePropName);
    }

    [RelayCommand]
    private void RefreshOutfits()
    {
        Outfits = _inventory.GetOutfits();
        OnPropertyChanged(nameof(Outfits));
    }

    // ── Save / Load ─────────────────────────────────────────

    public StringBuilder Save(StringBuilder builder)
    {
        builder.AppendLine($"SoloOutfit: {SelectedSoloOutfit}");
        builder.AppendLine($"SoloOutfitMode: {SelectedSoloUseMode}");
        builder.AppendLine($"FarmOutfit: {SelectedFarmOutfit}");
        builder.AppendLine($"FarmOutfitMode: {SelectedFarmUseMode}");
        builder.AppendLine($"DodgeOutfit: {SelectedDodgeOutfit}");
        builder.AppendLine($"DodgeOutfitMode: {SelectedDodgeUseMode}");
        builder.AppendLine($"BossOutfit: {SelectedBossOutfit}");
        builder.AppendLine($"BossOutfitMode: {SelectedBossUseMode}");
        return builder;
    }

    public void SetValues(Dictionary<string, string> values)
    {
        RestoreOutfit(values, "SoloOutfit",  "SoloOutfitMode",
            v => SelectedSoloOutfit  = v, m => SelectedSoloUseMode  = m,
            ref _soloUseModes,  nameof(SoloUseModes));

        RestoreOutfit(values, "FarmOutfit",  "FarmOutfitMode",
            v => SelectedFarmOutfit  = v, m => SelectedFarmUseMode  = m,
            ref _farmUseModes,  nameof(FarmUseModes));

        RestoreOutfit(values, "DodgeOutfit", "DodgeOutfitMode",
            v => SelectedDodgeOutfit = v, m => SelectedDodgeUseMode = m,
            ref _dodgeUseModes, nameof(DodgeUseModes));

        RestoreOutfit(values, "BossOutfit",  "BossOutfitMode",
            v => SelectedBossOutfit  = v, m => SelectedBossUseMode  = m,
            ref _bossUseModes,  nameof(BossUseModes));

        // Ensure saved outfit names appear in the list even before a Refresh
        Outfits = new[] { SelectedSoloOutfit, SelectedFarmOutfit, SelectedDodgeOutfit, SelectedBossOutfit }
            .Where(o => !string.IsNullOrEmpty(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(o => o, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
        OnPropertyChanged(nameof(Outfits));
    }

    private void RestoreOutfit(
        Dictionary<string, string> values,
        string outfitKey,
        string modeKey,
        Action<string?> setOutfit,
        Action<ClassUseMode?> setMode,
        ref List<ClassUseMode> modeList,
        string modeListPropName)
    {
        if (!values.TryGetValue(outfitKey, out string? outfit) || string.IsNullOrEmpty(outfit))
            return;

        // Set outfit name directly (avoids triggering Flash call during load)
        setOutfit(outfit);

        // Restore saved mode if valid
        if (values.TryGetValue(modeKey, out string? modeStr)
            && Enum.TryParse(modeStr, ignoreCase: true, out ClassUseMode mode))
        {
            if (!modeList.Contains(mode))
            {
                modeList = new List<ClassUseMode>(modeList) { mode };
                modeList.Sort();
                OnPropertyChanged(modeListPropName);
            }
            setMode(mode);
        }
    }
}
