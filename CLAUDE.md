# Skua AQW Bot — Project Context for Claude Code

## Project Overview
Skua is a C# WPF desktop bot for AdventureQuest Worlds (AQW). It uses a plugin architecture, a CoreBots scripting system, and a DI container (Microsoft.Extensions.DependencyInjection).

**Build command:**
```
dotnet build C:\Skua\Skua.App.WPF\Skua.App.WPF.csproj --configuration Release -p:Platform=x64
```
Built output lands in `C:\Skua\Build\x64\` and `C:\Skua\Build\x64\Assemblies\`.

**Deploy individual assemblies** (when full rebuild isn't needed):
```powershell
dotnet publish C:\Skua\Skua.WPF\Skua.WPF.csproj -c Release -p:Platform=x64 -o C:\Skua\_publish_tmp
Copy-Item "C:\Skua\_publish_tmp\Skua.WPF.dll" "C:\Skua\Build\x64\Assemblies\Skua.WPF.dll" -Force
```

---

## Solution Structure
```
C:\Skua\
├── Skua.Core.Utils\
├── Skua.Core.Models\          ← WikiPage.cs lives here (Wiki\ subfolder)
├── Skua.Core.Interfaces\      ← IWikiService.cs (Scripts\ subfolder)
├── Skua.Core\                 ← WikiService.cs, Services.cs, ScriptInterface.cs, GetScriptsService.cs
├── Skua.WPF\                  ← WikiUserControl.xaml.cs, DoIHaveWindow.xaml.cs
├── Skua.App.WPF\              ← Entry point, Program.cs
└── Build\x64\Assemblies\      ← Final DLL output
```

**External plugin directory:** `C:\DropRatePlugin\`
**Scripts folder:** `C:\Users\tdeno\AppData\Roaming\Skua\Scripts\`
**Script cache (Roslyn):** `C:\Users\tdeno\AppData\Roaming\Skua\Scripts\Cached-Scripts\`
**Drop rate data:** `C:\Users\tdeno\AppData\Roaming\Skua\droprates\`
**Wiki scraper:** `C:\Skua\Tools\WikiScraper\`

---

## Session 1 — DragonslayerGeneral.cs Bot Script

### What was built
A CoreBots `.cs` script to farm DragonSlayer General class in AQW.

**File:** `C:\Users\tdeno\AppData\Roaming\Skua\Scripts\Other\Classes\DragonslayerGeneral.cs`

### Key fixes
1. Scripts must live in `%appdata%\Skua\Scripts\` — `//cs_include` paths are relative there
2. Removed `Adv.RankUpClass("Dragonslayer")` — hangs in `/lair` before touching dragontown
3. Removed `Farm.Gold(30000)` — was sending to a gold farm map before dragontown
4. Quest 5293 must be registered — "DragonSlayer Farming Quest" makes Dragon Claws drop
5. Kill condition must be `"Dracolich Slain"` (temp item), not `"Dragon Claw"`
6. `Core.EquipClass(ClassType.Farm)` not `Core.EquipClass("Dragonslayer")` — string overload doesn't exist
7. `Core.KillMonster` post-kill cell-jumps by design — replaced with `Bot.Hunt.Monster()` for farming
8. `Bot.Monsters.Exists("Blood Dracolich")` is correct API — `Bot.Combat.MonsterExists()` doesn't exist
9. Both Tempest and Blood Dracolich spawn at `r4/Right`

---

## Session 2 — Wiki Query Engine

### What was built
`Bot.Wiki` service: loads `aqwwiki_full.json.gz` (~97,800 pages) at runtime for script/plugin use.

### Files created
| File | Destination |
|------|-------------|
| `WikiPage.cs` | `C:\Skua\Skua.Core.Models\Wiki\WikiPage.cs` |
| `IWikiService.cs` | `C:\Skua\Skua.Core.Interfaces\Scripts\IWikiService.cs` |
| `WikiService.cs` | `C:\Skua\Skua.Core\Services\WikiService.cs` |

### Files modified
| File | Change |
|------|--------|
| `IScriptInterface.cs` | Added `IWikiService Wiki { get; }` |
| `ScriptInterface.cs` | Added `Wiki` property + background load via `Task.Run` |
| `Services.cs` | Added `services.AddSingleton<IWikiService, WikiService>()` |
| `Program.cs` | Added crash log to Desktop (`skua_crash.txt`) |

