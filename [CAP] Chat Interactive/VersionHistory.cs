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

// File contains version history notes for RICS (RimWorld Interactive Chat Services) mod, including version numbers, release dates, and detailed changelogs of features, fixes, and updates. Each entry is structured to provide clear information about the changes made in each version.

using System;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive
{
    public static class VersionHistory
    {
        public static Dictionary<string, string> UpdateNotes = new Dictionary<string, string>
        {
            /// <summary>
            /// Version History Notes
            /// Each entry should include: Version number, release date, and detailed changelog of features, fixes, and updates.
            /// Do not use Emojis or special formatting in the changelog text, as it may not display properly in all contexts.
            /// Keep only the last 10 version.  Other history is in Changelog.txt.
            /// Update Changelog.txt also when you update this, keep all information in the changelog.txt
            /// </summary>
            


            {"1.40",
@"===========================================================
                         RICS 1.40 - Update
                         Released: June 26, 2026
===========================================================

<b>MEMORANDUM</b>
─────────────────
This release includes all changes from June 21–23 plus the June 26 heal logic improvement. Major themes: AI/event notification system, extensive UI and editor quality-of-life refactors, raid & military aid rebalancing, multi-use cooldowns, social command fixes, and heal refund logic.

<b>UPDATED / IMPROVED</b>
──────────────
• AI / Event system: Added AI event notification system via Harmony patch. Improved event usage and cooldown feedback messages.
• UI & Editor quality of life: Multiple refactors across DebugRaces dialog, command UI height logic, event editor layout, command settings for raid/military aid, queue visibility, and pawn header layout. Increased letter body truncation limit to 2000 characters.
• Raid & Military Aid: Rebalanced wager scaling. Added multi-use incident cooldowns (X uses per Y days). Lowered minimum wager to 50 coins for several commands.
• Social commands: Fixed flirt command to allow flirting with current love partners.
• Heal command: Refund coins when !healpawn results in no injuries healed. Improved overall heal logic and condition checking.
• AI ChatBot & context: Improved robustness, messaging, platformUserId/name handling, viewer pawn prioritization, and added chatUsername to pawn reports.
• Viewer persistence: Viewers data now saved immediately after chat message processing.
• Game state support: Added component tracking and improved season calculation.

<b>FIXED</b>
────────────
• Minor stability improvements in chat processing and AI-related paths.
• Queue visibility and pawn header layout issues resolved.

<b>ADDED</b>
────────────
• Multi-use incident cooldown system (configurable X uses per Y days).
• AI event notification system.

<b>TRANSLATIONS</b>
───────────────────
• Added new translation key in HealPawnCommandHandler:
  `<RICS.HPCH.Return.NoInjuriesHealed>No injuries were healed on your pawn. Coins have been refunded.</RICS.HPCH.Return.NoInjuriesHealed>`
"
},
{
                "1.41",
                @"===========================================================
                    RICS 1.41 - Changelog
                    Released: June 2026
===========================================================

<b>MEMORANDUM</b>
─────────────────
- Rimazon Lockers can now be unloaded directly by pawns (no more manual eject needed in many cases).
- New Drop Spots system added for better item delivery control.
- New `!storyteller` command added.
- Translations are now ~99.9% complete across the entire mod. If you're planning to contribute a new language for RICS, now is the perfect time to start!

<b>UPDATED</b>
─────────────────
- `!mypawn` command expanded:
  - `!mypawn mech` Now shows Mechinator bandwidth and controlled mechs (if the pawn is a Mechinator).
  - `!mypawn psycasts` Added support for **Vanilla Psycasts Expanded** — lists psycasts on your pawn (note: this integration is a bit fragile and may require a game reload if it breaks).
- Major improvements to pawn death, resurrection, and release messages — viewers now get much clearer feedback with details on what happened.
- Cooldown display simplified to easy-to-read ""X times every Y days"" format.
- Food breakdown expanded for richer in-game flavor.
- General polish and fixes across many pawn and store commands.

<b>FIXED</b>
─────────────────
- Fixed several issues with pawn messages, translation keys, and error handling.
- Improved dead pawn detection and handling in multiple commands.
- Cleaned up and standardized many behind-the-scenes systems.

<b>ADDED</b>
─────────────────
- **New Drop Spots** system for better control over where items appear.
- Rimazon Lockers can now be unloaded by pawns.
- New `!storyteller` command (shows current storyteller + difficulty).
- New **AI Chat Bot** tab in the settings (for advanced users experimenting with external storyteller bots).
- Ability to dye dead pawns with special messages.
- Expanded raw food categories and other small quality-of-life additions.

<b>TRANSLATIONS</b>
─────────────────
- Massive translation pass completed: nearly all strings (especially pawn messages, errors, and XML entries) are now properly keyed and centralized.
- Many improvements to death, resurrection, and command feedback text for better localization."
            },
            {"1.42",
                "===========================================================\r\n" +
                "                         RICS version - Changelog\r\n" +
                "                         Released: July 4, 2026\r\n" +
                "===========================================================\r\n\r\n" +
                "<b>MEMORANDUM</b>\r\n─────────────────\r\n" +
                "Streamers your settings will be migrated to the new structure. Just be sure to check the settings in the Updated section.\r\n\r\n" +
                "Modders who write custom commands can now make custom settings for your commands.\r\n" +
                "See the new Wiki Page on GitHub in section 4 Modders.\r\n\r\n" +
                "<b>UPDATED</b>\r\n──────────────\r\n" +
                "- Command settings system rewritten for better extensibility\r\n" +
                "- !raid command settings migrated\r\n" +
                "- !militaryaid command settings migrated\r\n" +
                "- !surgery command settings migrated\r\n" +
                "- (and other core commands)\r\n\r\n" +
                "<b>FIXED</b>\r\n────────────\r\n" +
                "(none in this update — mostly structural improvements)\r\n\r\n" +
                "<b>ADDED</b>\r\n────────────\r\n" +
                "- Custom command settings support for modders\r\n" +
                "- New commands now able to expose their own toggles, numeric fields, headers, buttons, and gaps in the in-game Command Editor\r\n\r\n" +
                "<b>TRANSLATIONS</b>\r\n───────────────────\r\n" +
                "- Updated strings related to the new command settings system\r\n\r\n" +
                "Full commit history: https://github.com/ekudram/-cap-RimworldInteractiveChatServices/commits/master/"},
            { "1.43c",
                @"
===========================================================
                RICS 1.43c - Changelog
                Released: July 10, 2026
===========================================================

<b>Memorandum</b>
─────────────────-
Major Spawn & Delivery Overhaul is live! We've greatly improved how and where items, pawns, animals, and mechs appear in your colony.

Mechs purchased by a Mechanitor will now spawn properly **under their control**.

!rescueme command added for rescuing pawns from being captured.

HOTFIX for sealed maps: !mypawn and !mypawn gear now work properly on sealed maps (e.g. space colonies, underground bunkers, RV's etc.).
HOTFIX for animal spawning. Was sending free pets.
HOTFIX for !healpawn command. Was removing implants and bionics instead of healing like it was supposed to.

<b>Updated</b>
──────────────
- Complete overhaul of the pawn/item/mech spawn & delivery pipeline
- Prioritized locker delivery for items (pawns use colony-aware placement)
- Enhanced Mechinator bandwidth checks and control assignment
- Improved handling for nomadic colonies and underground maps

<b>Fixed</b>
────────────
- Mech spawning/control issues when purchased
- Various delivery and spawn edge cases(space maps, lockers, minified buildings, etc.)
- Logging and namespace cleanups

<b>Added</b>
────────────
- Nomadic/no-homebase spawn support
- Partial underground map compatibility
- Smarter multi-map delivery logic


<b>Translations</b>
───────────────────
- Updated strings for new purchase/delivery behaviors and AI improvements"
    },
            {
    "1.44",
    @"===========================================================
                         RICS 1.44 - Changelog
                         Released: July 19, 2026
===========================================================

<b>MEMORANDUM</b>
─────────────────
GitHub Price List was updated
- You can now upload your CommandSettings.json file for the new Commands Tab
- Added Dark Mode for the GitHub Price List

You will need to sync your fork to the main branch or copy the new files from the main branch to your fork.
- Files changed: index.html, rics-store.js, rics-store.css, and CommandSettings.json

<b>UPDATED</b>
──────────────
- Improved item and pawn delivery system (better handling of roofs and tricky colony layouts like underground bases or space maps)
- Revamped Tabs in the Settings dialog for better organization and clarity

<b>FIXED</b>
────────────
- Animals and Mechs should now spawn properly again. No more empty pods.
- Fixed bug where usernames were not being shown in the mass heal letter

<b>ADDED</b>
────────────
- New Gear and Apparel system for Twitch Raids. Should make raids significantly more challenging.
- New !mypawn genes command to display a viewer's pawn genes.

<b>TRANSLATIONS</b>
───────────────────
- Fixed spacing in the Traits Command Handler translation keys.

<b>Special thanks to Kanboru</b> for the `!mypawn genes` command contribution!
"
},
            {"1.45",
                @"===========================================================
                         RICS version 1.45 - Changelog
                         Released: July 25, 2026
===========================================================

<b>MEMORANDUM</b>
─────────────────

Updates to how the Twitch Raids system works.
Updates to the !mypawn command to show more information about the viewer's pawn.

<b>UPDATED</b>
──────────────
- Twitch Raids system now properly respects the colony's current state and adjusts raid difficulty accordingly.
- Twitch Raiders might have a better chance of joining from chat.
  - This is a Twitch limitation and not a RICS issue, but we have improved the detection logic to help with this.
- Updated command handler and VPE patch to display suggestions in error messages.
- !flirt and other social commands will hit Karma harder if the viewer's pawn is already in a relationship and they target another pawn they are not in a relationship with.

<b>FIXED</b>
────────────
- !mypawn relations command now properly shows the relationships of the viewer's pawn, including lovers, spouses, and family members.
- !mypawn stats command now properly shows the stats of the viewer's pawn, including health, skills, and beauty.
- !flirt and other social commands adjusted to properly handle pawns with multiple love partners or complex relationship networks.
- !setfavoritecolor should now properly set the exact favorite color.

<b>ADDED</b>
────────────
- Setting to change Twitch Raid join window duration (default 240 seconds). This allows streamers to adjust how long the system collects raider names after a raid is detected.
- Added logic to suggest similar psycast class names when user input does not match any known class.
- !mypawn family command added to show the family members of the viewer's pawn, including parents, siblings, and children.
- !mypawn friends command added to show the friends of the viewer's pawn, including best friends and acquaintances.
- !mypawn rivals command added to show the rivals of the viewer's pawn, including enemies and competitors.
- Viewer Dialog with Mass Action: you can now select the amount of coins to give to all viewers at once.

<b>TRANSLATIONS</b>
───────────────────
The following translation files have been updated with new keys. Also for the !mypawn family, friends, and rivals commands:

TabDrawer_Twitch.xml

Dialog_ViewerManager.xml

MyPawnCommandHandler.xml"
                },
            {"1.46",
                @"===========================================================
                         RICS version 1.46 - Changelog
                         Released: August 1, 2026
===========================================================

<b>MEMORANDUM</b>
─────────────────
- Bug fixes + one new useful command.
- !pawncheck added so you (or chat) can quickly check another viewer’s pawn for injuries/health issues.
- Twitch bot account setup is clearer and less error-prone.

<b>UPDATED</b>
──────────────
- Removed some early Messages.Message calls that could crash when DefOfs weren’t ready yet. Errors still go to the log.
- If you leave the Bot Username blank, RICS will automatically fill it with your streamer/channel name so connection is more reliable.

<b>FIXED</b>
────────────
- !surgery now properly respects research requirements (when the global “Require Research” setting is on). Unresearched surgeries are blocked and it tells you what’s missing.
- Only one Mechlink can be installed per pawn (no more stacking them for a free mechaniod).
- Fixed mod-related errors with events/incidents that didn’t have valid incident workers (prevents crashes when building the store).

<b>ADDED</b>
────────────
- !pawncheck <viewer> – Quick injury/health check on another viewer’s assigned pawn (cleaner than full !mypawn body report).
- Improved Twitch Bot Account UI with clearer help text and auto-fill behavior.

<b>TRANSLATIONS</b>
───────────────────
- none
"
            },
            {"1.47",
                @"===========================================================
                         RICS version 1.47 - Changelog
                         Released: August 29, 2026
===========================================================

<b>MEMORANDUM</b>
─────────────────
- RICS now with Kick
- Kick chat send is tested and working. Reading Kick already worked; RICS can now reply in Kick chat.
- Kick wiki was rewritten (Kick.com app first, then Authorize in RICS). Use the Wiki button on this window — Kick Settings.
- Easier !pawn for chat, optional light item ownership, and a lot of command cleanup (less log spam, fewer odd errors).
- RICS ownership is a lighter built-in take on pawn-owned gear. Do not use it with Possessions Plus — if that mod is loaded, RICS ownership turns itself off.
- Turn RICS ownership on for a new game when you can. Mid-save is riskier; back up first.
- The idea of colonists truly owning their weapons and apparel was inspired by Side1iner's Possessions Plus. Thank you. RICS's version is our own smaller system (no copied code).
- Twitch Extension / Viewer Hub code is in this build but is not turned on yet.
- Optional extra mod: [CAP] RICS Personal Storage — personal chests for a pawn's stuff.

<b>ADDED</b>
────────────
- Kick: Authorize Kick (user login with chat:write) so RICS can send command replies to Kick. Tested live. Each streamer creates their own Kick developer app. Redirect URL must be exactly http://localhost:17890/kick/callback
- RICS pawn item ownership (off by default in Mod Settings → Global). Weapons and apparel can belong to a colonist. Others cannot equip or wear that gear. Items can pass on when the owner dies. Browse owned gear from Play Settings and (when ownership is on) the RICS toolbar / main tab. Chat: !mypawn owned and !mypawn disown. Viewer Hub Owned tab lists that gear with Unclaim (LocalHttp GET /extension/owned, POST /extension/owned/disown).
- Optional companion: Personal Storage units. Assign a chest to a pawn so their extra gear has a home.
- Bare !pawn is viewer-friendly. One enabled race (typical Human-only colony): !pawn buys that race — random age and gender inside your race settings, Baseliner or another xenotype you actually enabled. Several races: lists the first 8 with prices (same look as !races). Xenotypes stay on !xenotypes [race].
- Reset to base on the big editors (store, events, and similar) with a confirm prompt and backup.
- Viewer coin ticks now follow real-world time (about every 2 minutes), even if the game is paused or running fast.

<b>UPDATED</b>
──────────────
- Kick tab: Redirect URI, Authorize Kick button, and Chat send authorized as ... status. Connect without Authorize still reads; send needs Authorize. You must be live on Kick to connect.
- !pawn help text matches the new behavior.
- Ownership picker: Select when you are only choosing whose items to look at. Make owner when you are assigning a chest or an item. Viewer tag is (Twitch), not (Twitch:username).

<b>FIXED</b>
────────────
- Kick !bal (and every other Kick command) no longer uses your Twitch wallet just because the names match. Same handle on Kick vs Twitch is two viewers. Kick starts at starting coins. A Kick user cannot spoof a Twitch balance by sharing a username.
- Chat could buy xenotypes you had turned off in Race Settings. Disabled types are blocked now.
- Unique / special weapons in the store now follow research gating like their base weapon when Require Research is on.
- Twitch connect message said Rimwold. It now says Rimworld.
- Failed equip/wear should not charge viewers for gear that never arrived.
- Command handlers: less debug spam in the log, clearer errors in chat.
- Pawn Race Settings: Reset all Prices now also resets Base Price to the race's RimWorld pawn market value (ThingDef.BaseMarketValue).

<b>UNDER THE HOOD</b>
──────────────────
Skip this if you only care about play. Command pipeline, cooldowns, lootboxes, and XML text cleaning (helps avoid bad characters in saves). Twitch Extension bridge is present but inactive. Colony AI / gamestate work is for the few streams that use the bot — ignore it for a normal RICS game.

<b>TRANSLATIONS</b>
───────────────────
- Ownership, owned-items browser, and !pawn usage keys.
- Dialog_PawnRaceSettings.xml — Reset all Prices now mentions Base Price; added RICS.Message.ResetAllPricesDone.
"
            },
            {"1.48",
@"===========================================================
                         RICS version 1.48 - Changelog
                         Released: August 30, 2026
===========================================================

<b>MEMORANDUM</b>
─────────────────

Note Major fix to Custom Colors that could cause a crash on save load.  If you are using Custom Colors, please update to this version.

Testing has shown that you can turn on RICS Ownership mid-game, but it is safer to turn it on at the start of a new game.
If you turn it on mid-game, you may have to manually assign ownership to gear that has been purchased previously.
If you are unsure, please back up your save before turning it on mid-game.
Do not turn it on if you are using Possessions Plus or any other mod that adds item ownership.  This will cause conflicts and may break your game.

<b>UPDATED</b>
──────────────

<b>FIXED</b>
────────────
- Rimazon Invoice formating for total price had incorrect spacing.
- Return message for !pawn now will show you price paid.
- Fixed Scrolling issues in the Locker Contents Dialog
- Fix ColorDef shortHash 0 aborting TerrainGrid.ExposeColorGrid on save load.
Runtime favorite ColorDefs were added with default shortHash 0. 1.6 TerrainGrid.ExposeColorGrid Dictionary.Add(shortHash)
then throws Key: 0, skips colorGridDeflate, and cascades into PathGrid NREs.
Assign unique non-zero hashes before DefDatabase.Add and repair any live hash-0 ColorDefs during ReRegisterAll.

<b>ADDED</b>
────────────

<b>TRANSLATIONS</b>
───────────────────
- Changed some in RICSGeneral.xml to remove 'RICS -' from the start of some messages.
Also changed message about Ownership and adding mid game to be more clear about the risks of turning it on mid-game.
<RICS.Ownership.Settings.Warning>WARNING: Do not use other mods that add item ownership if you turn this on. Can be turned on Mid-Game. Save your game first.</RICS.Ownership.Settings.Warning>
"
                },
            {"1.49",
@"===========================================================
                         RICS version 1.49 - Changelog
                         Released: September 2026
===========================================================

<b>MEMORANDUM</b>
─────────────────
- Quality-of-life for streamers with xenotypes list, a clearer !pricecheck hint, and a few economy defaults that were already in the last builds but not listed here.

<b>UPDATED</b>
──────────────
- Starting coins can now go up to 100,000 (was 10,000).
- New viewers start with karma decay at 0 by default (it no longer ticks down unless you turn it on).

<b>FIXED</b>
────────────
- Buying a colonist (!pawn) could sometimes drop an invisible person and leave junk pixels on the edge of the map. New pawns now get a complete look (body, head, hair) before they arrive.
- Pawn Race Settings: xenotypes are grouped with Biotech first, then by the mod they came from. Long lists (50+) can scroll all the way to the last row.
- Pawn Race Settings xenotype column said Price (silver); it now says Price.
- If Biotech is turned off, pawn purchases use Base Price only (old xenotype prices in the save are ignored).
- Pawn Race Settings: with Biotech on, a short note under Base Price explains that chat pays the xenotype row (Human Baseliner = Human).
- !pricecheck with no item name now asks you to type one, with an example (!pricecheck steel), instead of a generic command error.

<b>ADDED</b>
────────────
- Store editor: after the 1x / 3x / 5x stack buttons, a number box and Set applies the same max purchase count to every visible item. Typing does nothing until you click Set.

<b>TRANSLATIONS</b>
───────────────────
- !pricecheck usage text.
- Store editor bulk Set quantity keys.
"
                },

            {"1.50",
@"===========================================================
                         RICS version 1.50 - Changelog
                         September 19, 2026
===========================================================

<b>MEMORANDUM</b>
─────────────────
- Chat moderators can silence a viewer inside RICS without touching Twitch, Kick, or YouTube. The person can still talk on the platform; RICS just ignores their commands.
- RimWorld XML sometimes fails to save large mod settings. RICS now keeps a JSON backup and can restore from it.

<b>FIXED</b>
───────────
- Can no longer purchase pawn if xenotype does not exist. Must put in proper xenotype or leave blank for Baseliner.
- Chat now shows the coin price on purchase success messages (it was dropping out of the sentence).
- Buying a xenotype that is not on your allowed list no longer quietly gives a normal human. The buy fails instead.
- If a new colonist would arrive with a missing or broken head, the purchase is cancelled and coins are not taken.
- Psytrainers can only be used by actual psycasters. Skill neurotrainers and psychic amplifiers still work for everyone.
- Skill neurotrainers cannot be used with !use if the pawn cannot use that skill (coins are not taken).
- People using Vanilla Psycasts Expanded are treated as psycasters, so Psytrainer checks work for them.

<b>UPDATED</b>
──────────────
- Settings window close now writes XML and a JSON backup. Save Backup still works. Only the 5 newest timestamped backups are kept.
- If XML and the latest JSON backup do not match on load, you are asked to Review settings or Load backup.
- Removed the general dev bypass of disabled commands from release. Captolamia can still run disabled commands on his own channel. On other streams only !captolamia runs for him (version / identity check). Everyone else still gets a RICS tip when that command is enabled.

<b>ADDED</b>
────────────
- !rban user — permanent RICS-only ban (aliases: !ricsban).
- !runban user — clear RICS ban and timeout (aliases: !ricsunban).
- !rto user [duration] — timed RICS-only silence, wall clock not game ticks (aliases: !ricstimout, !rtimeout). Default 5 minutes. Examples: !rto bob 10, !rto bob 10m.
- Viewer Manager: timed-out viewers show amber and remaining time; Unban also clears timeout.
- Global option: Load settings from latest JSON backup on startup (overwrites XML after RimWorld loads it).

<b>TRANSLATIONS</b>
───────────────────
- Keys for !rban / !runban / !rto and Viewer Manager timeout labels.
- Keys for JSON settings load option and mismatch dialog.
- Keys for !use skill neurotrainer blocked when the pawn cannot use that skill.
"
                },
                        {"1.51",
@"===========================================================
                         RICS version 1.51 - Changelog
                         
===========================================================

<b>FIXED</b>
───────────
- Channel-point redeem thank-you uses the custom coin name from settings instead of the word coins.
- Bought pawns with no gender chosen now roll male or female. They no longer arrive as Other.

<b>UPDATED</b>
──────────────
- !mypawn story shows race and xenotype (when Biotech is active).

<b>TRANSLATIONS</b>
───────────────────
- Redeem thank-you keyed string.
- !mypawn story race and xenotype keys.
"
                }



/*
            // Add more versions here as they're released Keep oldest 10,  Changelog.txt keeps all changlogs.
===========================================================
                         RICS version - Changelog
                         Pre-Release (for pre release testing) For Release Use:  Released: Month Day, Year  
===========================================================

<b>MEMORANDUM</b>
─────────────────

<b>UPDATED</b>
──────────────

<b>FIXED</b>
────────────

<b>ADDED</b>
────────────

<b>TRANSLATIONS</b>
───────────────────
*/
            // Add more versions here as they're released+
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
                // New dialog auto-focuses the latest version (your request)
                OpenVersionHistory(newVersion);
                Logger.Message($"Updated from version {oldVersion} to {newVersion}. Showing new history dialog.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error showing update notification: {ex.Message}");
            }
        }


        private static string FallbackUpdateMessage(string newVersion, string oldVersion)
        {
            return $"RICS has been updated to version {newVersion}.\n\n" +
                   $"Previous version: {(string.IsNullOrEmpty(oldVersion) ? "First install / unknown" : oldVersion)}\n\n" +
                   "Please check the mod's documentation or Steam Workshop page for the detailed changelog.";
        }

  
        /// <summary>
        /// Opens the new recallable version history dialog.
        /// Pass a version string to auto-highlight it (e.g. for update notification).
        /// Streamer can call this anytime via debug console or future settings button.
        /// </summary>
        public static void OpenVersionHistory(string highlightVersion = null)
        {
            if (Find.WindowStack == null)
            {
                Logger.Warning("Cannot open version history — WindowStack not ready yet");
                return;
            }

            var dialog = new Dialog_RICS_VersionHistory(highlightVersion);
            Find.WindowStack.Add(dialog);
        }
    }
}