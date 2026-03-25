using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using Skua.Core.Models.Items;
using Skua.Core.Models.Skills;
using System.Text;

namespace Skua.Core.ViewModels;

/// <summary>
/// Handles equipment for both modes:
///   Classic (per-item) — Helm/Armor/Cape/Weapon/Pet/GroundItem per scenario
///   Outfit (Wardrobe)  — Wardrobe outfit name + ClassUseMode per scenario
/// Both sets of data are always saved/loaded so switching modes never loses data.
/// </summary>
public partial class CBOClassEquipmentViewModel : ObservableObject, IManageCBOptions
{
    public CBOClassEquipmentViewModel(IScriptInventory inventory, IAdvancedSkillContainer advancedSkills)
    {
        _inventory      = inventory;
        _advancedSkills = advancedSkills;
    }

    private readonly IScriptInventory       _inventory;
    private readonly IAdvancedSkillContainer _advancedSkills;

    // ══════════════════════════════════════════════════════════════════════════
    //  CLASSIC MODE — per-item equipment lists
    // ══════════════════════════════════════════════════════════════════════════

    public List<string> Helms       { get; private set; } = new();
    public List<string> Armors      { get; private set; } = new();
    public List<string> Capes       { get; private set; } = new();
    public List<string> Weapons     { get; private set; } = new();
    public List<string> Pets        { get; private set; } = new();
    public List<string> GroundItems { get; private set; } = new();

    // Solo
    [ObservableProperty] private string? _selectedSoloHelm;
    [ObservableProperty] private string? _selectedSoloArmor;
    [ObservableProperty] private string? _selectedSoloCape;
    [ObservableProperty] private string? _selectedSoloWeapon;
    [ObservableProperty] private string? _selectedSoloPet;
    [ObservableProperty] private string? _selectedSoloGroundItem;

    // Farm
    [ObservableProperty] private string? _selectedFarmHelm;
    [ObservableProperty] private string? _selectedFarmArmor;
    [ObservableProperty] private string? _selectedFarmCape;
    [ObservableProperty] private string? _selectedFarmWeapon;
    [ObservableProperty] private string? _selectedFarmPet;
    [ObservableProperty] private string? _selectedFarmGroundItem;

    // Dodge
    [ObservableProperty] private string? _selectedDodgeHelm;
    [ObservableProperty] private string? _selectedDodgeArmor;
    [ObservableProperty] private string? _selectedDodgeCape;
    [ObservableProperty] private string? _selectedDodgeWeapon;
    [ObservableProperty] private string? _selectedDodgePet;
    [ObservableProperty] private string? _selectedDodgeGroundItem;

    // Boss
    [ObservableProperty] private string? _selectedBossHelm;
    [ObservableProperty] private string? _selectedBossArmor;
    [ObservableProperty] private string? _selectedBossCape;
    [ObservableProperty] private string? _selectedBossWeapon;
    [ObservableProperty] private string? _selectedBossPet;
    [ObservableProperty] private string? _selectedBossGroundItem;