### Wiki JSON format
```json
{
  "slug": {
    "title": "...",
    "url": "...",
    "tags": ["monster", "..."],
    "text": "...",
    "html": "...",
    "tables": [[[row1col1, row1col2], ...], ...],
    "scraped": "ISO timestamp"
  }
}
```
`tables` is `List<List<List<string>>>` — table → rows → cells.

### IWikiService key methods
```csharp
WikiPage? GetBySlug(string slug);
WikiPage? GetByTitle(string title);
List<WikiPage> Search(string query, int limit = 10);
List<(string Monster, string Map)> FindDropSource(string itemName);
WikiPage? GetEnhancement(string name);
WikiPage? GetQuest(string name);
WikiPage? GetMonster(string name);
WikiPage? GetItem(string name);
```

---

## Session 3 — Class Enhancer, Outfit System, Plugin Suite

### What was built
- **Class Enhancer UI** — enhancement selection using `Bot.Wiki.GetEnhancementEffects()`
- **Outfit System** — CoreBots loadout management tied to class type (Farm/Solo/Dodge/Boss)
- **OutfitEquipPlugin** — plugin that applies outfit on class change

Commits: `1fa25eb`, `37253aa`, `879085a`

---

## Session 4 — Drop Rate Tracker Plugin

### What was built
**Location:** `C:\DropRatePlugin\` (external plugin, not part of Skua solution)
- `DropRatePlugin.cs` — ISkuaPlugin implementation
- `DropRateHudViewModel.cs` — WPF HUD with session + all-time columns
- `deploy_plugins.ps1` — deploys plugin DLLs to `C:\Skua\Build\x64\Assemblies\`

### Architecture decisions
- **Per-monster kill attribution**: `_itemSourceMonster` maps item → monster that dropped it; `_monsterKills` counts per-monster. Items use monster's kill count as denominator, not quest-level fallback.
- **`_cumulative` is immutable base**: loaded from file at session start, never mutated in-memory. `ComputeEffective()` = base + live session, used for all saves and display.
- **`_currentUser` cached in `OnScriptStarted()`**: Player.Username is null at `Load()` time. Defer `LoadCumulative()`/`LoadSession()` until script starts.
- **`ThrottledRefresh()`** (called from `OnItemDropped`) only refreshes farm goals — never the rate panel. Rate panel only refreshes from `OnMonsterKilled` so kill is already counted before rate is computed (AQW sends drop packet before kill packet).

### Shared data outputs
- `%AppData%\Skua\droprates\DropRates_{user}.json` — cumulative per-character rates
- `%AppData%\Skua\droprates\Session_{user}.json` — current session (crash recovery)
- `%AppData%\Skua\droprates\DropRateLookup.json` — shared, user-agnostic index read by DoIHave plugin
- `%AppData%\Skua\wiki\wiki_additions.json` — "dr-{slug}" entries for Tools > Wiki search

### Known bug — 3× session accumulation (UNFIXED)
**Symptom:** All-time = exactly 3× session data after 2 runs of same script.

**Root cause:** `OnScriptStopped()` calls `SaveCumulative()` (which bakes session into the file) AND `SaveSession()` (which saves raw session data) but **never deletes the Session file**. Next `OnScriptStarted()` calls `LoadCumulative()` (file has last session) then `LoadSession()` (restores `_sessionData` = last session). Script runs again → `_sessionData` = loaded(1×) + new(1×) = 2×. `ComputeEffective()` = cumulative(1×) + sessionData(2×) = 3×. Gets worse each run.

**Fix (not yet applied):** Add to `OnScriptStopped()`:
```csharp
try { File.Delete(DataPath("Session")); } catch { }
```
Session file is for crash recovery only. Once `OnScriptStopped` fires cleanly, the data is already in the cumulative file.

**Immediate workaround:** Delete `%AppData%\Skua\droprates\Session_{username}.json` and `DropRates_{username}.json` to start clean.

---

## Session 5 — DoIHave Plugin UTF-8 Fix

**File:** `C:\Skua\Skua.WPF\UserControls\DoIHaveWindow.xaml.cs`

### Problem
Persistent `Cannot transcode invalid UTF-8 JSON text to UTF-16 string` error when loading wiki items and merge shops. Previous fix using `reader.ReadToEnd()` then `JsonDocument.Parse(string)` still threw because Parse(string) internally re-encodes to UTF-8 and can throw on U+FFFD sequences.

### Fix — `ParseJsonLenient(Stream source)`
```csharp
private static JsonDocument ParseJsonLenient(Stream source)
{
    var lenient = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    var clean = new MemoryStream();
    using (var sr = new StreamReader(source, lenient, detectEncodingFromByteOrderMarks: false, bufferSize: 65536, leaveOpen: true))
    using (var sw = new StreamWriter(clean, lenient, bufferSize: 65536, leaveOpen: true))
    {
        char[] buf = new char[65536];
        int n;
        while ((n = sr.Read(buf, 0, buf.Length)) > 0)
            sw.Write(buf, 0, n);
        sw.Flush();
    }
    clean.Position = 0;
    return JsonDocument.Parse(clean);
}
```
Key: decodes bytes → chars (bad bytes → U+FFFD, no throw) → re-encodes to clean MemoryStream → parses bytes directly. The MemoryStream intermediate step ensures bytes reaching `JsonDocument.Parse` are already validated UTF-8. Applied to both `LoadWikiItems()` and `LoadMergeShops()`.

---

## Session 6 — Script Cache MissingMethodException Fix

### Problem
Scripts crash with `MissingMethodException: CoreBots.BuyItem method not found` after auto-updater runs. Skua compiles scripts with Roslyn and caches DLLs in `Cached-Scripts\`. Auto-updater downloads new script versions but leaves stale cached DLLs that reference old method signatures.

### Fix — `GetScriptsService.cs`
After any scripts downloaded, `DownloadAllWhereAsync()` now:
1. Calls `Compiler.ClearAssemblyCache()`
2. Deletes all files in `%AppData%\Skua\Scripts\Cached-Scripts\`

**Manual cache clear (workaround):**
```powershell
Remove-Item "$env:APPDATA\Skua\Scripts\Cached-Scripts\*" -Force
```

---

## Session 7 — Wiki Quest Rewards Fix

### Problem
Wiki item pages show `<h2>Quest reward from</h2>` heading but empty list underneath.

### Two-layer fix

**Layer 1 — Python scraper (future scrapes):**
`C:\Skua\Tools\WikiScraper\parsers\price_parser.py`

`_parse_quest_from_ul()` and `_parse_quest_inline()` now return nested list format:
```python
return ["Quest"] + [[slug, name], ...]  # was: ["Quest", name, slug] (flat, wrong)
```
Output now matches the `["Quest", [slug, name], ...]` format that `output.py` expects (same as Drop format).

**Layer 2 — C# runtime patch (existing JSON data):**
`C:\Skua\Skua.WPF\UserControls\WikiUserControl.xaml.cs`

`FixEmptyQuestRewardList(html, text)` detects `<h2>Quest reward from</h2><ul></ul>` and reconstructs the list by parsing the plain-text field. Called inside `RenderPage()` before display.

Note: Re-running the Python scraper with the fixed `price_parser.py` would generate correct HTML in `aqwwiki_full.json.gz` and make the runtime patch unnecessary long-term.

---

## Session 8 — Army/Multi-Client Sync Feasibility Study

**Status: Read-only analysis, no code changes made.**

### What already exists
- **`CoreUltra.cs`** — `WaitForArmy(int questId)` and `CheckArmyProgress()` using `.sync` files in `%AppData%\Skua\Options\`. Opt-in, requires script author to call.
- **`Skua.Manager`** — process launcher only; fires and forgets, no feedback loop
- **`IScriptSync`** interface — `SendCommandToAll()`, `RegisterCommand()` present but no IPC backend

### Why automatic step extraction is hard
Scripts are procedural C#. `isCompletedBefore(questId)` guards skip sections for already-done clients. A step counter that only increments on actions will be wrong after resume — a client that skips a guard looks like it's at step 0 but is actually further ahead.

**Core insight:** Guard skips = being AHEAD. Game state (quest completions, inventory) IS the progress state. A separate runtime counter can't represent this accurately.

### Viable approaches ranked by effort

| Approach | Effort | Notes |
|---|---|---|
| Loose map-sync + Manager dashboard | Low | Broadcast current map; Manager shows per-client status. Zero script changes. |
| Strengthen `WaitForArmy()` | Low | Timeout/retry, stale-file detection, `WaitForArmyOrTimeout()` |
| Quest spine declaration | Low per-script | `Core.Army.Spine(q1, q2, q3)` — one line, progress derived from `isCompletedBefore` |
| Section wrapper (TheFarmerJoe style) | Medium | `Core.Army.Section("name", () => { ... })` — TheFarmerJoe already split this way |
| Auto step extraction via AST | Very High | Fragile, breaks on conditionals/loops, not recommended |

**TheFarmerJoe** is the best proof-of-concept target — already split into named sections, just needs them codified with the Section wrapper pattern. Build the CoreBots infrastructure around TheFarmerJoe first.

---

## Deployment Notes

### Deploy a single assembly quickly
```powershell
# Close Skua first, then:
dotnet publish C:\Skua\Skua.Core\Skua.Core.csproj -c Release -p:Platform=x64 -o C:\Skua\_publish_tmp
Copy-Item "C:\Skua\_publish_tmp\Skua.Core.dll" "C:\Skua\Build\x64\Assemblies\" -Force

