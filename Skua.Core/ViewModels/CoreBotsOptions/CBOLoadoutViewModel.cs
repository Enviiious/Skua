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

        // In Outfit mode, disable per-item equip checks before saving
        // so ClassSelectViewModel writes False for all four flags.
        // Restore afterwards so the in-memory UI state is unaffected.
        if (UseOutfitMode)
        {
            bool solo  = ClassSelectViewModel.UseSoloEquipment;
            bool farm  = ClassSelectViewModel.UseFarmEquipment;
            bool dodge = ClassSelectViewModel.UseDodgeEquipment;
            bool boss  = ClassSelectViewModel.UseBossEquipment;

            ClassSelectViewModel.UseSoloEquipment  = false;
            ClassSelectViewModel.UseFarmEquipment  = false;
            ClassSelectViewModel.UseDodgeEquipment = false;
            ClassSelectViewModel.UseBossEquipment  = false;

            ClassSelectViewModel.Save(builder);

            ClassSelectViewModel.UseSoloEquipment  = solo;
            ClassSelectViewModel.UseFarmEquipment  = farm;
            ClassSelectViewModel.UseDodgeEquipment = dodge;
            ClassSelectViewModel.UseBossEquipment  = boss;
        }
        else
        {
            ClassSelectViewModel.Save(builder);
        }

        ClassEquipmentViewModel.Save(builder);
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
