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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Add this class to a new file or within your existing files

namespace CAP_ChatInteractive
{
    public static class VersionHistory
    {
        public static Dictionary<string, string> UpdateNotes = new Dictionary<string, string>
        {
            {
                "1.0.14",
                @"Xenotype Pricing System Update 

CRITICAL MIGRATION REQUIRED
If you were using the previous version (1.0.13 before 2026.01.11), you MUST reset all xenotype prices!
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
            // Add more versions here as they're released
        };

        public static string GetUpdateNotes(string version)
        {
            if (UpdateNotes.TryGetValue(version, out string notes))
            {
                return notes;
            }

            // Default/fallback message
            return $"RICS has been updated to version {version}.\n\n" +
                   "Please check the mod's documentation or release notes for detailed changelog.\n\n" +
                   "Thank you for using RICS!";
        }

        public static string GetMigrationNotes(string fromVersion, string toVersion)
        {
            // Special handling for migrations from older versions
            if (string.IsNullOrEmpty(fromVersion) || fromVersion == "0")
            {
                return GetUpdateNotes(toVersion) + "\n\n" +
                       "NOTE: This appears to be your first time using RICS with this save file, " +
                       "or you're migrating from a very old version. " +
                       "Please review the changes above carefully.";
            }

            return GetUpdateNotes(toVersion);
        }
    }
}