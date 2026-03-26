using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using Skua.Core.Models.Skills;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace UltraPlugin;

public partial class UltraBossWindow : Window
{
    public UltraBossWindow(IScriptInterface bot)
    {
        InitializeComponent();
        DataContext = new UltraBossViewModel(bot);
    }
}

public partial class UltraBossViewModel : ObservableObject
{
    private readonly IScriptInterface _bot;

    // class name (case-insensitive) → modes defined in AdvancedSkills.json
    private readonly Dictionary<string, List<ClassUseMode>> _skillModes;

    public UltraBossViewModel(IScriptInterface bot)
    {
        _bot       = bot;
        Ultras     = new ObservableCollection<UltraInfo>(UltraInfo.All);
        _skillModes = LoadSkillModes();

        // Start with all modes until a class is chosen
        Modes = new ObservableCollection<ClassUseMode>(Enum.GetValues<ClassUseMode>());
    }

    // ── Data ──────────────────────────────────────────────────────────────

    public ObservableCollection<UltraInfo>    Ultras           { get; }
    public ObservableCollection<ClassUseMode> Modes            { get; }
    public ObservableCollection<string>       AvailableClasses { get; } = new();

    // ── Observables ───────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedUltra))]
    [NotifyPropertyChangedFor(nameof(ShowTaunterToggle))]
    [NotifyPropertyChangedFor(nameof(RoleLabel))]
    [NotifyPropertyChangedFor(nameof(RoleDescription))]
    private UltraInfo? _selectedUltra;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleLabel))]
    [NotifyPropertyChangedFor(nameof(RoleDescription))]
    private bool _isTaunter;

    [ObservableProperty] private string       _selectedClass = string.Empty;
    [ObservableProperty] private ClassUseMode _selectedMode  = ClassUseMode.Base;
    [ObservableProperty] private string       _statusText    = string.Empty;

    // ── Computed ──────────────────────────────────────────────────────────

    public bool HasSelectedUltra  => SelectedUltra is not null;
    public bool ShowTaunterToggle => SelectedUltra?.HasTaunter == true;

    public string RoleLabel
    {
        get
        {
            if (SelectedUltra is null)     return "YOUR ROLE";
            if (!SelectedUltra.HasTaunter) return "DPS / SUPPORT";
            return IsTaunter ? "TAUNTER" : "DPS / SUPPORT";
        }
    }

    public string RoleDescription
    {
        get
        {
            if (SelectedUltra is null)     return "Select an Ultra boss from the list on the left.";
            if (!SelectedUltra.HasTaunter) return SelectedUltra.DpsRole;
            return IsTaunter ? SelectedUltra.TauntRole : SelectedUltra.DpsRole;
        }
    }

    // ── Reactions ─────────────────────────────────────────────────────────

    partial void OnSelectedUltraChanged(UltraInfo? value)
    {
        IsTaunter = false;
        RefreshClasses();
        // RefreshModes is called inside RefreshClasses after SelectedClass is set
    }

    partial void OnIsTaunterChanged(bool value)
    {
        RefreshClasses();
    }

    partial void OnSelectedClassChanged(string value)
    {
        RefreshModes();
    }

    // ── Class list ────────────────────────────────────────────────────────

    private void RefreshClasses()
    {
        AvailableClasses.Clear();

        if (SelectedUltra is null) return;

        var list = (SelectedUltra.HasTaunter && IsTaunter)
            ? SelectedUltra.TaunterClasses
            : SelectedUltra.DpsClasses;

        foreach (var cls in list)
            AvailableClasses.Add(cls);

        // Pick first entry if current selection isn't in the new list
        if (AvailableClasses.Count > 0
            && !AvailableClasses.Contains(SelectedClass, StringComparer.OrdinalIgnoreCase))
        {
            SelectedClass = AvailableClasses[0]; // triggers OnSelectedClassChanged → RefreshModes
        }
        else
        {
            RefreshModes(); // class unchanged but taunter toggled — still refresh modes
        }
    }

    // ── Mode list ─────────────────────────────────────────────────────────

    private void RefreshModes()
    {
        string cls = SelectedClass?.Trim() ?? string.Empty;

        List<ClassUseMode>? available = null;

        if (!string.IsNullOrEmpty(cls)
            && _skillModes.TryGetValue(cls, out var found)
            && found.Count > 0)
        {
            available = found;
        }

        var current = SelectedMode;
        Modes.Clear();

        if (available is not null)
        {
            foreach (var m in available)
                Modes.Add(m);
        }
        else
        {
            // Class not found in AdvancedSkills — show all as fallback
            foreach (var m in Enum.GetValues<ClassUseMode>())
                Modes.Add(m);
        }

        // Try to keep the current mode if it's still valid
        if (Modes.Contains(current))
        {
            SelectedMode = current;
        }
        else if (IsTaunter && Modes.Contains(ClassUseMode.Supp))
        {
            SelectedMode = ClassUseMode.Supp;
        }
        else
        {
            SelectedMode = Modes.Count > 0 ? Modes[0] : ClassUseMode.Base;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task StartAsync()
    {
        if (SelectedUltra is null)
        {
            StatusText = "Please select an Ultra boss first.";
            return;
        }

        string cls = SelectedClass.Trim();
        if (string.IsNullOrEmpty(cls))
        {
            StatusText = "Please select or type your class name.";
            return;
        }

        if (!_bot.Player.Playing)
        {
            StatusText = "Not logged in — please log in first.";
            return;
        }

        StatusText = "Starting…";
        try
        {
            await Task.Run(() => _bot.Auto.StartAutoAttack(cls, SelectedMode));
            string role = (SelectedUltra.HasTaunter && IsTaunter) ? "Taunter" : "DPS";
            StatusText = $"Auto Attack running — {cls} [{SelectedMode}] [{role}] · {SelectedUltra.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        StatusText = "Stopping…";
        try
        {
            await Task.Run(() => _bot.Auto.Stop());
            StatusText = "Auto Attack stopped.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error stopping: {ex.Message}";
        }
    }

    // ── AdvancedSkills loader ─────────────────────────────────────────────

    /// <summary>
    /// Reads AdvancedSkills.json (and UserAdvancedSkills.json if present) from
    /// %AppData%\Skua and returns a case-insensitive map of class → available modes.
    /// UserAdvancedSkills entries override base entries when both files exist.
    /// </summary>
    private static Dictionary<string, List<ClassUseMode>> LoadSkillModes()
    {
        var result = new Dictionary<string, List<ClassUseMode>>(StringComparer.OrdinalIgnoreCase);

        string skuaDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skua");

        // Base file first, user file second so it can override
        string[] paths =
        {
            Path.Combine(skuaDir, "AdvancedSkills.json"),
            Path.Combine(skuaDir, "UserAdvancedSkills.json"),
        };

        foreach (string path in paths)
        {
            if (!File.Exists(path)) continue;

            try
            {
                using var stream = File.OpenRead(path);
                using var doc    = JsonDocument.Parse(stream);

                foreach (var classEntry in doc.RootElement.EnumerateObject())
                {
                    var modes = new List<ClassUseMode>();

                    foreach (var modeEntry in classEntry.Value.EnumerateObject())
                    {
                        if (Enum.TryParse<ClassUseMode>(modeEntry.Name, ignoreCase: true, out var mode))
                            modes.Add(mode);
                    }

                    if (modes.Count > 0)
                        result[classEntry.Name] = modes;
                }
            }
            catch { /* skip unreadable files */ }
        }

        return result;
    }
}
