// VersionHistory.cs 
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
// 
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// CAP Chat Interactive is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with CAP Chat Interactive. If not, see <https://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using Verse;

// Add this class to a new file or within your existing files

namespace CAP_ChatInteractive
{
    public static class VersionHistory
    {
        public static Dictionary<string, string> UpdateNotes = new Dictionary<string, string>
        {
            {
                "1.0.14a",
                @"Xenotype Pricing System Update 

CRITICAL MIGRATION REQUIRED
If you updated to 1.0.14 you can skip this step.
If you are updating directly from 1.0.13 or earlier, you MUST follow the migration steps below to reset all xenotype prices!
If you were using the previous version (1.0.13 before 2026.01.10), you MUST reset all xenotype prices!
The old system used arbitrary multipliers (0.5x-8x), but the new system uses actual silver prices based on Rimworld's gene market values.

Immediate Action Required:
1. Open Pawn Race Settings (RICS -> RICS Button -> Pawn Races)
2. Select any race
3. Click 'Reset All Prices' button in the header
4. OR click 'Reset' next to each xenotype individually
5. Repeat for all races you use

What Changed?

OLD SYSTEM (Broken)
Total Price = Race Base Price × Xenotype Multiplier
Example: Human (1000) × Sanguophage (2.2x) = 2200
Multipliers were arbitrary guesses (0.5x to 8x)

NEW SYSTEM (Correct)
Total Price = Race Base Price + Xenotype Price
Xenotype Price = Sum[(gene.marketValueFactor - 1) × Race Base Price]
Uses Rimworld's actual gene marketValueFactor

Key Benefits of New System:
- Accurate: Matches Rimworld's caravan/trade values exactly
- Transparent: Shows actual silver, not confusing multipliers
- Consistent: 1 silver = 1 unit of value (Rimworld standard)
- Mod-Compatible: Works with all gene mods using marketValueFactor
- Future-Proof: Based on Rimworld's official valuation system

New Features Added:
1. Help System
   - Click '?' Help button in Race Settings for complete documentation
   - Includes migration instructions and price calculation examples
   - Explains all settings and features

2. Bulk Reset Options
   - 'Reset All Prices' button resets all xenotypes for selected race
   - Individual 'Reset' buttons next to each xenotype
   - Tooltips show calculated price before resetting

3. Improved UI
   - 'Price (silver)' column instead of confusing 'Multiplier'
   - Clear separation: Race Base Price + Xenotype Price
   - Better input validation (0-1,000,000 silver range)

4. Updated Debug Tools
   - Debug actions now show actual silver values
   - 'Recalculate All Xenotype Prices' updates to new system
   - Gene details show marketValueFactor contributions

--------------------------------------------------
Combat Extended Compatibility Fix 1.0.14a
Problem: Store Editor was crashing when Combat Extended was installed.

Root Cause: Combat Extended adds ammunition and other items that don't
have properly defined categories in RimWorld's item definitions. 
When the store editor tried to use null categories as dictionary keys,
it caused a System.ArgumentNullException.

Solution: Added defensive null-handling throughout the store system.
Items with null categories are now automatically assigned to ""Uncategorized"" instead of causing crashes.

Changes Made:

- Store Editor UI now handles null categories gracefully
- Category filtering and display updated to work with mods that don't follow vanilla conventions
- Store item creation ensures ""Uncategorized"" as fallback for null categories

Result:
- Store Editor opens without crashing with Combat Extended
- Combat Extended items with null categories appear in ""Uncategorized"" section
- Better compatibility with mods that have non-standard item definitions
- Existing store data automatically migrates to handle null categories

Note: If you see items in ""Uncategorized"" that should have proper categories, this is working as intended - they're from mods that don't define categories properly.

--------------------------------------------------
Mech Faction Fix

Problem Solved: Purchased mechanoids (mechs) were spawning with neutral/incorrect faction instead of belonging to the player.

Root Cause: The special pawn delivery system for animals wasn't handling mechanoids properly. While animals were getting their faction set to the player, mechs were being generated with null/neutral factions.

Solution Applied: Updated the pawn delivery logic to detect mechanoids (pawn.RaceProps.IsMechanoid) and set their faction to Faction.OfPlayer immediately after generation, just like animals.

Result:
- Mechs now spawn as player-controlled units
- No more neutral/hostile mechanoids from store purchases
- Maintains compatibility with existing animal delivery system
- Works for both vanilla and modded mechanoids

Note: If you don't have a mechinator, purchased mechanoids may have limited functionality, but they will at least belong to your faction.

--------------------------------------------------
Rimazon Store Command Updates

Changes Made:

Healer & Resurrector Mech Serum Availability Logic
- Before: Required BOTH Enabled AND IsUsable to be true
- After: Now allows EITHER IsUsable = true OR Enabled = true
- Result: Items can be used if marked as usable, even when disabled from new purchases

Buy/Backpack Command Updates
- Before: Blocked ALL commands if item was disabled (!storeItem.Enabled)
- After: Only blocks !buy and !backpack commands when item is disabled
- Result: Users can still !equip and !wear items they already own, even if items are no longer available for purchase

Type Validation Improvements
Each command type now validates specific item flags:
- !buy/!backpack: Checks Enabled status (purchase availability)
- !equip: Checks IsEquippable flag
- !wear: Checks IsWearable flag
- !use: Checks IsUsable flag (in separate handler)

Key Benefits:
- Better user experience - can use items even if removed from store
- Clear separation between purchase vs. usage permissions
- More flexible store management for roleplay scenarios

Important: To fully disable an item, you must set both usage flags (IsUsable/IsWearable/IsEquippable) AND Enabled to false.

--------------------------------------------------
!mypawn body Command Improvements

What's Changed:
- Reduced message spam - Grouped similar injuries (e.g., 'Scratch (x40)' instead of listing 40 individual scratches)
- Better condition counting - Now shows 3 conditions (not 120) when pawn has many similar injuries
- Critical conditions prioritized - Missing limbs, severe bleeding, and dangerous conditions always show first
- Accurate health assessment - Bleeding >100% per hour now shows as 'Critical (Bleeding Out!)' instead of 'Good' or 'Fair'
- Clear urgency indicators with specific warnings

Key Improvements:
- Concise reporting: Shows unique condition types instead of every individual scratch/bruise
- Emergency awareness: Missing limbs and severe bleeding are now properly highlighted
- Realistic status: A pawn bleeding at 500% per hour is correctly marked as Critical, not Good/Fair
- Better grouping: Similar injuries on same body part are combined with count (x#)

Examples:
- Before: 120 conditions listed, 'Overall Status: Fair'
- After: 3 conditions, 'Overall Status: Critical (Bleeding Out!)'
- Before: Missing limbs might not appear in long lists
- After: Missing limbs always show with highest priority

--------------------------------------------------
RICS Store Editor Update

Added new category-specific enable/disable buttons to make managing modded stores easier for streamers!

New Features:
- Custom item names can now be set for any store item
- Enable/Disable by Type: Quickly toggle all usable/wearable/equippable items within a category
- Category-Focused: Only affects items in the selected category (not the entire store)
- Smart Filtering: Uses proper game logic to identify item types
- One-Click Bulk Actions: Set prices, enable/disable, or reset entire categories at once

Perfect For:
- Streamers with 100+ mods
- Quickly disabling all weapons or apparel from specific mods
- Batch price adjustments per category
- Managing viewer purchase permissions by item type

The update adds dropdown menus to each category section with options to enable/disable all items or specific types (usable, wearable, equippable) within that category only.

Example: In the 'Weapon' category, you can now disable all equippable weapons with one click!

--------------------------------------------------
Max Karma Setting Change

The maximum Karma setting has been increased from 200 to 1000.
Default remains at 200.

Important Notes:
- This change only affects the maximum allowed value in settings
- Existing save files will retain their current karma settings
- This does not automatically increase anyone's karma

How Karma Works:
- Every 100 coins spent changes Karma by 1 point (Good: +1, Bad: -1)
- 100 Karma means viewer gets 100% of coin reward
- 999 Karma means viewer gets 999% coin reward
- Example: Default coin reward is 10, at 100 Karma = 10 coins every 2 minutes

Warning: Setting Karma max very high will accelerate your economy significantly. Use this only if you want a faster-paced game economy.

--------------------------------------------------
General Notes:
- Multiple bug fixes and performance improvements
- Better error handling throughout the mod"
            },
            {
                "1.0.15",
                @"===============================================================================
                         RICS 1.0.15 - Changelog
                         Released: January 2026
===============================================================================

New Features
────────────

• Added new translation files:
  - Twitch Settings Tab
  - Game & Events Tab
  - Rewards Tab

• New commands (thanks to @kanboru!):
  !SetTraits       → Mass set multiple traits at once (great for applying lists)
  !Factions        → Lists all factions currently in the game
  !Colonists       → Shows count of colonists + animals in the colony

Added Admin/Utility Commands
────────────────────────────

• !cleanlootboxes [dry/dryrun | all | orphans]
  - dry/dryrun   → Shows orphaned lootboxes (no changes)
  - all          → Deletes ALL lootboxes from everyone (DANGEROUS!)
  - orphans      → Removes only orphaned lootboxes

• !cleanviewers [dry/dryrun | plat/platform | all]
  - dry/dryrun   → Shows how many viewers would be cleaned
  - plat/platform → Removes viewers with missing platform ID (safe & recommended)
  - all          → Aggressive cleanup (use with caution)

• !togglestore [on/off/alts]
  Turns store-related commands on/off:
  backpack, wear, equip, use, addtrait, removetrait, replacetrait, settraits,
  pawn, surgery, event, weather, militaryaid, raid, revivepawn, healpawn, passion
  
  Special values:
  • !togglestore     → Toggles current state
  • !togglestore on  → Forces ON (also accepts: enable, 1, true)
  • !togglestore off → Forces OFF (also accepts: disable, 0, false)

Improvements & Fixes
────────────────────

• Fixed several commands failing to find pawns in some situations
  (!healpawn, !revivepawn, etc.)

• Improved Drop Pod & item placement logic (especially for underground bases):
  RICS now tries to find a valid surface drop location in this priority order:
  1. Ship landing beacon          (highest priority)
  2. Orbital trade beacon
  3. Caravan hitching spot
  4. Near average colonist position (vanilla-like behavior)
  5. Center of map                (last resort)

  → RICS now actively avoids underground maps
  → Recommendation: Underground base owners should encourage viewers to use !backpack

• Fixed invalid users with no platform ID staying in viewer list
  → Use !cleanviewers plat/platform to safely remove them

• Fixed !backpack not properly handling multiple stackable items
  → Can now properly place stacks (note: still possible to overload inventory → pawn may drop excess)

===============================================================================
Have fun with the new commands and cleaner systems! 🚀
Big thanks to @kanboru for the awesome new command contributions!
==============================================================================="
                },
                {
                "1.0.16",
                @"===============================================================================
                         RICS 1.0.16 - Changelog
                         Released: February 2026
===============================================================================
FIXES

Set default moderator command cooldowns to 0 seconds.
Fixed the cooldown slider for commands defaulting to 0.
Fixed cooldowns not resetting properly; now check for a reset once per game day.
Fixed Event Karma not working (thanks to Veloxcity for the fix).
Fixed the Colonists command to count only viewers with an assigned pawn.
Fixed GiftCoins command to prevent giving coins to yourself.
Removed vehiclePawns from the store to prevent crashes when purchased. (This update will handle this automatically.)
Fixed Surgeries to use all available medicine categories (3 of each), including modded medicine.

ADDED

New Surgery subcommands: genderswap and body changes (fatbody, femininebody, hulkingbody, masculinebody, thinbody). Includes checks for genes, ideology, HAR body types, age, pregnancy, etc.
Expanded Surgery commands for Biotech: blood transfusions, sterilization (vasectomy/tubal ligation), hemogen extraction, IUD implantation/removal, vasectomy reversal, pregnancy termination, and more.
Added translations for the YouTube tab.
Added translations for the Command Settings and Event Settings dialog windows.
Added extra safety checks when loading JSON persistence files.
Added body type info to !mypawn body.
Added a link to the !mypawn wiki page on GitHub."

               },
            {    "1.0.16a",
@"===============================================================================
        RICS 1.0.16a - Changelog
        Released: March 2026
===============================================================================
HOTFIXES
Fixed finding pawns from commands for !revivepawn, !healpawn and others.
Fixed Validation errors when loading storeitems with isUseable/isWearable/isEquippable.
Put in controls for !flirt command to prevent abuse.
Animals are back in the store (they were accidentally removed in 1.0.16).
"
},
            {"1.0.16b",
@"===============================================================================
        RICS 1.0.16b - Changelog
        Released: March 2026
===============================================================================
HOTFIXES
Fixed !pawn command not working for everyone ... sorry about that!
"
},
            {"1.0.16c",
@"===============================================================================
        RICS 1.0.16c - Changelog
        Released: March 2026
===============================================================================
HOTFIXES
!pawnqueue command fixed (was broken in 1.0.16)
!joinqueue command fixed (was broken in 1.0.16)
Fixed pawn queue handling for platform IDs (was broken in 1.0.16)
Finally fixed que not removing viewers when pawn was assigned (was broken for a long time)

Feature added: (stealth update)
!storage command to show what the colony has in storage (items and quantities).
!study command to show anomolies being studied by colonists.

Both commands are from Kanboru's excellent contributions!
"
            },
            {"1.0.16e",
@"===============================================================================
        RICS 1.0.16e - Changelog
        Released: March 2026
===============================================================================
HOTFIXES

Put mechs back in.  You can buy them again.
Put Vehicles back in.  You can buy them safely now.
minor fixes to the !buy command.
"
            },
            {"1.0.17b",
                @"RICS 1.0.17b - Changelog  
Released: Feb 3 2026  

HOTFIX Release for Rimazon Locker
You can now use the locker
You will have to eject items from the locker.
Pawns will not remove things from the locker.
Vehicles removed again becuase they wont spawn nicely.

**MAJOR FEATURE**  
- **Rimazon Delivery Locker**: Items can now be delivered to lockers on the map. The system will prioritize available lockers before resorting to standard drop pods.  

**NEW FEATURES**  
- **Price Check Command**: Use `!pricecheck [item] [quality] [material] [quantity]` to view an item's cost before purchasing.  
  Example: `!pricecheck assault rifle masterwork 1`  

**FIXES**  
- Body and gender surgeries now function correctly after thorough testing.  
- Fixed `!joinqueue` command break introduced in a prior version (resolved in 1.0.16d).  
- Surgery drop methods corrected—invoice now displays the correct item destination.  
- Resolved Store Item Editor issue that could reset some prices to 0.  
- Purchasing multi-word races is now supported.  
- Improved `!storage` command parsing for more reliable item handling (additional refinements planned).  

**KNOWN ISSUES**  
- **Whispers controls are currently broken**—targeted for repair in version 1.0.18.  
- Item parser requires improvements in handling quality and material parameters."
                },
            {"1.0.18",
                @"===============================================================================
        RICS 1.0.18 - Changelog
Released: Feb 6, 2026

Fixed:
Items not minifiing is now fixed.  Note:  Building type items do not fit in the locker.
Fixed/added Surgery command ""giveblood"" ""getblood"" sub commands
Research Checks for Purchases is now more agressive checking benches and all recipes.

Translations added for:
Dialog Event Settings translation keys complete
Dialog Event Custom Cooldown Window translation keys complete
Rimazon locker translation keys complete
Dialog Pawn Queue translation keys complete
Dialog Pawn Race Settings translation keys complete
Window For Quality and Research translation keys complete"
                },
            {"1.19a",
                @"
===============================================================================
                            RICS 1.19a - Update/Hotfix
===============================================================================
Released: February 15, 2026

===============================================================================
HOTFIX:
===============================================================================
- Json persistence files now persist across saves and games,
  • Was not saving properly with 1.19.
- Fix buy pawn, not finding pawns on quests or caravan.

===============================================================================
NEW FEATURES:
===============================================================================
- Json files now persist across all saves and games
  • Data from all mods in any savegame is preserved
  • Settings remain intact when adding/removing mods between playthroughs

- New version numbering format: 1.19 (instead of 1.0.19)
  • All future versions will follow this format

- Updated Github Purchase List structure
  • Sync your fork to Main/Master branch
  • Alternative: Copy assets/js/rics-store.js from main
  • Optional CSS improvement: Copy assets/css/rics-store.css

- Consolidated GameComponent Files into single file
  • Better control over data file loading

- Expanded !dye command
  • !dye hair {color} or favorite color if left bland and Biotech loaded
  • Add more hair colors to mod.

===============================================================================
UPDATES:
===============================================================================
- Store Items Editor enhancements:
  • Can now select from Categories or Mod Sources
  • Added ability to enable/disable items from Mod Sources

===============================================================================
FIXES:
===============================================================================
- Consolidated GameComponent Files properly implemented
- RaceSettings now correctly called with Dictionary as source of truth
- RaceSettings now correctly shows xenotypes availible for Race
- Fixed Ownership issues for Possession Mod
- Rimazon Locker adjusted: occupies 1x1 floor space but extends 2 tiles tall

===============================================================================
TRANSLATIONS:
===============================================================================
Added or fixed translation keys for:
- Store Items Editor
- Debug Def Window (Store Items Editor)
- Chat Interactive Settings window
- Pawn Race Settings Window
- Raid Strategies Window
- Buy Items Command Handler Return Messages (1.19a)
- Buy Pawn Command Handler Return Messages  (1.19a)

===============================================================================
NOTES:
===============================================================================
Please sync your GitHub repository to Main/Master branch
for all changes to work properly.
" },
            { "1.20a",
                @"==========================================================================
RICS 1.20a - Update
Released: February 20, 2026
MAJOR FEATURE / BALANCE CHANGE:

-Complete Event Pricing Overhaul (flattened curve for better chat interaction)New targets:
-Regular events: buyable in ~45 minutes of normal watching
-Doom-tier events (Toxic Fallout, Volcanic Winter, Mass Animal Insanity, major ship parts, etc.): buyable in ~2 hours of normal watching
-Price changes:
Good & Neutral events: most under 700 coins
Regular bad events: ~650–900 coins
True Doom events: capped at 1800 coins maximum (previously 15k+)
How to apply:
Open Events Editor (gear icon)
Click ""Reset New Defaults"" (with confirmation)
Prices update instantly with improved Doom detection
Optional: Debug action ""Delete JSON & Rebuild Incidents"" for clean reset
Modded events remain auto-disabled by default

OTHER UPDATES:
Updated:

-!help command now links directly to Commands Wiki
-!pawn list races no longer shows excluded races
-Drop pod landing priority list reworked:
-Explicitly named ""drop spot"" (case-insensitive)
-Orbital trade beacon (outdoors / no roof only)
-Ship landing beacon
-Caravan hitching/packing spot
-Near average free colonist position
-Underground maps try surface first
-Absolute fallback: map center


Fixed:

-Improved Item Store Parser
-Minor Rimazon Invoice fix
-Resurrection no longer targets off-map, buried, or dessicated pawns
-Apparel (especially packs) not registering as wearable — fixed in core logic
-(JSON reset recommended; see below for debug quick-fix)
-Race Parser now checks Race before Xenotype to prevent errors with not finding races
/// NEW LOGIC RESEARCH CHECKS FOR RECIPES/CRAFTING (for buy/!buy commands):
/// - Direct ThingDef / recipeMaker checks unchanged (early exit for buildings).
/// - For recipes: we now require ONLY ONE valid path.
///   1. Recipe prereqs (single + list) must ALL be finished.
///   2. Among recipeUsers benches, AT LEAST ONE must have ALL prereqs finished (or no prereqs).
///   - Stops at the FIRST valid bench (as requested) and FIRST valid recipe.
/// - If any producing recipe exists but NO valid path → block (research still required).
/// - No producing recipes at all → allow (raw resources, etc.).
/// 
/// Verified RimWorld built-in behavior:
/// - CraftingSpot (defName ""CraftingSpot"") has no researchPrerequisites and is available from start.
/// - Tribalwear recipe(s) list CraftingSpot as a recipeUser → craftable with 0 research.
/// - Electric benches (e.g. ElectricTailorBench) have Electricity in researchPrerequisites.
/// - Vanilla only blocks an item if EVERY possible recipe + bench path is gated.
///   (Confirmed via wiki: crafting spot produces tribalwear with no gate; other benches do not block it.)

Added:

-$ can now be used as alias for !buy
-Example: $ cowboy hat Muffalo wool masterwork 2

Translations:

-Completed Dye Command Handler translation file
-Completed Heal Pawn Command Handler translation file
-Added new keys to Dialog_EventEditor.xml

==========================================================================
Store wearable fix procedure (if apparel packs won't !wear):
To see if you have this issue open the Store Editor and search for packs.
If the wearable column is blank for packs,
you have the issue and need to reset the wearable status for packs.

Enable dev mode
Debug actions → CAP → ""Store: Reset Packs Only"" (wearable fix)

If that doesn't work,
Then → CAP → ""Delete JSON & Rebuild Store"" (full reset)
Warning: resets all custom store settings to defaults

=========================================================================="
            }
                        // Add more versions here as they're released
        };

        public static void CheckForVersionUpdate()
        {
            var mod = CAPChatInteractiveMod.Instance;
            if (mod == null)
            {
                Logger.Error("Cannot check version update - CAPChatInteractiveMod.Instance is null");
                return;
            }

            var settingsContainer = mod.Settings;
            if (settingsContainer == null)
            {
                Logger.Error("Cannot check version update - mod Settings container is null");
                return;
            }

            var globalSettings = settingsContainer.GlobalSettings;
            if (globalSettings == null)
            {
                Logger.Error("Cannot check version update - GlobalSettings is null");
                return;
            }

            string currentVersion = globalSettings.modVersion ?? "Unknown";
            string savedVersion = globalSettings.modVersionSaved;

            Logger.Debug($"Version check - Current: {currentVersion}, Saved: {savedVersion ?? "None"}");

            bool isFirstTimeOrMigration = string.IsNullOrEmpty(savedVersion);

            if (isFirstTimeOrMigration || savedVersion != currentVersion)
            {
                string previousVersion = savedVersion ?? "First install / migration";

                globalSettings.modVersionSaved = currentVersion;

                try
                {
                    settingsContainer.Write();
                    Logger.Debug($"Updated saved version from '{previousVersion}' to '{currentVersion}'");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to save settings after version update: {ex.Message}");
                }

                ShowUpdateNotification(currentVersion, previousVersion);
            }
            else
            {
                Logger.Debug("No version change detected");
            }
        }

        public static void ShowUpdateNotification(string newVersion, string oldVersion)
        {
            if (Find.WindowStack == null)
            {
                Logger.Warning("Cannot show update notification - WindowStack is not available yet");
                return;
            }

            try
            {
                string updateNotes = GetUpdateNotesForVersion(newVersion, oldVersion);
                Find.WindowStack.Add(new Dialog_RICS_Updates(updateNotes));
                Logger.Message($"[RICS] Updated from version {oldVersion} to {newVersion}. Showing update notes.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error showing update notification: {ex.Message}");
            }
        }

        private static string GetUpdateNotesForVersion(string newVersion, string oldVersion)
        {
            if (!UpdateNotes.TryGetValue(newVersion, out string notes))
            {
                return FallbackUpdateMessage(newVersion, oldVersion);
            }

            // Optional: you could append migration note here if desired
            if (string.IsNullOrEmpty(oldVersion))
            {
                notes += "\n\nNOTE: This appears to be your first time using RICS with this save file.";
            }

            return notes;
        }

        private static string FallbackUpdateMessage(string newVersion, string oldVersion)
        {
            return $"RICS has been updated to version {newVersion}.\n\n" +
                   $"Previous version: {(string.IsNullOrEmpty(oldVersion) ? "First install / unknown" : oldVersion)}\n\n" +
                   "Please check the mod's documentation or Steam Workshop page for the detailed changelog.";
        }
    }
}