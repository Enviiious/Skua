using Skua.Core.ViewModels;
using System;
using System.Windows.Controls;

namespace Skua.WPF.UserControls;

public partial class CBOOutfitEquipmentUserControl : UserControl
{
    public CBOOutfitEquipmentUserControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Auto-refresh the outfit list from the player's Wardrobe when a
    /// dropdown is opened so outfits are always current.
    /// </summary>
    private void ComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (DataContext is CBOClassEquipmentViewModel vm)
            vm.RefreshOutfitsCommand.Execute(null);
    }
}