dotnet publish C:\Skua\Skua.WPF\Skua.WPF.csproj -c Release -p:Platform=x64 -o C:\Skua\_publish_tmp
Copy-Item "C:\Skua\_publish_tmp\Skua.WPF.dll" "C:\Skua\Build\x64\Assemblies\" -Force
```

### Deploy plugins
```powershell
# C:\DropRatePlugin\deploy_plugins.ps1
```

---

## CoreBots API Reference

```csharp
// Navigation
Core.Join(map, cell, pad)                         // join map + jump cell
Core.KillMonster(map, cell, pad, monster, item, quant)  // joins, kills for drop, post-kill cell-jump
Bot.Hunt.Monster(name)                            // kills in current map, no cell-jump (preferred for loops)
Bot.Monsters.Exists(name)                         // checks if monster alive in current cell

// Quests
Core.EnsureAccept(questId)
Core.EnsureComplete(questId, itemId?)
Core.RegisterQuests(ids...)
Core.isCompletedBefore(questId)                   // checks permanent quest completion

// Inventory
Core.CheckInventory(nameOrId, quant)              // checks inv + bank
Core.AddDrop(itemName)                            // adds to auto-pickup
Core.Unbank(items...)
Core.ToBank(items...)

// Class/Equipment
Core.EquipClass(ClassType.Farm / .Solo / .Dodge / .Boss)
Core.Equip(gear...)