    [RelayCommand]
    private void RefreshInventory()
    {
        Helms       = _inventory.Items?.Where(i => i.Category == ItemCategory.Helm   && i.EnhancementLevel > 0).Select(i => i.Name).OrderBy(n => n).ToList() ?? new();
        Armors      = _inventory.Items?.Where(i => i.Category == ItemCategory.Armor                          ).Select(i => i.Name).OrderBy(n => n).ToList() ?? new();
        Capes       = _inventory.Items?.Where(i => i.Category == ItemCategory.Cape   && i.EnhancementLevel > 0).Select(i => i.Name).OrderBy(n => n).ToList() ?? new();
        Weapons     = _inventory.Items?.Where(i => i.ItemGroup == "Weapon"            && i.EnhancementLevel > 0).Select(i => i.Name).OrderBy(n => n).ToList() ?? new();
        Pets        = _inventory.Items?.Where(i => i.Category == ItemCategory.Pet                            ).Select(i => i.Name).OrderBy(n => n).ToList() ?? new();
        GroundItems = _inventory.Items?.Where(i => i.Category == ItemCategory.Misc                           ).Select(i => i.Name).OrderBy(n => n).ToList() ?? new();

        OnPropertyChanged(nameof(Helms));
        OnPropertyChanged(nameof(Armors));
        OnPropertyChanged(nameof(Capes));
        OnPropertyChanged(nameof(Weapons));
        OnPropertyChanged(nameof(Pets));
        OnPropertyChanged(nameof(GroundItems));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  OUTFIT MODE — wardrobe outfit + ClassUseMode per scenario
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Wardrobe outfit names fetched from the player's inventory.</summary>
    public List<string> Outfits { get; private set; } = new();

    // Solo
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
    [ObservableProperty] private List<ClassUseMode> _soloUseModes      = new() { ClassUseMode.Base };
    [ObservableProperty] private ClassUseMode?      _selectedSoloUseMode;

    // Farm
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
    [ObservableProperty] private List<ClassUseMode> _farmUseModes      = new() { ClassUseMode.Base };
    [ObservableProperty] private ClassUseMode?      _selectedFarmUseMode;

    // Dodge
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
    [ObservableProperty] private List<ClassUseMode> _dodgeUseModes     = new() { ClassUseMode.Base };
    [ObservableProperty] private ClassUseMode?      _selectedDodgeUseMode;

    // Boss
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
    [ObservableProperty] private List<ClassUseMode> _bossUseModes      = new() { ClassUseMode.Base };
    [ObservableProperty] private ClassUseMode?      _selectedBossUseMode;

    [RelayCommand]
    private void RefreshOutfits()
    {
        Outfits = _inventory.GetOutfits();
        OnPropertyChanged(nameof(Outfits));
    }

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
                if (string.Equals(skill.ClassName, className, StringComparison.OrdinalIgnoreCase))
                    found.Add(skill.ClassUseMode);

            modes = found.Count > 0 ? found.OrderBy(m => m).ToList() : new() { ClassUseMode.Base };
        }
        else
        {
            modes = Enum.GetValues<ClassUseMode>().ToList();
        }

        modeList = modes;
        OnPropertyChanged(modeListPropName);

        if (selectedMode == null || !modes.Contains(selectedMode.Value))
            selectedMode = modes.FirstOrDefault();
        OnPropertyChanged(selectedModePropName);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Save / Load — both modes always persisted
    // ══════════════════════════════════════════════════════════════════════════

