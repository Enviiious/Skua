# Skua AQW Bot — Project Context for Claude Code

## Project Overview
Skua is a C# WPF desktop bot for AdventureQuest Worlds (AQW). It uses a plugin architecture, a CoreBots scripting system, and a DI container (Microsoft.Extensions.DependencyInjection). The rebuild target is:
```
dotnet build C:\Skua\Skua.App.WPF\Skua.App.WPF.csproj --configuration Release -p:Platform=x64
```
Built output lands in `C:\Skua\Build\x64\` and `C:\Skua\Build\x64\Assemblies\`.

---

## Solution Structure
```
C:\Skua\
├── Skua.Core.Utils\
├── Skua.Core.Models\          ← WikiPage.cs lives here (Wiki\ subfolder)
├── Skua.Core.Interfaces\      ← IWikiService.cs lives here (Scripts\ subfolder)
├── Skua.Core\                 ← WikiService.cs, Services.cs, ScriptInterface.cs
├── Skua.WPF\
├── Skua.App.WPF\              ← Entry point, Program.cs
└── Build\x64\Assemblies\      ← Final DLL output
```

---

## Session 1 — DragonslayerGeneral.cs Bot Script

### What was built
A CoreBots `.cs` script to farm DragonSlayer General class in AQW.

**File:** `C:\Users\tdeno\AppData\Roaming\Skua\Scripts\Other\Classes\DragonslayerGeneral.cs`

### Key fixes made through debugging
1. **Script must live in `%appdata%\Skua\Scripts\`** — `//cs_include` paths are relative to that folder
2. **Removed `Adv.RankUpClass("Dragonslayer")`** — hangs doing quest chain in `/lair` before ever touching dragontown
3. **Removed `Farm.Gold(30000)`** — was sending to a gold farm map before dragontown
4. **Quest 5293 must be registered** — "DragonSlayer Farming Quest" is what makes Dragon Claws drop; without it monsters never drop claws
5. **Kill condition must be `"Dracolich Slain"`** — this is the temp quest item; using `"Dragon Claw"` as kill condition caused instant returns with no actual combat
6. **`Core.EquipClass(ClassType.Farm)`** not `Core.EquipClass("Dragonslayer")` — string overload doesn't exist
7. **`Core.KillMonster` does a post-kill cell jump** to an empty cell after every kill by design — replaced with `Bot.Hunt.Monster()` to stay in cell
8. **Added `Core.Join("dragontown", "r4", "Right")` before kill loops** — bot was attempting kills before joining the map
9. **`Bot.Monsters.Exists("Blood Dracolich")`** is the correct API — `Bot.Combat.MonsterExists()` does not exist
10. **Both Tempest and Blood Dracolich spawn at `r4/Right`**

### Final working script logic
```
EnchantedScaleandClaw(75, 100):
  - Buy Dragonslayer class from /lair if missing
  - RankUpClass("Dragonslayer") — already rank 10, instant return
  - EquipClass(ClassType.Farm)
  - AddDrop Enchanted Scale + Dragon Claw
  - RegisterQuests(5293, 5294)
  - Join dragontown r4/Right
  - While claws < 100: Hunt Tempest, check, Hunt Blood if exists
  - While scales < 75: Hunt Tempest, check, Hunt Blood if exists
  - CancelRegisteredQuests
```

---

## Session 2 — Wiki Query Engine (IN PROGRESS)

### Goal
Load `aqwwiki_full.json` (scraped from AQW wiki, ~97,800 pages) at runtime and expose a `Bot.Wiki` query engine in Skua core for use by all scripts and plugins.

### Wiki JSON format
The scraper outputs:
```json
{
  "slug": {
    "title": "...",
    "url": "...",
    "tags": ["monster", "..."],
    "text": "...",
    "html": "...",
    "tables": [[[row1col1, row1col2], [row2col1, ...]], ...],
    "scraped": "ISO timestamp"
  }
}
```
`tables` is `List<List<List<string>>>` — table → rows → cells.

Default wiki path: `%appdata%\Skua\aqwwiki_full.json`

### Files created

| File | Destination |
|------|-------------|
| `WikiPage.cs` | `C:\Skua\Skua.Core.Models\Wiki\WikiPage.cs` |
| `IWikiService.cs` | `C:\Skua\Skua.Core.Interfaces\Scripts\IWikiService.cs` |
| `WikiService.cs` | `C:\Skua\Skua.Core\Services\WikiService.cs` |

### Files modified

