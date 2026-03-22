using Skua.Core.Models.Items;

namespace Skua.Core.Interfaces;

/// <summary>
/// Defines methods for equipping items from a player's inventory by item ID, name, or inventory item instance.
/// </summary>
public interface ICanEquip : ICheckEquipped
{
    /// <summary>Equips the item with the specified ID.</summary>
    void EquipItem(int id);

    /// <summary>Equips the item with the specified name.</summary>
    void EquipItem(string name)
    {
        if (TryGetItem(name, out InventoryItem? item))
            EquipItem(item!.ID);
    }

    /// <summary>Equips a usable item (slot 6) by ID.</summary>
    void EquipUsableItem(int id)
    {
        if (TryGetItem(id, out InventoryItem? item))
            EquipUsableItem(item);
    }

    /// <summary>Equips a usable item (slot 6) by name.</summary>
    void EquipUsableItem(string name)
    {
        if (TryGetItem(name, out InventoryItem? item))
            EquipUsableItem(item);
    }

    /// <summary>Equips a usable item (slot 6) by InventoryItem instance.</summary>
    void EquipUsableItem(InventoryItem? item);

    /// <summary>Equips multiple items by name.</summary>
    void EquipItems(params string[] names)
    {
        foreach (string t in names)
        {
            if (TryGetItem(t, out InventoryItem? item))
                EquipItem(item!.ID);
        }
    }

    /// <summary>Equips multiple items by ID.</summary>
    void EquipItems(params int[] ids)
    {
        foreach (int t in ids)
            EquipItem(t);
    }

    /// <summary>
    /// Equips a saved in-game outfit by name using the equipLoadout packet.
    /// </summary>
    void EquipOutfit(string outfitName);

    /// <summary>
    /// Returns the names of all outfits saved in the player's in-game Wardrobe.
    /// </summary>
    List<string> GetOutfits();

    /// <summary>
    /// Reads the class name from a saved outfit without equipping anything.
    /// Looks at the armor ("ar") slot of the outfit data to get the class name.
    /// Returns null if the outfit is not found or the player is not logged in.
    /// </summary>
    string? GetOutfitClassName(string outfitName);
}