    public StringBuilder Save(StringBuilder builder)
    {
        // Classic per-item (keys: Helm1Select … GroundItem4Select)
        builder.AppendLine($"Helm1Select: {SelectedSoloHelm}");
        builder.AppendLine($"Armor1Select: {SelectedSoloArmor}");
        builder.AppendLine($"Cape1Select: {SelectedSoloCape}");
        builder.AppendLine($"Weapon1Select: {SelectedSoloWeapon}");
        builder.AppendLine($"Pet1Select: {SelectedSoloPet}");
        builder.AppendLine($"GroundItem1Select: {SelectedSoloGroundItem}");

        builder.AppendLine($"Helm2Select: {SelectedFarmHelm}");
        builder.AppendLine($"Armor2Select: {SelectedFarmArmor}");
        builder.AppendLine($"Cape2Select: {SelectedFarmCape}");
        builder.AppendLine($"Weapon2Select: {SelectedFarmWeapon}");
        builder.AppendLine($"Pet2Select: {SelectedFarmPet}");
        builder.AppendLine($"GroundItem2Select: {SelectedFarmGroundItem}");

        builder.AppendLine($"Helm3Select: {SelectedDodgeHelm}");
        builder.AppendLine($"Armor3Select: {SelectedDodgeArmor}");
        builder.AppendLine($"Cape3Select: {SelectedDodgeCape}");
        builder.AppendLine($"Weapon3Select: {SelectedDodgeWeapon}");
        builder.AppendLine($"Pet3Select: {SelectedDodgePet}");
        builder.AppendLine($"GroundItem3Select: {SelectedDodgeGroundItem}");

        builder.AppendLine($"Helm4Select: {SelectedBossHelm}");
        builder.AppendLine($"Armor4Select: {SelectedBossArmor}");
        builder.AppendLine($"Cape4Select: {SelectedBossCape}");
        builder.AppendLine($"Weapon4Select: {SelectedBossWeapon}");
        builder.AppendLine($"Pet4Select: {SelectedBossPet}");
        builder.AppendLine($"GroundItem4Select: {SelectedBossGroundItem}");

        // Outfit mode (keys: SoloOutfit, SoloOutfitMode … BossOutfitMode)
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
        // ── Classic per-item ──────────────────────────────────────────────────
        void AddIfNotEmpty(List<string> list, string? item)
        {
            if (!string.IsNullOrEmpty(item) && !list.Contains(item))
                list.Add(item);
        }
        string Val(string key) => values.TryGetValue(key, out string? v) ? v : string.Empty;

        SelectedSoloHelm       = Val("Helm1Select");       AddIfNotEmpty(Helms,       SelectedSoloHelm);
        SelectedSoloArmor      = Val("Armor1Select");      AddIfNotEmpty(Armors,      SelectedSoloArmor);
        SelectedSoloCape       = Val("Cape1Select");       AddIfNotEmpty(Capes,       SelectedSoloCape);
        SelectedSoloWeapon     = Val("Weapon1Select");     AddIfNotEmpty(Weapons,     SelectedSoloWeapon);
        SelectedSoloPet        = Val("Pet1Select");        AddIfNotEmpty(Pets,        SelectedSoloPet);
        SelectedSoloGroundItem = Val("GroundItem1Select"); AddIfNotEmpty(GroundItems, SelectedSoloGroundItem);

        SelectedFarmHelm       = Val("Helm2Select");       AddIfNotEmpty(Helms,       SelectedFarmHelm);
        SelectedFarmArmor      = Val("Armor2Select");      AddIfNotEmpty(Armors,      SelectedFarmArmor);
        SelectedFarmCape       = Val("Cape2Select");       AddIfNotEmpty(Capes,       SelectedFarmCape);
        SelectedFarmWeapon     = Val("Weapon2Select");     AddIfNotEmpty(Weapons,     SelectedFarmWeapon);
        SelectedFarmPet        = Val("Pet2Select");        AddIfNotEmpty(Pets,        SelectedFarmPet);
        SelectedFarmGroundItem = Val("GroundItem2Select"); AddIfNotEmpty(GroundItems, SelectedFarmGroundItem);

        SelectedDodgeHelm       = Val("Helm3Select");       AddIfNotEmpty(Helms,       SelectedDodgeHelm);
        SelectedDodgeArmor      = Val("Armor3Select");      AddIfNotEmpty(Armors,      SelectedDodgeArmor);
        SelectedDodgeCape       = Val("Cape3Select");       AddIfNotEmpty(Capes,       SelectedDodgeCape);
        SelectedDodgeWeapon     = Val("Weapon3Select");     AddIfNotEmpty(Weapons,     SelectedDodgeWeapon);
        SelectedDodgePet        = Val("Pet3Select");        AddIfNotEmpty(Pets,        SelectedDodgePet);
        SelectedDodgeGroundItem = Val("GroundItem3Select"); AddIfNotEmpty(GroundItems, SelectedDodgeGroundItem);

        SelectedBossHelm       = Val("Helm4Select");       AddIfNotEmpty(Helms,       SelectedBossHelm);
        SelectedBossArmor      = Val("Armor4Select");      AddIfNotEmpty(Armors,      SelectedBossArmor);
        SelectedBossCape       = Val("Cape4Select");       AddIfNotEmpty(Capes,       SelectedBossCape);
        SelectedBossWeapon     = Val("Weapon4Select");     AddIfNotEmpty(Weapons,     SelectedBossWeapon);
        SelectedBossPet        = Val("Pet4Select");        AddIfNotEmpty(Pets,        SelectedBossPet);
        SelectedBossGroundItem = Val("GroundItem4Select"); AddIfNotEmpty(GroundItems, SelectedBossGroundItem);

        Helms.Sort(StringComparer.OrdinalIgnoreCase);
        Armors.Sort(StringComparer.OrdinalIgnoreCase);
        Capes.Sort(StringComparer.OrdinalIgnoreCase);
        Weapons.Sort(StringComparer.OrdinalIgnoreCase);
        Pets.Sort(StringComparer.OrdinalIgnoreCase);
        GroundItems.Sort(StringComparer.OrdinalIgnoreCase);

        OnPropertyChanged(nameof(Helms));
        OnPropertyChanged(nameof(Armors));
        OnPropertyChanged(nameof(Capes));
        OnPropertyChanged(nameof(Weapons));
        OnPropertyChanged(nameof(Pets));
        OnPropertyChanged(nameof(GroundItems));

        // ── Outfit mode ───────────────────────────────────────────────────────
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

        // Pre-populate outfit list with saved names so dropdowns aren't empty
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

        setOutfit(outfit);

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
