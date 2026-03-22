using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Text;

namespace Skua.Core.ViewModels;

public class CBOLoadoutViewModel : ObservableObject, IManageCBOptions
{
    public CBOLoadoutViewModel(CBOClassEquipmentViewModel classEquipmentViewModel)
    {
        ClassEquipmentViewModel = classEquipmentViewModel;
    }

    public CBOClassEquipmentViewModel ClassEquipmentViewModel { get; }

    public StringBuilder Save(StringBuilder builder)
    {
        ClassEquipmentViewModel.Save(builder);
        return builder;
    }

    public void SetValues(Dictionary<string, string> values)
    {
        ClassEquipmentViewModel.SetValues(values);
    }
}
