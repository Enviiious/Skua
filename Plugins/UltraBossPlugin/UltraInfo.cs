namespace UltraPlugin;

/// <summary>
/// Describes one Ultra boss — its map, roles, and recommended classes.
/// Class lists are drawn from the in-game scripts and the Simplified Ultras Guide spreadsheet.
/// </summary>
public sealed class UltraInfo
{
    public string Name             { get; init; } = string.Empty;
    public string Map              { get; init; } = string.Empty;
    public bool   HasTaunter       { get; init; }
    public int    TaunterCount     { get; init; }
    public string TauntRole        { get; init; } = string.Empty;
    public string DpsRole          { get; init; } = string.Empty;
    public string Mechanics        { get; init; } = string.Empty;
    public string PlayerCount      { get; init; } = string.Empty;

    /// <summary>Classes recommended for the taunter role.</summary>
    public IReadOnlyList<string> TaunterClasses { get; init; } = Array.Empty<string>();

    /// <summary>Classes recommended for DPS / support.</summary>
    public IReadOnlyList<string> DpsClasses { get; init; } = Array.Empty<string>();

    // ── Static catalogue ─────────────────────────────────────────────────────

    public static readonly IReadOnlyList<UltraInfo> All = new[]
    {
        new UltraInfo
        {
            Name         = "Astral Empyrean",
            Map          = "astralempyrean",
            HasTaunter   = true,
            TaunterCount = 1,
            PlayerCount  = "4–7 players",
            TauntRole    = "LR and VDK alternate taunting. Taunt when the boss says \"Behold our Starfire\". Never taunt twice in a row or you will die.",
            DpsRole      = "Burn the boss. Follow zone callouts — avoid red zones or you will die instantly. Do NOT use Vainglory Cape.",
            Mechanics    = "Difficulty: 5/10 · Boss does % HP damage and reduces healing. Out-of-position or back-to-back taunt = instant kill. Reset if 3+ players die.",
            TaunterClasses = new[] { "Legion Revenant", "Verus Doomknight" },
            DpsClasses     = new[] { "Chrono ShadowSlayer", "Archfiend", "Arachnomancer", "Legendary Hero", "Lord of Order", "Arcana Invoker", "King's Echo", "Sentinel", "Great Thief", "Light Caster", "Phantom Chronomancer" },
        },
        new UltraInfo
        {
            Name         = "Champion Drakath",
            Map          = "championdrakath",
            HasTaunter   = true,
            TaunterCount = 1,
            PlayerCount  = "2–7 players",
            TauntRole    = "Taunt every time Drakath loses 2M HP (18.1M, 16.1M, 14.1M…). Taunter needs 3501+ HP buffed. If one taunt is missed the run is likely lost.",
            DpsRole      = "Burn freely between taunt triggers. StoneCrusher with full forge (Anima, Absolution, Valiance) can guarantee no deaths.",
            Mechanics    = "Difficulty: 6/10 · 3000 damage nukes (3500 after 10M HP). LOTS of debuffs. LR can taunt with Healer helm + Healer enhancements.",
            TaunterClasses = new[] { "ArchPaladin", "Legion Revenant" },
            DpsClasses     = new[] { "Chrono ShadowSlayer", "King's Echo", "Arcana Invoker", "Paladin Chronomancer", "StoneCrusher", "Lord of Order", "Chaos Slayer" },
        },
        new UltraInfo
        {
            Name         = "Dark Carnax",
            Map          = "darkcarnax",
            HasTaunter   = false,
            TaunterCount = 0,
            PlayerCount  = "1–7 players (solo possible)",
            TauntRole    = string.Empty,
            DpsRole      = "Burn freely. Move to the designated cell when zone shifts trigger. Solo mode skips army sync.",
            Mechanics    = "Automated zone movement. Script handles cell navigation at each phase. Solo attempt supported.",
            DpsClasses   = new[] { "Legion Revenant", "Arcana Invoker", "Chrono ShadowSlayer", "Lich", "Archfiend", "Quantum Chronomancer", "Hollowborn Vindicator", "Arachnomancer", "Infinity Knight", "Verus Doomknight", "King's Echo", "Phantom Chronomancer", "Great Thief" },
        },
        new UltraInfo
        {
            Name         = "Flibbitiestgibbet",
            Map          = "flibbitiestgibbet",
            HasTaunter   = false,
            TaunterCount = 0,
            PlayerCount  = "4–7 players",
            TauntRole    = string.Empty,
            DpsRole      = "Select your class and the item to farm. Script handles quests and drops automatically.",
            Mechanics    = "Straightforward burn fight with quest turn-in automation.",
            DpsClasses   = new[] { "Great Thief", "Verus Doomknight", "Legendary Hero", "Legion Revenant", "Archfiend", "Lord of Order", "Arcana Invoker", "StoneCrusher", "Light Caster", "King's Echo", "Shaman", "Dragon of Time", "Sentinel", "Master Ranger" },
        },
        new UltraInfo
        {
            Name         = "NightBane",
            Map          = "nightbane",
            HasTaunter   = false,
            TaunterCount = 0,
            PlayerCount  = "4–7 players",
            TauntRole    = string.Empty,
            DpsRole      = "Burn NightBane. Script auto-handles quests and loot collection.",
            Mechanics    = "Same structure as Flibbitiestgibbet. Select item to farm before starting.",
            DpsClasses   = new[] { "Great Thief", "Verus Doomknight", "Legendary Hero", "Legion Revenant", "Archfiend", "Lord of Order", "Arcana Invoker", "StoneCrusher", "Light Caster", "King's Echo", "Shaman", "Dragon of Time", "Sentinel", "Master Ranger" },
        },
        new UltraInfo
        {
            Name         = "Queen Iona",
            Map          = "queeniona",
            HasTaunter   = true,
            TaunterCount = 1,
            PlayerCount  = "4–7 players",
            TauntRole    = "Void Highlord: move to VHL cell when Iona charges, return after discharge. Use skills 1, 4, 2 rotation.",
            DpsRole      = "Burn between charge phases. Move on taunter callout.",
            Mechanics    = "Charge-based zone movement. VHL has special handling. Counter-charge timing is critical.",
            TaunterClasses = new[] { "Void Highlord" },
            DpsClasses     = new[] { "Void Highlord", "Arcana Invoker", "Dragon of Time", "Legion Revenant" },
        },
        new UltraInfo
        {
            Name         = "Ultra Avatar Tyndarius",
            Map          = "ultraavatartyndarius",
            HasTaunter   = true,
            TaunterCount = 2,
            PlayerCount  = "4–7 players",
            TauntRole    = "AP: spam taunt on Tyndarius (Penitence recommended). LoO: spam taunt on Left Orb + debuff Tyndarius. CSS: kill Right Orb fast, then Left Orb on respawn.",
            DpsRole      = "LR taunts one ball (usually Left). DPS kills remaining ball then burns Tyndarius.",
            Mechanics    = "Difficulty: 4/10 · LR should always taunt one ball. AP taunts Tyndarius. LoO focuses/debuffs Tyndarius.",
            TaunterClasses = new[] { "ArchPaladin", "Legion Revenant", "Lord of Order" },
            DpsClasses     = new[] { "Chrono ShadowSlayer", "Chaos Avenger", "King's Echo", "Arcana Invoker", "Lich", "Verus Doomknight", "Dragon of Time" },
        },
        new UltraInfo
        {
            Name         = "Ultra Dage",
            Map          = "ultradage",
            HasTaunter   = true,
            TaunterCount = 2,
            PlayerCount  = "2–7 players",
            TauntRole    = "CAv: loop taunt with skills 3 and 6. AP: use 4 after every Dage zone; use 5 right before 4 debuff ends. Both should use Dauntless. Classes with HoT should also use Dauntless.",
            DpsRole      = "Burn freely. Stay out of zones. Classes with healing-over-time are very valuable. Having a decay is highly recommended.",
            Mechanics    = "Difficulty: 4/10 · Dage inverts healing. AP can invert his heal by using 4 when he heals. 3–5k nukes outside zone. Easy with 2 players.",
            TaunterClasses = new[] { "Chaos Avenger", "ArchPaladin" },
            DpsClasses     = new[] { "Legion Revenant", "Chrono ShadowSlayer", "Lich", "Archfiend", "Arachnomancer", "Infinity Knight", "Quantum Chronomancer", "Phantom Chronomancer", "Verus Doomknight", "King's Echo", "Great Thief", "Hollowborn Vindicator" },
        },
        new UltraInfo
        {
            Name         = "Ultra Darkon",
            Map          = "ultradarkon",
            HasTaunter   = true,
            TaunterCount = 1,
            PlayerCount  = "4–7 players",
            TauntRole    = "LR and LoO loop taunt for the entire fight. LoO should start spamming skill 5 at ~4.6M HP. Do NOT use Vainglory Cape.",
            DpsRole      = "Burn Darkon. StoneCrusher with full forge (Anima, Absolution, Valiance, Fighter) guarantees survival. Use potions if possible.",
            Mechanics    = "Difficulty: 7/10 · 4:30 minute time limit. Up to 1950 damage hits. Debuffs on every mechanic.",
            TaunterClasses = new[] { "Legion Revenant", "Lord of Order" },
            DpsClasses     = new[] { "Chrono ShadowSlayer", "Alpha Omega", "Light Caster", "Paladin Chronomancer", "StoneCrusher", "Arcana Invoker", "Lich", "Hollowborn Vindicator", "King's Echo" },
        },
        new UltraInfo
        {
            Name         = "Ultra Drago",
            Map          = "ultradrago",
            HasTaunter   = true,
            TaunterCount = 2,
            PlayerCount  = "4–7 players",
            TauntRole    = "AP: taunt Left summon (Axe). LoO: loop taunt with AP on Axe (Penitence recommended). AP should have 3000+ HP.",
            DpsRole      = "LR and DPS focus Right summon (Bow) first. 3-minute time limit — burn fast.",
            Mechanics    = "Difficulty: 5/10 · Time limit 3 minutes. Small nukes from summons. Left summon debuffs resistance.",
            TaunterClasses = new[] { "ArchPaladin", "Lord of Order" },
            DpsClasses     = new[] { "Chrono ShadowSlayer", "Legion Revenant", "Chaos Avenger", "King's Echo", "Arcana Invoker", "Archfiend", "Lich", "Sentinel" },
        },
        new UltraInfo
        {
            Name         = "Ultra Engineer",
            Map          = "ultraengineer",
            HasTaunter   = true,
            TaunterCount = 1,
            PlayerCount  = "4 players (army sync recommended)",
            TauntRole    = "AP taunts during the mech phase. Focus bottom summon first — it starts reflecting after a while.",
            DpsRole      = "Defeat both summons to damage Engineer. Nuke damage is shared among players (very low with 4 alive). Focus bottom summon first.",
            Mechanics    = "Difficulty: 1/10 · 2 summons block damage to Engineer. Nuke divides by players. Solo possible but very slow.",
            TaunterClasses = new[] { "ArchPaladin" },
            DpsClasses     = new[] { "Lich", "Legion Revenant", "Lord of Order", "StoneCrusher", "Arcana Invoker", "Chrono ShadowSlayer", "Verus Doomknight", "Void Highlord", "Chaos Avenger" },
        },
        new UltraInfo
        {
            Name         = "Ultra Ezrajal",
            Map          = "ultraezrajal",
            HasTaunter   = false,
            TaunterCount = 0,
            PlayerCount  = "1–7 players (soloable)",
            TauntRole    = string.Empty,
            DpsRole      = "Having over 3171 HP (buffed) is mandatory. Press Escape when Ezrajal starts reflecting (lasts 6 seconds). Use Mana Vamp to lock weapon, then swap to main enhancement.",
            Mechanics    = "Difficulty: 2/10 · 3171 damage nuke. Reflect phase: stop attacking immediately. Heal skill, then reflect, then nuke.",
            DpsClasses   = new[] { "Chrono ShadowSlayer", "Verus Doomknight", "Legion Revenant", "Lord of Order", "Arcana Invoker", "ArchPaladin", "Dragon of Time", "Void Highlord", "Chaos Avenger" },
        },
        new UltraInfo
        {
            Name         = "Ultra Gramiel",
            Map          = "ultragramiel",
            HasTaunter   = true,
            TaunterCount = 2,
            PlayerCount  = "4–7 players",
            TauntRole    = "Two taunters per crystal (L-T1/L-T2 and R-T1/R-T2). Do NOT use decay on Gramiel before both crystals are down. Taunting a crystal debuff twice in a row = instant kill. Revitalize Elixir strongly recommended.",
            DpsRole      = "Kill both crystals first, then burn Gramiel. Avoid using numbers for taunt callouts — use class names instead.",
            Mechanics    = "Difficulty: 9/10 · Do not decay Gramiel before crystals die. Crystal double-taunt = instant kill. Each ultra mechanic applies here.",
            TaunterClasses = new[] { "StoneCrusher", "Light Caster", "ArchPaladin", "Lord of Order", "Verus Doomknight", "Void Highlord" },
            DpsClasses     = new[] { "Arcana Invoker", "Legion Revenant", "Light Caster", "StoneCrusher", "Lord of Order", "Archfiend", "King's Echo", "Shaman" },
        },
        new UltraInfo
        {
            Name         = "Ultra Nulgath",
            Map          = "ultranulgath",
            HasTaunter   = true,
            TaunterCount = 1,
            PlayerCount  = "4–7 players",
            TauntRole    = "T1 counts to 2, then taunts. After Nulgath raises hand with red glow, next taunt counts to 4–5 then taunts. Repeat. Turn monster animations ON.",
            DpsRole      = "Burn Nulgath. 1 summon spawns — kill it. Boss instantly kills all players if summon is not handled.",
            Mechanics    = "Difficulty: 6/10 · Stacking debuffs. 1 summon (instantly kills all if ignored). Manual taunt timing — watch Nulgath's raised hand animation.",
            TaunterClasses = new[] { "ArchPaladin", "Lord of Order" },
            DpsClasses     = new[] { "Chrono ShadowSlayer", "Verus Doomknight", "Legion Revenant", "Dragon of Time", "King's Echo", "Arcana Invoker", "Archfiend", "Lich" },
        },
        new UltraInfo
        {
            Name         = "Ultra Speaker",
            Map          = "ultraspeaker",
            HasTaunter   = true,
            TaunterCount = 1,
            PlayerCount  = "4–7 players (2900+ HP recommended)",
            TauntRole    = "Everyone taunts in rotation — no one taunts twice in a row or they may explode. Everyone must zone at least once to remove debuffs. Anyone who taunted must wait one zone before re-entering.",
            DpsRole      = "Follow the taunt rotation chart. Stay in zone at least once per cycle to clear debuffs.",
            Mechanics    = "Difficulty: 8/10 · HP amount matters. Resistance debuffs on auto attack. Heal during zones. Nukes inside red zone.",
            TaunterClasses = new[] { "ArchPaladin", "Legion Revenant", "Lord of Order", "King's Echo" },
            DpsClasses     = new[] { "Chrono ShadowSlayer", "Quantum Chronomancer", "Verus Doomknight", "Sentinel", "Archfiend", "Void Highlord" },
        },
        new UltraInfo
        {
            Name         = "Ultra Warden",
            Map          = "ultrawarden",
            HasTaunter   = true,
            TaunterCount = 1,
            PlayerCount  = "1–7 players (soloable)",
            TauntRole    = "LR taunts when Warden has ~500k HP left. Take it SLOW — boss nukes every time he loses 5% HP. Healing over time (Revitalize Elixir) strongly recommended.",
            DpsRole      = "Pace your DPS carefully. At least 2 healing classes recommended in group. Recommended to have HoT in your kit.",
            Mechanics    = "Difficulty: 3/10 · Nukes every 5% HP lost. Petrify 7% chance. Sets HP to 1. Soloable.",
            TaunterClasses = new[] { "Legion Revenant" },
            DpsClasses     = new[] { "Chrono ShadowSlayer", "Alpha Omega", "Verus Doomknight", "Paladin Chronomancer", "Legendary Hero", "Lord of Order", "StoneCrusher", "Arcana Invoker", "Dragon of Time", "Yami no Ronin" },
        },
        new UltraInfo
        {
            Name         = "Xyfrag",
            Map          = "xyfrag",
            HasTaunter   = true,
            TaunterCount = 1,
            PlayerCount  = "4–7 players",
            TauntRole    = "Hold aggro. Stop attacking immediately when the charge aura is detected — GenericChargeListener handles this automatically. Resume when charge ends.",
            DpsRole      = "Burn Xyfrag outside charge windows. Script pauses attacks automatically during charge phase.",
            Mechanics    = "Single taunter. GenericChargeListener monitors Xyfrag's charge aura and signals stop/resume for all DPS.",
            TaunterClasses = new[] { "Void Highlord", "ArchPaladin", "Legion Revenant" },
            DpsClasses     = new[] { "Void Highlord", "Arcana Invoker", "Legion Revenant", "Verus Doomknight", "Chrono ShadowSlayer" },
        },
    };
}