// Shopping
Core.BuyItem(map, shopId, itemName, quant)

// Utility
Core.Logger(message)
Core.Sleep(ms)                                    // uses ActionDelay if ms <= 0
```

### CoreBots class hierarchy
```
CoreBots (singleton: CoreBots.Instance)
  ├── CoreStory      — KillQuest() automation
  ├── CoreFarms      — Gold, Rep, Class XP boosts
  ├── CoreAdvanced   — enhanced purchasing, requirements
  ├── CoreDailies    — daily quest completions
  └── CoreUltra      — WaitForArmy(), file-based multi-client sync
```

Scripts use `//cs_include Scripts/CoreBots.cs` and access via `CoreBots.Instance`.

---

## AQW Packet Ordering Gotcha
AQW sends the **drop packet BEFORE the kill packet** for the same kill event. Any drop rate calculation in `OnItemDropped` will see 1 drop / 0 kills (or 1 kill from the previous kill) = inflated rate. Always compute rates from `OnMonsterKilled`, never from `OnItemDropped`.

---

## Environment
- **User:** Ty, San Tan Valley AZ
- **Skua source:** `C:\Skua\`
- **Skua exe:** `C:\Skua\Build\x64\Skua.exe`
- **Scripts folder:** `C:\Users\tdeno\AppData\Roaming\Skua\Scripts\`
- **Build output:** `C:\Skua\Build\x64\` and `C:\Skua\Build\x64\Assemblies\`
- **Wiki JSON:** `C:\Users\tdeno\AppData\Roaming\Skua\aqwwiki_full.json.gz`
- **.NET:** net10.0 / net10.0-windows
- **Platform:** x64
