using CommunityToolkit.Mvvm.ComponentModel;
using System.Text;

namespace Skua.Core.ViewModels;

public partial class CBOLoadoutViewModel : ObservableObject, IManageCBOptions
{
    public CBOLoadoutViewModel(CBOClassSelectViewModel classSelectViewModel, CBOClassEquipmentViewModel classEquipmentViewModel)
    {
        ClassSelectViewModel    = classSelectViewModel;
        ClassEquipmentViewModel = classEquipmentViewModel;
    }

    public CBOClassSelectViewModel    ClassSelectViewModel    { get; }
    public CBOClassEquipmentViewModel ClassEquipmentViewModel { get; }

    [ObservableProperty]
    private bool _useOutfitMode = false;

    public StringBuilder Save(StringBuilder builder)
    {
        builder.AppendLine($"UseOutfitMode: {UseOutfitMode}");
        ClassSelectViewModel.Save(builder);
        ClassEquipmentViewModel.Save(builder);

        // In Outfit mode the OutfitEquipPlugin handles gear — force all
        // per-item EquipChecks off so the script doesn't also try to equip
        // items by name when switching back from Classic mode.
        if (UseOutfitMode)
        {
            builder.AppendLine("SoloEquipCheck: False");
            builder.AppendLine("FarmEquipCheck: False");
            builder.AppendLine("DodgeEquipCheck: False");
            builder.AppendLine("BossEquipCheck: False");
        }

        return builder;
    }

    public void SetValues(Dictionary<string, string> values)
    {
        if (values.TryGetValue("UseOutfitMode", out string? val))
            UseOutfitMode = bool.TryParse(val, out bool b) && b;

        ClassSelectViewModel.SetValues(values);
        ClassEquipmentViewModel.SetValues(values);
    }
}