| File | Change |
|------|--------|
| `IScriptInterface.cs` | Added `IWikiService Wiki { get; }` property |
| `ScriptInterface.cs` | Added `Wiki` property, constructor param, background load via `Task.Run` |
| `Services.cs` | Added `services.AddSingleton<IWikiService, WikiService>()` |
| `Program.cs` | Added crash log to Desktop (`skua_crash.txt`) wrapping entire `Main()` |

### IWikiService API surface
```csharp
// State
bool IsLoaded { get; }
int PageCount { get; }
string? LoadedPath { get; }

// Loading
Task<bool> LoadAsync(string path);
Task<bool> LoadAsync(); // defaults to %appdata%\Skua\aqwwiki_full.json

// Direct lookup
WikiPage? GetBySlug(string slug);
WikiPage? GetByTitle(string title);

// Search
List<WikiPage> Search(string query, int limit = 10);
List<WikiPage> SearchTitles(string query, int limit = 10);
List<WikiPage> GetByTag(string tag);
List<WikiPage> GetByTags(IEnumerable<string> tags);

// Enhancements
WikiPage? GetEnhancement(string name);
List<WikiPage> GetAllEnhancements();
List<string> GetEnhancementEffects(string name);

// Quests
WikiPage? GetQuest(string name);
List<WikiPage> GetAllQuests();
List<(string Monster, string Map)> FindDropSource(string itemName);

// Monsters
WikiPage? GetMonster(string name);
List<WikiPage> GetAllMonsters();
List<string> GetMonsterLocations(string monsterName);
List<string> GetMonsterDrops(string monsterName);

// Items
WikiPage? GetItem(string name);
List<WikiPage> GetAllItems();
string GetItemSource(string itemName);
```

### Current status — STUCK on DLL copy failure
The code compiles clean (0 errors). The build fails at the final copy step because the previous PowerShell session locked the DLLs in `C:\Skua\Build\x64\Assemblies\` while doing reflection checks.

**Fix pending:**
```powershell
Copy-Item "C:\Skua\Skua.App.WPF\bin\x64\Release\net10.0-windows\Assemblies\Skua.Core.Interfaces.dll" "C:\Skua\Build\x64\Assemblies\Skua.Core.Interfaces.dll" -Force
Copy-Item "C:\Skua\Skua.App.WPF\bin\x64\Release\net10.0-windows\Assemblies\Skua.Core.dll" "C:\Skua\Build\x64\Assemblies\Skua.Core.dll" -Force
Copy-Item "C:\Skua\Skua.App.WPF\bin\x64\Release\net10.0-windows\Assemblies\Skua.Core.Models.dll" "C:\Skua\Build\x64\Assemblies\Skua.Core.Models.dll" -Force
```

After copying, launch `C:\Skua\Build\x64\Skua.exe`. Verify with:
```csharp
Core.Logger($"Wiki loaded: {Bot.Wiki.IsLoaded} — {Bot.Wiki.PageCount} pages");
```

---

## Next Steps (planned)
1. ✅ Get Skua loading with `Bot.Wiki` wired up
2. Drop `aqwwiki_full.json` into `%appdata%\Skua\`
3. Build **enhancement descriptions** in the Class Enhancer UI using `Bot.Wiki.GetEnhancementEffects()`
4. Build **quest drop sources** — show which monster/map drops quest items using `Bot.Wiki.FindDropSource()`
5. Build **item source lookup** — show where to obtain any item

---

## CoreBots API Notes (for script writing)
- `Core.KillMonster(map, cell, pad, monster, dropItem, quant, isTemp, log)` — joins map, jumps cell, kills for drop. **Post-kill jumps to empty cell by design.**
- `Bot.Hunt.Monster(name)` — kills monster in current map without post-kill cell jump. Preferred for farming loops.
- `Bot.Monsters.Exists(name)` — checks if monster is alive in current cell
- `Core.Join(map, cell, pad)` — joins map and jumps to cell
- `Core.RegisterQuests(ids...)` — registers quest IDs so drops unlock
- `Core.AddDrop(itemName)` — adds item to auto-pickup list
- `Core.CheckInventory(nameOrId, quant)` — checks inv + bank
- `Core.EquipClass(ClassType.Farm / .Solo / .SafetyNet)` — equips class from CBO settings
- Quest 5293 = DragonSlayer Farming Quest (enables Dragon Claw drops)
- Quest 5294 = Enchanted Scale quest

---

## Environment
- User: Ty, San Tan Valley AZ
- Skua source: `C:\Skua\`
- Scripts folder: `C:\Users\tdeno\AppData\Roaming\Skua\Scripts\`
- Build output: `C:\Skua\Build\x64\`
- Wiki JSON target: `C:\Users\tdeno\AppData\Roaming\Skua\aqwwiki_full.json`
- .NET: net10.0 / net10.0-windows
- Platform: x64
